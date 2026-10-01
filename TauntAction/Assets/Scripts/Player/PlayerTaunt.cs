using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// <summary>
/// Left click taunts an enemy within range:
/// first the enemy closest to the mouse cursor (if one is near it),
/// otherwise the one best matching the mouse direction (smallest angle, then nearest).
/// </summary>
public class PlayerTaunt : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] float range = 14f;
    [Tooltip("An enemy within this distance of the cursor's floor point is picked directly (closest to the cursor wins).")]
    [SerializeField] float cursorSnapRadius = 2f;
    [Tooltip("Half of the aim cone, in degrees (30 = 60° cone).")]
    [SerializeField] float halfAngle = 30f;
    [Tooltip("Angles closer than this count as equal; the nearer enemy wins.")]
    [SerializeField] float angleTieTolerance = 1f;

    [Header("Cooldown")]
    [Tooltip("Test value, not final.")]
    [SerializeField] float cooldown = 1f;

    [Header("Debug visuals")]
    [SerializeField] bool showAimCone = true;
    [SerializeField] Color coneColor = new Color(0.45f, 0.45f, 0.5f);
    [SerializeField] Color candidateColor = Color.white;
    [SerializeField] Color candidateCooldownColor = new Color(0.35f, 0.35f, 0.35f);
    [SerializeField] float floorHeight = 0.06f;

    [Header("Reference")]
    [Tooltip("Falls back to Camera.main.")]
    [SerializeField] Camera aimCamera;

    Health health;
    float cooldownTimer;
    LineRenderer candidateRing;
    LineRenderer cone;

    const int ConeArcSegments = 16;

    public ITauntable Candidate { get; private set; }
    /// <summary>The cursor's point on the floor (valid while HasAim).</summary>
    public Vector3 AimPoint { get; private set; }
    public bool HasAim { get; private set; }
    public float CooldownRemaining => Mathf.Max(0f, cooldownTimer);

    void Awake()
    {
        health = GetComponent<Health>();
        if (health != null) health.Died += _ => enabled = false;
        if (aimCamera == null) aimCamera = Camera.main;

        candidateRing = RingVisual.Create("TauntCandidateRing", null, 0.9f, 0.05f, candidateColor);
        candidateRing.gameObject.SetActive(false);
        BuildCone();
    }

    void OnDisable()
    {
        Candidate = null;
        if (candidateRing != null) candidateRing.gameObject.SetActive(false);
        if (cone != null) cone.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (candidateRing != null) Destroy(candidateRing.gameObject);
        if (cone != null) Destroy(cone.gameObject);
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        bool hasAim = TryGetAim(out Vector3 aimPoint, out Vector3 aimDir);
        HasAim = hasAim;
        AimPoint = aimPoint;
        Candidate = hasAim ? FindTarget(aimPoint, aimDir) : null;

        // A click with no candidate does not spend the cooldown.
        if (Candidate != null && cooldownTimer <= 0f && TauntPressed())
        {
            if (Candidate.Taunt(transform))
                cooldownTimer = cooldown;
        }

        UpdateVisuals(hasAim, aimDir);
    }

    // ---- Targeting --------------------------------------------------------

    bool TryGetAim(out Vector3 point, out Vector3 dir)
    {
        point = transform.position;
        dir = transform.forward;
        var mouse = Mouse.current;
        if (mouse == null || aimCamera == null) return false;

        Ray ray = aimCamera.ScreenPointToRay(mouse.position.ReadValue());
        var floor = new Plane(Vector3.up, Vector3.zero);
        if (!floor.Raycast(ray, out float t)) return false;

        point = ray.GetPoint(t);
        dir = Flat(point - transform.position);
        if (dir.sqrMagnitude < 0.0001f) return false;
        dir.Normalize();
        return true;
    }

    ITauntable FindTarget(Vector3 aimPoint, Vector3 aimDir)
    {
        // Pointing right at an enemy beats direction, so stacked enemies stay selectable.
        return FindNearCursor(aimPoint) ?? FindInCone(aimDir);
    }

    ITauntable FindNearCursor(Vector3 aimPoint)
    {
        ITauntable best = null;
        float bestDist = cursorSnapRadius;

        foreach (var t in TauntRegistry.All)
        {
            if (t as Object == null || !t.CanBeTaunted) continue;
            if (Flat(t.transform.position - transform.position).magnitude > range) continue;

            float d = Flat(t.transform.position - aimPoint).magnitude;
            if (d <= bestDist)
            {
                best = t;
                bestDist = d;
            }
        }
        return best;
    }

    ITauntable FindInCone(Vector3 aimDir)
    {
        ITauntable best = null;
        float bestAngle = float.MaxValue;
        float bestDist = float.MaxValue;

        foreach (var t in TauntRegistry.All)
        {
            if (t as Object == null || !t.CanBeTaunted) continue;

            Vector3 to = Flat(t.transform.position - transform.position);
            float dist = to.magnitude;
            if (dist > range || dist < 0.001f) continue;

            float angle = Vector3.Angle(aimDir, to);
            if (angle > halfAngle) continue;

            bool clearlyBetter = angle < bestAngle - angleTieTolerance;
            bool tieButNearer = Mathf.Abs(angle - bestAngle) <= angleTieTolerance && dist < bestDist;
            if (clearlyBetter || tieButNearer)
            {
                best = t;
                bestAngle = angle;
                bestDist = dist;
            }
        }
        return best;
    }

    static bool TauntPressed()
    {
        var mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    // ---- Visuals ----------------------------------------------------------

    void UpdateVisuals(bool hasAim, Vector3 aimDir)
    {
        bool showRing = Candidate != null;
        candidateRing.gameObject.SetActive(showRing);
        if (showRing)
        {
            Vector3 p = Candidate.transform.position;
            candidateRing.transform.position = new Vector3(p.x, floorHeight, p.z);
            RingVisual.SetColor(candidateRing, cooldownTimer > 0f ? candidateCooldownColor : candidateColor);
        }

        bool showCone = showAimCone && hasAim;
        cone.gameObject.SetActive(showCone);
        if (showCone) UpdateCone(aimDir);
    }

    void BuildCone()
    {
        var go = new GameObject("TauntAimCone");
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cone = go.AddComponent<LineRenderer>();
        cone.useWorldSpace = true;
        cone.loop = true;
        cone.alignment = LineAlignment.TransformZ;
        cone.widthMultiplier = 0.04f;
        cone.shadowCastingMode = ShadowCastingMode.Off;
        cone.receiveShadows = false;
        cone.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        RingVisual.SetColor(cone, coneColor);
        cone.positionCount = ConeArcSegments + 2;
        go.SetActive(false);
    }

    void UpdateCone(Vector3 aimDir)
    {
        Vector3 origin = new Vector3(transform.position.x, floorHeight, transform.position.z);
        cone.SetPosition(0, origin);
        for (int i = 0; i <= ConeArcSegments; i++)
        {
            float a = Mathf.Lerp(-halfAngle, halfAngle, i / (float)ConeArcSegments);
            cone.SetPosition(i + 1, origin + Quaternion.Euler(0f, a, 0f) * aimDir * range);
        }
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
}
