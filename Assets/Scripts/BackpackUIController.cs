using System.Collections;
using Cinemachine;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BackpackUIController : MonoBehaviour
{
    [Header("Backpack UI")]
    [SerializeField] private GameObject bag;
    [SerializeField] private Button closeButton;
    [SerializeField] private CinemachineFreeLook freeLook;
    [SerializeField] private Transform playerModel;
    [SerializeField] private KeyCode toggleKey = KeyCode.G;
    [Min(0.05f)] [SerializeField] private float transitionDuration = 0.4f;
    [Range(0.8f, 1f)] [SerializeField] private float hiddenScale = 0.94f;

    [Header("Inventory Camera")]
    [Range(0.1f, 0.9f)] [SerializeField] private float characterScreenX = 0.82f;
    [Tooltip("数值越小，角色在最终画面中越靠上。")]
    [Range(0.1f, 0.9f)] [SerializeField] private float characterScreenY = 0.45f;
    [Range(0f, 1f)] [SerializeField] private float inventoryCameraY = 0.5f;

    private CanvasGroup bagCanvasGroup;
    private RectTransform bagRect;
    private Vector3 visibleBagScale;
    private Coroutine transitionRoutine;
    private CinemachineComposer[] cameraComposers;
    private float[] previousScreenXValues;
    private float[] previousScreenYValues;
    private string previousXAxisName;
    private string previousYAxisName;
    private float previousXMaxSpeed;
    private float previousYMaxSpeed;
    private float previousXAxisValue;
    private float previousYAxisValue;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool hasCapturedDefaultState;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public static bool IsAnyBackpackOpen { get; private set; }

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(CloseBag);

        if (bag != null)
        {
            bagCanvasGroup = bag.GetComponent<CanvasGroup>();
            if (bagCanvasGroup == null)
                bagCanvasGroup = bag.AddComponent<CanvasGroup>();
            bagRect = bag.GetComponent<RectTransform>();
            visibleBagScale = bagRect != null ? bagRect.localScale : Vector3.one;
            bagCanvasGroup.alpha = 0f;
            bagCanvasGroup.interactable = false;
            bagCanvasGroup.blocksRaycasts = false;
            bag.SetActive(false);
        }

        isOpen = false;
        IsAnyBackpackOpen = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            SetBagOpen(!isOpen);

        if (IsAnyBackpackOpen && freeLook != null)
            LockCameraInput();
    }

    public void OpenBag() => SetBagOpen(true);
    public void CloseBag() => SetBagOpen(false);
    public void ToggleBag() => SetBagOpen(!isOpen);

    private void SetBagOpen(bool open)
    {
        if (bag == null)
        {
            Debug.LogError("Backpack UI: 请在 Inspector 中设置 Bag。", this);
            return;
        }

        if (isOpen == open && transitionRoutine == null)
            return;

        isOpen = open;
        if (transitionRoutine != null)
            StopCoroutine(transitionRoutine);

        if (open)
        {
            if (!hasCapturedDefaultState)
                CaptureDefaultState();
            IsAnyBackpackOpen = true;
            bag.SetActive(true);
            bagCanvasGroup.interactable = false;
            bagCanvasGroup.blocksRaycasts = false;
            LockCameraInput();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            // 淡出完成前仍锁住角色和镜头。
            bagCanvasGroup.interactable = false;
            bagCanvasGroup.blocksRaycasts = false;
        }

        transitionRoutine = StartCoroutine(PlayTransition(open));
    }

    private IEnumerator PlayTransition(bool opening)
    {
        float startAlpha = bagCanvasGroup.alpha;
        Vector3 startScale = bagRect != null ? bagRect.localScale : Vector3.one;
        Vector3 targetScale = opening ? visibleBagScale : visibleBagScale * hiddenScale;
        float startX = freeLook != null ? freeLook.m_XAxis.Value : 0f;
        float startY = freeLook != null ? freeLook.m_YAxis.Value : 0f;
        float targetX = previousXAxisValue;
        float targetY = previousYAxisValue;

        if (opening && freeLook != null)
        {
            if (playerModel != null && Camera.main != null)
            {
                // 不依赖角色或 FreeLook 的绝对角度。根据相机当前所在方向，
                // 计算它还需要绕角色旋转多少度才能来到角色正前方。
                Vector3 currentCameraSide = Camera.main.transform.position - playerModel.position;
                Vector3 desiredCameraSide = playerModel.forward;
                currentCameraSide.y = 0f;
                desiredCameraSide.y = 0f;

                if (currentCameraSide.sqrMagnitude > 0.0001f &&
                    desiredCameraSide.sqrMagnitude > 0.0001f)
                {
                    float angleToFront = Vector3.SignedAngle(
                        currentCameraSide.normalized,
                        desiredCameraSide.normalized,
                        Vector3.up);
                    targetX = startX + angleToFront;
                }
            }
            targetY = inventoryCameraY;
        }

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / transitionDuration);
            float smoothT = t * t * (3f - 2f * t);
            bagCanvasGroup.alpha = Mathf.Lerp(startAlpha, opening ? 1f : 0f, smoothT);
            if (bagRect != null)
                bagRect.localScale = Vector3.Lerp(startScale, targetScale, smoothT);

            if (freeLook != null)
            {
                freeLook.m_XAxis.Value = Mathf.LerpAngle(startX, targetX, smoothT);
                freeLook.m_YAxis.Value = Mathf.Lerp(startY, targetY, smoothT);
                InterpolateCameraComposition(opening, smoothT);
            }
            yield return null;
        }

        if (opening)
        {
            bagCanvasGroup.alpha = 1f;
            if (bagRect != null)
                bagRect.localScale = visibleBagScale;
            bagCanvasGroup.interactable = true;
            bagCanvasGroup.blocksRaycasts = true;
        }
        else
        {
            bagCanvasGroup.alpha = 0f;
            bag.SetActive(false);
            RestoreInputAndCursor();
            IsAnyBackpackOpen = false;
            hasCapturedDefaultState = false;
        }
        transitionRoutine = null;
    }

    private void CaptureDefaultState()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (freeLook != null)
        {
            previousXAxisName = freeLook.m_XAxis.m_InputAxisName;
            previousYAxisName = freeLook.m_YAxis.m_InputAxisName;
            previousXMaxSpeed = freeLook.m_XAxis.m_MaxSpeed;
            previousYMaxSpeed = freeLook.m_YAxis.m_MaxSpeed;
            previousXAxisValue = freeLook.m_XAxis.Value;
            previousYAxisValue = freeLook.m_YAxis.Value;
            cameraComposers = freeLook.GetComponentsInChildren<CinemachineComposer>(true);
            previousScreenXValues = new float[cameraComposers.Length];
            previousScreenYValues = new float[cameraComposers.Length];
            for (int i = 0; i < cameraComposers.Length; i++)
            {
                previousScreenXValues[i] = cameraComposers[i].m_ScreenX;
                previousScreenYValues[i] = cameraComposers[i].m_ScreenY;
            }
        }
        hasCapturedDefaultState = true;
    }

    private void InterpolateCameraComposition(bool opening, float t)
    {
        if (cameraComposers == null || previousScreenXValues == null || previousScreenYValues == null)
            return;
        int count = Mathf.Min(cameraComposers.Length,
            Mathf.Min(previousScreenXValues.Length, previousScreenYValues.Length));
        for (int i = 0; i < count; i++)
        {
            if (cameraComposers[i] == null)
                continue;
            float fromX = opening ? previousScreenXValues[i] : characterScreenX;
            float fromY = opening ? previousScreenYValues[i] : characterScreenY;
            float toX = opening ? characterScreenX : previousScreenXValues[i];
            float toY = opening ? characterScreenY : previousScreenYValues[i];
            cameraComposers[i].m_ScreenX = Mathf.Lerp(fromX, toX, t);
            cameraComposers[i].m_ScreenY = Mathf.Lerp(fromY, toY, t);
        }
    }

    private void LockCameraInput()
    {
        if (freeLook == null)
            return;
        freeLook.m_XAxis.m_InputAxisName = string.Empty;
        freeLook.m_YAxis.m_InputAxisName = string.Empty;
        freeLook.m_XAxis.m_InputAxisValue = 0f;
        freeLook.m_YAxis.m_InputAxisValue = 0f;
        freeLook.m_XAxis.m_MaxSpeed = 0f;
        freeLook.m_YAxis.m_MaxSpeed = 0f;
    }

    private void RestoreInputAndCursor()
    {
        if (freeLook != null)
        {
            freeLook.m_XAxis.Value = previousXAxisValue;
            freeLook.m_YAxis.Value = previousYAxisValue;
            freeLook.m_XAxis.m_InputAxisName = previousXAxisName;
            freeLook.m_YAxis.m_InputAxisName = previousYAxisName;
            freeLook.m_XAxis.m_InputAxisValue = 0f;
            freeLook.m_YAxis.m_InputAxisValue = 0f;
            freeLook.m_XAxis.m_MaxSpeed = previousXMaxSpeed;
            freeLook.m_YAxis.m_MaxSpeed = previousYMaxSpeed;
            InterpolateCameraComposition(false, 1f);
        }
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseBag);
        if (hasCapturedDefaultState)
            RestoreInputAndCursor();
        IsAnyBackpackOpen = false;
    }
}
