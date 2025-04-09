using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
   public GameObject inventoryPanel;
   public GameObject slotPanel;
   public GameObject inventorySlot;
   public GameObject inventoryItem;
   private ItemDatabase database;

   public int slotAmount;
   public List<Item> items = new List<Item>();
   public List<GameObject> slots = new List<GameObject>();

   void Start()
   {
      database = GetComponent<ItemDatabase>();
      
      inventoryPanel = GameObject.Find("Inventory Panel");
      slotPanel = inventoryPanel.transform.Find("Slot Panel").gameObject;
      for (int i = 0; i < slotAmount; i++)
      {
         items.Add(new Item());
      }
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
            itemObj.transform.SetParent(slots[i].transform);
         }
      }
   }
}
