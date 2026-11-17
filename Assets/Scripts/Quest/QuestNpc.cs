using System.Collections;
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

    [Header("Dialogue Labels")]
    [SerializeField] private string acceptLabel = "接受";
    [SerializeField] private string turnInLabel = "交付";
    [SerializeField] private string continueLabel = "继续";
    [SerializeField] private string closeLabel = "确定";

    [Header("Interaction")]
    [SerializeField] private Transform player;
    [Min(0.1f)] [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private TMP_Text interactionPrompt;

    [Header("Fixed Dialogue Staging")]
    [Tooltip("玩家开始对话前会走到这个位置；不设置时直接开始对话。")]
    [SerializeField] private Transform playerStandPoint;
    [Min(0.1f)] [SerializeField] private float stagingMoveSpeed = 2.5f;
    [Min(1f)] [SerializeField] private float stagingRotationSpeed = 540f;
    [Min(0.01f)] [SerializeField] private float stagingStopDistance = 0.05f;

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
    private bool isStagingDialogue;
    private PlayerController stagedPlayerController;
    private ThirdPersonCam stagedLookController;
    private Animator stagedAnimator;
    private bool stagedPlayerControllerWasEnabled;
    private bool stagedLookControllerWasEnabled;

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
        SetPrompt(nearby && !isStagingDialogue && !QuestUI.Instance.IsDialogueOpen);
        if (nearby && !isStagingDialogue && !QuestUI.Instance.IsDialogueOpen && Input.GetKeyDown(KeyCode.E))
            Interact();
    }

    private void Interact()
    {
        QuestManager manager = QuestManager.Instance;
        QuestUI ui = QuestUI.Instance;
        QuestData quest = FindCurrentQuest(manager);
        if (manager == null || ui == null || quest == null) return;

        if (playerStandPoint != null)
        {
            StartCoroutine(MovePlayerToDialoguePoint(manager, ui, quest));
            return;
        }

        OpenQuestDialogue(manager, ui, quest);
    }

    private IEnumerator MovePlayerToDialoguePoint(
        QuestManager manager,
        QuestUI ui,
        QuestData quest)
    {
        isStagingDialogue = true;
        SetPrompt(false);

        stagedPlayerController = player.GetComponent<PlayerController>();
        stagedLookController = player.GetComponent<ThirdPersonCam>();
        stagedAnimator = player.GetComponentInChildren<Animator>();
        stagedPlayerControllerWasEnabled = stagedPlayerController != null && stagedPlayerController.enabled;
        stagedLookControllerWasEnabled = stagedLookController != null && stagedLookController.enabled;

        if (stagedPlayerController != null)
            stagedPlayerController.enabled = false;
        if (stagedLookController != null)
        {
            stagedLookController.SetMouseInputEnabled(false);
            stagedLookController.enabled = false;
        }

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.velocity = new Vector3(0f, body.velocity.y, 0f);

        SetWalkingAnimation(true);
        Transform facingTransform = stagedLookController != null && stagedLookController.playerObj != null
            ? stagedLookController.playerObj
            : player;

        Vector3 planarOffset = playerStandPoint.position - player.position;
        planarOffset.y = 0f;
        float stopDistanceSqr = stagingStopDistance * stagingStopDistance;

        while (planarOffset.sqrMagnitude > stopDistanceSqr)
        {
            Vector3 currentPosition = player.position;
            // 站位点只决定水平位置。Y 轴继续交给刚体和地面处理，避免
            // StandPoint 高度有细小误差时永远无法满足到达条件。
            Vector3 planarTarget = new Vector3(
                playerStandPoint.position.x,
                currentPosition.y,
                playerStandPoint.position.z);
            Vector3 nextPosition = Vector3.MoveTowards(
                currentPosition,
                planarTarget,
                stagingMoveSpeed * Time.fixedDeltaTime);
            Vector3 moveDirection = nextPosition - currentPosition;
            moveDirection.y = 0f;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                facingTransform.rotation = Quaternion.RotateTowards(
                    facingTransform.rotation,
                    targetRotation,
                    stagingRotationSpeed * Time.fixedDeltaTime);
            }

            if (body != null)
                body.MovePosition(nextPosition);
            else
                player.position = nextPosition;

            yield return new WaitForFixedUpdate();

            planarOffset = playerStandPoint.position - player.position;
            planarOffset.y = 0f;
        }

        Vector3 finalPosition = player.position;
        finalPosition.x = playerStandPoint.position.x;
        finalPosition.z = playerStandPoint.position.z;
        if (body != null)
        {
            body.position = finalPosition;
            body.velocity = Vector3.zero;
        }
        else
        {
            player.position = finalPosition;
        }

        // 先退出走路状态，再原地转向 NPC，最后才打开对话。
        SetWalkingAnimation(false);
        Vector3 lookDirection = transform.position - facingTransform.position;
        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            while (Quaternion.Angle(facingTransform.rotation, targetRotation) > 0.5f)
            {
                facingTransform.rotation = Quaternion.RotateTowards(
                    facingTransform.rotation,
                    targetRotation,
                    stagingRotationSpeed * Time.deltaTime);
                yield return null;
            }
            facingTransform.rotation = targetRotation;
        }

        RestoreStagingControl();
        isStagingDialogue = false;
        OpenQuestDialogue(manager, ui, quest);
    }

    private void OpenQuestDialogue(QuestManager manager, QuestUI ui, QuestData quest)
    {
        PositionDialogueCamera();

        QuestState state = manager.GetState(quest);
        if (state == QuestState.InProgress)
        {
            manager.ReportNpcTalk(npcId);
            if (manager.GetState(quest) == QuestState.Completed)
                ShowCompletedDialogueAndNextQuest(quest, manager, ui);
            else
            {
                ui.ShowDialogue(quest.InProgressDialogue, closeLabel, false, null, dialogueCamera);
            }
        }
        else if (state == QuestState.ReadyToTurnIn)
        {
            ui.ShowDialogue(quest.TurnInDialogue, turnInLabel, true,
                () => CompleteQuestAndContinue(quest, manager, ui), dialogueCamera, true);
        }
        else if (state == QuestState.Available)
        {
            ShowQuestOffer(quest, manager, ui);
        }
        else
        {
            // 任务已经完成后再次交谈，只重复结束对话的最后一句。
            ui.ShowDialogue(quest.CompletedDialogue, closeLabel, false, null,
                dialogueCamera, false, true);
        }
    }

    private void SetWalkingAnimation(bool walking)
    {
        if (stagedAnimator == null) return;
        stagedAnimator.SetBool("Push", false);
        stagedAnimator.SetBool("Run", walking);
    }

    private void RestoreStagingControl()
    {
        if (stagedPlayerController != null)
            stagedPlayerController.enabled = stagedPlayerControllerWasEnabled;
        if (stagedLookController != null)
        {
            stagedLookController.enabled = stagedLookControllerWasEnabled;
            stagedLookController.SetMouseInputEnabled(stagedLookControllerWasEnabled);
        }

        stagedPlayerController = null;
        stagedLookController = null;
        stagedAnimator = null;
    }

    private void ShowQuestOffer(QuestData quest, QuestManager manager, QuestUI ui)
    {
        ui.ShowDialogue(quest.OfferDialogue, acceptLabel, true,
            () => manager.AcceptQuest(quest), dialogueCamera);
    }

    private void CompleteQuestAndContinue(QuestData quest, QuestManager manager, QuestUI ui)
    {
        if (!manager.TurnInQuest(quest))
        {
            ui.CloseDialogue();
            return;
        }

        ShowCompletedDialogueAndNextQuest(quest, manager, ui);
    }

    private void ShowCompletedDialogueAndNextQuest(
        QuestData quest,
        QuestManager manager,
        QuestUI ui)
    {
        QuestData nextQuest = FindNextAvailableQuest(manager, quest);
        if (nextQuest != null)
        {
            ui.ShowDialogue(quest.CompletedDialogue, continueLabel, false,
                () => ShowQuestOffer(nextQuest, manager, ui), dialogueCamera, true);
        }
        else
        {
            ui.ShowDialogue(quest.CompletedDialogue, closeLabel, false, null, dialogueCamera);
        }
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

        if (playerStandPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(playerStandPoint.position, stagingStopDistance);
            Gizmos.DrawLine(transform.position, playerStandPoint.position);
        }
    }

    private void OnDisable()
    {
        if (!isStagingDialogue) return;
        SetWalkingAnimation(false);
        RestoreStagingControl();
        isStagingDialogue = false;
    }
}
