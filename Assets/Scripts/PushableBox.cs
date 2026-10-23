using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家靠近并面对箱子时显示描边；按住 E，再按 W 连续推动。
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Interaction/Pushable Box")]
public class PushableBox : MonoBehaviour
{
    //记录场景中所有可推动箱子，并保证同一时间只选中、推动一个。
    private static readonly List<PushableBox> ActiveBoxes = new List<PushableBox>();
    private static PushableBox currentTarget;
    private static PushableBox pushingBox;
    private static int lastSelectionFrame = -1;

    [Header("Interaction")]
    [SerializeField] private PlayerController player;
    [Min(0.1f)] [SerializeField] private float interactionRange = 2.5f;
    [Min(0.05f)] [SerializeField] private float pushContactDistance = 0.4f;
    [Range(-1f, 1f)] [SerializeField] private float facingDot = 0.45f;
    [SerializeField] private KeyCode pushKey = KeyCode.E;

    [Header("Movement")]
    [Min(0.1f)] [SerializeField] private float boxPushSpeed = 2.5f;
    [Min(0.1f)] [SerializeField] private float boxMass = 8f;

    [Header("Outline")]
    [Range(0, 31)] [SerializeField] private int outlineRenderingLayer = 1;

    [Header("Prompt")]
    [SerializeField] private TMP_Text pushPrompt;
    [SerializeField] private bool faceCamera = true;

    private Rigidbody boxRigidbody;
    private Renderer[] targetRenderers;
    private uint[] originalRenderingLayerMasks;
    private Collider[] targetColliders;
    private Collider[] playerColliders;
    private bool interactionVisible;
    private bool isPushing;
    private Vector3 pushDirection;

    //进入游戏时清空静态记录，避免关闭域重载后保留上次运行的数据。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveBoxes.Clear();
        currentTarget = null;
        pushingBox = null;
        lastSelectionFrame = -1;
    }

    private void Awake()
    {
        //自动查找玩家、提示文字和物理组件，减少 Inspector 中的手动配置。
        if (player == null)
            player = FindObjectOfType<PlayerController>();

        if (player != null)
            playerColliders = player.GetComponentsInChildren<Collider>(true);

        if (pushPrompt == null)
            pushPrompt = GetComponentInChildren<TMP_Text>(true);

        boxRigidbody = GetComponent<Rigidbody>();
        if (boxRigidbody == null)
            boxRigidbody = gameObject.AddComponent<Rigidbody>();

        boxRigidbody.mass = boxMass;
        boxRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        boxRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        boxRigidbody.constraints = RigidbodyConstraints.FreezeRotation;

        CacheComponents();
        SetInteractionVisible(false);
    }

    private void OnEnable()
    {
        if (!ActiveBoxes.Contains(this))
            ActiveBoxes.Add(this);
        lastSelectionFrame = -1;
    }

    private void Update()
    {
        //选择最近且位于玩家前方的箱子，并根据 E 键进入或退出推动状态。
        RefreshNearestTarget();

        if (isPushing)
        {
            if (!Input.GetKey(pushKey) || BackpackUIController.IsAnyBackpackOpen || !PlayerStillClose())
                StopPushing();
            return;
        }

        if (currentTarget == this && Input.GetKey(pushKey) && !BackpackUIController.IsAnyBackpackOpen)
            StartPushing();
    }

    private void FixedUpdate()
    {
        //推动状态下只有向前输入有效，箱子沿开始推动时记录的方向移动。
        if (!isPushing || player == null || boxRigidbody == null)
            return;

        Vector3 velocity = boxRigidbody.velocity;
        if (Input.GetAxisRaw("Vertical") > 0f)
        {
            velocity.x = pushDirection.x * boxPushSpeed;
            velocity.z = pushDirection.z * boxPushSpeed;
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }
        boxRigidbody.velocity = velocity;
    }

    private void LateUpdate()
    {
        //让世界空间提示文字始终面向主摄像机。
        if (!interactionVisible || pushPrompt == null || !faceCamera || Camera.main == null)
            return;

        pushPrompt.transform.rotation = Quaternion.LookRotation(
            pushPrompt.transform.position - Camera.main.transform.position,
            Camera.main.transform.up);
    }

    private void StartPushing()
    {
        // 描边可以提前出现，但必须真正贴近箱子才能开始推动。
        if (GetDistanceToPlayer() > pushContactDistance)
            return;

        Vector3 direction = transform.position - player.transform.position;
        direction.y = 0f;
        if (!player.BeginPush(this, direction))
            return;

        pushDirection = direction.normalized;
        isPushing = true;
        pushingBox = this;
        currentTarget = null;
        SetInteractionVisible(false);
    }

    private void StopPushing()
    {
        //清除玩家和箱子的推动状态，同时停止箱子的水平移动。
        if (!isPushing)
            return;

        isPushing = false;
        if (pushingBox == this)
            pushingBox = null;
        if (player != null)
            player.EndPush(this);

        if (boxRigidbody != null)
        {
            Vector3 velocity = boxRigidbody.velocity;
            boxRigidbody.velocity = new Vector3(0f, velocity.y, 0f);
        }
        lastSelectionFrame = -1;
    }

    private bool PlayerStillClose()
    {
        return player != null && GetDistanceToPlayer() <= pushContactDistance + 0.25f;
    }

    private static void RefreshNearestTarget()
    {
        //所有箱子都会调用此方法，但每帧只统一计算一次最近目标。
        if (lastSelectionFrame == Time.frameCount)
            return;

        lastSelectionFrame = Time.frameCount;

        // 按住 E 推动一个箱子时，不允许其他箱子在下一帧继续接管同一次按键。
        if (pushingBox != null)
        {
            currentTarget = null;
            foreach (PushableBox box in ActiveBoxes)
            {
                if (box != null)
                    box.SetInteractionVisible(false);
            }
            return;
        }

        PushableBox nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = ActiveBoxes.Count - 1; i >= 0; i--)
        {
            PushableBox candidate = ActiveBoxes[i];
            if (candidate == null)
            {
                ActiveBoxes.RemoveAt(i);
                continue;
            }

            if (!candidate.isActiveAndEnabled || candidate.player == null || candidate.isPushing)
                continue;

            float distance = candidate.GetDistanceToPlayer();
            if (distance <= candidate.interactionRange && candidate.PlayerFacesBox() && distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        currentTarget = nearest;
        foreach (PushableBox box in ActiveBoxes)
        {
            if (box != null && !box.isPushing)
                box.SetInteractionVisible(box == currentTarget);
        }
    }

    private bool PlayerFacesBox()
    {
        //通过点积判断箱子是否大致位于玩家当前朝向的前方。
        Transform facing = player.orientation != null ? player.orientation : player.transform;
        Vector3 toBox = transform.position - player.transform.position;
        toBox.y = 0f;
        return toBox.sqrMagnitude > 0.01f && Vector3.Dot(facing.forward, toBox.normalized) >= facingDot;
    }

    private float GetDistanceToPlayer()
    {
        //计算玩家碰撞体与箱子碰撞体表面的最近距离，用于避免隔空推动。
        float closestDistance = Vector3.Distance(transform.position, player.transform.position);
        foreach (Collider targetCollider in targetColliders)
        {
            if (targetCollider == null || !targetCollider.enabled)
                continue;

            if (playerColliders == null || playerColliders.Length == 0)
            {
                closestDistance = Mathf.Min(closestDistance,
                    Vector3.Distance(targetCollider.ClosestPoint(player.transform.position), player.transform.position));
                continue;
            }

            foreach (Collider playerCollider in playerColliders)
            {
                if (playerCollider == null || !playerCollider.enabled || playerCollider.isTrigger)
                    continue;

                // 用两个碰撞体表面之间的间距，而不是玩家物体原点，贴住时结果接近 0。
                Vector3 pointOnBox = targetCollider.ClosestPoint(playerCollider.bounds.center);
                Vector3 pointOnPlayer = playerCollider.ClosestPoint(pointOnBox);
                pointOnBox = targetCollider.ClosestPoint(pointOnPlayer);
                closestDistance = Mathf.Min(closestDistance, Vector3.Distance(pointOnBox, pointOnPlayer));
            }
        }
        return closestDistance;
    }

    private void CacheComponents()
    {
        //缓存箱子的渲染器、描边层和碰撞体，避免每帧重复查找组件。
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        List<Renderer> boxRenderers = new List<Renderer>();
        foreach (Renderer targetRenderer in allRenderers)
        {
            if (targetRenderer.GetComponentInParent<TMP_Text>() == null)
                boxRenderers.Add(targetRenderer);
        }

        targetRenderers = boxRenderers.ToArray();
        originalRenderingLayerMasks = new uint[targetRenderers.Length];
        for (int i = 0; i < targetRenderers.Length; i++)
            originalRenderingLayerMasks[i] = targetRenderers[i].renderingLayerMask;

        targetColliders = GetComponentsInChildren<Collider>(true);
    }

    private void SetInteractionVisible(bool visible)
    {
        //描边和交互提示始终一起显示或隐藏。
        interactionVisible = visible;
        uint outlineBit = 1u << outlineRenderingLayer;
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
                targetRenderers[i].renderingLayerMask = visible
                    ? originalRenderingLayerMasks[i] | outlineBit
                    : originalRenderingLayerMasks[i] & ~outlineBit;
        }

        if (pushPrompt != null)
            pushPrompt.enabled = visible;
    }

    private void OnDisable()
    {
        //箱子被关闭或销毁时，从全局列表移除并清理交互状态。
        StopPushing();
        ActiveBoxes.Remove(this);
        if (currentTarget == this)
            currentTarget = null;
        lastSelectionFrame = -1;
        if (targetRenderers != null)
            SetInteractionVisible(false);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0.1f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
