using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct Vehicle : IComponentData
    {
        public Entity consumerEntity;
        public Entity producerEntity;
        public BuildingType targetBuildingType;
        public int pathIndex;
    }
}
