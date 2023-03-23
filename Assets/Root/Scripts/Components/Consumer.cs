using Unity.Entities;

namespace Root
{
    public struct Consumer : IComponentData
    {
        public Entity associatedProducer;
    }
}
