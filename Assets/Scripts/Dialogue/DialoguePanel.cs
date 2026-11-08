using System;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class DialoguePanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private TMP_Text contentText;
    [SerializeField] private Image portraitImage;

    [Header("Choices")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text confirmButtonText;
    [SerializeField] private Button cancelButton;
    [SerializeField] private TMP_Text cancelButtonText;

    [Header("Gameplay paused while dialogue is open")]
    [Tooltip("For example PlayerController. Their previous enabled states are restored on close.")]
    [SerializeField] private Behaviour[] behavioursToDisable;

    private DialogueData dialogue;
    private int lineIndex;
    private Action confirmed;
    private Action cancelled;
    private string finalConfirmLabel;
    private bool finalCancelVisible;
    private PlayableDirector cameraTimeline;
    private bool[] previousBehaviourStates;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmPressed);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelPressed);
        Close();
    }

    public void Show(
        DialogueData data,
        string confirmLabel,
        string cancelLabel,
        Action onConfirmed,
        Action onCancelled,
        bool showCancel,
        PlayableDirector timeline = null)
    {
        if (data == null || data.Lines.Count == 0)
        {
            Debug.LogWarning("DialoguePanel: dialogue has no lines.", this);
            return;
        }

        dialogue = data;
        lineIndex = 0;
        confirmed = onConfirmed;
        cancelled = onCancelled;
        finalConfirmLabel = string.IsNullOrWhiteSpace(confirmLabel) ? "确定" : confirmLabel;
        finalCancelVisible = showCancel;
        cameraTimeline = timeline;
        DisableGameplayBehaviours();
        if (cameraTimeline != null)
        {
            cameraTimeline.time = 0d;
            cameraTimeline.Play();
        }

        if (cancelButtonText != null)
            cancelButtonText.text = string.IsNullOrWhiteSpace(cancelLabel) ? "取消" : cancelLabel;
        if (panelRoot != null)
            panelRoot.SetActive(true);

        RefreshLine();
    }

    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
        if (cameraTimeline != null)
            cameraTimeline.Stop();
        cameraTimeline = null;
        RestoreGameplayBehaviours();
        dialogue = null;
        confirmed = null;
        cancelled = null;
    }

    private void RefreshLine()
    {
        DialogueLine line = dialogue.Lines[lineIndex];
        if (speakerNameText != null) speakerNameText.text = line.SpeakerName;
        if (contentText != null) contentText.text = line.Text;
        if (portraitImage != null)
        {
            portraitImage.sprite = line.Portrait;
            portraitImage.gameObject.SetActive(line.Portrait != null);
        }

        bool isLastLine = lineIndex >= dialogue.Lines.Count - 1;
        if (confirmButtonText != null)
            confirmButtonText.text = isLastLine ? finalConfirmLabel : "继续";
        if (cancelButton != null)
            cancelButton.gameObject.SetActive(isLastLine && finalCancelVisible);
    }

    private void OnConfirmPressed()
    {
        if (dialogue == null)
            return;

        if (lineIndex < dialogue.Lines.Count - 1)
        {
            lineIndex++;
            RefreshLine();
            return;
        }

        Action callback = confirmed;
        Close();
        callback?.Invoke();
    }

    private void OnCancelPressed()
    {
        Action callback = cancelled;
        Close();
        callback?.Invoke();
    }

    private void DisableGameplayBehaviours()
    {
        previousBehaviourStates = new bool[behavioursToDisable?.Length ?? 0];
        for (int i = 0; i < previousBehaviourStates.Length; i++)
        {
            Behaviour behaviour = behavioursToDisable[i];
            if (behaviour == null) continue;
            previousBehaviourStates[i] = behaviour.enabled;
            behaviour.enabled = false;
        }
    }

    private void RestoreGameplayBehaviours()
    {
        if (previousBehaviourStates == null) return;
        for (int i = 0; i < previousBehaviourStates.Length; i++)
        {
            if (behavioursToDisable[i] != null)
                behavioursToDisable[i].enabled = previousBehaviourStates[i];
        }
        previousBehaviourStates = null;
    }
}
