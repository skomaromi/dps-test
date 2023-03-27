using Unity.Entities;
using Unity.Transforms;

namespace DapperTest
{
    public partial class BuildingLabelPlacementSystem : SystemBase
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
                .ForEach((Entity entity, BuildingLabelHolder holder, in Translation translation) =>
            {
                holder.label = BuildingLabelManager.Instance.InstantiateLabel(translation.Value);

                BuildingType buildingType = EntityManager.HasComponent<Producer>(entity) ? 
                    BuildingType.Producer : 
                    BuildingType.Consumer;
                
                holder.label.SetBuildingType(buildingType);
                
                commandBuffer.RemoveComponent<NeedsProductCountLabelTag>(entity);
            }).WithoutBurst().Run();
        }
    }
}
