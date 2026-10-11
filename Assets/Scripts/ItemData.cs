using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Backpack/Item Data")]
public sealed class ItemData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;
    [SerializeField] private string categoryName = "材料";
    [TextArea(2, 6)]
    [SerializeField] private string description;

    [Header("Inventory Rules")]
    [Min(1)]
    [SerializeField] private int maxStack = 99;

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string CategoryName => categoryName;
    public string Description => description;
    public int MaxStack => Mathf.Max(1, maxStack);
}
