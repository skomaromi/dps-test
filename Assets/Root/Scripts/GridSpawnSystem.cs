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
                    
                    // if add fails, point already populated by non-Empty TileType
                    tileMap.TryAdd(position, TileType.Empty);
                }
            }
        }
    }
}
