using Unity.Entities;

namespace DapperTest
{
    [GenerateAuthoringComponent]
    public struct Producer : IComponentData
    {
        // configuration
        public float productionIntervalSeconds;
        
        // state
        public int lastRecipientConsumerIndex;
        public double timeLastProduced;
    }
}
