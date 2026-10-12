using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Backpack/Item Data")]
public sealed class ItemData : ScriptableObject //物品数据：Item Data
{
    // 物品数据类，继承自 ScriptableObject，用于在 Unity 编辑器中创建和管理物品数据资产。
    //使用 ScriptableObject 的好处，是场景物体、背包 UI、任务系统都可以引用同一份物品资料，不必在每个预制体里重复填写名称、图标和描述。
    [Header("Identity")]
    [SerializeField] private string itemId; //物品唯一 ID
    [SerializeField] private string displayName; //显示名称

    [Header("Presentation")]
    [SerializeField] private Sprite icon; //背包图标
    [SerializeField] private string categoryName = "材料"; //物品类别
    [TextArea(2, 6)] 
    [SerializeField] private string description; //物品描述

    [Header("Inventory Rules")]
    [Min(1)]
    [SerializeField] private int maxStack = 99; //单个格子最大堆叠数量

    public string ItemId => string.IsNullOrWhiteSpace(itemId) ? name : itemId; 
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public string CategoryName => categoryName;
    public string Description => description;
    public int MaxStack => Mathf.Max(1, maxStack);
}
