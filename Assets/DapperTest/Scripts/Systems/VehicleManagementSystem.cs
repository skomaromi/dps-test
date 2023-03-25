using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DapperTest
{
    public partial class VehicleManagementSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
        }

        protected override void OnUpdate()
        {
            GameSettings settings = GetSingleton<GameSettings>();
            BufferFromEntity<ConsumerProducerPathNode> consumerProducerPathBufferFromEntity = GetBufferFromEntity<ConsumerProducerPathNode>();
            BufferFromEntity<ConsumerSlot> consumerSlotBufferFromEntity = GetBufferFromEntity<ConsumerSlot>();
            ComponentDataFromEntity<Producer> producerFromEntity = GetComponentDataFromEntity<Producer>();
            ComponentDataFromEntity<Consumer> consumerFromEntity = GetComponentDataFromEntity<Consumer>();
            EntityCommandBuffer commandBuffer = beginSimulationSystem.CreateCommandBuffer();
            double time = Time.ElapsedTime;
            
            Entities.ForEach((Entity vehicleEntity, ref VehicleMovement vehicleMovement, in Vehicle vehicle) =>
            {
                double nextMovementTime = vehicleMovement.timeLastMoved + vehicle.movementIntervalSeconds;
                if (time < nextMovementTime)
                    return;

                Entity consumerEntity = vehicleMovement.consumerEntity;
                
                if (vehicleMovement.targetBuildingType == BuildingType.Producer)
                {
                    // going from consumer to producer
                    // increment along path
                    int newPathIndex = vehicleMovement.pathIndex + 1;
                    
                    // does the next point exist?
                    DynamicBuffer<ConsumerProducerPathNode> customerProducerPathBuffer = consumerProducerPathBufferFromEntity[consumerEntity];
                    int maxPathIndex = customerProducerPathBuffer.Length - 1;
                    bool nextPointExists = newPathIndex <= maxPathIndex;

                    if (nextPointExists)
                    {
                        // commit movement
                        ConsumerProducerPathNode pathNode = customerProducerPathBuffer[newPathIndex];
                        float3 tilePosition = settings.ConvertToWorldPosition(pathNode.gridPosition);
                        Translation translation = new Translation() { Value = tilePosition };
                        commandBuffer.SetComponent(vehicleEntity, translation);

                        vehicleMovement.pathIndex = newPathIndex;
                    }
                    else
                    {
                        // handle producer reached
                        Entity producerEntity = vehicleMovement.producerEntity;
                        
                        // remove one product
                        // ... from total available products
                        Producer producer = producerFromEntity[producerEntity];
                        producer.availableProductCount--;
                        producerFromEntity[producerEntity] = producer;
                        
                        // ... from own slot
                        DynamicBuffer<ConsumerSlot> consumerSlotBuffer = consumerSlotBufferFromEntity[producerEntity];

                        for (int i = consumerSlotBuffer.Length - 1; i >= 0; i--)
                        {
                            ConsumerSlot consumerSlot = consumerSlotBuffer[i];
                            
                            if (consumerSlot.entity != consumerEntity)
                                continue;

                            consumerSlot.availableProducts--;
                            consumerSlot.reservedProducts--;
                        }

                        vehicleMovement.targetBuildingType = BuildingType.Consumer;
                    }
                    
                }
                else
                {
                    // going from producer to consumer
                    // decrement along path
                    int newPathIndex = vehicleMovement.pathIndex - 1;
                    bool nextPointExists = newPathIndex >= 0;

                    if (nextPointExists)
                    {
                        // commit movement
                        // TODO: this code is duplicated from above
                        DynamicBuffer<ConsumerProducerPathNode> customerProducerPathBuffer = consumerProducerPathBufferFromEntity[consumerEntity];
                        ConsumerProducerPathNode pathNode = customerProducerPathBuffer[newPathIndex];
                        float3 tilePosition = settings.ConvertToWorldPosition(pathNode.gridPosition);
                        Translation translation = new Translation() { Value = tilePosition };
                        commandBuffer.SetComponent(vehicleEntity, translation);

                        vehicleMovement.pathIndex = newPathIndex;
                    }
                    else
                    {
                        // handle consumer reached
                        // increment consumer product count
                        Consumer consumer = consumerFromEntity[consumerEntity];
                        consumer.availableProductCount++;
                        commandBuffer.SetComponent(consumerEntity, consumer);
                        
                        // destroy self
                        commandBuffer.DestroyEntity(vehicleEntity);
                    }
                }

                vehicleMovement.timeLastMoved = time;
            }).Schedule();
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
