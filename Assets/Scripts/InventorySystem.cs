using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InventorySystem : MonoBehaviour
{
    [Serializable]
    private sealed class InventoryEntry
    {
        public ItemData item;
        public int quantity;
    }

    [Header("Backpack UI References")]
    [SerializeField] private Transform slotsRoot;
    [SerializeField] private GameObject detailsPanel;
    [SerializeField] private Image detailsIcon;
    [SerializeField] private TMP_Text detailsName;
    [SerializeField] private TMP_Text detailsCategory;
    [SerializeField] private TMP_Text detailsDescription;
    [SerializeField] private TMP_Text detailsQuantity;
    [SerializeField] private TMP_Text capacityText;

    private readonly List<InventoryEntry> entries = new List<InventoryEntry>();
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private int selectedIndex = -1;

    public static InventorySystem Instance { get; private set; }
    public event Action InventoryChanged;

    private void Awake()
    {
        Instance = this;
        InitializeSlots();
        EnsureEntryCapacity();
        RefreshUI();
    }

    public bool TryAddItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0 || slots.Count == 0)
            return false;

        int freeCapacity = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null)
                freeCapacity += item.MaxStack;
            else if (entry.item == item)
                freeCapacity += Mathf.Max(0, item.MaxStack - entry.quantity);
        }
        if (freeCapacity < quantity)
            return false;

        int remaining = quantity;
        for (int i = 0; i < entries.Count && remaining > 0; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.item != item || entry.quantity >= item.MaxStack)
                continue;
            int added = Mathf.Min(remaining, item.MaxStack - entry.quantity);
            entry.quantity += added;
            remaining -= added;
        }

        while (remaining > 0)
        {
            int emptyIndex = entries.FindIndex(entry => entry == null);
            if (emptyIndex < 0)
                return false;
            int added = Mathf.Min(remaining, item.MaxStack);
            entries[emptyIndex] = new InventoryEntry { item = item, quantity = added };
            if (selectedIndex < 0)
                selectedIndex = emptyIndex;
            remaining -= added;
        }

        NotifyChanged();
        return true;
    }

    public int GetItemCount(ItemData item)
    {
        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].item == item)
                total += entries[i].quantity;
        }
        return total;
    }

    public bool HasItem(ItemData item, int quantity = 1) =>
        item != null && GetItemCount(item) >= quantity;

    public bool RemoveItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0 || GetItemCount(item) < quantity)
            return false;

        for (int i = entries.Count - 1; i >= 0 && quantity > 0; i--)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.item != item)
                continue;
            int removed = Mathf.Min(quantity, entry.quantity);
            entry.quantity -= removed;
            quantity -= removed;
            if (entry.quantity <= 0)
                entries[i] = null;
        }

        if (GetOccupiedSlotCount() == 0)
            selectedIndex = -1;
        NotifyChanged();
        return true;
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return;
        selectedIndex = index;
        RefreshUI();
    }

    public bool HasItemAt(int index) =>
        index >= 0 && index < entries.Count && entries[index] != null;

    public Sprite GetItemIconAt(int index) =>
        HasItemAt(index) ? entries[index].item.Icon : null;

    public void MoveOrSwapSlots(int fromIndex, int toIndex)
    {
        if (!HasItemAt(fromIndex) || toIndex < 0 || toIndex >= entries.Count || fromIndex == toIndex)
            return;

        InventoryEntry temp = entries[toIndex];
        entries[toIndex] = entries[fromIndex];
        entries[fromIndex] = temp;
        selectedIndex = toIndex;
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        RefreshUI();
        InventoryChanged?.Invoke();
    }

    private void InitializeSlots()
    {
        slots.Clear();
        if (slotsRoot == null)
            return;

        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            GameObject slotObject = slotsRoot.GetChild(i).gameObject;
            InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>();
            if (slot == null)
            {
                Debug.LogWarning("Inventory slot is missing InventorySlotUI: " + slotObject.name, slotObject);
                continue;
            }
            slot.Initialize(this, slots.Count);
            slots.Add(slot);
        }
    }

    private void EnsureEntryCapacity()
    {
        while (entries.Count < slots.Count)
            entries.Add(null);
        if (entries.Count > slots.Count)
            entries.RemoveRange(slots.Count, entries.Count - slots.Count);
    }

    private int GetOccupiedSlotCount()
    {
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                count++;
        }
        return count;
    }

    private void RefreshUI()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry != null)
                slots[i].ShowItem(entry.item.Icon, entry.quantity, i == selectedIndex);
            else
                slots[i].ShowEmpty(i == selectedIndex);
        }

        if (capacityText != null)
            capacityText.text = $"容量 {GetOccupiedSlotCount()} / {slots.Count}";

        bool hasSelection = HasItemAt(selectedIndex);
        if (detailsPanel != null)
            detailsPanel.SetActive(hasSelection);
        if (!hasSelection)
            return;

        InventoryEntry selected = entries[selectedIndex];
        if (detailsIcon != null)
        {
            detailsIcon.sprite = selected.item.Icon;
            detailsIcon.preserveAspect = true;
        }
        if (detailsName != null) detailsName.text = selected.item.DisplayName;
        if (detailsCategory != null) detailsCategory.text = selected.item.CategoryName;
        if (detailsDescription != null) detailsDescription.text = selected.item.Description;
        if (detailsQuantity != null) detailsQuantity.text = "持有数量：" + GetItemCount(selected.item);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
