using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using ItemStockage;

public class S_ShoppingListManager : MonoBehaviour
{
    public static List<string> SharedShoppingList { get; private set; } = new List<string>();
    [SerializeField] private S_ItemManager _itemManager;

    [SerializeField] private int minItems = 5;
    [SerializeField] private int maxItems = 10;
    private void Awake()
    {
        GenerateList();
    }

    private void GenerateList()
    {
        SharedShoppingList.Clear();

        string[] subfolders = { "Prefab/Items/Dry", "Prefab/Items/Hygiene", "Prefab/Items/Snacks", "Prefab/Items/Beverages", "Prefab/Items/Bakery", "Prefab/Items/Vegetable", "Prefab/Items/Meat" };

        List<string> pool = new List<string>();
        Regex regex = new Regex(@"^PFB_(.+?)_[A-Z]$");

        foreach (string folder in subfolders)
        {
            GameObject[] allItems = Resources.LoadAll<GameObject>(folder);

            foreach (GameObject item in allItems)
            {
                Match match = regex.Match(item.name);
                if (match.Success)
                {
                    string itemName = match.Groups[1].Value;
                    pool.Add(itemName);
                }
            }
        }

        int itemCount = Random.Range(minItems, maxItems + 1);

        for (int i = 0; i < itemCount && pool.Count > 0; i++)
        {
            int index = Random.Range(0, pool.Count);
            SharedShoppingList.Add(pool[index]);
            pool.RemoveAt(index);
        }
        
        _itemManager.InitializeItemPlacement();
    }
}
