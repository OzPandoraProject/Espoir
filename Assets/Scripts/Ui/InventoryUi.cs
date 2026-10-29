using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InventoryUi : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;
    [SerializeField] private Transform slotParent;
    [SerializeField] private int maxSlots = 128;

    private List<GameObject> slots = new List<GameObject>();

    private void Start()
    {
        CreateSlots();
    }

    private void CreateSlots()
    {
        for (int i = 0; i < maxSlots; i++)
        {
            GameObject newSlot = Instantiate(slotPrefab, slotParent);

            newSlot.name = $"InventorySlot_{i + 1}";

            slots.Add(newSlot);
        }
    }
}
