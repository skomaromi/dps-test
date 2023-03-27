using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    public struct ConsumerProducerPathNode : IBufferElementData
    {
        public int2 gridPosition;
    }
}
