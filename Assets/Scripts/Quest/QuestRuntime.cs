using System.Collections.Generic;

public sealed class QuestObjectiveRuntime
{
    public QuestObjectiveRuntime(QuestObjectiveData data) { Data = data; }
    public QuestObjectiveData Data { get; }
    public int EventProgress { get; internal set; }
}

public sealed class QuestRuntime
{
    public QuestRuntime(QuestData data)
    {
        Data = data;
        State = QuestState.Available;
        Objectives = new List<QuestObjectiveRuntime>();
        foreach (QuestObjectiveData objective in data.Objectives)
        {
            if (objective != null)
                Objectives.Add(new QuestObjectiveRuntime(objective));
        }
    }

    public QuestData Data { get; }
    public QuestState State { get; internal set; }
    public List<QuestObjectiveRuntime> Objectives { get; }
}
