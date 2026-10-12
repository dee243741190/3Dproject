using System.Collections; 
using Cinemachine; 
using UnityEngine; 
using UnityEngine.UI; 

[DisallowMultipleComponent] 
public sealed class BackpackUIController : MonoBehaviour //负责打开/关闭整个背包、鼠标、角色和镜头状态
{
    [Header("Backpack UI")] 
    [SerializeField] private GameObject bag; //整个背包 UI 的根物体
    [SerializeField] private Button closeButton; //背包界面中的关闭按钮
    [SerializeField] private CinemachineFreeLook freeLook; //玩家使用的第三人称自由视角相机
    [SerializeField] private Transform playerModel; //玩家模型，用来计算角色正面方向
    [SerializeField] private KeyCode toggleKey = KeyCode.G; //打开和关闭背包的按键，默认 G
    [Min(0.05f)] [SerializeField] private float transitionDuration = 0.4f; //打开和关闭动画的持续时间
    [Range(0.8f, 1f)] [SerializeField] private float hiddenScale = 0.94f; //隐藏状态下背包缩小到的比例

    [Header("Inventory Camera")] 
    [Range(0.1f, 0.9f)] [SerializeField] private float characterScreenX = 0.82f; //背包打开时角色在画面中的水平位置
    [Tooltip("数值越小，角色在最终画面中越靠上。")]
    [Range(0.1f, 0.9f)] [SerializeField] private float characterScreenY = 0.45f; //角色在画面中的垂直位置
    [Range(0f, 1f)] [SerializeField] private float inventoryCameraY = 0.5f; //背包打开时 FreeLook 相机的纵向轨道位置

    private CanvasGroup bagCanvasGroup; 
    private RectTransform bagRect; 
    private Vector3 visibleBagScale; 
    private Coroutine transitionRoutine; 
    private CinemachineComposer[] cameraComposers; 
    private float[] previousScreenXValues; 
    private float[] previousScreenYValues;
    private string previousXAxisName; 
    private string previousYAxisName; 
    private float previousXMaxSpeed; 
    private float previousYMaxSpeed; 
    private float previousXAxisValue; 
    private float previousYAxisValue; 
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible; 
    private bool hasCapturedDefaultState; 
    private bool isOpen; 

    public bool IsOpen => isOpen; //允许其他脚本读取当前背包实例的状态
    public static bool IsAnyBackpackOpen { get; private set; } //整个系统共享的背包打开状态

    private void Awake() //初始化背包界面和关闭按钮
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseBag); //点击关闭按钮时调用 CloseBag

        if (bag != null)
        {
            bagCanvasGroup = bag.GetComponent<CanvasGroup>(); //尝试获取已有的 CanvasGroup
            if (bagCanvasGroup == null)
                bagCanvasGroup = bag.AddComponent<CanvasGroup>(); //没有时自动添加
            bagRect = bag.GetComponent<RectTransform>(); //获取 UI 矩形，用于缩放动画
            visibleBagScale = bagRect != null ? bagRect.localScale : Vector3.one; //保存正常显示尺寸
            bagCanvasGroup.alpha = 0f; //初始完全透明
            bagCanvasGroup.interactable = false; //初始不能操作 UI
            bagCanvasGroup.blocksRaycasts = false; //初始不阻挡鼠标射线
            bag.SetActive(false); //游戏开始时隐藏背包
        }

        isOpen = false; //重置当前实例的打开状态
        IsAnyBackpackOpen = false; //重置全局背包状态
    }

    private void Update() //每帧检测背包开关键，并持续锁定背包状态下的相机输入
    {
        if (Input.GetKeyDown(toggleKey))
            SetBagOpen(!isOpen); //对当前状态取反，实现按一次打开、再按一次关闭

        if (IsAnyBackpackOpen && freeLook != null)
            LockCameraInput(); //防止其他脚本在背包打开期间重新启用相机输入
    }

    public void OpenBag() => SetBagOpen(true); //供按钮或其他脚本直接打开背包
    public void CloseBag() => SetBagOpen(false); //供关闭按钮或其他脚本关闭背包
    public void ToggleBag() => SetBagOpen(!isOpen); //在打开和关闭之间切换

    private void SetBagOpen(bool open) //设置背包目标状态并启动过渡动画
    {
        //没有连接 Bag 时无法操作背包，并输出可以定位当前组件的错误
        if (bag == null)
        {
            Debug.LogError("Backpack UI: 请在 Inspector 中设置 Bag。", this);
            return;
        }

        if (isOpen == open && transitionRoutine == null)
            return; //目标状态已经达到且没有动画运行时，不重复执行

        isOpen = open; //先记录新的目标状态
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine); //快速反向操作时停止旧动画，从当前状态开始新动画

        if (open)
        {
            if (!hasCapturedDefaultState)
                CaptureDefaultState(); //第一次打开前保存原来的相机和鼠标状态
            IsAnyBackpackOpen = true; //让移动、拾取等系统知道背包已经打开
            bag.SetActive(true); //先激活背包，才能播放淡入动画
            bagCanvasGroup.interactable = false; //动画完成前禁止点击
            bagCanvasGroup.blocksRaycasts = false; //动画完成前不阻挡射线
            LockCameraInput(); //禁止玩家继续用鼠标旋转相机
            Cursor.lockState = CursorLockMode.None; //解除鼠标锁定
            Cursor.visible = true; //显示鼠标指针
        }
        else
        {
            // 淡出完成前仍锁住角色和镜头。
            bagCanvasGroup.interactable = false;
            bagCanvasGroup.blocksRaycasts = false;
        }

        transitionRoutine = StartCoroutine(PlayTransition(open)); //启动打开或关闭动画
    }

    private IEnumerator PlayTransition(bool opening) //同时播放 UI 淡入缩放、镜头旋转和构图动画
    {
        float startAlpha = bagCanvasGroup.alpha; //记录动画开始时的透明度
        Vector3 startScale = bagRect != null ? bagRect.localScale : Vector3.one; //记录开始缩放
        Vector3 targetScale = opening ? visibleBagScale : visibleBagScale * hiddenScale; //计算目标缩放
        float startX = freeLook != null ? freeLook.m_XAxis.Value : 0f; 
        float startY = freeLook != null ? freeLook.m_YAxis.Value : 0f; 
        float targetX = previousXAxisValue; 
        float targetY = previousYAxisValue; 

        //打开背包时，将镜头移动到角色正面并调整到背包展示高度
        if (opening && freeLook != null)
        {
            if (playerModel != null && Camera.main != null)
            {
                // 不依赖角色或 FreeLook 的绝对角度。根据相机当前所在方向，
                // 计算它还需要绕角色旋转多少度才能来到角色正前方。
                Vector3 currentCameraSide = Camera.main.transform.position - playerModel.position; //角色到当前相机的水平朝向
                Vector3 desiredCameraSide = playerModel.forward; //希望相机到达的角色正面方向
                currentCameraSide.y = 0f; 
                desiredCameraSide.y = 0f; 
                if (currentCameraSide.sqrMagnitude > 0.0001f &&
                    desiredCameraSide.sqrMagnitude > 0.0001f)
                {
                    float angleToFront = Vector3.SignedAngle(
                        currentCameraSide.normalized,
                        desiredCameraSide.normalized,
                        Vector3.up);
                    targetX = startX + angleToFront; 
                }
            }
            targetY = inventoryCameraY; 
        }

        float elapsed = 0f; //已经播放的动画时间
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime; 
            float t = Mathf.Clamp01(elapsed / transitionDuration); 
            float smoothT = t * t * (3f - 2f * t); 
            bagCanvasGroup.alpha = Mathf.Lerp(startAlpha, opening ? 1f : 0f, smoothT); //透明度插值
            if (bagRect != null)
                bagRect.localScale = Vector3.Lerp(startScale, targetScale, smoothT); //缩放插值

            if (freeLook != null)
            {
                freeLook.m_XAxis.Value = Mathf.LerpAngle(startX, targetX, smoothT); 
                freeLook.m_YAxis.Value = Mathf.Lerp(startY, targetY, smoothT); 
                InterpolateCameraComposition(opening, smoothT); 
            }
            yield return null; 
        }

        //动画结束后强制设置最终状态，避免浮点插值留下误差
        if (opening)
        {
            bagCanvasGroup.alpha = 1f; 
            if (bagRect != null)
                bagRect.localScale = visibleBagScale; 
            bagCanvasGroup.interactable = true; 
            bagCanvasGroup.blocksRaycasts = true; 
        }
        else
        {
            bagCanvasGroup.alpha = 0f; 
            bag.SetActive(false); 
            RestoreInputAndCursor(); 
            IsAnyBackpackOpen = false; 
            hasCapturedDefaultState = false; 
        }
        transitionRoutine = null; 
    }

    private void CaptureDefaultState() //保存打开背包前的鼠标、相机输入和画面构图状态
    {
        previousCursorLockMode = Cursor.lockState; //保存鼠标原来的锁定模式
        previousCursorVisible = Cursor.visible; //保存鼠标原来的显示状态
        if (freeLook != null)
        {
            previousXAxisName = freeLook.m_XAxis.m_InputAxisName; 
            previousYAxisName = freeLook.m_YAxis.m_InputAxisName; 
            previousXMaxSpeed = freeLook.m_XAxis.m_MaxSpeed; 
            previousYMaxSpeed = freeLook.m_YAxis.m_MaxSpeed; 
            previousXAxisValue = freeLook.m_XAxis.Value; 
            previousYAxisValue = freeLook.m_YAxis.Value; 
            cameraComposers = freeLook.GetComponentsInChildren<CinemachineComposer>(true); 
            previousScreenXValues = new float[cameraComposers.Length]; 
            previousScreenYValues = new float[cameraComposers.Length]; 
            for (int i = 0; i < cameraComposers.Length; i++)
            {
                previousScreenXValues[i] = cameraComposers[i].m_ScreenX; 
                previousScreenYValues[i] = cameraComposers[i].m_ScreenY; 
            }
        }
        hasCapturedDefaultState = true; //标记原始状态已经保存
    }

    private void InterpolateCameraComposition(bool opening, float t) //改变角色在最终画面中的位置
    {
        if (cameraComposers == null || previousScreenXValues == null || previousScreenYValues == null)
            return; //没有保存构图数据时不处理

        //取三个数组中的最小长度，避免数据长度不一致导致索引越界
        int count = Mathf.Min(cameraComposers.Length,
            Mathf.Min(previousScreenXValues.Length, previousScreenYValues.Length));
        for (int i = 0; i < count; i++)
        {
            if (cameraComposers[i] == null)
                continue; //该层相机不存在时跳过
            float fromX = opening ? previousScreenXValues[i] : characterScreenX;
            float fromY = opening ? previousScreenYValues[i] : characterScreenY; 
            float toX = opening ? characterScreenX : previousScreenXValues[i]; 
            float toY = opening ? characterScreenY : previousScreenYValues[i]; 
            cameraComposers[i].m_ScreenX = Mathf.Lerp(fromX, toX, t); 
            cameraComposers[i].m_ScreenY = Mathf.Lerp(fromY, toY, t); 
        }
    }

    private void LockCameraInput() //禁止玩家在背包打开时用鼠标控制 FreeLook 相机
    {
        if (freeLook == null)
            return;
        freeLook.m_XAxis.m_InputAxisName = string.Empty; 
        freeLook.m_YAxis.m_InputAxisName = string.Empty; 
        freeLook.m_XAxis.m_InputAxisValue = 0f; 
        freeLook.m_YAxis.m_InputAxisValue = 0f; 
        freeLook.m_XAxis.m_MaxSpeed = 0f; 
        freeLook.m_YAxis.m_MaxSpeed = 0f; 
    }

    private void RestoreInputAndCursor() //恢复打开背包前保存的相机和鼠标状态
    {
        if (freeLook != null)
        {
            freeLook.m_XAxis.Value = previousXAxisValue; 
            freeLook.m_YAxis.Value = previousYAxisValue; 
            freeLook.m_XAxis.m_InputAxisName = previousXAxisName; 
            freeLook.m_YAxis.m_InputAxisName = previousYAxisName; 
            freeLook.m_XAxis.m_InputAxisValue = 0f; 
            freeLook.m_YAxis.m_InputAxisValue = 0f; 
            freeLook.m_XAxis.m_MaxSpeed = previousXMaxSpeed; 
            freeLook.m_YAxis.m_MaxSpeed = previousYMaxSpeed; 
            InterpolateCameraComposition(false, 1f); 
        }
        Cursor.lockState = previousCursorLockMode; //恢复鼠标锁定模式
        Cursor.visible = previousCursorVisible; //恢复鼠标显示状态
    }

    private void OnDestroy() //控制器销毁时解除事件并清理全局状态
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseBag); 
        if (hasCapturedDefaultState)
            RestoreInputAndCursor(); 
        IsAnyBackpackOpen = false; 
    }
}
