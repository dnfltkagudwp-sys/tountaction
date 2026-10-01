using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Aims (laser telegraph), locks, fires one straight bullet that can hit the player or other enemies.</summary>
public class ShooterEnemy : EnemyBase
{
    [Header("Shot")]
    [SerializeField] float projectileSpeed = 16f;
    [SerializeField] float projectileRadius = 0.25f;
    [SerializeField] float projectileMaxDistance = 40f;
    [SerializeField] Color projectileColor = new Color(0.6f, 1f, 1f);

    [Header("Telegraph")]
    [SerializeField] float laserMaxLength = 40f;
    [SerializeField] Color laserStartColor = new Color(0.1f, 0.35f, 0.35f);
    [SerializeField] Color laserEndColor = new Color(0.3f, 1f, 1f);

    LineRenderer laser;
    Material projectileMat;

    public override string StateLabel => Current == State.Attack ? "Fire" : Current.ToString();

    // Bullets are thin, so cover only blocks the lane if it blocks the bullet.
    protected override float LaneCheckRadius => projectileRadius;

    protected override void Awake()
    {
        base.Awake();
        BuildLaser();
        projectileMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        projectileMat.SetColor("_BaseColor", projectileColor);
    }

    protected override void OnDiedExtra() => laser.gameObject.SetActive(false);

    protected override void OnEnterState(State next) => laser.gameObject.SetActive(next == State.Windup);

    protected override void OnWindupTick(float progress)
    {
        laser.material.SetColor("_BaseColor", Color.Lerp(laserStartColor, laserEndColor, progress));
        laser.widthMultiplier = Mathf.Lerp(0.03f, 0.1f, progress);

        // Show exactly where the bullet would stop.
        Vector3 origin = Muzzle();
        Vector3 dir = Flat(transform.forward).normalized;
        float len = laserMaxLength;
        if (CastObstacle(origin, dir, laserMaxLength, projectileRadius, out RaycastHit hit)) len = hit.distance;
        laser.SetPosition(0, origin);
        laser.SetPosition(1, origin + dir * len);
    }

    protected override void UpdateAttack()
    {
        Fire();
        EnterState(State.Recover, recoverTime);
    }

    void Fire()
    {
        Vector3 dir = Flat(transform.forward).normalized;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bullet";
        DestroyImmediate(go.GetComponent<Collider>()); // must not register as an obstacle
        go.transform.position = Muzzle();
        go.transform.localScale = Vector3.one * projectileRadius * 2f;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = projectileMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<Projectile>().Init(transform, dir, projectileSpeed, projectileRadius, projectileMaxDistance, damageToPlayer, damageToEnemy);
    }

    Vector3 Muzzle() => transform.position + Flat(transform.forward).normalized * (bodyRadius + projectileRadius + 0.05f);

    void BuildLaser()
    {
        var go = new GameObject("AimLaser");
        go.transform.SetParent(transform, false);
        laser = go.AddComponent<LineRenderer>();
        laser.useWorldSpace = true;
        laser.positionCount = 2;
        laser.shadowCastingMode = ShadowCastingMode.Off;
        laser.receiveShadows = false;
        laser.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        go.SetActive(false);
    }
}
