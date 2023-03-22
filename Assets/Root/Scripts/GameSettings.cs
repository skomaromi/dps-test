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
        public int maxEmptyTiles;
        
        // a factor of grid area, used to prevent infinite loops
        public float moveLimitFactor;

        // prefabs
        public Entity blockedPrefab;
        public Entity emptyPrefab;

        public Entity vehiclePrefab;
    }
}
