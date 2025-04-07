using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LitJson;
using System.IO;

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
            database.Add(new Item((int)itemData[i]["id"], itemData[i]["title"].ToString(), (int)itemData[i]["value"], itemData[i]["type"].ToString(), 
                (int)itemData[i]["stats"]["power"], (int)itemData[i]["stats"]["defense"], (int)itemData[i]["stats"]["chance"], (int)itemData[i]["stats"]["health"],
                (int)itemData[i]["stats"]["deals"], itemData[i]["description"].ToString(), (bool)itemData[i]["stackable"], 
                (int)itemData[i]["rarity"], itemData[i]["slug"].ToString()));
        }
        
    }
}

public class Item
{
    public int ID { get; set; }
    public string Title { get; set; }
    public int Value { get; set; }
    public string Type { get; set; }
    public int Power { get; set; }
    public int Defense { get; set; }
    public int Chance { get; set; }
    public int Health { get; set; }
    public int Deals { get; set; }
    public string Description { get; set; }
    public bool Stackable { get; set; }
    public int Rarity { get; set; }
    public string Slug { get; set; }

    public Item(int id, string title, int value, string type, int power, int defense, int chance, int health, int deals, string description, bool stackable, int rarity, string slug)
    {
        this.ID = id;
        this.Title = title;
        this.Value = value;
        this.Type = type;
        this.Power = power;
        this.Defense = defense;
        this.Chance = chance;
        this.Health = health;
        this.Deals = deals;
        this.Description = description;
        this.Stackable = stackable;
        this.Rarity = rarity;
        this.Slug = slug;
    }

    /* public Item(int id, string title, int value, string description, bool stackable, int rarity, string slug)
    {
        this.ID = id;
        this.Title = title;
        this.Value = value;
        this.Description = description;
        this.Stackable = stackable;
        this.Rarity = rarity;
        this.Slug = slug;
    }*/
    public Item()
    {
        this.ID = -1;
    }
}