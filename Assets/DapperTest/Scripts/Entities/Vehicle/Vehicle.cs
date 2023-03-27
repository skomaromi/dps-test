using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct Vehicle : IComponentData
    {
        public float movementIntervalSeconds;
    }
}
