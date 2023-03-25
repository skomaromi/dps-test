using Unity.Entities;

namespace DapperTest
{
    public struct ConsumerSlot : IBufferElementData
    {
        public Entity entity;
        
        // incremented by Producer
        public int availableProducts;
        // incremented by Consumer, represents how much vehicles have been
        // dispatched so far
        public int reservedProducts;
    }
}
