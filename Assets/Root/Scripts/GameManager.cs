using Unity.Entities;
using UnityEngine;

namespace Root
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private GameObject vehiclePrefab;

        private Entity vehicleOriginalEntity;
        
        private BlobAssetStore blobAssetStore;

        private void Awake()
        {
            InitializePrefabConversion();
            ConvertPrefab(vehiclePrefab, out vehicleOriginalEntity);
        }

        private void Start()
        {
            Instantiate(vehicleOriginalEntity, new Vector3(1, 2, 3));
        }

        private void Instantiate(Entity original, Vector3 position)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            EntityManager entityManager = world.EntityManager;
            entityManager.Instantiate(original);
        }

        private void OnDestroy()
        {
            blobAssetStore.Dispose();
        }
        
        private void InitializePrefabConversion()
        {
            blobAssetStore = new BlobAssetStore();
        }

        private void ConvertPrefab(GameObject gameObject, out Entity entity)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            GameObjectConversionSettings settings = GameObjectConversionSettings.FromWorld(world, blobAssetStore);
            entity = GameObjectConversionUtility.ConvertGameObjectHierarchy(gameObject, settings);
        }
    }
}