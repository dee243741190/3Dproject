using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public sealed class QuestNpcEntry
{
    [SerializeField] private QuestData quest;
    [SerializeField] private DialogueData offerDialogue;
    [SerializeField] private DialogueData inProgressDialogue;
    [SerializeField] private DialogueData turnInDialogue;
    [SerializeField] private DialogueData completedDialogue;
    [Header("Optional camera Timelines")]
    [SerializeField] private PlayableDirector offerTimeline;
    [SerializeField] private PlayableDirector inProgressTimeline;
    [SerializeField] private PlayableDirector turnInTimeline;
    [SerializeField] private PlayableDirector completedTimeline;

    public QuestData Quest => quest;
    public DialogueData OfferDialogue => offerDialogue;
    public DialogueData InProgressDialogue => inProgressDialogue;
    public DialogueData TurnInDialogue => turnInDialogue;
    public DialogueData CompletedDialogue => completedDialogue;
    public PlayableDirector OfferTimeline => offerTimeline;
    public PlayableDirector InProgressTimeline => inProgressTimeline;
    public PlayableDirector TurnInTimeline => turnInTimeline;
    public PlayableDirector CompletedTimeline => completedTimeline;
}

[DisallowMultipleComponent]
public sealed class QuestNpc : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Unique ID used by TalkToNpc objectives, for example bard.")]
    [SerializeField] private string npcId;

    [Header("Quests (order is priority)")]
    [SerializeField] private List<QuestNpcEntry> quests = new List<QuestNpcEntry>();

    [Header("Interaction")]
    [SerializeField] private Transform player;
    [Min(0.1f)] [SerializeField] private float interactionRange = 2.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private TMP_Text interactionPrompt;
    [SerializeField] private DialoguePanel dialoguePanel;

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            foreach (QuestNpcEntry entry in quests)
                QuestManager.Instance.RegisterQuest(entry?.Quest);
        }
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (player == null || dialoguePanel == null) return;
        bool nearby = Vector3.Distance(player.position, transform.position) <= interactionRange;
        SetPromptVisible(nearby && !dialoguePanel.IsOpen);
        if (nearby && !dialoguePanel.IsOpen && Input.GetKeyDown(interactionKey)) Interact();
    }

    public void Interact()
    {
        QuestManager manager = QuestManager.Instance;
        if (manager == null || dialoguePanel == null) return;

        QuestNpcEntry entry = FindDialogueEntry(manager);
        if (entry == null) return;
        QuestState state = manager.GetState(entry.Quest);

        if (state == QuestState.InProgress)
        {
            manager.ReportNpcTalk(npcId);
            DialogueData dialogue = manager.GetState(entry.Quest) == QuestState.Completed
                ? entry.CompletedDialogue : entry.InProgressDialogue;
            PlayableDirector timeline = manager.GetState(entry.Quest) == QuestState.Completed
                ? entry.CompletedTimeline : entry.InProgressTimeline;
            dialoguePanel.Show(dialogue, "确定", "", null, null, false, timeline);
        }
        else if (state == QuestState.ReadyToTurnIn)
        {
            dialoguePanel.Show(entry.TurnInDialogue, "交付", "取消",
                () => manager.TurnInQuest(entry.Quest), null, true, entry.TurnInTimeline);
        }
        else if (state == QuestState.Available)
        {
            dialoguePanel.Show(entry.OfferDialogue, "接受", "取消",
                () => manager.AcceptQuest(entry.Quest), null, true, entry.OfferTimeline);
        }
        else
        {
            dialoguePanel.Show(entry.CompletedDialogue, "确定", "", null, null, false,
                entry.CompletedTimeline);
        }
    }

    private QuestNpcEntry FindDialogueEntry(QuestManager manager)
    {
        foreach (QuestNpcEntry entry in quests)
        {
            if (entry?.Quest == null) continue;
            QuestState state = manager.GetState(entry.Quest);
            if (state == QuestState.InProgress || state == QuestState.ReadyToTurnIn) return entry;
        }
        foreach (QuestNpcEntry entry in quests)
        {
            if (entry?.Quest != null && manager.GetState(entry.Quest) == QuestState.Available &&
                manager.ArePrerequisitesCompleted(entry.Quest)) return entry;
        }
        for (int i = quests.Count - 1; i >= 0; i--)
        {
            if (quests[i]?.Quest != null && manager.GetState(quests[i].Quest) == QuestState.Completed)
                return quests[i];
        }
        return null;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionPrompt != null) interactionPrompt.gameObject.SetActive(visible);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.8f, 1f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}
