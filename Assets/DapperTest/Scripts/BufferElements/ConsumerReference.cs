using Unity.Entities;

namespace DapperTest
{
    public struct ConsumerReference : IBufferElementData
    {
        public Entity entity;
    }
}
