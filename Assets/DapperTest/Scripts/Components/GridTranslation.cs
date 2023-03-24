using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    public struct GridTranslation : IComponentData
    {
        public int2 position;
    }
}
