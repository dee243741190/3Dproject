using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class DialogueLine
{
    [SerializeField] private string speakerName;
    [SerializeField] private Sprite portrait;
    [TextArea(2, 6)] [SerializeField] private string text;

    public string SpeakerName => speakerName;
    public Sprite Portrait => portrait;
    public string Text => text;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogue/Dialogue Data")]
public sealed class DialogueData : ScriptableObject
{
    [SerializeField] private List<DialogueLine> lines = new List<DialogueLine>();
    public IReadOnlyList<DialogueLine> Lines => lines;
}
