using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Root
{
    public partial class GridInitializationSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;
        
        // JUST CHECKING THINGS OUT!
        // private bool jobsRunning;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            
            EntityQuery query = GetEntityQuery(
                ComponentType.ReadWrite<GameSettings>(),
                ComponentType.Exclude<GridInitializedTag>()
            );
            
            RequireForUpdate(query);
        }

        // TODO: IJobWithEntity?
        private struct GridInitializationJob : IJob
        {
            public GameSettings settings;
            public Entity settingsEntity;
            
            public NativeParallelHashMap<int2, TileType> tileMap;
            public EntityCommandBuffer commandBuffer;
            public Random random;

            public void Execute()
            {
                WalkMap(settings, ref tileMap, ref random);
                
                // TODO: split in subregions, parallelize and chain work before and
                // after?
                FillUnpopulatedTiles(settings, ref tileMap);
                
                PlaceTiles(TileType.Producer, settings.producerCount, ref tileMap, settings, ref random);
                PlaceTiles(TileType.Consumer, settings.consumerCount, ref tileMap, settings, ref random);
                
                // TODO: NOT optimal. use flags
                GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Empty);
                GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Producer);
                GridUtility.SpawnPrefabs(settings, ref commandBuffer, ref tileMap, TileType.Consumer);

                // add tag to mark grid as initialized
                commandBuffer.AddComponent<GridInitializedTag>(settingsEntity);
            }
        }

        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            Entity settingsEntity = GetSingletonEntity<GameSettings>();
            
            Vector2Int gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;
            
            NativeParallelHashMap<int2, TileType> tileMap =
                new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.TempJob);
            
            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();

            // TODO: there are better ways to do this?
            Random random = new Random((uint)Stopwatch.GetTimestamp());

            GridInitializationJob gridInitializationJob = new GridInitializationJob()
            {
                settings = settings,
                settingsEntity = settingsEntity,
                tileMap = tileMap,
                commandBuffer = commandBuffer,
                random = random
            };

            JobHandle gridInitializationJobHandle = gridInitializationJob.Schedule();
            gridInitializationJobHandle.Complete();

            tileMap.Dispose(gridInitializationJobHandle);
            
            // TODO: do we need this?
            // beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
        
        private static void FillUnpopulatedTiles(GameSettings settings,
            ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            Vector2Int gridSize = settings.gridSize;
            
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int2 position = new int2(x, y);
                    
                    // if add fails, point already populated by non-Empty TileType
                    tileMap.TryAdd(position, TileType.Empty);
                }
            }
        }
        
        private static bool AnyAdjacentTileIsType(int2 mapPoint, TileType tileType,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings)
        {
            int2 pointAbove = MoveInDirection(mapPoint, Direction.Up);
            if (IsWithinGrid(pointAbove, settings.gridSize) && tileMap[pointAbove] == tileType)
                return true;
            
            int2 pointRight = MoveInDirection(mapPoint, Direction.Right);
            if (IsWithinGrid(pointRight, settings.gridSize) && tileMap[pointRight] == tileType)
                return true;
            
            int2 pointBelow = MoveInDirection(mapPoint, Direction.Down);
            if (IsWithinGrid(pointBelow, settings.gridSize) && tileMap[pointBelow] == tileType)
                return true;
            
            int2 pointLeft = MoveInDirection(mapPoint, Direction.Left);
            if (IsWithinGrid(pointLeft, settings.gridSize) && tileMap[pointLeft] == tileType)
                return true;

            return false;
        }

        private static void PlaceTiles(TileType tileType, int tileCount,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings, ref Random random)
        {
            int remainingCount = tileCount;

            while (remainingCount > 0)
            {
                int2 mapPoint = GetRandomPointInGrid(ref random, settings);
                
                if (tileMap[mapPoint] != TileType.Blocked && 
                    !AnyAdjacentTileIsType(mapPoint, TileType.Blocked, ref tileMap, settings))
                    continue;

                tileMap[mapPoint] = tileType;
                remainingCount--;
            }
        }
    }
}
