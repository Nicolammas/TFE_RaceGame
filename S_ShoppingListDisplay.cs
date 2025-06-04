using UnityEngine;
using TMPro;
using DG.Tweening;

public class S_ShoppingListDisplay : MonoBehaviour
{
    [SerializeField] private Transform listContent;
    [SerializeField] private GameObject listItemPrefab;

    private void Start()
    {
        PopulateList();
    }

    void PopulateList()
    {
        foreach (Transform child in listContent)
            Destroy(child.gameObject);

        foreach (string itemName in S_ShoppingListManager.SharedShoppingList)
        {
            GameObject itemGO = Instantiate(listItemPrefab, listContent);
            itemGO.GetComponent<TMP_Text>().text = itemName;
        }
    }
}
