using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Hides selected UI at scene start, then fades it in after a manual delay.
/// Does not change player, mouse, camera, or Timeline controls.
/// </summary>
public sealed class TimelineGameplayLock : MonoBehaviour
{
    [Header("UI timing")]
    [Tooltip("Optional Timeline to wait for. When empty, the PlayableDirector on this object is used.")]
    [SerializeField] private PlayableDirector introTimeline;

    [Tooltip("Fallback delay used only when there is no Timeline to wait for.")]
    [Min(0f)]
    [SerializeField] private float lockDurationSeconds;

    [Tooltip("Drag the UI objects that should be hidden and later shown here.")]
    [SerializeField] private GameObject[] uiObjectsToFadeIn;

    [Tooltip("How many seconds the UI fade-in takes.")]
    [Min(0f)]
    [SerializeField] private float uiFadeDuration = 1f;

    private readonly List<CanvasGroup> uiCanvasGroups = new List<CanvasGroup>();

    private void Awake()
    {
        if (introTimeline == null)
            introTimeline = GetComponent<PlayableDirector>();
        PrepareUi();
    }

    private IEnumerator Start()
    {
        if (introTimeline != null)
        {
            // Play On Awake is started before this coroutine reaches its first
            // frame. Follow the real sequence instead of guessing its length.
            yield return null;
            while (introTimeline.state == PlayState.Playing)
                yield return null;
        }
        else if (lockDurationSeconds > 0f)
            yield return new WaitForSecondsRealtime(lockDurationSeconds);

        if (uiFadeDuration <= 0f)
        {
            SetUiVisible();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < uiFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(elapsed / uiFadeDuration);

            foreach (CanvasGroup canvasGroup in uiCanvasGroups)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = alpha;
            }

            yield return null;
        }

        SetUiVisible();
    }

    private void PrepareUi()
    {
        uiCanvasGroups.Clear();
        if (uiObjectsToFadeIn == null)
            return;

        foreach (GameObject uiObject in uiObjectsToFadeIn)
        {
            if (uiObject == null)
                continue;

            uiObject.SetActive(true);
            CanvasGroup canvasGroup = uiObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = uiObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            uiCanvasGroups.Add(canvasGroup);
        }
    }

    private void SetUiVisible()
    {
        foreach (CanvasGroup canvasGroup in uiCanvasGroups)
        {
            if (canvasGroup == null)
                continue;

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
    }
}
