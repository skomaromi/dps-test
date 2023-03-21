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
    public enum Direction
    {
        Up,
        Right,
        Down,
        Left
    }

    public enum TileType
    {
        Void,
        Inhabitable,
        
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
            
            EntityCommandBuffer ecb = beginSimulationSystem.CreateCommandBuffer();

            Random random = new Random((uint)Stopwatch.GetTimestamp());
            
            Vector2Int gridSize = settings.gridSize;
            int gridArea = gridSize.x * gridSize.y;

            NativeParallelHashMap<int2, TileType> tileMap =
                new NativeParallelHashMap<int2, TileType>(gridArea, Allocator.TempJob);

            Job.WithCode(() =>
            {
                WalkMap(settings, ref tileMap, ref random);
                FillUnpopulatedWithVoid(settings, ref tileMap);
                SpawnPrefabs(settings, ref ecb, ref tileMap);

                // add tag to mark grid as initialized
                ecb.AddComponent<GridInitializedTag>(settingsEntity);
            }).Schedule();

            Dependency = tileMap.Dispose(Dependency);
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }

        private static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap)
        {
            foreach (KeyValue<int2, TileType> pair in tileMap)
            {
                Entity entityInstance = commandBuffer.Instantiate(
                    pair.Value == TileType.Inhabitable ? settings.inhabitablePrefab : settings.voidPrefab);

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

        private static void FillUnpopulatedWithVoid(GameSettings settings,
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
                        
                    tileMap.Add(position, TileType.Void);
                }
            }
        }

        private static bool IsWithinGrid(int2 position, Vector2Int gridSize)
        {
            return
                position.x > 0 && position.x < gridSize.x &&
                position.y > 0 && position.y < gridSize.y;
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
            
            while (remainingSteps > 0)
            {
                if (!tileMap.ContainsKey(position))
                    tileMap.Add(position, TileType.Inhabitable);
                    
                // TODO: might secretly bump head a few times into the same wall, rework logic to change direction
                // the first time it does that
                bool shouldChangeDirection = random.NextBool();

                if (shouldChangeDirection)
                    direction = GetRandomDirection(ref random);

                int2 newPosition = MoveInDirection(position, direction);
                bool nextPositionIsOutOfBounds = !IsWithinGrid(newPosition, gridSize);

                if (nextPositionIsOutOfBounds)
                    continue;
                    
                position = newPosition;
                remainingSteps--;
            }
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
