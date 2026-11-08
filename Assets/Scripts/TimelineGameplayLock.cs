using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides selected UI at scene start, then fades it in after a manual delay.
/// Does not change player, mouse, camera, or Timeline controls.
/// </summary>
public sealed class TimelineGameplayLock : MonoBehaviour
{
    [Header("UI timing")]
    [Tooltip("How many seconds the selected UI stays hidden after the scene starts.")]
    [Min(0f)]
    [SerializeField] private float lockDurationSeconds = 5f;

    [Tooltip("Drag the UI objects that should be hidden and later shown here.")]
    [SerializeField] private GameObject[] uiObjectsToFadeIn;

    [Tooltip("How many seconds the UI fade-in takes.")]
    [Min(0f)]
    [SerializeField] private float uiFadeDuration = 1f;

    private readonly List<CanvasGroup> uiCanvasGroups = new List<CanvasGroup>();

    private void Awake()
    {
        PrepareUi();
    }

    private IEnumerator Start()
    {
        if (lockDurationSeconds > 0f)
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
