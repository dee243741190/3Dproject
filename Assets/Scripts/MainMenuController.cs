using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Cinemachine;

/// <summary>
/// 主菜单控制器：负责开始新游戏、打开设置、退出游戏，
/// 并在主菜单淡出后切换镜头、启用玩家操作。
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    // 主菜单 UI 引用：用于绑定按钮、控制整体透明度和淡出时间。
    [Header("Menu")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    // 进入游戏后需要启用的玩家移动和镜头控制脚本。
    [Header("New Game")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ThirdPersonCam thirdPersonCamera;

    [Header("Optional Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    // 防止玩家重复点击“新游戏”，导致多个切换协程同时运行。
    private bool isTransitioning;

    // 游戏 HUD 在主菜单阶段隐藏，镜头切换完成后再淡入。
    [Header("Gameplay UI")]
    [SerializeField] private CanvasGroup gameUI;
    [Min(0f)] [SerializeField] private float gameUIFadeDuration = 0.6f;

    // Cinemachine 镜头引用：用于把画面从菜单镜头交接给第三人称镜头。
    [Header("Camera handoff")]
    [SerializeField] private CinemachineBrain cameraBrain;
    [SerializeField] private CinemachineVirtualCamera menuCamera;
    [SerializeField] private CinemachineFreeLook gameplayCamera;

    private void Awake()
    {
        // Inspector 未绑定时，尝试按场景对象名称自动寻找游戏 UI。
        if (gameUI == null)
        {
            GameObject root = GameObject.Find("GameUI");
            if (root != null) gameUI = root.GetComponent<CanvasGroup>();
        }
        if (gameUI != null)
        {
            // 主菜单显示期间隐藏 HUD，并禁止 HUD 拦截鼠标点击。
            gameUI.alpha = 0f;
            gameUI.interactable = false;
            gameUI.blocksRaycasts = false;
        }
        // 为镜头引用提供自动查找兜底；仍建议优先在 Inspector 中绑定。
        if (cameraBrain == null) cameraBrain = FindObjectOfType<CinemachineBrain>();
        if (gameplayCamera == null) gameplayCamera = FindObjectOfType<CinemachineFreeLook>();
        if (menuCamera == null)
        {
            GameObject root = GameObject.Find("BeginCamera");
            if (root != null) menuCamera = root.GetComponent<CinemachineVirtualCamera>();
        }
        // 确保菜单拥有 CanvasGroup，供透明度和交互状态切换使用。
        if (menuCanvasGroup == null)
            menuCanvasGroup = GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null)
            menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        // 初始化为菜单状态：关闭玩家操作，显示并启用主菜单。
        SetGameplayControl(false);
        SetMenuVisible(true);

        // 注册按钮事件；未绑定的按钮会安全跳过，不产生空引用异常。
        newGameButton?.onClick.AddListener(StartNewGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        quitButton?.onClick.AddListener(QuitGame);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void Start()
    {
        // 菜单需要显示并解锁鼠标，避免沿用之前的光标状态。
        ShowMenuCursor();
    }

    private void LateUpdate()
    {
        // 其他游戏脚本也可能修改光标；点击“新游戏”之前，以主菜单状态为准。
        if (!isTransitioning && gameObject.activeInHierarchy)
            ShowMenuCursor();
    }

    public void StartNewGame()
    {
        // 切换流程已经启动时忽略重复点击。
        if (isTransitioning)
            return;

        isTransitioning = true;
        // 淡出期间立即禁止菜单交互，避免继续点击其他按钮。
        menuCanvasGroup.interactable = false;
        StartCoroutine(StartNewGameRoutine());
    }

    private IEnumerator StartNewGameRoutine()
    {
        // 第一阶段：淡出主菜单，然后关闭它的显示和射线拦截。
        yield return FadeCanvasGroup(menuCanvasGroup, 0f, fadeOutDuration);

        SetMenuVisible(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 降低菜单镜头的优先级，等待 Cinemachine 切换到第三人称镜头。
        if (menuCamera != null && gameplayCamera != null)
            menuCamera.Priority = gameplayCamera.Priority - 1;
        yield return null;
        if (cameraBrain != null && gameplayCamera != null)
        {
            while (cameraBrain.IsBlending || cameraBrain.ActiveVirtualCamera != gameplayCamera)
                yield return null;
        }

        // 镜头交接完成后才允许玩家移动和转动视角。
        SetGameplayControl(true);
        // 新游戏正式开始时接取初始任务；没有任务管理器时安全跳过。
        QuestManager.Instance?.AcceptStartingQuest();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        // 必须等淡入协程完成后才能关闭主菜单对象。
        yield return FadeCanvasGroup(gameUI, 1f, gameUIFadeDuration);
        if (gameUI != null)
        {
            gameUI.interactable = true;
            gameUI.blocksRaycasts = true;
        }
        gameObject.SetActive(false);
    }

    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
    {
        // 通用 UI 淡入淡出：使用非缩放时间，因此不受 Time.timeScale 影响。
        if (group == null) yield break;
        float startAlpha = group.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        group.alpha = targetAlpha;
    }

    public void OpenSettings()
    {
        // 显示设置面板；未配置面板时输出提示日志。
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        else
            Debug.Log("设置：等待接入设置面板。", this);
    }

    public void CloseSettings()
    {
        // 供设置面板中的“关闭/返回”按钮调用。
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        // 编辑器中停止播放；打包后的游戏中退出应用程序。
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetGameplayControl(bool enabled)
    {
        // 统一启用或禁用玩家移动、第三人称镜头和鼠标视角输入。
        if (playerController != null)
            playerController.enabled = enabled;
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetMouseInputEnabled(enabled);
            thirdPersonCamera.enabled = enabled;
        }
    }

    private void SetMenuVisible(bool visible)
    {
        // 同步透明度、交互和射线拦截，防止隐藏菜单挡住游戏 UI。
        if (menuCanvasGroup == null)
            return;

        menuCanvasGroup.alpha = visible ? 1f : 0f;
        menuCanvasGroup.interactable = visible;
        menuCanvasGroup.blocksRaycasts = visible;
    }

    private static void ShowMenuCursor()
    {
        // 主菜单光标必须解锁并显示，玩家才能点击 UI 按钮。
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        // 对应 Awake 中的事件注册，销毁对象时解除监听。
        newGameButton?.onClick.RemoveListener(StartNewGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        quitButton?.onClick.RemoveListener(QuitGame);
    }
}
