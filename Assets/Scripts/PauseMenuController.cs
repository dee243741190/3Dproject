using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    // 暂停菜单界面引用：控制菜单显示、按钮点击和射线拦截。
    [Header("Pause UI")]
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button returnToMainMenuButton;

    // 与主菜单共用的设置面板，暂停时可以从这里打开。
    [Header("Shared Settings")]
    [SerializeField] private GameObject settingsPanel;

    // 暂停期间需要临时禁用的游戏控制脚本，以及用于判断主菜单状态的引用。
    [Header("Gameplay Control")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ThirdPersonCam thirdPersonCamera;
    [SerializeField] private MainMenuController mainMenu;

    // 记录当前是否暂停，以及设置面板是否由暂停菜单打开。
    private bool isPaused;
    private bool settingsWasOpen;

    // 保存暂停前各组件的启用状态，恢复游戏时还原，避免错误开启原本已禁用的组件。
    private bool playerWasEnabled;
    private bool cameraWasEnabled;

    // 保存暂停前的时间缩放值，兼容慢动作等非 1 倍速状态。
    private float previousTimeScale = 1f;

    // 向其他脚本只读公开当前暂停状态。
    public bool IsPaused => isPaused;

    private void Awake()
    {
        // 确保暂停菜单拥有 CanvasGroup，用于统一控制显示和交互状态。
        if (pauseCanvasGroup == null)
            pauseCanvasGroup = GetComponent<CanvasGroup>();
        if (pauseCanvasGroup == null)
            pauseCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Inspector 未赋值时，连同场景中的非激活对象一起自动查找所需组件。
        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>(true);
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindObjectOfType<ThirdPersonCam>(true);
        if (mainMenu == null)
            mainMenu = FindObjectOfType<MainMenuController>(true);

        // 注册按钮事件；未绑定的按钮会通过空条件运算符安全跳过。
        resumeButton?.onClick.AddListener(ResumeGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        returnToMainMenuButton?.onClick.AddListener(ReturnToMainMenu);

        // 游戏开始时隐藏暂停菜单，避免它遮挡或拦截游戏界面。
        SetPauseVisible(false);
    }

    private void Update()
    {
        // 设置面板被其他脚本关闭后，重新恢复暂停菜单按钮的交互。
        if (isPaused && settingsWasOpen &&
            (settingsPanel == null || !settingsPanel.activeSelf))
        {
            settingsWasOpen = false;
            SetPauseInteraction(true);
        }

        // 仅在按下 Esc 的这一帧处理暂停、恢复或关闭设置面板。
        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            // 设置面板打开时，Esc 优先返回暂停菜单，而不是直接恢复游戏。
            settingsPanel.SetActive(false);
            settingsWasOpen = false;
            SetPauseInteraction(true);
            return;
        }

        if (isPaused)
        {
            // 当前已经暂停且未打开设置面板时，Esc 恢复游戏。
            ResumeGame();
            return;
        }

        // 主菜单和 NPC 对话开启时，Esc 不抢占它们的输入。
        if ((mainMenu != null && mainMenu.gameObject.activeInHierarchy) ||
            (QuestUI.Instance != null && QuestUI.Instance.IsDialogueOpen))
            return;

        PauseGame();
    }

    public void PauseGame()
    {
        // 已处于暂停状态时忽略重复调用。
        if (isPaused)
            return;

        isPaused = true;
        // 记录原时间缩放；如果本来就是 0，则使用 1，避免恢复后仍保持冻结。
        previousTimeScale = Mathf.Approximately(Time.timeScale, 0f)
            ? 1f
            : Time.timeScale;

        if (playerController != null)
        {
            // 先保存原状态，再禁用玩家移动。
            playerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        if (thirdPersonCamera != null)
        {
            // 暂停镜头脚本和鼠标视角输入，同时保留暂停前的启用状态。
            cameraWasEnabled = thirdPersonCamera.enabled;
            thirdPersonCamera.SetMouseInputEnabled(false);
            thirdPersonCamera.enabled = false;
        }

        // 冻结使用缩放时间的游戏逻辑，并显示可操作的暂停菜单与鼠标。
        Time.timeScale = 0f;
        SetPauseVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        // 非暂停状态下不执行恢复逻辑，避免误改时间缩放和控制组件。
        if (!isPaused)
            return;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // 恢复暂停前的时间缩放并关闭所有暂停相关界面。
        Time.timeScale = previousTimeScale;
        isPaused = false;
        settingsWasOpen = false;
        SetPauseVisible(false);

        // 按暂停前保存的状态还原玩家和镜头，而不是无条件启用。
        if (playerController != null)
            playerController.enabled = playerWasEnabled;
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetMouseInputEnabled(cameraWasEnabled);
            thirdPersonCamera.enabled = cameraWasEnabled;
        }

        // 回到游戏状态后重新锁定并隐藏光标。
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSettings()
    {
        // 设置只能从暂停状态打开，并且必须已绑定设置面板。
        if (!isPaused || settingsPanel == null)
            return;

        settingsWasOpen = true;
        // 设置面板打开期间禁用暂停菜单交互，防止底层按钮被误点。
        SetPauseInteraction(false);
        settingsPanel.SetActive(true);
        // 将设置面板移到同一 Canvas 的最上层，保证它显示在暂停菜单之上。
        settingsPanel.transform.SetAsLastSibling();
    }

    public void ReturnToMainMenu()
    {
        // 先恢复时间缩放，再重载当前场景，让场景回到主菜单的初始状态。
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void SetPauseVisible(bool visible)
    {
        // 同步透明度、按钮交互和射线拦截，彻底切换暂停菜单的显示状态。
        pauseCanvasGroup.alpha = visible ? 1f : 0f;
        pauseCanvasGroup.interactable = visible;
        pauseCanvasGroup.blocksRaycasts = visible;
    }

    private void SetPauseInteraction(bool enabled)
    {
        // 只切换交互能力而不改变透明度，供设置面板覆盖暂停菜单时使用。
        pauseCanvasGroup.interactable = enabled;
        pauseCanvasGroup.blocksRaycasts = enabled;
    }

    private void OnDestroy()
    {
        // 对应 Awake 中的事件注册，销毁时解除监听以避免残留回调。
        resumeButton?.onClick.RemoveListener(ResumeGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        returnToMainMenuButton?.onClick.RemoveListener(ReturnToMainMenu);

        // 若对象在暂停期间被销毁，务必恢复时间缩放，防止游戏永久冻结。
        if (isPaused)
            Time.timeScale = previousTimeScale;
    }
}
