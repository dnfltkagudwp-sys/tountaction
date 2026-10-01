using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class ChargerEnemy : MonoBehaviour, ITauntable
{
    public enum State { Idle, Windup, Charge, Recover, Stunned }

    [Header("Targeting")]
    [SerializeField] Transform target;

    [Header("Natural attack (pressure)")]
    [Tooltip("Attack on its own every few seconds without a taunt. Turn OFF to verify the pure taunt loop.")]
    [SerializeField] bool naturalAttackEnabled = true;
    [Tooltip("Wait after returning to Idle. The full cycle also includes windup + charge + recovery (~2.4s).")]
    [SerializeField] float naturalIntervalMin = 1.5f;
    [SerializeField] float naturalIntervalMax = 3f;
    [Tooltip("The first natural attack is ready this soon after spawning (random in range), instead of a full interval.")]
    [SerializeField] float firstAttackDelayMin = 0.5f;
    [SerializeField] float firstAttackDelayMax = 1.5f;
    [Tooltip("Natural attacks only start when the target is within this range.")]
    [SerializeField] float aggroRange = 14f;
    [Tooltip("Skip natural attacks while a wall/obstacle blocks the lane to the target; close in instead. Taunts ignore this.")]
    [SerializeField] bool naturalNeedsClearLane = true;

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
    [Tooltip("Touching this enemy outside a charge hurts the player (not other enemies).")]
    [SerializeField] float contactDamage = 1f;
    [SerializeField] float contactRadius = 1.0f;

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
    [UnityEngine.Serialization.FormerlySerializedAs("chargeDamage")]
    [SerializeField] float damageToPlayer = 1f;
    [Tooltip("Charge damage to other enemies. Higher than wall self-damage so enemy-on-enemy stays the best play.")]
    [SerializeField] float damageToEnemy = 2f;
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

    float orbitSign = 1f;
    bool laneBlocked;

    /// <summary>True while Idle and a wall/obstacle sits between this enemy and its target.</summary>
    public bool IsLaneBlocked => laneBlocked;

    public bool CanBeTaunted => enabled && !health.IsDead;
    public bool IsTaunted => tauntActive;
    /// <summary>Seconds until the next natural attack may start; negative when off or not idle.</summary>
    public float NaturalTimeRemaining => naturalAttackEnabled && Current == State.Idle ? Mathf.Max(0f, naturalTimer) : -1f;

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
        orbitSign = Random.value < 0.5f ? -1f : 1f;
        // A taunt can arrive before Start; don't overwrite it.
        if (!tauntActive) EnterState(State.Idle, idleCooldown);
        // Start ready to attack: in range = attacking soon, no long opening stare.
        naturalTimer = Random.Range(firstAttackDelayMin, firstAttackDelayMax);
    }

    void Update()
    {
        stateTimer -= Time.deltaTime;

        if (Current != State.Charge) ApplyContactDamage();

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

        laneBlocked = naturalNeedsClearLane && target != null && IsLaneToTargetBlocked();

        if (repositionEnabled) Reposition();

        if (!naturalAttackEnabled || stateTimer > 0f || naturalTimer > 0f || target == null || laneBlocked) return;
        if (FlatDistanceTo(target.position) <= aggroRange)
            EnterState(State.Windup, windupTime);
    }

    bool IsLaneToTargetBlocked()
    {
        Vector3 to = Flat(target.position - transform.position);
        float dist = to.magnitude;
        if (dist < 0.001f) return false;
        // Other enemies don't count: charging into them is the point.
        return CastObstacle(transform.position, to / dist, dist, out _);
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

        // Target hidden behind cover: close in (the detour walks around the obstacle) until the lane opens.
        if (laneBlocked) radial = 1f;

        Vector3 move = toDir * radial;
        if (orbitInRange)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, toDir) * orbitSign;
            move += tangent * (radial == 0f ? 1f : 0.5f);
        }

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
        Vector3 pos = transform.position + BlockedByObstacles(move * moveSpeed * Time.deltaTime);

        float limit = arenaHalfExtent - bodyRadius;
        if (Mathf.Abs(pos.x) > limit || Mathf.Abs(pos.z) > limit)
        {
            pos.x = Mathf.Clamp(pos.x, -limit, limit);
            pos.z = Mathf.Clamp(pos.z, -limit, limit);
            orbitSign = -orbitSign; // circle the other way instead of grinding along the wall
        }
        transform.position = pos;

        // Keep facing the target so the next windup reads clearly.
        Quaternion want = Quaternion.LookRotation(toDir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, idleTurnSpeed * Time.deltaTime);
    }

    // ---- Contact damage ---------------------------------------------------

    void ApplyContactDamage()
    {
        if (contactDamage <= 0f) return;
        var cols = Physics.OverlapSphere(transform.position, contactRadius);
        foreach (var c in cols)
        {
            var victim = c.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.IsDead) continue;
            // Enemies don't hurt each other by touching; only charges do.
            if (victim.GetComponent<ITauntable>() != null) continue;

            Vector3 dir = Flat(victim.transform.position - transform.position).normalized;
            victim.TakeDamage(new DamageInfo(contactDamage, gameObject, dir));
        }
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
        bool hitWall = false;
        ImpactSurface surface = null;

        // Walls and obstacles are real colliders; stop just short of the first one in the path.
        if (CastObstacle(transform.position, chargeDir, step, out RaycastHit hit))
        {
            step = Mathf.Max(0f, hit.distance - Skin);
            hitWall = true;
            surface = hit.collider.GetComponentInParent<ImpactSurface>();
        }
        Vector3 pos = transform.position + chargeDir * step;

        // Safety net in case the arena edge has no collider.
        float limit = arenaHalfExtent - bodyRadius;
        if (Mathf.Abs(pos.x) > limit) { pos.x = Mathf.Clamp(pos.x, -limit, limit); hitWall = true; }
        if (Mathf.Abs(pos.z) > limit) { pos.z = Mathf.Clamp(pos.z, -limit, limit); hitWall = true; }

        traveled += (pos - transform.position).magnitude;
        transform.position = pos;

        bool hitEnemy = DetectHits();

        if (hitEnemy && stopOnEnemyHit) EnterState(State.Stunned, enemyHitStunTime);
        else if (hitWall) HitWall(surface);
        else if (traveled >= chargeDistance) EnterState(State.Recover, recoverTime);
    }

    void HitWall(ImpactSurface surface)
    {
        float stun = surface != null && surface.StunTimeOverride >= 0f ? surface.StunTimeOverride : wallStunTime;
        // Enter the stun first: dying from the self-damage must win over the stun color.
        EnterState(State.Stunned, stun);
        if (surface != null && surface.ChargeSelfDamage > 0f)
            health.TakeDamage(new DamageInfo(surface.ChargeSelfDamage, surface.gameObject, -chargeDir));
    }

    // ---- Obstacles --------------------------------------------------------

    const float Skin = 0.02f;
    const float DetourHoldTime = 0.5f;
    float detourTimer;
    float detourSign = 1f;
    bool steppingOffWall;

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

    /// <summary>Nearest non-Health collider along the path (walls, blocks). Health owners are handled by hit detection.</summary>
    bool CastObstacle(Vector3 origin, Vector3 dir, float dist, out RaycastHit best)
    {
        best = default;
        bool found = false;
        float bestDist = float.MaxValue;
        var hits = Physics.SphereCastAll(origin, bodyRadius, dir, dist + Skin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
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
            float side = Vector3.Dot(delta, along);
            detourSign = Mathf.Abs(side) > len * 0.1f ? Mathf.Sign(side) : orbitSign;
        }
        detourTimer = DetourHoldTime;

        Vector3 slide = along * detourSign * len;
        if (!CastObstacle(transform.position + allowed, slide.normalized, slide.magnitude, out _)) allowed += slide;
        else detourSign = -detourSign; // cornered: try the other side
        return allowed;
    }

    void EnterState(State next, float duration)
    {
        Current = next;
        stateTimer = duration;
        if (next != State.Idle) laneBlocked = false;

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

            bool isEnemy = victim.GetComponent<ITauntable>() != null;
            victim.TakeDamage(new DamageInfo(isEnemy ? damageToEnemy : damageToPlayer, gameObject, chargeDir));
            if (isEnemy) hitEnemy = true;
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
