using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct ProductCountData : IComponentData
    {
        public int availableProductCount;
    }
}
