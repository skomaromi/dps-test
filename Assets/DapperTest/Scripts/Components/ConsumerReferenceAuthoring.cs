using Unity.Entities;
using UnityEngine;

namespace DapperTest
{
    public class ConsumerReferenceAuthoring : MonoBehaviour, IConvertGameObjectToEntity
    {
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddBuffer<ConsumerReference>(entity);
        }
    }
}
