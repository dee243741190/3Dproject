using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour
{
    [Header("Game Start")]
    [SerializeField] private QuestData startingQuest;

    private readonly Dictionary<string, QuestRuntime> quests = new Dictionary<string, QuestRuntime>();
    private InventorySystem inventory;
    private bool isTurningIn;
    private bool startingQuestAccepted;
    private bool rewardsNeedCheck;

    public static QuestManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        inventory = InventorySystem.Instance;
        if (inventory == null)
        {
            Debug.LogWarning("QuestManager: scene has no InventorySystem.", this);
            return;
        }
        inventory.InventoryChanged += RefreshActiveQuests;
        rewardsNeedCheck = true;

    }

    // 由主菜单在镜头交接完成、恢复操作时调用。
    public void AcceptStartingQuest()
    {
        if (startingQuestAccepted || startingQuest == null)
            return;
        startingQuestAccepted = AcceptQuest(startingQuest);
    }

    public QuestRuntime RegisterQuest(QuestData data)
    {
        if (data == null) return null;
        if (quests.TryGetValue(data.QuestId, out QuestRuntime existing)) return existing;
        QuestRuntime runtime = new QuestRuntime(data);
        quests.Add(data.QuestId, runtime);
        NotifyChanged(runtime);
        return runtime;
    }

    public QuestRuntime GetQuest(QuestData data) => data == null ? null : RegisterQuest(data);
    public QuestState GetState(QuestData data) => GetQuest(data)?.State ?? QuestState.Available;

    public bool ArePrerequisitesCompleted(QuestData data)
    {
        if (data == null) return false;
        foreach (QuestData prerequisite in data.Prerequisites)
        {
            if (prerequisite != null && GetState(prerequisite) != QuestState.Completed)
                return false;
        }
        return true;
    }

    public bool AcceptQuest(QuestData data)
    {
        QuestRuntime quest = RegisterQuest(data);
        if (quest == null || quest.State != QuestState.Available || !ArePrerequisitesCompleted(data))
            return false;
        quest.State = QuestState.InProgress;
        EvaluateQuest(quest);
        NotifyChanged(quest);
        return true;
    }

    public void ReportNpcTalk(string npcId)
    {
        if (string.IsNullOrWhiteSpace(npcId)) return;
        foreach (QuestRuntime quest in quests.Values)
        {
            if (quest.State != QuestState.InProgress) continue;
            bool changed = false;
            foreach (QuestObjectiveRuntime objective in quest.Objectives)
            {
                if (objective.Data.Type != QuestObjectiveType.TalkToNpc ||
                    objective.EventProgress >= objective.Data.RequiredAmount ||
                    !string.Equals(objective.Data.TargetId, npcId, StringComparison.OrdinalIgnoreCase))
                    continue;
                objective.EventProgress++;
                changed = true;
            }
            if (!changed) continue;
            EvaluateQuest(quest);
            NotifyChanged(quest);
        }
    }

    public bool TurnInQuest(QuestData data)
    {
        QuestRuntime quest = GetQuest(data);
        if (quest == null || quest.State != QuestState.ReadyToTurnIn || inventory == null) return false;
        Dictionary<ItemData, int> items = BuildItemsToConsume(quest);

        foreach (KeyValuePair<ItemData, int> item in items)
        {
            if (!inventory.HasItem(item.Key, item.Value))
            {
                quest.State = QuestState.InProgress;
                NotifyChanged(quest);
                return false;
            }
        }

        isTurningIn = true;
        foreach (KeyValuePair<ItemData, int> item in items)
            inventory.RemoveItem(item.Key, item.Value);
        isTurningIn = false;
        quest.State = QuestState.Completed;
        NotifyChanged(quest);
        return true;
    }

    public int GetObjectiveProgress(QuestObjectiveRuntime objective)
    {
        if (objective == null) return 0;
        if (objective.Data.Type == QuestObjectiveType.CollectItem)
            return inventory == null || objective.Data.Item == null
                ? 0 : Mathf.Min(inventory.GetItemCount(objective.Data.Item), objective.Data.RequiredAmount);
        return Mathf.Min(objective.EventProgress, objective.Data.RequiredAmount);
    }

    public bool IsObjectiveComplete(QuestObjectiveRuntime objective) =>
        objective != null && GetObjectiveProgress(objective) >= objective.Data.RequiredAmount;

    private void RefreshActiveQuests()
    {
        rewardsNeedCheck = true;
        if (isTurningIn) return;
        foreach (QuestRuntime quest in quests.Values)
        {
            if (quest.State != QuestState.InProgress && quest.State != QuestState.ReadyToTurnIn) continue;
            EvaluateQuest(quest);
            NotifyChanged(quest);
        }
    }

    private void EvaluateQuest(QuestRuntime quest)
    {
        bool allComplete = quest.Objectives.Count > 0;
        foreach (QuestObjectiveRuntime objective in quest.Objectives)
        {
            if (IsObjectiveComplete(objective)) continue;
            allComplete = false;
            break;
        }
        if (!allComplete)
        {
            quest.State = QuestState.InProgress;
            return;
        }
        quest.State = quest.Data.CompletionMode == QuestCompletionMode.AutoComplete
            ? QuestState.Completed : QuestState.ReadyToTurnIn;
    }

    private static Dictionary<ItemData, int> BuildItemsToConsume(QuestRuntime quest)
    {
        Dictionary<ItemData, int> totals = new Dictionary<ItemData, int>();
        foreach (QuestObjectiveRuntime objective in quest.Objectives)
        {
            QuestObjectiveData data = objective.Data;
            if (data.Type != QuestObjectiveType.CollectItem || !data.ConsumeOnTurnIn || data.Item == null) continue;
            if (totals.ContainsKey(data.Item)) totals[data.Item] += data.RequiredAmount;
            else totals.Add(data.Item, data.RequiredAmount);
        }
        return totals;
    }

    private void NotifyChanged(QuestRuntime quest)
    {
        if (quest.State == QuestState.Completed)
            rewardsNeedCheck = true;
        QuestUI.Instance?.ShowQuest(quest, this);
    }

    // 完成和背包变化后检查一次。容量不足时保留奖励，等背包变化再补发。
    private void LateUpdate()
    {
        if (!rewardsNeedCheck || inventory == null) return;
        rewardsNeedCheck = false;
        foreach (QuestRuntime quest in quests.Values)
        {
            if (quest.State != QuestState.Completed || quest.RewardGranted) continue;
            ItemData item = quest.Data.RewardItem;
            // 添加物品会同步触发背包事件，提前标记，避免重复发奖。
            quest.RewardGranted = true;
            if (item == null) continue;
            if (inventory.TryAddItem(item, quest.Data.RewardQuantity))
            {
                QuestUI.Instance?.ShowReward(item, quest.Data.RewardQuantity);
                Debug.Log($"任务奖励：{item.DisplayName} × {quest.Data.RewardQuantity}", this);
            }
            else
            {
                quest.RewardGranted = false;
                Debug.Log($"背包空间不足：{quest.Data.DisplayName} 的奖励暂存，腾出空间后自动发放。", this);
            }
        }
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.InventoryChanged -= RefreshActiveQuests;
        if (Instance == this) Instance = null;
    }
}
