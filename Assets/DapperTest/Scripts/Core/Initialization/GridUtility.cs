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

        public static void SpawnPrefabs(GameSettings settings, ref EntityCommandBuffer commandBuffer, ref NativeParallelHashMap<int2, TileType> tileMap, TileType tileTypeMask)
        {
            foreach (KeyValue<int2, TileType> pair in tileMap)
            {
                TileType tileType = pair.Value;
                
                if (!tileTypeMask.HasFlag(tileType))
                    continue;

                Entity entityInstance = commandBuffer.Instantiate(settings.GetTilePrefab(tileType));

                int2 tileCoordinates = pair.Key;

                float3 tilePosition = settings.ConvertToWorldPosition(tileCoordinates);
                
                Translation translation = new Translation() { Value = tilePosition };
                commandBuffer.SetComponent(entityInstance, translation);
                
                if (tileType == TileType.Producer || 
                    tileType == TileType.Consumer)
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
