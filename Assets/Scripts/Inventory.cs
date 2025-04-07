using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
   public GameObject inventoryPanel;
   public GameObject slotPanel;
   public GameObject inventorySlot;
   public GameObject inventoryItem;

   public int slotAmount;
   public List<Item> items = new List<Item>();
   public List<GameObject> slots = new List<GameObject>();

   void Start()
   {
      slotAmount = 42;
      inventoryPanel = GameObject.Find("Inventory Panel");
      slotPanel = inventoryPanel.transform.Find("Slot Panel").gameObject;
      for (int i = 0; i < slotAmount; i++)
      {
         slots.Add(Instantiate(inventorySlot));
      }
   }
}
