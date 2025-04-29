using UnityEngine;

[System.Serializable]  // Add this attribute to make it serialize properly
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
    public Sprite Icon { get; set; }

    public Item(int id, string title, int value, string type, int power, int defense, 
        int chance, int health, int deals, string description, bool stackable, 
        int rarity, string slug)
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

    public Item()
    {
        this.ID = -1;
    }
}