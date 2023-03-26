using TMPro;
using UnityEngine;

namespace DapperTest
{
    public class ProductCountLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        public void SetAvailableProductsCount(int count)
        {
            string text = "Available products: " + count;
            label.text = text;
        }
    }
}
