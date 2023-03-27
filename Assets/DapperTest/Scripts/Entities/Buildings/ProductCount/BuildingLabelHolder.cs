using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public class BuildingLabelHolder : IComponentData
    {
        public BuildingLabel label;
    }
}
