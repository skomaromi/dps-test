using Unity.Entities;

namespace DapperTest
{
    public static class ProductCountLabelUtility
    {
        public static void MarkLabelNeedsUpdate(EntityCommandBuffer commandBuffer, Entity entity)
        {
            commandBuffer.AddComponent<ProductCountLabelNeedsUpdateTag>(entity);
        }

        public static void MarkLabelNeedsUpdate(EntityCommandBuffer.ParallelWriter commandBuffer, Entity entity, int entityInQueryIndex)
        {
            commandBuffer.AddComponent<ProductCountLabelNeedsUpdateTag>(entityInQueryIndex, entity);
        }
    }
}
