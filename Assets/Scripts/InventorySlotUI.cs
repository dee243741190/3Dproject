using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler,
    IEndDragHandler, IDropHandler
{
    [Header("Slot UI References")]
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject selectedFrame;
    [SerializeField] private Button button;
    private InventorySystem owner;
    private int slotIndex;
    private static InventorySlotUI draggingSlot;
    private GameObject dragVisual;

    private void Reset()
    {
        button = GetComponent<Button>();
    }

    public void Initialize(InventorySystem inventory, int index)
    {
        owner = inventory;
        slotIndex = index;
        if (button != null)
        {
            button.onClick.RemoveListener(SelectThisSlot);
            button.onClick.AddListener(SelectThisSlot);
        }
        ShowEmpty(false);
    }

    public void ShowItem(Sprite icon, int quantity, bool selected)
    {
        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.preserveAspect = true;
            itemIcon.gameObject.SetActive(icon != null);
        }
        if (quantityText != null)
        {
            quantityText.text = "x" + quantity;
            quantityText.gameObject.SetActive(true);
        }
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
        if (button != null)
            button.interactable = true;
    }

    public void ShowEmpty(bool selected)
    {
        if (itemIcon != null)
            itemIcon.gameObject.SetActive(false);
        if (quantityText != null)
            quantityText.gameObject.SetActive(false);
        if (selectedFrame != null)
            selectedFrame.SetActive(selected);
        if (button != null)
            button.interactable = true;
    }

    private void SelectThisSlot()
    {
        if (owner != null)
            owner.SelectSlot(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (owner == null || !owner.HasItemAt(slotIndex))
            return;

        draggingSlot = this;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        dragVisual = new GameObject("Dragging Item Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dragVisual.transform.SetParent(canvas.transform, false);
        RectTransform rect = dragVisual.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f);
        Image image = dragVisual.GetComponent<Image>();
        image.sprite = owner.GetItemIconAt(slotIndex);
        image.preserveAspect = true;
        image.raycastTarget = false;
        dragVisual.transform.SetAsLastSibling();
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragVisual != null)
            dragVisual.transform.position = eventData.position;
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggingSlot != null && draggingSlot.owner == owner)
            owner.MoveOrSwapSlots(draggingSlot.slotIndex, slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragVisual != null)
            Destroy(dragVisual);
        dragVisual = null;
        draggingSlot = null;
    }
}
