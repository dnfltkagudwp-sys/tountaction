using System.Collections.Generic;
using UnityEngine;

/// <summary>Charges in a straight line. Hits anything with Health on the way; stops on enemies and walls.</summary>
public class ChargerEnemy : EnemyBase
{
    [Header("Charge")]
    [SerializeField] float chargeSpeed = 18f;
    [SerializeField] float chargeDistance = 12f;
    [SerializeField] float hitRadius = 0.9f;
    [Tooltip("Hitting another enemy ends the charge on the spot (like a wall) instead of passing through.")]
    [SerializeField] bool stopOnEnemyHit = true;
    [SerializeField] float enemyHitStunTime = 1.0f;
    [SerializeField] float wallStunTime = 1.2f;

    float traveled;
    Vector3 chargeDir;
    Transform lane;
    Material laneMat;
    readonly HashSet<Object> hitThisCharge = new HashSet<Object>();

    public override string StateLabel => Current == State.Attack ? "Charge" : Current.ToString();

    protected override void Awake()
    {
        base.Awake();
        BuildLane();
    }

    protected override void OnDiedExtra() => lane.gameObject.SetActive(false);

    protected override void OnEnterState(State next)
    {
        lane.gameObject.SetActive(next == State.Windup);
        if (next == State.Attack)
        {
            chargeDir = Flat(transform.forward).normalized;
            traveled = 0f;
            hitThisCharge.Clear();
        }
    }

    protected override void OnWindupTick(float progress) =>
        laneMat.SetColor("_BaseColor", Color.Lerp(new Color(0.35f, 0.1f, 0.1f), new Color(1f, 0.1f, 0.05f), progress));

    protected override void UpdateAttack()
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

    // ---- Hit detection (explicit, no rigidbody physics) -------------------

    /// <summary>Returns true if another enemy (anything tauntable) was hit this frame.</summary>
    bool DetectHits()
    {
        bool hitEnemy = false;
        foreach (var c in Physics.OverlapSphere(transform.position, hitRadius))
        {
            var victim = c.GetComponentInParent<Health>();
            if (victim == null || victim == health || victim.IsDead) continue;
            if (!hitThisCharge.Add(victim)) continue;

            victim.TakeDamage(new DamageInfo(DamageFor(victim), gameObject, chargeDir));
            if (IsEnemy(victim)) hitEnemy = true;
        }
        return hitEnemy;
    }

    // ---- Visuals ----------------------------------------------------------

    void BuildLane()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "ChargeLane";
        DestroyImmediate(go.GetComponent<Collider>());
        lane = go.transform;
        lane.SetParent(transform, false);
        lane.localPosition = new Vector3(0f, -0.97f, chargeDistance * 0.5f);
        lane.localScale = new Vector3(hitRadius * 2f, 0.02f, chargeDistance);

        laneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        go.GetComponent<Renderer>().sharedMaterial = laneMat;
        go.SetActive(false);
    }

    void OnValidate()
    {
        if (lane != null)
        {
            lane.localPosition = new Vector3(0f, -0.97f, chargeDistance * 0.5f);
            lane.localScale = new Vector3(hitRadius * 2f, 0.02f, chargeDistance);
        }
    }

#if UNITY_EDITOR
    protected override void OnDrawGizmos()
    {
        base.OnDrawGizmos();
        if (Current == State.Attack)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, hitRadius);
        }
    }
#endif
}
