using Unity.Entities;

namespace DapperTest
{
    public partial class ProducerManagementSystem : SystemBase
    {
        private BeginSimulationEntityCommandBufferSystem beginSimulationSystem;

        protected override void OnCreate()
        {
            beginSimulationSystem = World.GetExistingSystem<BeginSimulationEntityCommandBufferSystem>();
            
            RequireForUpdate(GetEntityQuery(GridInitializationSystem.InitializationCompletedQueryTypes));
        }

        protected override void OnUpdate()
        {
            double time = Time.ElapsedTime;
            EntityCommandBuffer.ParallelWriter commandBuffer = beginSimulationSystem.CreateCommandBuffer().AsParallelWriter();

            Entities.ForEach((Entity producerEntity, int entityInQueryIndex, DynamicBuffer<ConsumerSlot> consumerSlotBuffer, ref Producer producer, ref ProductCountData productCountData) =>
            {
                // has time for next production been reached?
                // placement between these two points in time should be as shown
                // below
                // next production time       time (current)
                //          |                        |
                // .........x........................x.................> time [s]
                double nextProductionTime = producer.timeLastProduced + producer.productionIntervalSeconds;
                if (time < nextProductionTime) 
                    return;
                
                // increment total available products
                productCountData.availableProductCount++;
                ProductCountLabelUtility.MarkLabelNeedsUpdate(commandBuffer, producerEntity, entityInQueryIndex);

                // allocate product to next consumer
                int consumerCount = consumerSlotBuffer.Length;

                // don't bother allocating new ticket if no consumers at all
                if (consumerCount != 0)
                {
                    int maxRecipientConsumerIndex = consumerCount - 1;

                    // increment and mathf-repeat index
                    int recipientConsumerIndex = producer.lastRecipientConsumerIndex + 1;

                    if (recipientConsumerIndex > maxRecipientConsumerIndex)
                        recipientConsumerIndex = 0;

                    // commit allocation
                    ConsumerSlot consumerSlot = consumerSlotBuffer[recipientConsumerIndex];
                    consumerSlot.availableProducts++;
                    consumerSlotBuffer[recipientConsumerIndex] = consumerSlot;

                    // store data for next iteration
                    producer.lastRecipientConsumerIndex = recipientConsumerIndex;
                }
                
                producer.timeLastProduced = time;
            }).ScheduleParallel();
            
            beginSimulationSystem.AddJobHandleForProducer(Dependency);
        }
    }
}
