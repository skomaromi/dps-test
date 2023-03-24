using Unity.Entities;

namespace DapperTest
{
    public struct Consumer : IComponentData
    {
        public Entity associatedProducer;
    }
}
