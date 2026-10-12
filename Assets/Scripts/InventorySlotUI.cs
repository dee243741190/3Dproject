using TMPro; //提供 TMP_Text 数量文字组件
using UnityEngine; //提供 MonoBehaviour、GameObject、Sprite 等 Unity 类型
using UnityEngine.EventSystems; //提供拖拽事件接口和 PointerEventData
using UnityEngine.UI; //提供 Button 和 Image UI 组件

[DisallowMultipleComponent] //禁止同一个格子重复挂载多个 InventorySlotUI
[RequireComponent(typeof(Button))] //确保挂载本脚本的物体一定具有 Button 组件
public sealed class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, //负责单个格子的点击、显示和拖拽
    IEndDragHandler, IDropHandler
{
    [Header("Slot UI References")] //Inspector 中的格子 UI 引用分组
    [SerializeField] private Image itemIcon; //显示当前格子中物品的图标
    [SerializeField] private TMP_Text quantityText; //显示当前格子的物品数量，例如 x99
    [SerializeField] private GameObject selectedFrame; //格子被选中时显示的边框
    [SerializeField] private Button button; //用于接收当前格子的点击事件
    private InventorySystem owner; //管理当前格子的背包系统
    private int slotIndex; //当前格子在背包数据列表中的索引
    private static InventorySlotUI draggingSlot; //记录本次拖拽从哪个格子开始，所有格子共享
    private GameObject dragVisual; //拖拽时跟随鼠标移动的临时物品图标

    private void Reset() //在编辑器中添加或重置组件时自动调用
    {
        button = GetComponent<Button>(); //自动获取当前物体上的 Button
    }

    public void Initialize(InventorySystem inventory, int index) //由背包系统初始化格子归属和索引
    {
        owner = inventory; //保存管理这个格子的背包系统
        slotIndex = index; //保存当前格子对应的数据位置
        if (button != null)
        {
            button.onClick.RemoveListener(SelectThisSlot); //先移除旧监听，防止重复初始化
            button.onClick.AddListener(SelectThisSlot); //点击格子时选择当前格子
        }
        ShowEmpty(false); //初始化时显示为空格并取消选中框
    }

    public void ShowItem(Sprite icon, int quantity, bool selected) //显示物品图标、数量和选中状态
    {
        if (itemIcon != null)
        {
            itemIcon.sprite = icon; //设置物品图标
            itemIcon.preserveAspect = true; //保持图标原始比例，避免拉伸
            itemIcon.gameObject.SetActive(icon != null); //有有效图标时才显示 Image
        }
        if (quantityText != null)
        {
            quantityText.text = "x" + quantity; //把数量显示为 x1、x99 等格式
            quantityText.gameObject.SetActive(true); //有物品时显示数量文字
        }
        if (selectedFrame != null)
            selectedFrame.SetActive(selected); //根据选择状态开关选中边框
        if (button != null)
            button.interactable = true; //保持格子按钮可交互
    }

    public void ShowEmpty(bool selected) //将当前格子显示为空格状态
    {
        if (itemIcon != null)
            itemIcon.gameObject.SetActive(false); //隐藏物品图标
        if (quantityText != null)
            quantityText.gameObject.SetActive(false); //隐藏数量文字
        if (selectedFrame != null)
            selectedFrame.SetActive(selected); //空格仍然可以显示选中边框
        if (button != null)
            button.interactable = true; //空格需要接收点击和拖放事件
    }

    private void SelectThisSlot() //响应当前格子的按钮点击
    {
        if (owner != null)
            owner.SelectSlot(slotIndex); //通知背包系统选中当前索引
    }

    public void OnBeginDrag(PointerEventData eventData) //按住有物品的格子并开始拖动时调用
    {
        //没有背包系统或当前格子为空时，不能开始拖拽
        if (owner == null || !owner.HasItemAt(slotIndex))
            return;

        draggingSlot = this; //记录本次拖拽的起始格子
        Canvas canvas = GetComponentInParent<Canvas>(); //寻找用于显示临时图标的父 Canvas
        if (canvas == null)
            return; //没有 Canvas 时无法创建 UI 拖拽图标

        //运行时创建一个只用于拖拽显示的临时 Image 对象
        dragVisual = new GameObject("Dragging Item Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragVisual.transform.SetParent(canvas.transform, false); //放到 Canvas 下并保持 UI 局部坐标
        RectTransform rect = dragVisual.GetComponent<RectTransform>(); //获取临时图标的矩形组件
        rect.sizeDelta = new Vector2(80f, 80f); //把临时图标大小设置为 80×80
        Image image = dragVisual.GetComponent<Image>(); //获取刚创建的 Image
        image.sprite = owner.GetItemIconAt(slotIndex); //显示起始格子的物品图标
        image.preserveAspect = true; //保持图标比例
        image.raycastTarget = false; //不阻挡鼠标射线，否则目标格子无法收到 OnDrop
        dragVisual.transform.SetAsLastSibling(); //放到 Canvas 最上层，避免被其他 UI 遮挡
        OnDrag(eventData); //立即把临时图标移动到当前鼠标位置
    }

    public void OnDrag(PointerEventData eventData) //拖拽进行过程中每次鼠标移动时调用
    {
        if (dragVisual != null)
            dragVisual.transform.position = eventData.position; //让临时图标跟随鼠标屏幕位置
    }

    public void OnDrop(PointerEventData eventData) //其他格子的物品被放到当前格子时调用
    {
        //必须存在拖拽起点，并且起点和终点属于同一个背包系统
        if (draggingSlot != null && draggingSlot.owner == owner)
            owner.MoveOrSwapSlots(draggingSlot.slotIndex, slotIndex); //移动到空格或交换两个格子的物品
    }

    public void OnEndDrag(PointerEventData eventData) //松开鼠标、结束拖拽时调用
    {
        if (dragVisual != null)
            Destroy(dragVisual); //销毁跟随鼠标的临时图标
        dragVisual = null; //清空当前格子保存的临时图标引用
        draggingSlot = null; //清空所有格子共享的拖拽起点
    }
}
