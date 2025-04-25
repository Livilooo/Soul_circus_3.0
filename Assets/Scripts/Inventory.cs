using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(ItemDatabase))]  // Add this to ensure ItemDatabase component exists
public class Inventory : MonoBehaviour
{
    [Header("UI References")]
    public GameObject inventoryPanel;
    public GameObject slotPanel;
    public GameObject inventorySlot;
    public GameObject inventoryItem;

    [Header("Inventory Settings")]
    public int slotAmount;
    public List<Item> items = new List<Item>();
    public List<GameObject> slots = new List<GameObject>();

    private ItemDatabase database;

    void Start()
    {
        database = GetComponent<ItemDatabase>();
        
        inventoryPanel = GameObject.Find("Inventory Panel");
        slotPanel = inventoryPanel.transform.Find("Slot Panel").gameObject;
        for (int i = 0; i < slotAmount; i++)
        {
            items.Add(new Item());
            
            GameObject slot = Instantiate(inventorySlot);
            slot.transform.SetParent(slotPanel.transform, false);
            slots.Add(slot);
        }
        AddItem(0);
    }

    public void AddItem(int id)
    {
        Item itemToAdd = database.FetchItemByID(id);
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].ID == -1)
            {
                items[i] = itemToAdd;
                GameObject itemObj = Instantiate(inventoryItem);
                itemObj.transform.SetParent(slots[i].transform, false);
                
                InventoryItem invItem = itemObj.GetComponent<InventoryItem>();
                if (invItem == null)
                {
                    Debug.LogError("InventoryItem component missing from prefab!");
                    return;
                }
                invItem.Setup(itemToAdd);
                break;
            }
        }
    }
}