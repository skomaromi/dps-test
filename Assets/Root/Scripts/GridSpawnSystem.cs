using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Root
{
    public partial class GridSpawnSystem : SystemBase
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

        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            Entity settingsEntity = GetSingletonEntity<GameSettings>();
            
            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();

            Random random = new Random((uint)Stopwatch.GetTimestamp());
            
            Vector2Int gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;

            NativeParallelHashMap<int2, TileType> tileMap =
                new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.TempJob);

            Job.WithCode(() =>
            {
                WalkMap(settings, ref tileMap, ref random);
                FillUnpopulatedTiles(settings, ref tileMap);
                PlaceTiles(TileType.Producer, settings.producerCount, ref tileMap, settings, ref random);
                PlaceTiles(TileType.Consumer, settings.consumerCount, ref tileMap, settings, ref random);
                PlaceRoadTiles(60, ref tileMap, settings, ref random);
                SpawnPrefabs(settings, ref commandBuffer, ref tileMap);

                // add tag to mark grid as initialized
                commandBuffer.AddComponent<GridInitializedTag>(settingsEntity);
            }).Schedule();
            
            Dependency = tileMap.Dispose(Dependency);
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }

        private static void PlaceRoadTiles(int tileCount, ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings, ref Random random)
        {
            int remainingCount = tileCount;

            int2 mapPoint = default;
            bool hasPoint = false;
            Direction direction = default;
            bool seekingNeighbour = false;
            bool triedNewDirection = false;

            while (true)
            {
                if (remainingCount < 0)
                    break;

                if (hasPoint)
                {
                    if (!seekingNeighbour)
                    {
                        tileMap[mapPoint] = TileType.Road;
                        remainingCount--;
                    }
                    
                    int2 pointCandidate = MoveInDirection(mapPoint, direction);

                    if (!IsWithinGrid(pointCandidate, settings.gridSize))
                    {
                        if (!triedNewDirection)
                        {
                            direction = GetRandomViableDirection(ref random, mapPoint, settings);
                            triedNewDirection = true;
                            seekingNeighbour = true;
                        }
                        else
                        {
                            hasPoint = false;
                            seekingNeighbour = false;
                        }
                    }
                    else
                    {
                        // is within grid
                        mapPoint = pointCandidate;
                        hasPoint = true;

                        seekingNeighbour = false;
                    }
                }
                else
                {
                    int2 pointCandidate = GetRandomPointInGrid(ref random, settings);

                    if (tileMap[pointCandidate] != TileType.Blocked)
                        continue;

                    mapPoint = pointCandidate;
                    hasPoint = true;
                }
            }
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

        private static bool AnyAdjacentTileIsType(int2 mapPoint, TileType tileType,
            ref NativeParallelHashMap<int2, TileType> tileMap, GameSettings settings)
        {
            // TODO: bring bounds check closer, deduplicate
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

        private static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            foreach (KeyValue<int2, TileType> pair in tileMap)
            {
                Entity entityInstance = commandBuffer.Instantiate(settings.GetTilePrefab(pair.Value));

                int2 tileCoordinates = pair.Key;
                float tileSize = settings.tileSize;

                float3 tilePosition = new float3(
                    tileSize * tileCoordinates.x,
                    0f,
                    tileSize * tileCoordinates.y);

                Translation translation = new Translation() { Value = tilePosition };
                commandBuffer.SetComponent(entityInstance, translation);
            }
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
    }
}
