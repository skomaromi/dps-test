using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

namespace DapperTest
{
    public partial class GridInitializationSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            
            EntityQuery query = GetEntityQuery(
                ComponentType.ReadOnly<GameSettings>(),
                ComponentType.Exclude<GridInitializedTag>()
            );
            
            RequireForUpdate(query);
        }

        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            Entity settingsEntity = GetSingletonEntity<GameSettings>();

            int2 gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;

            NativeParallelHashMap<int2, TileType> tileMap =
                new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.TempJob);

            EntityCommandBuffer commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            
            Random random = new Random((uint)Stopwatch.GetTimestamp());

            // TODO: separate thread
            GridInitializationJob gridInitializationJob = new GridInitializationJob()
            {
                settings = settings,
                settingsEntity = settingsEntity,
                tileMap = tileMap,
                commandBuffer = commandBuffer,
                random = random
            };
            gridInitializationJob.Run();
            
            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();

            EntityQuery producerQuery = GetEntityQuery(ComponentType.ReadWrite<Producer>());
            NativeArray<Entity> producerEntities = producerQuery.ToEntityArray(Allocator.TempJob);

            EntityQuery consumerQuery = GetEntityQuery(ComponentType.ReadOnly<Consumer>());

            // TODO: don't schedule job if no producers
            // TODO: ScheduleParallel
            // TODO: try remove diagonal roads
            JobHandle connectionJobHandle = new EstablishConsumerProducerConnectionJob()
            {
                gridSize = gridSize,
                tileMap = tileMap,
                producerEntities = producerEntities,
                gridTranslationFromEntity = GetComponentDataFromEntity<GridTranslation>(),
                consumerSlotBufferFromEntity = GetBufferFromEntity<ConsumerSlot>(),
                consumerProducerPathBufferFromEntity = GetBufferFromEntity<ConsumerProducerPathNode>()
            }.Schedule(consumerQuery);

            commandBuffer = new EntityCommandBuffer(Allocator.TempJob);
            
            JobHandle floorPaintingJobHandle = Job.WithCode(() =>
            {
                foreach (KeyValue<int2, TileType> pair in tileMap)
                {
                    int2 gridPosition = pair.Key;
                    TileType tileType = pair.Value;

                    if (tileType != TileType.Road && 
                        tileType != TileType.Blocked) 
                        continue;
                    
                    Entity entityInstance = commandBuffer.Instantiate(settings.GetTilePrefab(tileType));
                    float3 tilePosition = settings.ConvertToWorldPosition(gridPosition);
                    Translation translation = new Translation() { Value = tilePosition };
                    commandBuffer.SetComponent(entityInstance, translation);
                }
            }).Schedule(connectionJobHandle);

            producerEntities.Dispose(connectionJobHandle);
            tileMap.Dispose(floorPaintingJobHandle);
            
            floorPaintingJobHandle.Complete();
            commandBuffer.Playback(EntityManager);
            commandBuffer.Dispose();

            // TODO: do we need this?
            // beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
