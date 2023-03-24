using Unity.Entities;
using Unity.Mathematics;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct GridTranslation : IComponentData
    {
        public int2 position;
    }
}
