using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public class ProductCountLabelHolder : IComponentData
    {
        public ProductCountLabel label;
    }
}
