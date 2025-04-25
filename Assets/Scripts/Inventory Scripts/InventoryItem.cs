using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]  // Add this if it's a UI element
public class InventoryItem : MonoBehaviour
{
    [SerializeField]  // Add this to make it visible in inspector
    private Item item;

    public void Setup(Item item)
    {
        this.item = item;

        Transform iconTransform = transform.Find("Icon");
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null && item.Icon != null)
            {
                iconImage.sprite = item.Icon;
            }
        }

        Debug.Log($"Inventory item set up with ID: {item.ID}, Name: {item.Title}");
    }
}