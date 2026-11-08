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

    public string QuestId => string.IsNullOrWhiteSpace(questId) ? name : questId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public QuestCompletionMode CompletionMode => completionMode;
    public IReadOnlyList<QuestData> Prerequisites => prerequisites;
    public IReadOnlyList<QuestObjectiveData> Objectives => objectives;
}
