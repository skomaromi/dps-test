using TMPro;
using UnityEngine;

namespace DapperTest
{
    public class ProductCountLabel : MonoBehaviour
    {
        [SerializeField] private TextMeshPro label;

        public void SetAvailableProductsCount(int count)
        {
            string text = "Available products: " + count;
            label.text = text;
        }
    }
}
