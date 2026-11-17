using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using Cinemachine;

/// <summary>
/// Main menu entry point. Start New Game and Quit are implemented now;
/// Continue and Settings are reserved for their later systems.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [Min(0f)] [SerializeField] private float fadeOutDuration = 0.6f;

    [Header("New Game")]
    [SerializeField] private PlayableDirector beginTimeline;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ThirdPersonCam thirdPersonCamera;

    [Header("Optional Settings Panel")]
    [SerializeField] private GameObject settingsPanel;

    private bool isTransitioning;
    [Header("Gameplay UI")]
    [SerializeField] private CanvasGroup gameUI;
    [Min(0f)] [SerializeField] private float gameUIFadeDuration = 0.6f;
    [Header("Camera handoff")]
    [SerializeField] private CinemachineBrain cameraBrain;
    [SerializeField] private CinemachineVirtualCamera menuCamera;
    [SerializeField] private CinemachineFreeLook gameplayCamera;

    private void Awake()
    {
        if (gameUI == null)
        {
            GameObject root = GameObject.Find("GameUI");
            if (root != null) gameUI = root.GetComponent<CanvasGroup>();
        }
        if (gameUI != null)
        {
            gameUI.alpha = 0f;
            gameUI.interactable = false;
            gameUI.blocksRaycasts = false;
        }
        if (cameraBrain == null) cameraBrain = FindObjectOfType<CinemachineBrain>();
        if (gameplayCamera == null) gameplayCamera = FindObjectOfType<CinemachineFreeLook>();
        if (menuCamera == null)
        {
            GameObject root = GameObject.Find("BeginCamera");
            if (root != null) menuCamera = root.GetComponent<CinemachineVirtualCamera>();
        }
        if (menuCanvasGroup == null)
            menuCanvasGroup = GetComponent<CanvasGroup>();
        if (menuCanvasGroup == null)
            menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (beginTimeline != null)
        {
            beginTimeline.playOnAwake = false;
            beginTimeline.Stop();
        }

        SetGameplayControl(false);
        SetMenuVisible(true);

        newGameButton?.onClick.AddListener(StartNewGame);
        continueButton?.onClick.AddListener(ContinueGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        quitButton?.onClick.AddListener(QuitGame);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void Start()
    {
        ShowMenuCursor();
    }

    private void LateUpdate()
    {
        // Other gameplay scripts also manage the cursor. The main menu remains
        // authoritative until New Game has actually been pressed.
        if (!isTransitioning && gameObject.activeInHierarchy)
            ShowMenuCursor();
    }

    public void StartNewGame()
    {
        if (isTransitioning)
            return;

        isTransitioning = true;
        menuCanvasGroup.interactable = false;
        StartCoroutine(StartNewGameRoutine());
    }

    private IEnumerator StartNewGameRoutine()
    {
        yield return FadeCanvasGroup(menuCanvasGroup, 0f, fadeOutDuration);

        SetMenuVisible(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (beginTimeline != null)
        {
            beginTimeline.time = 0d;
            beginTimeline.Play();
            yield return null;

            while (beginTimeline.state == PlayState.Playing)
                yield return null;

            // Do not enable mouse look until the Timeline and its final camera
            // update have both completely finished.
            yield return new WaitForEndOfFrame();
        }

        // Timeline 释放镜头后，等待 Brain 完成向第三人称的交接。
        if (menuCamera != null && gameplayCamera != null)
            menuCamera.Priority = gameplayCamera.Priority - 1;
        yield return null;
        if (cameraBrain != null && gameplayCamera != null)
        {
            while (cameraBrain.IsBlending || cameraBrain.ActiveVirtualCamera != gameplayCamera)
                yield return null;
        }

        SetGameplayControl(true);
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

    public void ContinueGame()
    {
        Debug.Log("继续游戏：等待接入存档系统。", this);
    }

    public void OpenSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
        else
            Debug.Log("设置：等待接入设置面板。", this);
    }

    public void CloseSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetGameplayControl(bool enabled)
    {
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
        if (menuCanvasGroup == null)
            return;

        menuCanvasGroup.alpha = visible ? 1f : 0f;
        menuCanvasGroup.interactable = visible;
        menuCanvasGroup.blocksRaycasts = visible;
    }

    private static void ShowMenuCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        newGameButton?.onClick.RemoveListener(StartNewGame);
        continueButton?.onClick.RemoveListener(ContinueGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        quitButton?.onClick.RemoveListener(QuitGame);
    }
}
