using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    public struct VehicleMovement : IComponentData
    {
        public Entity consumerEntity;
        public Entity producerEntity;
        public BuildingType targetBuildingType;
        public double timeLastMoved;
        public int pathIndex;
        
        public float3 currentPosition;
        public float3 nextPosition;
    }
}
