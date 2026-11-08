using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public sealed class QuestObjectiveData
{
    [SerializeField] private string objectiveId;
    [TextArea(1, 3)] [SerializeField] private string description;
    [SerializeField] private QuestObjectiveType type;

    [Header("Target (fill the fields used by this objective type)")]
    [Tooltip("CollectItem uses an ItemData asset.")]
    [SerializeField] private ItemData item;
    [Tooltip("TalkToNpc uses the NPC's unique ID, for example bard.")]
    [SerializeField] private string targetId;
    [FormerlySerializedAs("amount")]
    [Min(1)] [SerializeField] private int requiredAmount = 1;
    [Tooltip("For CollectItem: remove this item when the quest is turned in.")]
    [SerializeField] private bool consumeOnTurnIn = true;

    public string ObjectiveId => objectiveId;
    public string Description => description;
    public QuestObjectiveType Type => type;
    public ItemData Item => item;
    public string TargetId => targetId;
    public int RequiredAmount => Mathf.Max(1, requiredAmount);
    public bool ConsumeOnTurnIn => consumeOnTurnIn;
}

public enum QuestObjectiveType
{
    CollectItem,
    TalkToNpc
}

public enum QuestCompletionMode
{
    AutoComplete,
    TurnInToNpc
}

[System.Serializable]
public sealed class QuestDialogue
{
    [SerializeField] private string speakerName = "吟游诗人";
    [TextArea(2, 5)] [SerializeField] private List<string> lines = new List<string>();

    public string SpeakerName => speakerName;
    public IReadOnlyList<string> Lines => lines;
}

[CreateAssetMenu(fileName = "NewQuest", menuName = "Quest/Quest Data")]
public sealed class QuestData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string questId;
    [SerializeField] private string displayName;

    [Header("Presentation")]
    [TextArea(2, 6)]
    [SerializeField] private string description;

    [Header("Flow")]
    [SerializeField] private QuestCompletionMode completionMode = QuestCompletionMode.TurnInToNpc;
    [Tooltip("Every prerequisite must be completed before this quest can be accepted.")]
    [SerializeField] private List<QuestData> prerequisites = new List<QuestData>();

    [Header("Objectives")]
    [FormerlySerializedAs("requiredItems")]
    [SerializeField] private List<QuestObjectiveData> objectives = new List<QuestObjectiveData>();

    [Header("NPC Dialogue")]
    [SerializeField] private QuestDialogue offerDialogue = new QuestDialogue();
    [SerializeField] private QuestDialogue inProgressDialogue = new QuestDialogue();
    [SerializeField] private QuestDialogue turnInDialogue = new QuestDialogue();
    [SerializeField] private QuestDialogue completedDialogue = new QuestDialogue();

    public string QuestId => string.IsNullOrWhiteSpace(questId) ? name : questId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public QuestCompletionMode CompletionMode => completionMode;
    public IReadOnlyList<QuestData> Prerequisites => prerequisites;
    public IReadOnlyList<QuestObjectiveData> Objectives => objectives;
    public QuestDialogue OfferDialogue => offerDialogue;
    public QuestDialogue InProgressDialogue => inProgressDialogue;
    public QuestDialogue TurnInDialogue => turnInDialogue;
    public QuestDialogue CompletedDialogue => completedDialogue;
}
