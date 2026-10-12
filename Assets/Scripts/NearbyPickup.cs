using System.Collections.Generic; 
using TMPro;
using UnityEngine; 

/// <summary>
/// 玩家靠近时开启物体描边并显示提示，离开后关闭。
/// FsOutline 通过 Renderer.renderingLayerMask 选择需要描边的 Renderer。
/// </summary>
[DisallowMultipleComponent] 
[AddComponentMenu("Interaction/Nearby Pickup")] 

//本代码功能：
//检测玩家是否靠近可拾取物。
//多个物品中只选择最近的一个。
//为最近物品显示描边和拾取提示。
//玩家按 E 后将物品添加到背包并删除场景物品。

public class NearbyPickup : MonoBehaviour
{
    private static readonly List<NearbyPickup> ActivePickups = new List<NearbyPickup>(); // 创建一个全局列表，保存当前所有启用的拾取物
    private static NearbyPickup currentTarget; //当前距离玩家最近、真正可以交互的拾取物
    private static int lastSelectionFrame = -1; //上一次选择目标的帧数，防止一帧重复计算

    [Header("Detection")] 
    [Tooltip("不指定时，会自动寻找挂有 PlayerController 的物体。")]
    [SerializeField] private Transform player; //读取玩家位置
    [Min(0.1f)]
    [SerializeField] private float interactionRange = 2.5f; //设置交互距离，默认是 2.5 米

    [Header("Inventory Item")] 
    [Tooltip("引用统一的 ItemData 资料。同类物品引用同一个资料即可自动叠加。")]
    [SerializeField] private ItemData itemData; //当前场景物品对应的 ItemData 资料
    [Min(1)] [SerializeField] private int pickupQuantity = 1; //设置一次拾取的数量，最少是 1
    [SerializeField] private KeyCode pickupKey = KeyCode.E; //设置拾取按键，默认是 E

    [Header("Outline")] 
    [Tooltip("Outline Rendering Layer 的序号")]
    [Range(0, 31)]
    [SerializeField] private int outlineRenderingLayer = 1; //描边使用的Rendering Layer

    [Header("Prompt")] 
    [Tooltip("把物体提示文字 TextMeshProUGUI 组件拖到这里。")]
    [SerializeField] private TMP_Text pickupPrompt; //保存“按 E 拾取”等提示文字
    [Tooltip("开启后，世界空间的 TextMeshPro 会自动面向主摄像机。")]
    [SerializeField] private bool faceCamera = true; //是否让世界空间提示文字始终面向摄像机

    private Renderer[] targetRenderers; //保存物品及其子物体的 Renderer，用于开关描边层
    private uint[] originalRenderingLayerMasks; //保存原始的 Rendering Layer Mask
    private Collider[] targetColliders; //保存物体的碰撞体组件
    private bool playerNearby; //标记当前物品是否为正在显示交互效果的目标

    /// <summary>当前距离玩家最近、正在显示描边的唯一道具。</summary>
    public static NearbyPickup CurrentTarget => currentTarget; //让其他脚本能够读取当前交互目标

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() //静态初始化,Unity 开始运行、初始化子系统时自动调用
    {
        ActivePickups.Clear(); //清空拾取物列表
        currentTarget = null; //清空当前目标
        lastSelectionFrame = -1; //重置上一帧参数
    }

    private void Awake() //物品初始化时缓存组件并关闭初始交互效果
    {
        CacheComponents(); //提前获取 Renderer 和 Collider，避免每帧重复查找
        SetInteractionVisible(false); //游戏开始时关闭描边并隐藏拾取提示
    }

    private void OnEnable() //物品或组件启用时，将自己登记到全局拾取物列表
    {
        if (!ActivePickups.Contains(this))
            ActivePickups.Add(this); //防止同一个物品被重复登记
        lastSelectionFrame = -1; //列表变化后要求下一帧重新选择目标
    }

    private void Start() //第一次 Update 前检查当前物品的玩家引用
    {
        if (player == null)
            Debug.LogWarning("NearbyPickup: 请在 Inspector 中设置 Player。", this);
    }

    private void Update() //每帧选择最近目标并检测拾取输入
    {
        RefreshNearestTarget(); //每帧刷新最近的拾取物

        if (currentTarget == this && !BackpackUIController.IsAnyBackpackOpen && Input.GetKeyDown(pickupKey)) //只有当前最近的拾取物才响应按键
        {
            TryPickup(); //尝试拾取物品
        }
    }

    private void TryPickup() //检查背包与物品数据，尝试完成拾取
    {
        InventorySystem inventory = InventorySystem.Instance; //获取场景中的背包系统单例
        if (inventory == null) //场景中没有背包系统时无法拾取
        {
            Debug.LogWarning("Pickup failed: the scene has no InventorySystem component.", this);
            return; //停止拾取，地面物品继续保留
        }

        if (itemData == null) //没有 ItemData 就不知道应该向背包添加什么物品
        {
            Debug.LogWarning("Pickup failed: " + gameObject.name + " 没有配置 ItemData。", this);
            return; //停止拾取，避免添加空物品数据
        }

        bool added = inventory.TryAddItem(itemData, pickupQuantity); //尝试将物品添加到背包

        if (!added) //整个背包没有足够容量时添加失败
        {
            Debug.Log("背包已满，无法拾取 " + itemData.DisplayName, this);
            return; //添加失败时不能销毁地面物品
        }

        SetNearby(false); //拾取成功后关闭描边和提示
        currentTarget = null; //清空目标，让下一帧选择其他拾取物
        gameObject.SetActive(false); //立即停用，避免 Destroy 生效前再次显示或响应输入
        Destroy(gameObject); //当前帧结束后销毁物体
    }

    private void LateUpdate() //相机更新后再调整提示文字朝向，可减少跟随抖动
    {
        //不是当前目标、没有提示文字或关闭面向相机功能时不处理
        if (!playerNearby || pickupPrompt == null || !faceCamera)
            return;

        Camera mainCamera = Camera.main; //获取带有 MainCamera 标签的主摄像机
        if (mainCamera != null)
        {
            // 让世界空间文字正面始终朝向镜头。
            pickupPrompt.transform.rotation = Quaternion.LookRotation(
                pickupPrompt.transform.position - mainCamera.transform.position, //计算从相机指向文字的方向
                mainCamera.transform.up); //使用相机上方向，让文字保持竖直
        }
    }

    private void OnDisable() //物品停用或销毁时，从拾取系统中注销
    {
        ActivePickups.Remove(this); //将当前物品从全局拾取物列表中移除
        if (currentTarget == this)
            currentTarget = null; //停用的物品是当前目标时清空目标
        lastSelectionFrame = -1; //要求下一帧重新选择其他目标
        SetInteractionVisible(false); //关闭描边和提示
        playerNearby = false; //重置当前实例的交互状态
    }

    private static void RefreshNearestTarget() //从全部拾取物中选择范围内距离最近的一个
    {
        // 多个道具都会调用 Update，但全局选择每帧只计算一次(选最近的一个)
        if (lastSelectionFrame == Time.frameCount)
            return;

        lastSelectionFrame = Time.frameCount; //标记当前帧已经执行过选择
        NearbyPickup nearest = null; //开始时还没有找到目标
        float nearestDistance = float.PositiveInfinity; //初始为正无穷，任何正常距离都更小

        //倒序遍历，方便在循环中安全删除已经失效的对象
        for (int i = ActivePickups.Count - 1; i >= 0; i--)
        {
            NearbyPickup candidate = ActivePickups[i]; //当前准备检查的候选物品
            if (candidate == null)
            {
                ActivePickups.RemoveAt(i); //清理已经销毁的空引用
                continue; //继续检查下一个物品
            }

            if (!candidate.isActiveAndEnabled)
                continue; //未启用的物品不参与选择

            if (candidate.player == null)
                continue; //没有玩家引用时无法计算距离

            float distance = candidate.GetDistanceToPlayer(); //计算玩家到该物品的最近距离

            //必须进入物品自己的交互范围，而且要比之前找到的目标更近
            if (distance <= candidate.interactionRange && distance < nearestDistance)
            {
                nearest = candidate; //暂定为新的最近目标
                nearestDistance = distance; //记录新的最近距离
            }
        }

        currentTarget = nearest; //保存最终目标；没有物品在范围内时为 null

        //最近目标开启交互效果，其他所有物品关闭交互效果
        for (int i = 0; i < ActivePickups.Count; i++)
        {
            NearbyPickup candidate = ActivePickups[i];
            if (candidate != null)
                candidate.SetNearby(candidate == currentTarget); //比较结果直接作为 true/false 参数
        }
    }

    private void CacheComponents() //一次性获取当前物品需要的组件
    {
        targetRenderers = GetComponentsInChildren<Renderer>(true); //true 表示也获取未激活子物体
        originalRenderingLayerMasks = new uint[targetRenderers.Length]; //为每个 Renderer 准备一个原始 Mask 位置

        for (int i = 0; i < targetRenderers.Length; i++)
            originalRenderingLayerMasks[i] = targetRenderers[i].renderingLayerMask; //保存原始层设置

        targetColliders = GetComponentsInChildren<Collider>(true); //获取用于距离计算的全部碰撞体
    }

    private float GetDistanceToPlayer() //计算玩家与当前物体的最近距离
    {
        //先使用玩家到物品原点的距离，保证没有碰撞体时也能进行检测
        float closestDistance = Vector3.Distance(transform.position, player.position);

        foreach (Collider targetCollider in targetColliders)
        {
            if (targetCollider == null || !targetCollider.enabled)
                continue; //无效或未启用的碰撞体不参与计算

            //ClosestPoint 获取碰撞体表面离玩家最近的位置，比计算物品中心更准确
            float distance = Vector3.Distance(
                targetCollider.ClosestPoint(player.position),
                player.position);
            closestDistance = Mathf.Min(closestDistance, distance); //保留所有距离中的最小值
        }

        return closestDistance; //返回玩家到物品的最终最近距离
    }

    private void SetNearby(bool nearby) //修改当前物品是否为交互目标
    {
        //状态没有发生变化时不重复修改 Renderer 和文字
        if (playerNearby == nearby)
            return;

        playerNearby = nearby; //保存新的交互状态
        SetInteractionVisible(nearby); //根据状态开关描边和提示
    }

    private void SetInteractionVisible(bool visible) //开启或关闭当前物品的描边和提示文字
    {
        //把 Rendering Layer 序号转换成位掩码；序号为 1 时结果是十进制 2
        uint outlineBit = 1u << outlineRenderingLayer;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null)
                continue; //Renderer 被销毁时跳过，避免空引用异常

            //visible 为 true 时用按位或加入描边层；false 时用按位与移除描边层
            targetRenderers[i].renderingLayerMask = visible
                ? originalRenderingLayerMasks[i] | outlineBit
                : originalRenderingLayerMasks[i] & ~outlineBit;
        }

        // 只开关文字组件。即使文字直接挂在拾取物本体上，也绝不能关闭整个物体。
        if (pickupPrompt != null)
            pickupPrompt.enabled = visible; //与描边同步显示或隐藏提示文字
    }

    private void OnDrawGizmosSelected() //在 Scene 窗口选中物品时显示交互范围
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f); //设置金黄色半透明辅助线
        Gizmos.DrawWireSphere(transform.position, interactionRange); //绘制交互距离线框球，不会出现在游戏画面
    }
}
