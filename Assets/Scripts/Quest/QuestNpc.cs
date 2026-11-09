using System.Collections.Generic;
using Cinemachine;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestNpc : MonoBehaviour
{
    [Header("NPC")]
    [SerializeField] private string npcId = "bard";
    [SerializeField] private List<QuestData> quests = new List<QuestData>();

    [Header("Interaction")]
    [SerializeField] private Transform player;
    [Min(0.1f)] [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private TMP_Text interactionPrompt;

    [Header("Dialogue camera (priority is raised while talking)")]
    [SerializeField] private CinemachineVirtualCamera dialogueCamera;
    [Tooltip("关闭时完全使用场景里 DialogueCamera 的手动位置和镜头参数。")]
    [SerializeField] private bool autoPositionCamera;
    [SerializeField] private float cameraDistance = 9f;
    [SerializeField] private float cameraHeight = 0.8f;
    [SerializeField] private float cameraLookHeight = 0.35f;
    [Range(20f, 80f)] [SerializeField] private float cameraFieldOfView = 50f;

    private bool promptVisible;
    private bool promptInitialized;

    private void Start()
    {
        if (player == null)
            Debug.LogWarning("QuestNpc: 请在 Inspector 中设置 Player。", this);
        foreach (QuestData quest in quests)
            QuestManager.Instance?.RegisterQuest(quest);
        if (interactionPrompt == null)
            Debug.LogWarning("QuestNpc: 请在 Inspector 中设置 Interaction Prompt。", this);
        SetPrompt(false);
    }

    private void Update()
    {
        if (player == null || QuestUI.Instance == null) return;
        bool nearby = Vector3.Distance(player.position, transform.position) <= interactionRange;
        SetPrompt(nearby && !QuestUI.Instance.IsDialogueOpen);
        if (nearby && !QuestUI.Instance.IsDialogueOpen && Input.GetKeyDown(KeyCode.E))
            Interact();
    }

    private void Interact()
    {
        QuestManager manager = QuestManager.Instance;
        QuestUI ui = QuestUI.Instance;
        QuestData quest = FindCurrentQuest(manager);
        if (manager == null || ui == null || quest == null) return;
        PositionDialogueCamera();

        QuestState state = manager.GetState(quest);
        if (state == QuestState.InProgress)
        {
            manager.ReportNpcTalk(npcId);
            if (manager.GetState(quest) == QuestState.Completed)
            {
                QuestData nextQuest = FindNextAvailableQuest(manager, quest);
                if (nextQuest != null)
                {
                    ui.ShowDialogue(quest.CompletedDialogue, "继续", false,
                        () => ShowQuestOffer(nextQuest, manager, ui), dialogueCamera, true);
                }
                else
                {
                    ui.ShowDialogue(quest.CompletedDialogue, "确定", false, null, dialogueCamera);
                }
            }
            else
            {
                ui.ShowDialogue(quest.InProgressDialogue, "确定", false, null, dialogueCamera);
            }
        }
        else if (state == QuestState.ReadyToTurnIn)
        {
            ui.ShowDialogue(quest.TurnInDialogue, "交付", true,
                () => manager.TurnInQuest(quest), dialogueCamera);
        }
        else if (state == QuestState.Available)
        {
            ui.ShowDialogue(quest.OfferDialogue, "接受", true,
                () => manager.AcceptQuest(quest), dialogueCamera);
        }
        else
        {
            ui.ShowDialogue(quest.CompletedDialogue, "确定", false, null, dialogueCamera);
        }
    }

    private void ShowQuestOffer(QuestData quest, QuestManager manager, QuestUI ui)
    {
        ui.ShowDialogue(quest.OfferDialogue, "接受", true,
            () => manager.AcceptQuest(quest), dialogueCamera);
    }

    private QuestData FindNextAvailableQuest(QuestManager manager, QuestData completedQuest)
    {
        foreach (QuestData quest in quests)
        {
            if (quest == null || quest == completedQuest) continue;
            if (manager.GetState(quest) == QuestState.Available && manager.ArePrerequisitesCompleted(quest))
                return quest;
        }
        return null;
    }

    private QuestData FindCurrentQuest(QuestManager manager)
    {
        if (manager == null) return null;
        foreach (QuestData quest in quests)
        {
            QuestState state = manager.GetState(quest);
            if (state == QuestState.InProgress || state == QuestState.ReadyToTurnIn) return quest;
        }
        foreach (QuestData quest in quests)
        {
            if (manager.GetState(quest) == QuestState.Available && manager.ArePrerequisitesCompleted(quest))
                return quest;
        }
        for (int i = quests.Count - 1; i >= 0; i--)
        {
            if (manager.GetState(quests[i]) == QuestState.Completed) return quests[i];
        }
        return null;
    }

    private void SetPrompt(bool visible)
    {
        if (promptInitialized && promptVisible == visible) return;
        promptInitialized = true;
        promptVisible = visible;
        if (interactionPrompt != null) interactionPrompt.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        if (!promptVisible || interactionPrompt == null || Camera.main == null) return;
        interactionPrompt.transform.rotation = Quaternion.LookRotation(
            interactionPrompt.transform.position - Camera.main.transform.position,
            Camera.main.transform.up);
    }

    private void PositionDialogueCamera()
    {
        if (dialogueCamera == null || player == null || !autoPositionCamera)
            return;

        Vector3 npcFocus = transform.position + Vector3.up * 1.25f;
        Vector3 playerFocus = player.position + Vector3.up * 1.25f;
        Vector3 focusPoint = (npcFocus + playerFocus) * 0.5f;
        Vector3 conversationDirection = npcFocus - playerFocus;
        conversationDirection.y = 0f;
        if (conversationDirection.sqrMagnitude < 0.01f)
            conversationDirection = transform.forward;

        Vector3 side = Vector3.Cross(Vector3.up, conversationDirection.normalized);

        // Use the side closest to the current gameplay camera. This avoids an
        // unnecessary 180-degree jump and gives a stable two-shot.
        if (Camera.main != null)
        {
            Vector3 toGameplayCamera = Camera.main.transform.position - focusPoint;
            if (Vector3.Dot(side, toGameplayCamera) < 0f)
                side = -side;
        }

        dialogueCamera.m_Lens.FieldOfView = cameraFieldOfView;
        dialogueCamera.transform.position = focusPoint + side * cameraDistance + Vector3.up * cameraHeight;
        dialogueCamera.transform.rotation = Quaternion.LookRotation(
            focusPoint + Vector3.up * cameraLookHeight - dialogueCamera.transform.position,
            Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
