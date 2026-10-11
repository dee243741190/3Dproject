using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家靠近时开启物体描边并显示提示，离开后关闭。
/// FsOutline 通过 Renderer.renderingLayerMask 选择需要描边的 Renderer。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Interaction/Nearby Pickup")]
public class NearbyPickup : MonoBehaviour
{
    private static readonly List<NearbyPickup> ActivePickups = new List<NearbyPickup>();
    private static NearbyPickup currentTarget;
    private static int lastSelectionFrame = -1;

    [Header("Detection")]
    [Tooltip("不指定时，会自动寻找挂有 PlayerController 的物体。")]
    [SerializeField] private Transform player;
    [Min(0.1f)]
    [SerializeField] private float interactionRange = 2.5f;

    [Header("Inventory Item")]
    [Tooltip("引用统一的 ItemData 资料。同类物品引用同一个资料即可自动叠加。")]
    [SerializeField] private ItemData itemData;
    [Min(1)] [SerializeField] private int pickupQuantity = 1;
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Header("Outline")]
    [Tooltip("你的 Outline Rendering Layer 的序号。截图中的 Outline 通常是 1。")]
    [Range(0, 31)]
    [SerializeField] private int outlineRenderingLayer = 1;

    [Header("Prompt")]
    [Tooltip("把物体上方现成的 TextMeshPro 或 TextMeshProUGUI 组件拖到这里。")]
    [SerializeField] private TMP_Text pickupPrompt;
    [Tooltip("开启后，世界空间的 TextMeshPro 会自动面向主摄像机。")]
    [SerializeField] private bool faceCamera = true;

    private Renderer[] targetRenderers;
    private uint[] originalRenderingLayerMasks;
    private Collider[] targetColliders;
    private bool playerNearby;

    /// <summary>当前距离玩家最近、正在显示描边的唯一道具。</summary>
    public static NearbyPickup CurrentTarget => currentTarget;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActivePickups.Clear();
        currentTarget = null;
        lastSelectionFrame = -1;
    }

    private void Awake()
    {
        CacheComponents();
        SetInteractionVisible(false);
    }

    private void OnEnable()
    {
        if (!ActivePickups.Contains(this))
            ActivePickups.Add(this);
        lastSelectionFrame = -1;
    }

    private void Start()
    {
        if (player == null)
            Debug.LogWarning("NearbyPickup: 请在 Inspector 中设置 Player。", this);
    }

    private void Update()
    {
        RefreshNearestTarget();

        if (currentTarget == this && !BackpackUIController.IsAnyBackpackOpen &&
            Input.GetKeyDown(pickupKey))
        {
            TryPickup();
        }
    }

    private void TryPickup()
    {
        InventorySystem inventory = InventorySystem.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("Pickup failed: the scene has no InventorySystem component.", this);
            return;
        }

        if (itemData == null)
        {
            Debug.LogWarning("Pickup failed: " + gameObject.name + " 没有配置 ItemData。", this);
            return;
        }

        bool added = inventory.TryAddItem(itemData, pickupQuantity);

        if (!added)
        {
            Debug.Log("背包已满，无法拾取 " + itemData.DisplayName, this);
            return;
        }

        SetNearby(false);
        currentTarget = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void LateUpdate()
    {
        if (!playerNearby || pickupPrompt == null || !faceCamera)
            return;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            // 让世界空间文字正面始终朝向镜头。
            pickupPrompt.transform.rotation = Quaternion.LookRotation(
                pickupPrompt.transform.position - mainCamera.transform.position,
                mainCamera.transform.up);
        }
    }

    private void OnDisable()
    {
        ActivePickups.Remove(this);
        if (currentTarget == this)
            currentTarget = null;
        lastSelectionFrame = -1;
        SetInteractionVisible(false);
        playerNearby = false;
    }

    private static void RefreshNearestTarget()
    {
        // 多个道具都会调用 Update，但全局选择每帧只计算一次。
        if (lastSelectionFrame == Time.frameCount)
            return;

        lastSelectionFrame = Time.frameCount;
        NearbyPickup nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            NearbyPickup candidate = ActivePickups[i];
            if (candidate == null)
            {
                ActivePickups.RemoveAt(i);
                continue;
            }

            if (!candidate.isActiveAndEnabled)
                continue;

            if (candidate.player == null)
                continue;

            float distance = candidate.GetDistanceToPlayer();
            if (distance <= candidate.interactionRange && distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        currentTarget = nearest;
        for (int i = 0; i < ActivePickups.Count; i++)
        {
            NearbyPickup candidate = ActivePickups[i];
            if (candidate != null)
                candidate.SetNearby(candidate == currentTarget);
        }
    }

    private void CacheComponents()
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true);
        originalRenderingLayerMasks = new uint[targetRenderers.Length];

        for (int i = 0; i < targetRenderers.Length; i++)
            originalRenderingLayerMasks[i] = targetRenderers[i].renderingLayerMask;

        targetColliders = GetComponentsInChildren<Collider>(true);
    }

    private float GetDistanceToPlayer()
    {
        float closestDistance = Vector3.Distance(transform.position, player.position);

        foreach (Collider targetCollider in targetColliders)
        {
            if (targetCollider == null || !targetCollider.enabled)
                continue;

            float distance = Vector3.Distance(
                targetCollider.ClosestPoint(player.position),
                player.position);
            closestDistance = Mathf.Min(closestDistance, distance);
        }

        return closestDistance;
    }

    private void SetNearby(bool nearby)
    {
        if (playerNearby == nearby)
            return;

        playerNearby = nearby;
        SetInteractionVisible(nearby);
    }

    private void SetInteractionVisible(bool visible)
    {
        uint outlineBit = 1u << outlineRenderingLayer;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null)
                continue;

            targetRenderers[i].renderingLayerMask = visible
                ? originalRenderingLayerMasks[i] | outlineBit
                : originalRenderingLayerMasks[i] & ~outlineBit;
        }

        // 只开关文字组件。即使文字直接挂在拾取物本体上，也绝不能关闭整个物体。
        if (pickupPrompt != null)
            pickupPrompt.enabled = visible;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
