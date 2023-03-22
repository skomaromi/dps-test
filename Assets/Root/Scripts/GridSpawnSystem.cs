using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Random = Unity.Mathematics.Random;

namespace Root
{
    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    public enum TileType
    {
        Empty,
        Blocked,
        
        Road,
        Producer,
        Consumer
    }
    
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
                FillUnpopulatedWithEmpty(settings, ref tileMap);
                SpawnPrefabs(settings, ref commandBuffer, ref tileMap);

                // add tag to mark grid as initialized
                commandBuffer.AddComponent<GridInitializedTag>(settingsEntity);
            }).Schedule();

            Dependency = tileMap.Dispose(Dependency);
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }

        private static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            foreach (KeyValue<int2, TileType> pair in tileMap)
            {
                Entity entityInstance = commandBuffer.Instantiate(
                    pair.Value == TileType.Blocked ? settings.blockedPrefab : settings.emptyPrefab);

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

        private static void FillUnpopulatedWithEmpty(GameSettings settings,
            ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            Vector2Int gridSize = settings.gridSize;
            
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    int2 position = new int2(x, y);
                    if (tileMap.ContainsKey(position))
                        continue;
                        
                    tileMap.Add(position, TileType.Empty);
                }
            }
        }

        private static bool IsWithinGrid(int2 position, Vector2Int gridSize)
        {
            return
                position.x >= 0 && position.x < gridSize.x &&
                position.y >= 0 && position.y < gridSize.y;
        }

        private static void WalkMap(GameSettings settings, ref NativeParallelHashMap<int2, TileType> tileMap,
            ref Random random)
        {
            Vector2Int gridSize = settings.gridSize;
            
            int gridMinAxis = math.min(gridSize.x, gridSize.y);
            int gridArea = gridSize.x * gridSize.y;
            
            int stepCount = random.NextInt(gridMinAxis, gridArea);
            
            UnityEngine.Debug.Log($"chose {stepCount} steps, total cell count: {gridArea}");

            Direction direction = GetRandomDirection(ref random);
            int remainingSteps = stepCount;
            
            int2 startingPosition = new int2(
                random.NextInt(0, gridSize.x),
                random.NextInt(0, gridSize.y));

            int2 position = startingPosition;

            int emptyTiles = gridArea;
            int maxEmptyTiles = settings.maxEmptyTiles;

            int moveLimit = (int)(gridArea * settings.moveLimitFactor);
            int totalMoves = 0;

            bool forceChangeDirection = false;
            
            while (true)
            {
                if (remainingSteps <= 0)
                {
                    // minimum step count is met, but is the empty tiles condition met as well?
                    if (emptyTiles > maxEmptyTiles)
                    {
                        // should continue
                        
                        // ... but what if already running for too long?
                        if (totalMoves >= moveLimit)
                        {
                            UnityEngine.Debug.Log($"running for too long! totalMoves: {totalMoves}, moveLimit: {moveLimit}");
                            break;
                        }
                    }
                    else
                    {
                        // can break
                        break;
                    }
                }

                totalMoves++;
                UnityEngine.Debug.Log($"iteration {totalMoves} of WalkMap");

                if (tileMap.TryAdd(position, TileType.Blocked))
                {
                    UnityEngine.Debug.Log($"marking tile {position.x}, {position.y} as blocked!");
                    emptyTiles--;
                }
                    
                // TODO: might secretly bump head a few times into the same wall, rework logic to change direction
                // the first time it does that
                bool shouldChangeDirection = random.NextBool() || forceChangeDirection;

                if (shouldChangeDirection)
                {
                    direction = GetRandomViableDirection(ref random, position, settings);
                    forceChangeDirection = false;
                }

                int2 newPosition = MoveInDirection(position, direction);
                bool newPositionIsOutOfBounds = !IsWithinGrid(newPosition, gridSize);

                if (newPositionIsOutOfBounds)
                {
                    UnityEngine.Debug.Log("next position out of bounds!");
                    forceChangeDirection = true;
                    continue;
                }
                    
                position = newPosition;
                remainingSteps--;
            }
        }

        // TODO: make more efficient. IsViableMove could directly compute destination position and check if hits
        // the corresponding wall (e.g. for `up` just check incremented y against gridSize)
        private static Direction GetRandomViableDirection(ref Random random, int2 position, GameSettings settings)
        {
            int viableDirectionCount = GetViableDirectionCount(position, settings);
            int directionIndex = random.NextInt(0, viableDirectionCount);
            return GetViableDirection(directionIndex, position, settings);
        }

        private static Direction GetViableDirection(int directionIndex, int2 position, GameSettings settings)
        {
            int candidateIndex = -1;

            bool CheckNext(Direction direction)
            {
                if (!IsViableMove(position, settings, direction)) 
                    return false;
                
                candidateIndex++;
                return candidateIndex == directionIndex;
            }

            if (CheckNext(Direction.Up))
                return Direction.Up;
            
            if (CheckNext(Direction.Right))
                return Direction.Right;
            
            if (CheckNext(Direction.Down))
                return Direction.Down;
            
            return Direction.Left;
        }
        
        private static int GetViableDirectionCount(int2 position, GameSettings settings)
        {
            int viableDirectionCount = 0;

            if (IsViableMove(position, settings, Direction.Up))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Right))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Down))
                viableDirectionCount++;
            if (IsViableMove(position, settings, Direction.Left))
                viableDirectionCount++;

            return viableDirectionCount;
        }

        private static bool IsViableMove(int2 position, GameSettings settings, Direction direction)
        {
            return IsWithinGrid(MoveInDirection(position, direction), settings.gridSize);
        }

        private static int2 MoveInDirection(int2 position, Direction direction)
        {
            int2 newPosition = position;
            
            switch (direction)
            {
                case Direction.Up:
                {
                    newPosition.y++;
                    break;
                }
                
                case Direction.Right:
                {
                    newPosition.x++;
                    break;
                }

                case Direction.Down:
                {
                    newPosition.y--;
                    break;
                }
                
                case Direction.Left:
                {
                    newPosition.x--;
                    break;
                }
            }
            
            return newPosition;
        }

        private static Direction GetRandomDirection(ref Random random)
        {
            return (Direction)random.NextInt(0, (int)Direction.Left);
        }
    }
}
