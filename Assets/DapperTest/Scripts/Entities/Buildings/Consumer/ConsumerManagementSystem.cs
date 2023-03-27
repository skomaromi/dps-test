using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DapperTest
{
    public partial class ConsumerManagementSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
            
            RequireForUpdate(GetEntityQuery(GridInitializationSystem.InitializationCompletedQueryTypes));
        }

        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            BufferFromEntity<ConsumerSlot> consumerSlotBufferFromEntity = GetBufferFromEntity<ConsumerSlot>();
            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();
            
            Entities.ForEach((Entity consumerEntity, in Consumer consumer, in GridTranslation consumerGridTranslation) =>
            {
                Entity producerEntity = consumer.associatedProducerEntity;

                DynamicBuffer<ConsumerSlot> consumerSlotBuffer = consumerSlotBufferFromEntity[producerEntity];
                
                // find consumer slot where entity matches this one
                ConsumerSlot consumerSlot = default;
                int consumerSlotIndex = default;
                
                for (int i = consumerSlotBuffer.Length - 1; i >= 0; i--)
                {
                    ConsumerSlot candidateConsumerSlot = consumerSlotBuffer[i];

                    if (candidateConsumerSlot.entity != consumerEntity) 
                        continue;
                    
                    consumerSlot = candidateConsumerSlot;
                    consumerSlotIndex = i;
                    break;
                }
                
                // dispatch new vehicles if more products available than
                // reserved, where reserved count = amount of vehicles
                // dispatched
                if (consumerSlot.availableProducts > consumerSlot.reservedProducts)
                {
                    // dispatch vehicle
                    // spawn vehicle
                    Entity vehicleInstance = commandBuffer.Instantiate(settings.vehiclePrefab);
                    
                    // spatial setup
                    float3 tilePosition = settings.ConvertToWorldPosition(consumerGridTranslation.position);
                    Translation translation = new Translation() { Value = tilePosition };
                    commandBuffer.SetComponent(vehicleInstance, translation);
                    
                    // vehicle data setup
                    VehicleMovement vehicleMovement = new VehicleMovement
                    {
                        consumerEntity = consumerEntity,
                        producerEntity = producerEntity,
                        targetBuildingType = BuildingType.Producer
                    };
                    commandBuffer.AddComponent(vehicleInstance, vehicleMovement);
                    
                    consumerSlot.reservedProducts++;
                    consumerSlotBuffer[consumerSlotIndex] = consumerSlot;
                }
            }).Schedule();
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
