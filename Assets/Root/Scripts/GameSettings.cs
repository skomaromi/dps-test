using Unity.Entities;
using UnityEngine;

namespace Root
{
    [GenerateAuthoringComponent]
    public struct GameSettings : IComponentData
    {
        // generation parameters
        public Vector2Int gridSize;
        public float tileSize;
        
        // factor of grid area (gridSize.x * gridSize.y)
        public float maxEmptyTilesFactor;
        
        // used to prevent infinite loops
        // factor of grid area
        public float moveLimitFactor;

        // prefabs
        public Entity blockedPrefab;
        public Entity emptyPrefab;

        public Entity producerPrefab;
        public Entity consumerPrefab;
        public Entity vehiclePrefab;
    }
}
