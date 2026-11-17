using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class QuestUI : MonoBehaviour
{
    [Header("Task UI")]
    [SerializeField] private GameObject taskPanel;
    [SerializeField] private TMP_Text taskTitleText;
    [FormerlySerializedAs("taskText")]
    [SerializeField] private TMP_Text taskObjectiveText;
    [SerializeField] private CanvasGroup taskCanvasGroup;
    [Min(0.05f)] [SerializeField] private float taskFadeDuration = 0.25f;

    [Header("Dialogue UI")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private TMP_Text continueHintText;

    [Header("Choice Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;
    [Tooltip("Overlay image shown only on the currently selected button.")]
    [SerializeField] private Sprite selectedButtonSprite;

    [Header("Optional")]
    [SerializeField] private Behaviour playerController;

    [Header("Reward Notification / 获得道具提示")]
    [SerializeField] private CanvasGroup rewardPanel;
    [SerializeField] private Image rewardIcon;
    [SerializeField] private TMP_Text rewardText;
    [Min(0.1f)] [SerializeField] private float rewardDisplayDuration = 2.5f;
    private readonly Queue<KeyValuePair<ItemData, int>> rewardQueue = new Queue<KeyValuePair<ItemData, int>>();
    private bool showingRewards;

    private IReadOnlyList<string> lines;
    private int lineIndex;
    private Action confirmed;
    private CinemachineVirtualCameraBase dialogueCamera;
    private int previousCameraPriority;
    private bool playerControllerWasEnabled;
    private Behaviour playerLookController;
    private bool playerLookControllerWasEnabled;
    private bool showCancelOnLastLine;
    private bool keepOpenAfterConfirm;
    private int selectedChoice = 1;
    private int inputBlockedThroughFrame = -1;
    private Image confirmSelectionMarker;
    private Image cancelSelectionMarker;

    public static QuestUI Instance { get; private set; }
    public bool IsDialogueOpen => dialoguePanel != null && dialoguePanel.activeSelf;
    public TMP_FontAsset UiFont => dialogueText != null ? dialogueText.font : null;

    private void Awake()
    {
        Instance = this;
        if (rewardPanel != null)
        {
            rewardPanel.alpha = 0f;
            rewardPanel.interactable = false;
            rewardPanel.blocksRaycasts = false;
            rewardPanel.gameObject.SetActive(false);
        }
        confirmButton?.onClick.AddListener(Confirm);
        cancelButton?.onClick.AddListener(CloseDialogue);
        PrepareChoiceButton(confirmButton, ref confirmSelectionMarker);
        PrepareChoiceButton(cancelButton, ref cancelSelectionMarker);
        if (taskCanvasGroup != null)
        {
            taskCanvasGroup.alpha = 0f;
            taskCanvasGroup.interactable = false;
            taskCanvasGroup.blocksRaycasts = false;
        }
        if (taskPanel != null) taskPanel.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void Start()
    {
        // Normal gameplay uses a captured mouse. Menus such as the backpack can
        // still override this after they open.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        UpdateTaskFade();
        if (!showingRewards && rewardQueue.Count > 0 && rewardPanel != null)
            StartCoroutine(ShowRewardQueue());

        if (!IsDialogueOpen || lines == null)
            return;

        bool choiceVisible = showCancelOnLastLine && lineIndex == lines.Count - 1;
        if (choiceVisible)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                SelectChoice(0);
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                SelectChoice(1);
            // Space/Return are already submitted by Unity's selected Button.
            // Handling them here as well would confirm twice in one frame: the
            // first confirmation opens the chained dialogue and the second one
            // immediately closes it.
            else if (Input.GetKeyDown(KeyCode.E))
            {
                if (selectedChoice == 0) CloseDialogue();
                else Confirm();
            }
        }
        else if (
            (Input.GetKeyDown(KeyCode.E) ||
             Input.GetKeyDown(KeyCode.Space) ||
             Input.GetKeyDown(KeyCode.Return) ||
             Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            Confirm();
        }
    }

    private void UpdateTaskFade()
    {
        if (taskCanvasGroup == null || taskPanel == null || !taskPanel.activeSelf)
            return;
        float targetAlpha = BackpackUIController.IsAnyBackpackOpen ? 0f : 1f;
        float speed = 1f / Mathf.Max(0.05f, taskFadeDuration);
        taskCanvasGroup.alpha = Mathf.MoveTowards(
            taskCanvasGroup.alpha,
            targetAlpha,
            Time.unscaledDeltaTime * speed);
        bool visible = taskCanvasGroup.alpha > 0.01f;
        taskCanvasGroup.interactable = visible;
        taskCanvasGroup.blocksRaycasts = visible;
    }

    public void ShowQuest(QuestRuntime quest, QuestManager manager)
    {
        if (quest == null || quest.State == QuestState.Available) return;
        if (quest.State == QuestState.Completed)
        {
            if (taskPanel != null) taskPanel.SetActive(false);
            if (taskCanvasGroup != null) taskCanvasGroup.alpha = 0f;
            return;
        }

        if (taskPanel != null && !taskPanel.activeSelf)
        {
            // Always start a newly displayed task from transparent. Only this
            // component owns the task CanvasGroup, avoiding startup-order races.
            if (taskCanvasGroup != null)
            {
                taskCanvasGroup.alpha = 0f;
                taskCanvasGroup.interactable = false;
                taskCanvasGroup.blocksRaycasts = false;
            }
            taskPanel.SetActive(true);
        }
        if (taskTitleText == null || taskObjectiveText == null) return;

        if (quest.State == QuestState.ReadyToTurnIn)
        {
            SetTaskText(quest.Data.DisplayName, quest.Data.ReadyToTurnInText);
            return;
        }

        StringBuilder text = new StringBuilder();
        foreach (QuestObjectiveRuntime objective in quest.Objectives)
        {
            if (text.Length > 0) text.AppendLine();
            QuestObjectiveData data = objective.Data;
            text.Append(string.IsNullOrWhiteSpace(data.Description) ? "完成任务目标" : data.Description);
            if (data.Type == QuestObjectiveType.CollectItem)
                text.Append(' ').Append(manager.GetObjectiveProgress(objective)).Append('/').Append(data.RequiredAmount);
        }
        SetTaskText(quest.Data.DisplayName, text.ToString());
    }

    private void SetTaskText(string title, string body)
    {
        taskTitleText.text = title;
        taskObjectiveText.text = body;
    }

    public void ShowDialogue(
        QuestDialogue dialogue,
        string confirmLabel,
        bool showCancel,
        Action onConfirmed,
        CinemachineVirtualCameraBase camera,
        bool keepOpen = false,
        bool startAtLastLine = false)
    {
        if (dialogue == null || dialogue.Lines.Count == 0) return;
        bool dialogueWasAlreadyOpen = IsDialogueOpen;
        lines = dialogue.Lines;
        lineIndex = startAtLastLine ? dialogue.Lines.Count - 1 : 0;
        confirmed = onConfirmed;
        dialogueCamera = camera;
        showCancelOnLastLine = showCancel;
        keepOpenAfterConfirm = keepOpen;
        // The key/button event that opened this dialogue can still be propagating.
        // Block every confirmation path for the rest of this frame so a chained
        // dialogue cannot be opened and immediately closed by the same input.
        inputBlockedThroughFrame = Time.frameCount;
        if (speakerNameText != null) speakerNameText.text = dialogue.SpeakerName;
        if (confirmButton != null)
        {
            TMP_Text label = confirmButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && !string.IsNullOrWhiteSpace(confirmLabel))
                label.text = confirmLabel;
        }
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        if (dialoguePanel != null) dialoguePanel.SetActive(true);

        if (!dialogueWasAlreadyOpen && playerController != null)
        {
            playerControllerWasEnabled = playerController.enabled;
            playerController.enabled = false;

            Rigidbody body = playerController.GetComponent<Rigidbody>();
            if (body != null)
                body.velocity = new Vector3(0f, body.velocity.y, 0f);

            playerLookController = playerController.GetComponent<ThirdPersonCam>();
            if (playerLookController != null)
            {
                playerLookControllerWasEnabled = playerLookController.enabled;
                playerLookController.enabled = false;
            }
        }
        if (!dialogueWasAlreadyOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (dialogueCamera != null)
            {
                previousCameraPriority = dialogueCamera.Priority;
                dialogueCamera.Priority = 100;
            }
        }
        RefreshDialogueLine();
    }

    private void Confirm()
    {
        if (Time.frameCount <= inputBlockedThroughFrame)
            return;
        if (lines == null) return;
        if (lineIndex < lines.Count - 1)
        {
            lineIndex++;
            RefreshDialogueLine();
            return;
        }
        Action callback = confirmed;
        bool stayOpen = keepOpenAfterConfirm && callback != null;
        if (stayOpen)
        {
            confirmed = null;
            callback.Invoke();
        }
        else
        {
            CloseDialogue();
            callback?.Invoke();
        }
    }

    private void RefreshDialogueLine()
    {
        if (dialogueText != null) dialogueText.text = lines[lineIndex];
        bool last = lineIndex == lines.Count - 1;
        bool showChoices = last && showCancelOnLastLine;
        if (confirmButton != null) confirmButton.gameObject.SetActive(showChoices);
        if (cancelButton != null) cancelButton.gameObject.SetActive(showChoices);
        if (continueHintText != null) continueHintText.gameObject.SetActive(!showChoices);
        if (showChoices) SelectChoice(1);
    }

    private void SelectChoice(int choice)
    {
        selectedChoice = Mathf.Clamp(choice, 0, 1);
        Button selected = selectedChoice == 0 ? cancelButton : confirmButton;
        selected?.Select();
        SetButtonVisual(cancelButton, selectedChoice == 0);
        SetButtonVisual(confirmButton, selectedChoice == 1);
    }

    private void SetButtonVisual(Button button, bool selected)
    {
        if (button == null) return;

        // Keep the normal button artwork as the base. The selected artwork is
        // a separate marker layered above it instead of replacing the base.
        Image marker = button == confirmButton
            ? confirmSelectionMarker
            : cancelSelectionMarker;
        if (marker != null)
            marker.gameObject.SetActive(selected && selectedButtonSprite != null);
    }

    private void PrepareChoiceButton(Button button, ref Image marker)
    {
        if (button == null) return;

        Transform existing = button.transform.Find("SelectionMarker");
        if (existing != null)
            marker = existing.GetComponent<Image>();

        if (marker == null)
        {
            GameObject markerObject = new GameObject(
                "SelectionMarker",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            markerObject.transform.SetParent(button.transform, false);
            marker = markerObject.GetComponent<Image>();

            RectTransform rect = marker.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        marker.sprite = selectedButtonSprite;
        marker.color = Color.white;
        marker.preserveAspect = true;
        marker.raycastTarget = false;
        marker.transform.SetAsLastSibling();
        marker.gameObject.SetActive(false);
    }

    public void CloseDialogue()
    {
        if (dialogueCamera != null)
            dialogueCamera.Priority = previousCameraPriority;
        dialogueCamera = null;
        if (playerController != null) playerController.enabled = playerControllerWasEnabled;
        if (playerLookController != null)
            playerLookController.enabled = playerLookControllerWasEnabled;
        playerLookController = null;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        lines = null;
        confirmed = null;
        keepOpenAfterConfirm = false;
        EventSystem.current?.SetSelectedGameObject(null);
    }

    public void ShowReward(ItemData item, int quantity)
    {
        if (item != null && quantity > 0)
            rewardQueue.Enqueue(new KeyValuePair<ItemData, int>(item, quantity));
    }

    private IEnumerator ShowRewardQueue()
    {
        showingRewards = true;
        while (rewardQueue.Count > 0)
        {
            // 先让玩家读完 NPC 的赠礼对话，再显示获得提示。
            while (IsDialogueOpen || BackpackUIController.IsAnyBackpackOpen)
                yield return null;
            var reward = rewardQueue.Dequeue();
            if (rewardIcon != null)
            {
                rewardIcon.sprite = reward.Key.Icon;
                rewardIcon.enabled = reward.Key.Icon != null;
            }
            if (rewardText != null)
                rewardText.text = reward.Key.DisplayName + " × " + reward.Value;
            rewardPanel.gameObject.SetActive(true);
            float elapsed = 0f;
            const float fade = 0.2f;
            float duration = rewardDisplayDuration + fade * 2f;
            while (elapsed < duration)
            {
                rewardPanel.alpha = Mathf.Clamp01(Mathf.Min(elapsed, duration - elapsed) / fade);
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            rewardPanel.alpha = 0f;
            rewardPanel.gameObject.SetActive(false);
        }
        showingRewards = false;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        showingRewards = false;
        if (rewardPanel != null)
        {
            rewardPanel.alpha = 0f;
            rewardPanel.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
