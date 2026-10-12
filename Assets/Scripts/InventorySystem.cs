using System; 
using System.Collections.Generic; 
using TMPro; 
using UnityEngine; 
using UnityEngine.UI; 

[DisallowMultipleComponent] 
public sealed class InventorySystem : MonoBehaviour //负责保存物品数据、堆叠、容量、交换格子和刷新界面
{
    [Serializable] 
    private sealed class InventoryEntry //表示一个背包格子的数据
    {
        public ItemData item; // 这个格子存放的物品种类
        public int quantity; // 这个格子中的物品数量
    }

    [Header("Backpack UI References")]
    [SerializeField] private Transform slotsRoot; //所有背包格子的父物体
    [SerializeField] private GameObject detailsPanel; //右侧物品详情面板
    [SerializeField] private Image detailsIcon; //详情图标
    [SerializeField] private TMP_Text detailsName; //物品名称
    [SerializeField] private TMP_Text detailsCategory; //物品分类
    [SerializeField] private TMP_Text detailsDescription; //物品描述
    [SerializeField] private TMP_Text detailsQuantity; //总持有数量
    [SerializeField] private TMP_Text capacityText; //背包已占用格数

    private readonly List<InventoryEntry> entries = new List<InventoryEntry>(); //真正的物品数据列表
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>(); //背包格子 UI 列表
    private int selectedIndex = -1; 

    public static InventorySystem Instance { get; private set; } //背包单例，供拾取等其他脚本访问
    public event Action InventoryChanged; 

    private void Awake() //初始化背包数据
    {
        Instance = this; //把当前组件登记为背包单例
        InitializeSlots(); 
        EnsureEntryCapacity(); 
        RefreshUI(); //刷新背包初始显示
    }

    public bool TryAddItem(ItemData item, int quantity = 1) //添加物品
    {
        //检查参数
        if (item == null || quantity <= 0 || slots.Count == 0)
            return false;

        //计算总剩余容量
        int freeCapacity = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null)
                freeCapacity += item.MaxStack;
            else if (entry.item == item)
                freeCapacity += Mathf.Max(0, item.MaxStack - entry.quantity);
        }

        //背包容量不足则全部拒绝
        if (freeCapacity < quantity)
            return false;

        int remaining = quantity; //记录还有多少物品没有放入背包

        //优先补充已有但还没有达到 MaxStack 的同类物品格
        for (int i = 0; i < entries.Count && remaining > 0; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.item != item || entry.quantity >= item.MaxStack)
                continue;
            int added = Mathf.Min(remaining, item.MaxStack - entry.quantity); //本格实际能加入的数量
            entry.quantity += added; //增加当前格数量
            remaining -= added; //扣除已经存入的数量
        }

        //已有物品堆补满后还有剩余，就寻找空格建立新的物品堆
        while (remaining > 0)
        {
            int emptyIndex = entries.FindIndex(entry => entry == null); //寻找第一个空格
            if (emptyIndex < 0)
                return false; 
            int added = Mathf.Min(remaining, item.MaxStack); //一个新格最多放入 MaxStack 个
            entries[emptyIndex] = new InventoryEntry { item = item, quantity = added };
            if (selectedIndex < 0)
                selectedIndex = emptyIndex; //背包原本没有选中项时，自动选中新物品
            remaining -= added;
        }

        NotifyChanged(); //刷新 UI 并通知其他系统
        return true; //告诉拾取脚本添加成功
    }

    public int GetItemCount(ItemData item) //统计指定物品在所有格子中的总数量
    {
        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null && entries[i].item == item)
                total += entries[i].quantity; //累加每个同类物品格的数量
        }
        return total;
    }

    //判断背包中是否拥有至少 quantity 个指定物品
    public bool HasItem(ItemData item, int quantity = 1) =>
        item != null && GetItemCount(item) >= quantity;

    public bool RemoveItem(ItemData item, int quantity = 1) //删除指定数量的物品
    {
        //物品为空、数量无效或持有数量不足时，不执行删除
        if (item == null || quantity <= 0 || GetItemCount(item) < quantity)
            return false;

        //从后面的格子向前删除，优先清理后建立的物品堆
        for (int i = entries.Count - 1; i >= 0 && quantity > 0; i--)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.item != item)
                continue;
            int removed = Mathf.Min(quantity, entry.quantity); //当前格实际删除的数量
            entry.quantity -= removed; //减少当前格中的数量
            quantity -= removed; //减少仍然需要删除的数量
            if (entry.quantity <= 0)
                entries[i] = null; //数量归零后将这个位置重新设为空格
        }

        //整个背包为空时取消选中状态
        if (GetOccupiedSlotCount() == 0)
            selectedIndex = -1;
        NotifyChanged(); //刷新 UI 并发送变化事件
        return true; //告诉调用者删除成功
    }

    public void SelectSlot(int index) //选中指定编号的格子
    {
        //索引超出范围时不进行处理
        if (index < 0 || index >= slots.Count)
            return;
        selectedIndex = index; //记录当前选中的格子
        RefreshUI(); //更新选中框和详情面板
    }

    //判断指定位置是否有效，并且该格子中是否存在物品
    public bool HasItemAt(int index) =>
        index >= 0 && index < entries.Count && entries[index] != null;

    //获取指定格子的物品图标；空格或无效索引返回 null
    public Sprite GetItemIconAt(int index) =>
        HasItemAt(index) ? entries[index].item.Icon : null;

    public void MoveOrSwapSlots(int fromIndex, int toIndex) //移动到空格或交换两个格子的物品
    {
        //起点必须有物品，终点必须有效，并且起点和终点不能相同
        if (!HasItemAt(fromIndex) || toIndex < 0 || toIndex >= entries.Count || fromIndex == toIndex)
            return;

        InventoryEntry temp = entries[toIndex]; //暂存终点原来的物品数据
        entries[toIndex] = entries[fromIndex]; //把起点物品放到终点
        entries[fromIndex] = temp; //把终点原数据放回起点；空格时这里为 null
        selectedIndex = toIndex; //移动完成后选中目标格子
        NotifyChanged(); //刷新界面并通知其他系统
    }

    private void NotifyChanged() //统一处理背包内容变化后的工作
    {
        RefreshUI(); //立即刷新背包界面
        InventoryChanged?.Invoke(); //有订阅者时发送背包变化事件
    }

    private void InitializeSlots() //收集 slotsRoot 下面所有直接子格子
    {
        slots.Clear(); //防止重复初始化时保留旧的格子引用
        if (slotsRoot == null)
            return; //没有设置格子父物体时无法继续初始化

        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            GameObject slotObject = slotsRoot.GetChild(i).gameObject; //获取第 i 个直接子物体
            InventorySlotUI slot = slotObject.GetComponent<InventorySlotUI>(); //获取格子控制脚本
            if (slot == null)
            {
                Debug.LogWarning("Inventory slot is missing InventorySlotUI: " + slotObject.name, slotObject);
                continue; //没有 InventorySlotUI 的子物体不能成为背包格子
            }
            slot.Initialize(this, slots.Count); //告诉格子所属背包和自己的索引
            slots.Add(slot); //将初始化完成的格子加入 UI 列表
        }
    }

    private void EnsureEntryCapacity() //让物品数据位置数量与 UI 格子数量一致
    {
        //数据位置不足时添加 null，null 就代表空格
        while (entries.Count < slots.Count)
            entries.Add(null);

        //数据位置比 UI 格子多时，删除超出的数据位置
        if (entries.Count > slots.Count)
            entries.RemoveRange(slots.Count, entries.Count - slots.Count);
    }

    private int GetOccupiedSlotCount() //统计当前已经被物品占用的格子数量
    {
        int count = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i] != null)
                count++; //只计算占用格数，不累加每格中的物品数量
        }
        return count;
    }

    private void RefreshUI() //根据 entries 中的数据刷新整个背包界面
    {
        //逐个刷新所有背包格子
        for (int i = 0; i < slots.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry != null)
                //有物品时显示图标、当前格数量和选中状态
                slots[i].ShowItem(entry.item.Icon, entry.quantity, i == selectedIndex);
            else
                //空格隐藏物品信息，但仍然需要更新选中框
                slots[i].ShowEmpty(i == selectedIndex);
        }

        //这里显示的是已占用格数/总格数，不是物品总数量
        if (capacityText != null)
            capacityText.text = $"容量 {GetOccupiedSlotCount()} / {slots.Count}";

        bool hasSelection = HasItemAt(selectedIndex); //当前选中的格子是否存在物品
        if (detailsPanel != null)
            detailsPanel.SetActive(hasSelection); //选中有效物品时才显示详情面板
        if (!hasSelection)
            return; //没有有效物品时无需继续填写详情内容

        InventoryEntry selected = entries[selectedIndex]; //取得当前选中格子的物品数据
        if (detailsIcon != null)
        {
            detailsIcon.sprite = selected.item.Icon; //显示选中物品的图标
            detailsIcon.preserveAspect = true; //保持图标原始比例，避免拉伸
        }
        if (detailsName != null) detailsName.text = selected.item.DisplayName; //显示物品名称
        if (detailsCategory != null) detailsCategory.text = selected.item.CategoryName; //显示物品分类
        if (detailsDescription != null) detailsDescription.text = selected.item.Description; //显示物品描述

        //显示这种物品在所有格子中的总数量，而不是只显示当前格数量
        if (detailsQuantity != null) detailsQuantity.text = "持有数量：" + GetItemCount(selected.item);
    }

    private void OnDestroy() //背包系统被销毁时调用
    {
        //只有当前对象还是全局单例时才清空，避免影响其他实例
        if (Instance == this)
            Instance = null;
    }
}
