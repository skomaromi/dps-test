using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace DapperTest
{
    public struct TilePaintingJob : IJob
    {
        public NativeParallelHashMap<int2, TileType> tileMap;
        public EntityCommandBuffer commandBuffer;
        public GameSettings settings;
        public Entity settingsEntity;

        public void Execute()
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
                
            commandBuffer.AddComponent<GridInitializationCompletedTag>(settingsEntity);
        }
    }
}
