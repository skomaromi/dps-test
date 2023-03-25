using Unity.Entities;

namespace DapperTest
{
    public partial class ProducerManagementSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            double time = Time.ElapsedTime;
            
            Entities.ForEach((DynamicBuffer<ConsumerSlot> consumerSlotBuffer, ref Producer producer) =>
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
                producer.availableProductCount++;

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
        }
    }
}
