using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    [Header("Pause UI")]
    [SerializeField] private CanvasGroup pauseCanvasGroup;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button returnToMainMenuButton;

    [Header("Shared Settings")]
    [SerializeField] private GameObject settingsPanel;

    [Header("Gameplay Control")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private ThirdPersonCam thirdPersonCamera;
    [SerializeField] private MainMenuController mainMenu;

    private bool isPaused;
    private bool settingsWasOpen;
    private bool playerWasEnabled;
    private bool cameraWasEnabled;
    private float previousTimeScale = 1f;

    public bool IsPaused => isPaused;

    private void Awake()
    {
        if (pauseCanvasGroup == null)
            pauseCanvasGroup = GetComponent<CanvasGroup>();
        if (pauseCanvasGroup == null)
            pauseCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (playerController == null)
            playerController = FindObjectOfType<PlayerController>(true);
        if (thirdPersonCamera == null)
            thirdPersonCamera = FindObjectOfType<ThirdPersonCam>(true);
        if (mainMenu == null)
            mainMenu = FindObjectOfType<MainMenuController>(true);

        resumeButton?.onClick.AddListener(ResumeGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        returnToMainMenuButton?.onClick.AddListener(ReturnToMainMenu);

        SetPauseVisible(false);
    }

    private void Update()
    {
        if (isPaused && settingsWasOpen &&
            (settingsPanel == null || !settingsPanel.activeSelf))
        {
            settingsWasOpen = false;
            SetPauseInteraction(true);
        }

        if (!Input.GetKeyDown(KeyCode.Escape))
            return;

        if (isPaused && settingsPanel != null && settingsPanel.activeSelf)
        {
            settingsPanel.SetActive(false);
            settingsWasOpen = false;
            SetPauseInteraction(true);
            return;
        }

        if (isPaused)
        {
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
        if (isPaused)
            return;

        isPaused = true;
        previousTimeScale = Mathf.Approximately(Time.timeScale, 0f)
            ? 1f
            : Time.timeScale;

        if (playerController != null)
        {
            playerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        if (thirdPersonCamera != null)
        {
            cameraWasEnabled = thirdPersonCamera.enabled;
            thirdPersonCamera.SetMouseInputEnabled(false);
            thirdPersonCamera.enabled = false;
        }

        Time.timeScale = 0f;
        SetPauseVisible(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        if (!isPaused)
            return;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        Time.timeScale = previousTimeScale;
        isPaused = false;
        settingsWasOpen = false;
        SetPauseVisible(false);

        if (playerController != null)
            playerController.enabled = playerWasEnabled;
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetMouseInputEnabled(cameraWasEnabled);
            thirdPersonCamera.enabled = cameraWasEnabled;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenSettings()
    {
        if (!isPaused || settingsPanel == null)
            return;

        settingsWasOpen = true;
        SetPauseInteraction(false);
        settingsPanel.SetActive(true);
        settingsPanel.transform.SetAsLastSibling();
    }

    public void ReturnToMainMenu()
    {
        // TODO: 接入存档系统后，在保存成功的回调里返回主菜单。
        Debug.Log("返回主菜单：等待接入存档系统，保存完成后再返回。", this);
    }

    private void SetPauseVisible(bool visible)
    {
        pauseCanvasGroup.alpha = visible ? 1f : 0f;
        pauseCanvasGroup.interactable = visible;
        pauseCanvasGroup.blocksRaycasts = visible;
    }

    private void SetPauseInteraction(bool enabled)
    {
        pauseCanvasGroup.interactable = enabled;
        pauseCanvasGroup.blocksRaycasts = enabled;
    }

    private void OnDestroy()
    {
        resumeButton?.onClick.RemoveListener(ResumeGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        returnToMainMenuButton?.onClick.RemoveListener(ReturnToMainMenu);

        if (isPaused)
            Time.timeScale = previousTimeScale;
    }
}
