using UnityEngine;
using System.Collections.Generic;
using LitJson;
using System.IO;

[RequireComponent(typeof(Inventory))]  // Add this if ItemDatabase should always be with Inventory
public class ItemDatabase : MonoBehaviour
{
    private List<Item> database = new List<Item>();
    private JsonData itemData;

    void Start()
    {
        itemData = JsonMapper.ToObject(File.ReadAllText(Application.dataPath + "/StreamingAssets/Items.json"));
        ConstructItemDatabase();
        
        Debug.Log(FetchItemByID(0).Description);
    }

    public Item FetchItemByID(int id)
    {
        for (int i = 0; i < database.Count; i++)
            if (database[i].ID == id)
            {
                return database[i];
            }
        return null;
    }

    void ConstructItemDatabase()
    {
        for (int i = 0; i < itemData.Count; i++)
        {
            database.Add(new Item(
                (int)itemData[i]["id"],
                itemData[i]["title"].ToString(),
                (int)itemData[i]["value"],
                itemData[i]["type"].ToString(),
                (int)itemData[i]["stats"]["power"],
                (int)itemData[i]["stats"]["defense"],
                (int)itemData[i]["stats"]["chance"],
                (int)itemData[i]["stats"]["health"],
                (int)itemData[i]["stats"]["deals"],
                itemData[i]["description"].ToString(),
                (bool)itemData[i]["stackable"],
                (int)itemData[i]["rarity"],
                itemData[i]["slug"].ToString()));
        }
    }
}