using Unity.Entities;

namespace Root
{
    public partial class RoadSpawnSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(typeof(HasPendingProducerAssociationTag));
        }

        protected override void OnUpdate()
        {
            if (query.CalculateEntityCount() > 0)
                return;

            Entities.WithAll<Consumer>();
        }
    }
}
