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
        private enum InitializationStep
        {
            InitialGridPopulation,
            InitialGridPopulationScheduled,
            EstablishConsumerProducerConnections,
            EstablishingConsumerProducerConnectionsScheduled,
            PaintTiles
        }
        
        public static readonly ComponentType[] InitializationCompletedQueryTypes = new ComponentType[]
        {
            ComponentType.ReadOnly<GameSettings>(),
            ComponentType.ReadOnly<GridInitializationCompletedTag>()
        };
        
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        private InitializationStep initializationStep;
        private NativeParallelHashMap<int2, TileType> tileMap;
        
        // job handles
        private JobHandle initialGridPopulationJobHandle;
        private JobHandle connectionJobHandle;
        
        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            
            EntityQuery query = GetEntityQuery(
                ComponentType.ReadOnly<GameSettings>(),
                ComponentType.Exclude<GridInitializationCompletedTag>()
            );
            
            RequireForUpdate(query);
        }

        protected override void OnUpdate()
        {
            switch (initializationStep)
            {
                case InitializationStep.InitialGridPopulation:
                    StartInitialGridPopulation();
                    break;
                
                case InitializationStep.InitialGridPopulationScheduled:
                    UpdateInitialGridPopulationScheduled();
                    break;
                
                case InitializationStep.EstablishConsumerProducerConnections:
                    StartEstablishingProducerConsumerConnections();
                    break;
                
                case InitializationStep.EstablishingConsumerProducerConnectionsScheduled:
                    UpdateEstablishingConsumerProducerConnectionsScheduled();
                    break;
                
                case InitializationStep.PaintTiles:
                    StartPaintingTiles();
                    break;
            }
        }

        private void StartInitialGridPopulation()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            Entity settingsEntity = GetSingletonEntity<GameSettings>();

            int2 gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;
            
            tileMap = new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.Persistent);

            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();
            
            Random random = new Random((uint)Stopwatch.GetTimestamp());

            initialGridPopulationJobHandle = new InitialGridPopulationJob()
            {
                settings = settings,
                settingsEntity = settingsEntity,
                tileMap = tileMap,
                commandBuffer = commandBuffer,
                random = random
            }.Schedule(Dependency);

            beginSimulationSystem.AddJobHandleForProducer(initialGridPopulationJobHandle);
            Dependency = initialGridPopulationJobHandle;

            initializationStep = InitializationStep.InitialGridPopulationScheduled;
        }
        
        private void UpdateInitialGridPopulationScheduled()
        {
            if (initialGridPopulationJobHandle.IsCompleted)
                initializationStep = InitializationStep.EstablishConsumerProducerConnections;
        }
        
        private void StartEstablishingProducerConsumerConnections()
        {
            GameSettings settings = GetSingleton<GameSettings>();

            // no producers to associate consumers with, skip to next step
            if (settings.producerCount == 0)
            {
                initializationStep = InitializationStep.PaintTiles;
                return;
            }
            
            EntityQuery producerQuery = GetEntityQuery(ComponentType.ReadWrite<Producer>());
            NativeArray<Entity> producerEntities = producerQuery.ToEntityArray(Allocator.TempJob);

            connectionJobHandle = new EstablishConsumerProducerConnectionJob()
            {
                gridSize = settings.gridSize,
                tileMap = tileMap,
                producerEntities = producerEntities,
                gridTranslationFromEntity = GetComponentDataFromEntity<GridTranslation>(),
                consumerSlotBufferFromEntity = GetBufferFromEntity<ConsumerSlot>(),
                consumerProducerPathBufferFromEntity = GetBufferFromEntity<ConsumerProducerPathNode>(),
            }.Schedule(Dependency);

            producerEntities.Dispose(connectionJobHandle);
            
            beginSimulationSystem.AddJobHandleForProducer(connectionJobHandle);
            Dependency = connectionJobHandle;
            
            initializationStep = InitializationStep.EstablishingConsumerProducerConnectionsScheduled;
        }
        
        private void UpdateEstablishingConsumerProducerConnectionsScheduled()
        {
            if (connectionJobHandle.IsCompleted)
                initializationStep = InitializationStep.PaintTiles;
        }

        private void StartPaintingTiles()
        {
            JobHandle tilePaintingJobHandle = new TilePaintingJob()
            {
                tileMap = tileMap,
                commandBuffer = beginSimulationSystem.CreateCommandBuffer(),
                settings = GetSingleton<GameSettings>(),
                settingsEntity = GetSingletonEntity<GameSettings>()
            }.Schedule(Dependency);

            tileMap.Dispose(tilePaintingJobHandle);
            
            beginSimulationSystem.AddJobHandleForProducer(tilePaintingJobHandle);
            Dependency = tilePaintingJobHandle;
        }
    }
}
