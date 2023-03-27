using TMPro;
using UnityEngine;

namespace DapperTest
{
    public class ProductCountLabel : MonoBehaviour
    {
        private const string AvailableProductsTextFormat = "Available products: {0}";
        
        [SerializeField] private TMP_Text label;

        public void SetAvailableProductsCount(int count)
        {
            // GC allocating TMP_Text.InternalTextBackingArrayToString() called
            // due to UNITY_EDITOR scripting define, standalone builds should
            // not be affected by this 
            label.SetText(AvailableProductsTextFormat, count);
        }
    }
}
