using System;
using TMPro;
using UnityEngine;

namespace DapperTest
{
    public class BuildingLabel : MonoBehaviour
    {
        private const string ProducerTitle = "Producer";
        private const string ConsumerTitle = "Consumer";
        private const string AvailableProductsTextFormat = "Available products: {0}";

        [SerializeField] private TMP_Text buildingTypeLabel;
        [SerializeField] private TMP_Text countLabel;
        
        public void SetBuildingType(BuildingType buildingType)
        {
            switch (buildingType)
            {
                case BuildingType.Producer:
                    buildingTypeLabel.SetText(ProducerTitle);
                    break;
                
                case BuildingType.Consumer:
                    buildingTypeLabel.SetText(ConsumerTitle);
                    break;
            }
        }

        public void SetAvailableProductsCount(int count)
        {
            // GC allocating TMP_Text.InternalTextBackingArrayToString() called
            // due to UNITY_EDITOR scripting define, standalone builds should
            // not be affected by this 
            countLabel.SetText(AvailableProductsTextFormat, count);
        }
    }
}
