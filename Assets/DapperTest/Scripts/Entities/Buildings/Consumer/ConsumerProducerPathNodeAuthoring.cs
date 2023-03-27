using Unity.Entities;
using UnityEngine;

namespace DapperTest
{
    public class ConsumerProducerPathNodeAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddBuffer<ConsumerProducerPathNode>(entity);
        }
    }
}
