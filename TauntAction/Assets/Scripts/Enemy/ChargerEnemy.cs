using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class ChargerEnemy : MonoBehaviour, ITauntable
{
    public enum State { Idle, Windup, Charge, Recover, Stunned }

    [Header("Targeting")]
    [SerializeField] Transform target;

    [Header("Natural attack (pressure)")]
    [Tooltip("Attack on its own every few seconds without a taunt. OFF while verifying the taunt loop.")]
    [SerializeField] bool naturalAttackEnabled = false;
    [SerializeField] float naturalIntervalMin = 4f;
    [SerializeField] float naturalIntervalMax = 6f;
    [Tooltip("Natural attacks only start when the target is within this range.")]
    [SerializeField] float aggroRange = 14f;

    [Header("Taunt")]
    [SerializeField] Color tauntRingColor = new Color(1f, 0.2f, 0.9f);
    [SerializeField] float tauntRingRadius = 1.1f;

    [Header("Windup (telegraph)")]
    [SerializeField] float windupTime = 1.0f;
    [SerializeField] float windupTurnSpeed = 360f;
    [Tooltip("Charge direction locks this many seconds before the charge starts.")]
    [SerializeField] float aimLockTime = 0.3f;

    [Header("Charge")]
    [SerializeField] float chargeSpeed = 18f;
    [SerializeField] float chargeDistance = 12f;
    [SerializeField] float hitRadius = 0.9f;
    [SerializeField] float chargeDamage = 1f;
    [Tooltip("Hitting another enemy ends the charge on the spot (like a wall) instead of passing through.")]
    [SerializeField] bool stopOnEnemyHit = true;
    [SerializeField] float enemyHitStunTime = 1.0f;

    [Header("Recovery")]
    [SerializeField] float recoverTime = 0.7f;
    [SerializeField] float wallStunTime = 1.2f;
    [SerializeField] float idleCooldown = 0.3f;

    [Header("Arena")]
    [SerializeField] float arenaHalfExtent = 15f;
    [SerializeField] float bodyRadius = 0.5f;

    [Header("Debug colors")]
    [SerializeField] Color idleColor = new Color(0.55f, 0.25f, 0.25f);
    [SerializeField] Color windupColor = new Color(1f, 0.85f, 0.2f);
    [SerializeField] Color chargeColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] Color recoverColor = new Color(0.3f, 0.15f, 0.15f);
    [SerializeField] Color stunColor = new Color(0.3f, 0.5f, 1f);

    public State Current { get; private set; } = State.Idle;

    float stateTimer;
    float naturalTimer;
    float traveled;
    Vector3 chargeDir;
    Health health;
    Renderer bodyRenderer;
    Material bodyMat;
    Transform lane;
    Material laneMat;
    LineRenderer tauntRing;
    readonly HashSet<Object> hitThisCharge = new HashSet<Object>();

    // Taunt accepted while busy: start the windup as soon as this enemy is free.
    bool tauntPending;
    // From an accepted taunt until that taunted charge is over (drives the ring).
    bool tauntActive;

    public bool CanBeTaunted => enabled && !health.IsDead;
    public bool IsTaunted => tauntActive;

    public void SetTarget(Transform t) => target = t;

    void Awake()
    {
        bodyRenderer = GetComponent<Renderer>();
        bodyMat = bodyRenderer.material;
        health = GetComponent<Health>();
        health.Died += OnDied;
        BuildLane();
        tauntRing = RingVisual.Create("TauntRing", transform, tauntRingRadius, 0.15f, tauntRingColor);
        tauntRing.transform.localPosition = new Vector3(0f, -0.94f, 0f);
        tauntRing.gameObject.SetActive(false);
    }

    void OnEnable() => TauntRegistry.Register(this);
    void OnDisable() => TauntRegistry.Unregister(this);

    void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    void OnDied(Health _)
    {
        enabled = false;
        lane.gameObject.SetActive(false);
        SetTaunt(false, false);
        SetBodyColor(new Color(0.1f, 0.1f, 0.1f));
    }

    // ---- Taunt ------------------------------------------------------------

    /// <summary>Attack the taunter now: skip the natural-attack wait and wind up immediately if possible.</summary>
    public bool Taunt(Transform taunter)
    {
        if (!CanBeTaunted || taunter == null) return false;

        target = taunter;
        switch (Current)
        {
            case State.Idle:
                SetTaunt(true, false);
                EnterState(State.Windup, windupTime);
                break;
            case State.Windup:
                // Already winding up; it now aims at the taunter.
                SetTaunt(true, false);
                break;
            default:
                // Charge / Recover / Stunned: queue it.
                SetTaunt(true, true);
                break;
        }
        return true;
    }

    void SetTaunt(bool active, bool pending)
    {
        tauntActive = active;
        tauntPending = pending;
        tauntRing.gameObject.SetActive(active);
    }

    void Start()
    {
        if (target == null)
        {
            var p = FindAnyObjectByType<PlayerMotor>();
            if (p != null) target = p.transform;
        }
        EnterState(State.Idle, idleCooldown);
    }

    void Update()
    {
        stateTimer -= Time.deltaTime;

        switch (Current)
        {
            case State.Idle:
                naturalTimer -= Time.deltaTime;
                UpdateIdle();
                break;
            case State.Windup: UpdateWindup(); break;
            case State.Charge: UpdateCharge(); break;
            case State.Recover:
            case State.Stunned:
                if (stateTimer <= 0f) EnterState(State.Idle, idleCooldown);
                break;
        }
    }

    // ---- States -----------------------------------------------------------

    void UpdateIdle()
    {
        // A queued taunt skips both the idle cooldown and the natural-attack wait.
        if (tauntPending)
        {
            tauntPending = false;
            EnterState(State.Windup, windupTime);
            return;
        }

        if (!naturalAttackEnabled || stateTimer > 0f || naturalTimer > 0f || target == null) return;
        if (FlatDistanceTo(target.position) <= aggroRange)
            EnterState(State.Windup, windupTime);
    }

    void UpdateWindup()
    {
        float untilLock = stateTimer - aimLockTime;
        if (untilLock > 0f && target != null)
        {
            Vector3 to = Flat(target.position - transform.position);
            if (to.sqrMagnitude > 0.0001f)
            {
                Quaternion want = Quaternion.LookRotation(to, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, windupTurnSpeed * Time.deltaTime);
            }
        }

        float t = 1f - Mathf.Clamp01(stateTimer / windupTime);
        laneMat.SetColor("_BaseColor", Color.Lerp(new Color(0.35f, 0.1f, 0.1f), new Color(1f, 0.1f, 0.05f), t));

        if (stateTimer <= 0f) EnterState(State.Charge, 0f);
    }

    void UpdateCharge()
    {
        float step = chargeSpeed * Time.deltaTime;
        Vector3 pos = transform.position + chargeDir * step;

        float limit = arenaHalfExtent - bodyRadius;
        bool hitWall = false;
        if (Mathf.Abs(pos.x) > limit) { pos.x = Mathf.Clamp(pos.x, -limit, limit); hitWall = true; }
        if (Mathf.Abs(pos.z) > limit) { pos.z = Mathf.Clamp(pos.z, -limit, limit); hitWall = true; }

        traveled += (pos - transform.position).magnitude;
        transform.position = pos;

        bool hitEnemy = DetectHits();

        if (hitEnemy && stopOnEnemyHit) EnterState(State.Stunned, enemyHitStunTime);
        else if (hitWall) EnterState(State.Stunned, wallStunTime);
        else if (traveled >= chargeDistance) EnterState(State.Recover, recoverTime);
    }

    void EnterState(State next, float duration)
    {
        Current = next;
        stateTimer = duration;

        lane.gameObject.SetActive(next == State.Windup);

        // The taunted charge is over once we leave it, unless another taunt is queued.
        if (tauntActive && !tauntPending && (next == State.Recover || next == State.Stunned || next == State.Idle))
            SetTaunt(false, false);

        switch (next)
        {
            case State.Idle:
                SetBodyColor(idleColor);
                // Restart the natural wait every time, so a taunted attack isn't followed by an instant natural one.
                naturalTimer = Random.Range(naturalIntervalMin, naturalIntervalMax);
                break;
            case State.Windup: SetBodyColor(windupColor); break;
            case State.Charge:
                SetBodyColor(chargeColor);
                chargeDir = Flat(transform.forward).normalized;
                traveled = 0f;
                hitThisCharge.Clear();
                break;
            case State.Recover: SetBodyColor(recoverColor); break;
            case State.Stunned: SetBodyColor(stunColor); break;
        }
    }

    // ---- Hit detection (explicit, no rigidbody physics) -------------------

    /// <summary>Returns true if another enemy (anything tauntable) was hit this frame.</summary>
    bool DetectHits()
    {
        bool hitEnemy = false;
        var cols = Physics.OverlapSphere(transform.position, hitRadius);
        foreach (var c in cols)
        {
            var victim = c.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.IsDead) continue;
            if (!hitThisCharge.Add(victim)) continue;

            victim.TakeDamage(new DamageInfo(chargeDamage, gameObject, chargeDir));
            if (victim.GetComponent<ITauntable>() != null) hitEnemy = true;
        }
        return hitEnemy;
    }

    // ---- Helpers ----------------------------------------------------------

    void BuildLane()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ChargeLane";
        Destroy(go.GetComponent<Collider>());
        lane = go.transform;
        lane.SetParent(transform, false);
        lane.localPosition = new Vector3(0f, -0.97f, chargeDistance * 0.5f);
        lane.localScale = new Vector3(hitRadius * 2f, 0.02f, chargeDistance);

        laneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        go.GetComponent<Renderer>().sharedMaterial = laneMat;
        go.SetActive(false);
    }

    void SetBodyColor(Color c) => bodyMat.SetColor("_BaseColor", c);

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    float FlatDistanceTo(Vector3 p) => Flat(p - transform.position).magnitude;

    void OnValidate()
    {
        if (lane != null)
        {
            lane.localPosition = new Vector3(0f, -0.97f, chargeDistance * 0.5f);
            lane.localScale = new Vector3(hitRadius * 2f, 0.02f, chargeDistance);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.8f, Current.ToString());
        if (Current == State.Charge)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
#endif
}
