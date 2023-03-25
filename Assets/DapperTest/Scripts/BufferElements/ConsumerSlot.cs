using Unity.Entities;

namespace DapperTest
{
    public struct ConsumerSlot : IBufferElementData
    {
        public Entity entity;
        public int availableProducts;
    }
}
