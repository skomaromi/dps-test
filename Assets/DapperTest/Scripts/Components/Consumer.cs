using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct Consumer : IComponentData
    {
        public Entity associatedProducerEntity;
    }
}
