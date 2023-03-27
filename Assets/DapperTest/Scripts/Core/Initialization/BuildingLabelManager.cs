using Unity.Mathematics;
using UnityEngine;

namespace DapperTest
{
    public class BuildingLabelManager : MonoBehaviour
    {
        [SerializeField] private GameObject labelPrefab;
        
        private static BuildingLabelManager instance;
        public static BuildingLabelManager Instance => instance;

        private void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        public BuildingLabel InstantiateLabel(float3 position)
        {
            GameObject labelObjectInstance = Instantiate(labelPrefab, position, Quaternion.identity);
            BuildingLabel label = labelObjectInstance.GetComponent<BuildingLabel>();
            label.SetAvailableProductsCount(0);
            return label;
        }
    }
}
