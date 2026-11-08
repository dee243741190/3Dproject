using System.Text;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestTrackerUI : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text objectivesText;

    private QuestRuntime shownQuest;

    private void Start()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.QuestChanged += OnQuestChanged;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnQuestChanged(QuestRuntime quest)
    {
        if (quest == null || quest.State == QuestState.Available)
            return;

        shownQuest = quest;
        Refresh();
    }

    private void Refresh()
    {
        if (shownQuest == null)
            return;

        bool visible = shownQuest.State != QuestState.Completed;
        if (panelRoot != null)
            panelRoot.SetActive(visible);
        if (!visible)
            return;

        if (questNameText != null)
            questNameText.text = shownQuest.Data.DisplayName;

        if (objectivesText == null)
            return;

        if (shownQuest.State == QuestState.ReadyToTurnIn)
        {
            objectivesText.text = "返回任务发布者处交付";
            return;
        }

        StringBuilder builder = new StringBuilder();
        foreach (QuestObjectiveRuntime objective in shownQuest.Objectives)
        {
            if (builder.Length > 0)
                builder.AppendLine();

            QuestObjectiveData data = objective.Data;
            int current = QuestManager.Instance.GetObjectiveProgress(objective);
            if (!string.IsNullOrWhiteSpace(data.Description))
                builder.Append(data.Description);
            else if (data.Type == QuestObjectiveType.CollectItem && data.Item != null)
                builder.Append("收集 ").Append(data.Item.DisplayName);
            else if (data.Type == QuestObjectiveType.TalkToNpc)
                builder.Append("与目标人物交谈");

            if (data.RequiredAmount > 1 || data.Type == QuestObjectiveType.CollectItem)
                builder.Append(' ').Append(current).Append('/').Append(data.RequiredAmount);
        }

        objectivesText.text = builder.ToString();
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            QuestManager.Instance.QuestChanged -= OnQuestChanged;
    }
}
