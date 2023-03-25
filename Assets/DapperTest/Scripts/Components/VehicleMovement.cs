using Unity.Entities;

namespace DapperTest
{
    public struct VehicleMovement : IComponentData
    {
        public Entity consumerEntity;
        public Entity producerEntity;
        public BuildingType targetBuildingType;
        public double timeLastMoved;
        public int pathIndex;
    }
}
