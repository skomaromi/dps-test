using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct GameSettings : IComponentData
    {
        // generation parameters
        public int2 gridSize;
        public float tileSize;
        
        // factor of grid area (gridSize.x * gridSize.y)
        public float maxEmptyTilesFactor;
        
        // used to prevent infinite loops
        // factor of grid area
        public float moveLimitFactor;
        
        // specific tile settings
        public int producerCount;
        public int consumerCount;

        // prefabs
        public Entity emptyPrefab;
        public Entity blockedPrefab;
        
        public Entity roadPrefab;
        public Entity producerPrefab;
        public Entity consumerPrefab;
        
        public Entity vehiclePrefab;

        public Entity GetTilePrefab(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Empty:
                    return emptyPrefab;
                
                case TileType.Blocked:
                    return blockedPrefab;
                
                case TileType.Road:
                    return roadPrefab;
                
                case TileType.Producer:
                    return producerPrefab;
                
                case TileType.Consumer:
                    return consumerPrefab;
                
                default:
                    return Entity.Null;
            }
        }
    }
}
