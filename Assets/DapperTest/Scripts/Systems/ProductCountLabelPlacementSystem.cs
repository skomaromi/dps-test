using Unity.Entities;
using Unity.Transforms;

namespace DapperTest
{
    public partial class ProductCountLabelPlacementSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();
            
            Entities
                .WithAny<NeedsProductCountLabelTag>()
                .ForEach((Entity entity, ProductCountLabelHolder holder, in Translation translation) =>
            {
                holder.label = ProductCountLabelManager.Instance.InstantiateLabel(translation.Value);
                commandBuffer.RemoveComponent<NeedsProductCountLabelTag>(entity);
            }).WithoutBurst().Run();
        }
    }
}
