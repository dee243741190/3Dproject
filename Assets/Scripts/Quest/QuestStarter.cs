using UnityEngine;

[DisallowMultipleComponent]
public sealed class QuestStarter : MonoBehaviour
{
    [SerializeField] private QuestData quest;

    private void Start()
    {
        QuestManager.Instance?.AcceptQuest(quest);
    }
}
