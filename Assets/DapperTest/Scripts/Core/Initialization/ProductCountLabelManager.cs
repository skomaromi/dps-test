using Unity.Mathematics;
using UnityEngine;

namespace DapperTest
{
    public class ProductCountLabelManager : MonoBehaviour
    {
        [SerializeField] private GameObject labelPrefab;
        
        private static ProductCountLabelManager instance;
        public static ProductCountLabelManager Instance => instance;

        private void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        public ProductCountLabel InstantiateLabel(float3 position)
        {
            GameObject labelObjectInstance = Instantiate(labelPrefab, position, Quaternion.identity);
            ProductCountLabel label = labelObjectInstance.GetComponent<ProductCountLabel>();
            label.SetAvailableProductsCount(0);
            return label;
        }
    }
}
