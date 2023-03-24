using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
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
                ComponentType.ReadWrite<GameSettings>(),
                ComponentType.Exclude<GridInitializedTag>()
            );
            
            RequireForUpdate(query);
        }

        // TODO: ensure this does not run more than once after entire map
        // init is done
        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            Entity settingsEntity = GetSingletonEntity<GameSettings>();

            int2 gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;

            NativeParallelHashMap<int2, TileType> tileMap =
                new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.TempJob);

            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();

            // TODO: there are better ways to do this?
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

            EntityQuery producerQuery = GetEntityQuery(ComponentType.ReadWrite<Producer>());
            NativeArray<Entity> producerEntities = producerQuery.ToEntityArray(Allocator.TempJob);

            EntityQuery consumerQuery = GetEntityQuery(ComponentType.ReadOnly<Consumer>());
            
            JobHandle connectionJob = new EstablishConsumerProducerConnectionJob()
            {
                gridSize = gridSize,
                tileMap = tileMap,
                producerEntities = producerEntities,
                gridTranslationFromEntity = GetComponentDataFromEntity<GridTranslation>()
            }.Schedule(consumerQuery);
            
            producerEntities.Dispose(connectionJob);
            tileMap.Dispose(connectionJob);

            // TODO: do we need this?
            // beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
