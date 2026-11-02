using UnityEngine;

public class FreeCamera : MonoBehaviour
{
    [Header("移动")]
    [Min(0.1f)] [SerializeField] private float moveSpeed = 20f;
    [Min(1f)] [SerializeField] private float sprintMultiplier = 2.5f;
    [Min(0f)] [SerializeField] private float moveSmoothTime = 0.08f;

    [Header("视角")]
    [Min(0.01f)] [SerializeField] private float mouseSensitivity = 2.5f;
    [Range(1f, 89f)] [SerializeField] private float maxPitch = 85f;

    [Header("防穿模")]
    [SerializeField] private bool enableCollision = true;
    [Min(0.01f)] [SerializeField] private float collisionRadius = 0.25f;
    [Min(0f)] [SerializeField] private float collisionPadding = 0.08f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private Vector3 currentVelocity;
    private Vector3 moveDampVelocity;
    private float yaw;
    private float pitch;

    private void Awake()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = NormalizeAngle(angles.x);
        SetCursorCaptured(true);
    }

    private void Update()
    {
        UpdateLook();
        UpdateMovement();
    }

    private void UpdateLook()
    {
        // Mouse X/Y 已经表示当前帧的鼠标位移，因此不能再乘 Time.deltaTime。
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void UpdateMovement()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float forward = Input.GetAxisRaw("Vertical");
        float vertical = 0f;

        if (Input.GetKey(KeyCode.E))
            vertical += 1f;
        if (Input.GetKey(KeyCode.Q))
            vertical -= 1f;

        Vector3 inputDirection =
            transform.right * horizontal +
            transform.forward * forward +
            Vector3.up * vertical;

        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float currentMoveSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            currentMoveSpeed *= sprintMultiplier;

        Vector3 targetVelocity = inputDirection * currentMoveSpeed;
        currentVelocity = Vector3.SmoothDamp(
            currentVelocity,
            targetVelocity,
            ref moveDampVelocity,
            moveSmoothTime);

        Vector3 displacement = currentVelocity * Time.deltaTime;
        transform.position += GetCollisionSafeDisplacement(displacement);
    }

    private Vector3 GetCollisionSafeDisplacement(Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (!enableCollision || distance <= Mathf.Epsilon)
            return displacement;

        Vector3 direction = displacement / distance;
        if (!Physics.SphereCast(
                transform.position,
                collisionRadius,
                direction,
                out RaycastHit hit,
                distance + collisionPadding,
                collisionMask,
                QueryTriggerInteraction.Ignore))
        {
            return displacement;
        }

        float safeDistance = Mathf.Max(0f, hit.distance - collisionPadding);
        currentVelocity = Vector3.ProjectOnPlane(currentVelocity, hit.normal);
        return direction * Mathf.Min(distance, safeDistance);
    }

    private void OnDisable()
    {
        SetCursorCaptured(false);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetCursorCaptured(hasFocus);
    }

    private static void SetCursorCaptured(bool captured)
    {
        Cursor.visible = !captured;
        Cursor.lockState = captured
            ? CursorLockMode.Locked
            : CursorLockMode.None;
    }

    private static float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
