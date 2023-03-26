using Unity.Entities;

namespace DapperTest
{
    public partial class ProductCountLabelUpdateSystem : SystemBase
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
                .WithAny<ProductCountLabelNeedsUpdateTag>()
                .ForEach((Entity entity, ProductCountLabelHolder holder, in ProductCountData productCountData) =>
            {
                holder.label.SetAvailableProductsCount(productCountData.availableProductCount); 
                commandBuffer.RemoveComponent<ProductCountLabelNeedsUpdateTag>(entity);
            }).WithoutBurst().Run();
        }
    }
}
