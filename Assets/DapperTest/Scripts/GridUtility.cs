using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DapperTest
{
    public static class GridUtility
    {
        public static bool IsWithinGrid(int2 position, int2 gridSize)
        {
            return
                position.x >= 0 && position.x < gridSize.x &&
                position.y >= 0 && position.y < gridSize.y;
        }
        
        public static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap)
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

        public static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap, TileType tileTypeToSpawn)
        {
            foreach (KeyValue<int2, TileType> pair in tileMap)
            {
                TileType tileType = pair.Value;
                
                if (tileType != tileTypeToSpawn)
                    continue;

                Entity entityInstance = commandBuffer.Instantiate(settings.GetTilePrefab(tileType));

                float tileSize = settings.tileSize;
                int2 tileCoordinates = pair.Key;

                float3 tilePosition = new float3(
                    tileSize * tileCoordinates.x, 
                    0f, 
                    tileSize * tileCoordinates.y);
                
                Translation translation = new Translation() { Value = tilePosition };
                commandBuffer.SetComponent(entityInstance, translation);
                
                // TODO: `if` on every foreach iteration, refactor?
                if (tileType == TileType.Producer || tileType == TileType.Consumer)
                {
                    // configure GridTranslation
                    GridTranslation gridTranslation = new GridTranslation()
                    {
                        position = tileCoordinates
                    };
                    
                    commandBuffer.SetComponent(entityInstance, gridTranslation);
                }
            }
        }
    }
}
