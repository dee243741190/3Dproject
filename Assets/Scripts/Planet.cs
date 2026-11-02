using UnityEngine;

public class Planet : MonoBehaviour
{
    public enum CelestialBody
    {
        Auto, Sun, Mercury, Venus, Earth, Moon, Mars,
        Jupiter, Saturn, Uranus, Neptune, Pluto
    }

    [Header("天体与公转中心")]
    [SerializeField] private CelestialBody body = CelestialBody.Auto;
    [SerializeField] private Transform aroundParent;

    [Header("演示时间倍率")]
    [Tooltip("地球自转一圈所需的演示秒数。")]
    [Min(0.1f)] [SerializeField] private float earthDaySeconds = 12f;
    [Tooltip("地球公转一圈所需的演示秒数。")]
    [Min(1f)] [SerializeField] private float earthYearSeconds = 30f;
    [Tooltip("公转周期压缩指数。0.25保留真实快慢顺序，同时让外行星可见地运动。")]
    [Range(0.1f, 1f)]
    [SerializeField] private float orbitPeriodCompression = 0.25f;

    [Header("真实参数（运行时由天体类型自动填写）")]
    [SerializeField] private float rotationPeriodDays = 0.9972697f;
    [SerializeField] private float orbitalPeriodDays = 365.256f;
    [SerializeField] private float axialTiltDegrees = 23.44f;
    [SerializeField] private float orbitalInclinationDegrees;
    [Range(0f, 0.95f)]
    [SerializeField] private float orbitalEccentricity = 0.0167f;

    private Vector3 orbitRight;
    private Vector3 orbitForward;
    private Vector3 orbitNormal;
    private Vector3 spinAxis;
    private float semiMajorAxis;
    private float startingMeanAnomaly;
    private CelestialBody resolvedBody;

    private void Awake()
    {
        EnsureCollisionSurface();

        resolvedBody = body == CelestialBody.Auto
            ? BodyFromName(gameObject.name)
            : body;

        ApplyAstronomicalPreset(resolvedBody);
        spinAxis = Quaternion.AngleAxis(axialTiltDegrees, Vector3.forward) * Vector3.up;

        if (aroundParent == null || orbitalPeriodDays <= 0f)
            return;

        Vector3 initialOffset = transform.position - aroundParent.position;
        float initialRadius = Mathf.Max(initialOffset.magnitude, 0.001f);

        // 场景中的初始距离作为半长轴，并选择椭圆上 r=a 的相位。
        // 这样既保留设定的平均距离，也不会在进入运行时瞬移。
        semiMajorAxis = initialRadius;
        Vector3 initialDirection = initialOffset.normalized;

        // 绕初始半径方向倾斜轨道面，确保初始位置位于轨道平面内。
        orbitNormal = Quaternion.AngleAxis(
            orbitalInclinationDegrees, initialDirection) * Vector3.up;

        Vector3 initialTangent = Vector3.Cross(orbitNormal, initialDirection).normalized;
        float startingTrueAnomaly = Mathf.Acos(-orbitalEccentricity);
        orbitRight = initialDirection * Mathf.Cos(startingTrueAnomaly) -
                     initialTangent * Mathf.Sin(startingTrueAnomaly);
        orbitForward = Vector3.Cross(orbitNormal, orbitRight).normalized;

        float startingEccentricAnomaly = 2f * Mathf.Atan2(
            Mathf.Sqrt(1f - orbitalEccentricity) * Mathf.Sin(startingTrueAnomaly * 0.5f),
            Mathf.Sqrt(1f + orbitalEccentricity) * Mathf.Cos(startingTrueAnomaly * 0.5f));
        startingMeanAnomaly = startingEccentricAnomaly -
                              orbitalEccentricity * Mathf.Sin(startingEccentricAnomaly);
    }

    private void Update()
    {
        RotateOnOwnAxis();

        // 月球需要等地球完成本帧公转后，再更新自己的世界坐标。
        if (resolvedBody != CelestialBody.Moon)
            OrbitParent();
    }

    private void LateUpdate()
    {
        if (resolvedBody == CelestialBody.Moon)
            OrbitParent();
    }

    private void RotateOnOwnAxis()
    {
        if (Mathf.Approximately(rotationPeriodDays, 0f))
            return;

        if (resolvedBody == CelestialBody.Moon && orbitalPeriodDays > 0f)
        {
            // 月球自转周期等于公转周期，从而保持同一面大致朝向地球。
            float displayedMoonOrbitSeconds =
                earthYearSeconds * orbitalPeriodDays / 365.256f;
            float moonDegreesPerSecond = 360f / displayedMoonOrbitSeconds;
            transform.Rotate(
                orbitNormal,
                moonDegreesPerSecond * Time.deltaTime,
                Space.World);
            return;
        }

        float degreesPerSecond =
            360f * 0.9972697f / (Mathf.Abs(rotationPeriodDays) * earthDaySeconds);
        transform.Rotate(spinAxis, degreesPerSecond * Time.deltaTime, Space.World);
    }

    private void OrbitParent()
    {
        if (aroundParent == null || orbitalPeriodDays <= 0f || semiMajorAxis <= 0f)
            return;

        // 严格真实比例会让海王星在实验时长内看起来完全静止。
        // 对周期比做幂压缩：仍然越远越慢，但所有行星都能明显运动。
        float realPeriodRatio = orbitalPeriodDays / 365.256f;
        // 月球保持真实的月/年周期比；只有行星使用可视化周期压缩。
        float displayedPeriodSeconds = resolvedBody == CelestialBody.Moon
            ? earthYearSeconds * realPeriodRatio
            : earthYearSeconds * Mathf.Pow(realPeriodRatio, orbitPeriodCompression);
        float meanMotion = 2f * Mathf.PI / displayedPeriodSeconds;
        float meanAnomaly = startingMeanAnomaly + meanMotion * Time.time;
        float eccentricAnomaly = SolveEccentricAnomaly(meanAnomaly, orbitalEccentricity);

        float x = semiMajorAxis * (Mathf.Cos(eccentricAnomaly) - orbitalEccentricity);
        float z = semiMajorAxis * Mathf.Sqrt(1f - orbitalEccentricity * orbitalEccentricity) *
                  Mathf.Sin(eccentricAnomaly);

        transform.position = aroundParent.position + orbitRight * x + orbitForward * z;
    }

    private static float SolveEccentricAnomaly(float meanAnomaly, float eccentricity)
    {
        float eccentricAnomaly = meanAnomaly;
        for (int i = 0; i < 5; i++)
        {
            float numerator = eccentricAnomaly -
                              eccentricity * Mathf.Sin(eccentricAnomaly) - meanAnomaly;
            float denominator = 1f - eccentricity * Mathf.Cos(eccentricAnomaly);
            eccentricAnomaly -= numerator / denominator;
        }

        return eccentricAnomaly;
    }

    private static CelestialBody BodyFromName(string objectName)
    {
        string cleanName = objectName.ToLowerInvariant();

        foreach (CelestialBody candidate in System.Enum.GetValues(typeof(CelestialBody)))
        {
            if (candidate == CelestialBody.Auto)
                continue;

            if (cleanName.Contains(candidate.ToString().ToLowerInvariant()))
                return candidate;
        }

        return CelestialBody.Earth;
    }

    private void ApplyAstronomicalPreset(CelestialBody selectedBody)
    {
        switch (selectedBody)
        {
            case CelestialBody.Sun: SetParameters(25.38f, 0f, 7.25f, 0f, 0f); break;
            case CelestialBody.Mercury: SetParameters(58.646f, 87.969f, 0.034f, 7.005f, 0.2056f); break;
            case CelestialBody.Venus: SetParameters(243.025f, 224.701f, 177.36f, 3.395f, 0.0068f); break;
            case CelestialBody.Earth: SetParameters(0.9972697f, 365.256f, 23.44f, 0f, 0.0167f); break;
            case CelestialBody.Moon: SetParameters(27.3217f, 27.3217f, 6.68f, 5.145f, 0.0549f); break;
            case CelestialBody.Mars: SetParameters(1.025957f, 686.98f, 25.19f, 1.85f, 0.0934f); break;
            case CelestialBody.Jupiter: SetParameters(0.41354f, 4332.59f, 3.13f, 1.303f, 0.0489f); break;
            case CelestialBody.Saturn: SetParameters(0.44401f, 10759.22f, 26.73f, 2.489f, 0.0565f); break;
            case CelestialBody.Uranus: SetParameters(0.71833f, 30688.5f, 97.77f, 0.773f, 0.0463f); break;
            case CelestialBody.Neptune: SetParameters(0.67125f, 60182f, 28.32f, 1.77f, 0.0095f); break;
            case CelestialBody.Pluto: SetParameters(6.387f, 90560f, 119.61f, 17.16f, 0.2488f); break;
        }
    }

    private void SetParameters(float rotationDays, float orbitDays, float tilt,
        float inclination, float eccentricity)
    {
        rotationPeriodDays = rotationDays;
        orbitalPeriodDays = orbitDays;
        axialTiltDegrees = tilt;
        orbitalInclinationDegrees = inclination;
        orbitalEccentricity = eccentricity;
    }

    private void EnsureCollisionSurface()
    {
        if (GetComponent<Collider>() != null)
            return;

        SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
        sphereCollider.center = Vector3.zero;
        sphereCollider.radius = 0.5f;
    }

}

