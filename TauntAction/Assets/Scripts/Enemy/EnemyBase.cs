using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Shared enemy brain: Idle (repositioning + natural-attack timer), taunt handling, Windup aiming,
/// Recover/Stunned, contact damage and obstacle helpers. Subclasses only define what the attack does.
/// </summary>
[RequireComponent(typeof(Health))]
public abstract class EnemyBase : MonoBehaviour, ITauntable
{
    public enum State { Idle, Windup, Attack, Recover, Stunned }
    public enum LaneBlockedResponse { Approach, Strafe }

    [Header("Targeting")]
    [SerializeField] protected Transform target;

    [Header("Natural attack (pressure)")]
    [Tooltip("Attack on its own every few seconds without a taunt. Turn OFF to verify the pure taunt loop.")]
    [SerializeField] bool naturalAttackEnabled = true;
    [Tooltip("Wait after returning to Idle. The full cycle also includes windup + attack + recovery.")]
    [SerializeField] float naturalIntervalMin = 1.5f;
    [SerializeField] float naturalIntervalMax = 3f;
    [Tooltip("The first natural attack is ready this soon after spawning (random in range), instead of a full interval.")]
    [SerializeField] float firstAttackDelayMin = 0.5f;
    [SerializeField] float firstAttackDelayMax = 1.5f;
    [Tooltip("Natural attacks only start when the target is within this range.")]
    [SerializeField] float aggroRange = 14f;
    [Tooltip("Skip natural attacks while a wall/obstacle blocks the lane to the target, and reposition instead. Taunts ignore this.")]
    [SerializeField] bool naturalNeedsClearLane = true;
    [Tooltip("Approach: close in and walk around the cover. Strafe: side-step to a spot with a clear lane.")]
    [SerializeField] LaneBlockedResponse laneBlockedResponse = LaneBlockedResponse.Approach;
    [Tooltip("Strafe: how far to the side to look for a clear lane.")]
    [SerializeField] float strafeProbeDistance = 2.5f;

    [Header("Repositioning (while Idle)")]
    [SerializeField] bool repositionEnabled = true;
    [SerializeField] float moveSpeed = 3.5f;
    [Tooltip("Approach while the target is farther than max. Inside it, hold position (unless the options below are on).")]
    [SerializeField] float keepDistanceMin = 6f;
    [SerializeField] float keepDistanceMax = 10f;
    [Tooltip("Back off when the target is closer than min. OFF so the player can walk in and line enemies up.")]
    [SerializeField] bool retreatWhenClose = false;
    [Tooltip("Circle the target while inside the distance band. OFF so lined-up shots stay lined up.")]
    [SerializeField] bool orbitInRange = false;
    [Tooltip("While holding, step toward the target when parked against a wall/obstacle, so it doesn't look stuck.")]
    [SerializeField] bool stepOffWalls = true;
    [Tooltip("Start stepping off when a wall is closer than this (from the body center).")]
    [SerializeField] float wallHugDistance = 1.5f;
    [Tooltip("Stop stepping off once every wall is at least this far.")]
    [SerializeField] float wallClearDistance = 2f;
    [Tooltip("Push away from other enemies closer than this.")]
    [SerializeField] float separationRadius = 3f;
    [SerializeField] float idleTurnSpeed = 240f;

    [Header("Contact damage")]
    [Tooltip("Touching this enemy outside its attack hurts the player (not other enemies).")]
    [SerializeField] float contactDamage = 1f;
    [SerializeField] float contactRadius = 1.0f;

    [Header("Taunt")]
    [SerializeField] Color tauntRingColor = new Color(1f, 0.2f, 0.9f);
    [Tooltip("Keep this outside the role ring so both stay visible.")]
    [SerializeField] float tauntRingRadius = 1.1f;

    [Header("Role ring (readability)")]
    [Tooltip("Thin always-on floor ring in the enemy's role color, so type and position read at a glance.")]
    [SerializeField] bool showRoleRing = true;
    [SerializeField] Color roleRingColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] float roleRingRadius = 0.8f;

    [Header("Windup (telegraph)")]
    [SerializeField] protected float windupTime = 1.0f;
    [SerializeField] float windupTurnSpeed = 360f;
    [Tooltip("Attack direction locks this many seconds before the attack starts.")]
    [SerializeField] float aimLockTime = 0.3f;

    [Header("Attack damage")]
    [FormerlySerializedAs("chargeDamage")]
    [SerializeField] protected float damageToPlayer = 1f;
    [Tooltip("Damage to other enemies. Higher than wall self-damage so enemy-on-enemy stays the best play.")]
    [SerializeField] protected float damageToEnemy = 2f;

    [Header("Recovery")]
    [SerializeField] protected float recoverTime = 0.7f;
    [SerializeField] float idleCooldown = 0.3f;

    [Header("Arena")]
    [SerializeField] protected float arenaHalfExtent = 15f;
    [SerializeField] protected float bodyRadius = 0.5f;

    [Header("Debug colors")]
    [SerializeField] Color idleColor = new Color(0.55f, 0.25f, 0.25f);
    [SerializeField] Color windupColor = new Color(1f, 0.85f, 0.2f);
    [FormerlySerializedAs("chargeColor")]
    [SerializeField] Color attackColor = new Color(1f, 0.15f, 0.1f);
    [SerializeField] Color recoverColor = new Color(0.3f, 0.15f, 0.15f);
    [SerializeField] Color stunColor = new Color(0.3f, 0.5f, 1f);

    public State Current { get; private set; } = State.Idle;

    protected Health health;
    protected float stateTimer;
    float naturalTimer;
    Material[] bodyMats;
    LineRenderer tauntRing;
    LineRenderer roleRing;

    // Taunt accepted while busy: start the windup as soon as this enemy is free.
    bool tauntPending;
    // From an accepted taunt until that taunted attack is over (drives the ring).
    bool tauntActive;

    float orbitSign = 1f;
    bool laneBlocked;
    float strafeSign;
    bool steppingOffWall;

    public bool CanBeTaunted => enabled && health != null && !health.IsDead;
    public bool IsTaunted => tauntActive;
    /// <summary>True while Idle and a wall/obstacle sits between this enemy and its target.</summary>
    public bool IsLaneBlocked => laneBlocked;
    /// <summary>Seconds until the next natural attack may start; negative when off or not idle.</summary>
    public float NaturalTimeRemaining => naturalAttackEnabled && Current == State.Idle ? Mathf.Max(0f, naturalTimer) : -1f;
    protected float AimLockTime => aimLockTime;
    public float WindupDuration => windupTime;
    /// <summary>0..1 through the current windup (0 outside it). For visuals.</summary>
    public float WindupProgress => Current == State.Windup && windupTime > 0f ? 1f - Mathf.Clamp01(stateTimer / windupTime) : 0f;
    /// <summary>State name for debug readouts (subclasses can rename Attack, e.g. "Charge").</summary>
    public virtual string StateLabel => Current.ToString();

    public void SetTarget(Transform t) => target = t;

    // ---- Subclass hooks ---------------------------------------------------

    /// <summary>Runs every frame while in the Attack state. Must eventually leave it.</summary>
    protected abstract void UpdateAttack();
    /// <summary>Called after the state changes (visuals, attack setup).</summary>
    protected virtual void OnEnterState(State next) { }
    /// <summary>Windup progress 0..1, every frame of the windup (telegraph visuals).</summary>
    protected virtual void OnWindupTick(float progress) { }
    /// <summary>Radius used to decide if the lane to the target is clear (body for a charge, bullet for a shot).</summary>
    protected virtual float LaneCheckRadius => bodyRadius;
    protected virtual void OnDiedExtra() { }

    // ---- Lifecycle --------------------------------------------------------

    protected virtual void Awake()
    {
        // Looked up before any helper visuals (lane, rings) are added as children.
        bodyMats = BodyVisual.InstanceMaterials(BodyVisual.Find(transform));
        health = GetComponent<Health>();
        health.Died += OnDied;
        tauntRing = RingVisual.Create("TauntRing", transform, tauntRingRadius, 0.15f, tauntRingColor);
        tauntRing.transform.localPosition = new Vector3(0f, -0.94f, 0f);
        tauntRing.gameObject.SetActive(false);

        if (showRoleRing)
        {
            roleRing = RingVisual.Create("RoleRing", transform, roleRingRadius, 0.07f, roleRingColor);
            roleRing.transform.localPosition = new Vector3(0f, -0.95f, 0f);
        }
    }

    protected virtual void OnEnable() => TauntRegistry.Register(this);
    protected virtual void OnDisable() => TauntRegistry.Unregister(this);

    protected virtual void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }

    void OnDied(Health _)
    {
        enabled = false;
        SetTaunt(false, false);
        if (roleRing != null) roleRing.gameObject.SetActive(false);
        OnDiedExtra();
        SetBodyColor(new Color(0.1f, 0.1f, 0.1f));
    }

    protected virtual void Start()
    {
        if (target == null)
        {
            var p = FindAnyObjectByType<PlayerMotor>();
            if (p != null) target = p.transform;
        }
        orbitSign = Random.value < 0.5f ? -1f : 1f;
        // A taunt can arrive before Start; don't overwrite it.
        if (!tauntActive) EnterState(State.Idle, idleCooldown);
        // Start ready to attack: in range = attacking soon, no long opening stare.
        naturalTimer = Random.Range(firstAttackDelayMin, firstAttackDelayMax);
    }

    protected virtual void Update()
    {
        stateTimer -= Time.deltaTime;

        if (Current != State.Attack) ApplyContactDamage();

        switch (Current)
        {
            case State.Idle:
                naturalTimer -= Time.deltaTime;
                UpdateIdle();
                break;
            case State.Windup: UpdateWindup(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Recover:
            case State.Stunned:
                if (stateTimer <= 0f) EnterState(State.Idle, idleCooldown);
                break;
        }
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
                // Attack / Recover / Stunned: queue it.
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

        laneBlocked = naturalNeedsClearLane && target != null && IsLaneBlockedFrom(transform.position);

        if (repositionEnabled) Reposition();

        if (!naturalAttackEnabled || stateTimer > 0f || naturalTimer > 0f || target == null || laneBlocked) return;
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

        OnWindupTick(1f - Mathf.Clamp01(stateTimer / windupTime));

        if (stateTimer <= 0f) EnterState(State.Attack, 0f);
    }

    protected void EnterState(State next, float duration)
    {
        Current = next;
        stateTimer = duration;
        if (next != State.Idle) laneBlocked = false;

        // The taunted attack is over once we leave it, unless another taunt is queued.
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
            case State.Attack: SetBodyColor(attackColor); break;
            case State.Recover: SetBodyColor(recoverColor); break;
            case State.Stunned: SetBodyColor(stunColor); break;
        }

        OnEnterState(next);
    }

    // ---- Repositioning ----------------------------------------------------

    bool IsLaneBlockedFrom(Vector3 origin)
    {
        Vector3 to = Flat(target.position - origin);
        float dist = to.magnitude;
        if (dist < 0.001f) return false;
        // Other enemies don't count: hitting them is the point.
        return CastObstacle(origin, to / dist, dist, LaneCheckRadius, out _);
    }

    /// <summary>Close in when the target is far; otherwise hold (optionally retreat/circle), staying apart from other enemies.</summary>
    void Reposition()
    {
        if (target == null) return;

        Vector3 to = Flat(target.position - transform.position);
        float dist = to.magnitude;
        if (dist < 0.001f) return;
        Vector3 toDir = to / dist;

        float radial = dist > keepDistanceMax ? 1f : (retreatWhenClose && dist < keepDistanceMin) ? -1f : 0f;

        // Holding against a wall reads as "stuck": walk straight toward the target (keeps lined-up shots lined up)
        // until clear of the wall, but never inside the min distance.
        if (stepOffWalls && radial == 0f)
        {
            float wallDist = NearestObstacleDistance(wallClearDistance);
            if (!steppingOffWall && wallDist < wallHugDistance) steppingOffWall = true;
            else if (steppingOffWall && wallDist >= wallClearDistance) steppingOffWall = false;
            if (steppingOffWall && dist > keepDistanceMin) radial = 1f;
        }
        else steppingOffWall = false;

        Vector3 move = Vector3.zero;
        Vector3 side = Vector3.Cross(Vector3.up, toDir);

        if (laneBlocked)
        {
            if (laneBlockedResponse == LaneBlockedResponse.Strafe && PickStrafeSide(side))
                move += side * strafeSign;
            else
                radial = 1f; // close in; the detour walks around the cover
        }
        else strafeSign = 0f;

        move += toDir * radial;
        if (orbitInRange)
            move += side * orbitSign * (radial == 0f ? 1f : 0.5f);

        foreach (var other in TauntRegistry.All)
        {
            if (ReferenceEquals(other, this) || other as Object == null) continue;
            Vector3 away = Flat(transform.position - other.transform.position);
            float d = away.magnitude;
            if (d < separationRadius && d > 0.001f)
                move += away / d * (1f - d / separationRadius) * 2f;
        }

        if (move.sqrMagnitude > 1f) move.Normalize();
        detourTimer -= Time.deltaTime;
        Vector3 before = transform.position;
        Vector3 pos = before + BlockedByObstacles(move * moveSpeed * Time.deltaTime);

        float limit = arenaHalfExtent - bodyRadius;
        if (Mathf.Abs(pos.x) > limit || Mathf.Abs(pos.z) > limit)
        {
            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.z = Mathf.Clamp(pos.z, -limit, limit);
            orbitSign = -orbitSign; // circle the other way instead of grinding along the wall
            strafeSign = 0f;        // re-pick the strafe side
        }
        transform.position = pos;

        // Keep facing the target so the next windup reads clearly.
        Quaternion want = Quaternion.LookRotation(toDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, idleTurnSpeed * Time.deltaTime);
    }

    const int StrafeProbeSteps = 3;

    /// <summary>
    /// Choose (once) the side that reaches a clear lane soonest, probing 1x..3x strafeProbeDistance out.
    /// False if neither side opens up (then we fall back to approaching).
    /// </summary>
    bool PickStrafeSide(Vector3 side)
    {
        if (strafeSign != 0f) return true;
        for (int step = 1; step <= StrafeProbeSteps; step++)
        {
            float d = strafeProbeDistance * step;
            foreach (float s in new[] { orbitSign, -orbitSign })
            {
                Vector3 dir = side * s;
                if (CastObstacle(transform.position, dir, d, bodyRadius, out _)) continue;
                if (IsLaneBlockedFrom(transform.position + dir * d)) continue;
                strafeSign = s;
                return true;
            }
        }
        return false;
    }

    // ---- Contact damage ---------------------------------------------------

    void ApplyContactDamage()
    {
        if (contactDamage <= 0f) return;
        foreach (var c in Physics.OverlapSphere(transform.position, contactRadius))
        {
            var victim = c.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.IsDead) continue;
            // Enemies don't hurt each other by touching; only attacks do.
            if (IsEnemy(victim)) continue;

            Vector3 dir = Flat(victim.transform.position - transform.position).normalized;
            victim.TakeDamage(new DamageInfo(contactDamage, gameObject, dir));
        }
    }

    // ---- Obstacles --------------------------------------------------------

    protected const float Skin = 0.02f;
    const float DetourHoldTime = 0.5f;
    float detourTimer;
    float detourSign = 1f;

    /// <summary>Flat distance to the nearest wall/obstacle within radius (floor and Health owners ignored).</summary>
    float NearestObstacleDistance(float radius)
    {
        float best = float.MaxValue;
        Vector3 pos = transform.position;
        foreach (var c in Physics.OverlapSphere(pos, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(transform) || c.GetComponentInParent<Health>() != null) continue;
            Vector3 cp = c.ClosestPoint(pos);
            if (cp.y < pos.y - 0.9f) continue; // the floor under us
            best = Mathf.Min(best, Flat(cp - pos).magnitude);
        }
        return best;
    }

    protected bool CastObstacle(Vector3 origin, Vector3 dir, float dist, out RaycastHit best) =>
        CastObstacle(origin, dir, dist, bodyRadius, out best);

    /// <summary>Nearest non-Health collider along the path (walls, blocks). Health owners are handled by hit detection.</summary>
    protected bool CastObstacle(Vector3 origin, Vector3 dir, float dist, float radius, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;
        var hits = Physics.SphereCastAll(origin, radius, dir, dist + Skin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.collider.GetComponentInParent<Health>() != null) continue;
            // Already overlapping at the start: ignore so we can move back out.
            if (h.distance <= 0f && h.point == Vector3.zero) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                best = h;
                found = true;
            }
        }
        return found;
    }

    /// <summary>Clip an idle move against obstacles, sliding along them when possible.</summary>
    Vector3 BlockedByObstacles(Vector3 delta)
    {
        float len = delta.magnitude;
        if (len < 0.0001f) return delta;
        Vector3 dir = delta / len;
        if (!CastObstacle(transform.position, dir, len, out RaycastHit hit)) return delta;

        Vector3 allowed = dir * Mathf.Max(0f, hit.distance - Skin);
        Vector3 normal = Flat(hit.normal).normalized;
        Vector3 along = Vector3.Cross(Vector3.up, normal);

        // Pick a side once and keep it while blocked, so we walk around the obstacle instead of jittering in front of it.
        if (detourTimer <= 0f)
        {
            float s = Vector3.Dot(delta, along);
            detourSign = Mathf.Abs(s) > len * 0.1f ? Mathf.Sign(s) : orbitSign;
        }
        detourTimer = DetourHoldTime;

        Vector3 slide = along * detourSign * len;
        if (!CastObstacle(transform.position + allowed, slide.normalized, slide.magnitude, out _)) allowed += slide;
        else
        {
            detourSign = -detourSign; // cornered: try the other side
            strafeSign = 0f;
        }
        return allowed;
    }

    // ---- Helpers ----------------------------------------------------------

    protected static bool IsEnemy(Health h) => h.GetComponent<ITauntable>() != null;

    protected float DamageFor(Health victim) => IsEnemy(victim) ? damageToEnemy : damageToPlayer;

    void SetBodyColor(Color c) => BodyVisual.SetColor(bodyMats, "_BaseColor", c);

    protected static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    float FlatDistanceTo(Vector3 p) => Flat(p - transform.position).magnitude;

#if UNITY_EDITOR
    protected virtual void OnDrawGizmos()
    {
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.8f, StateLabel);
    }
#endif
}
