using Unity.Entities;
using UnityEngine;

namespace Root
{
    [GenerateAuthoringComponent]
    public struct GameSettings : IComponentData
    {
        // grid size
        public Vector2Int gridSize;
        public float tileSize;
        
        // prefabs
        public Entity inhabitablePrefab;
        public Entity voidPrefab;
        
        public Entity vehiclePrefab;
    }
}
