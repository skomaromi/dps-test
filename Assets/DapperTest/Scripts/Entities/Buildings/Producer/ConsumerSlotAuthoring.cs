using Unity.Entities;
using UnityEngine;

namespace DapperTest
{
    public class ConsumerSlotAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddBuffer<ConsumerSlot>(entity);
        }
    }
}
