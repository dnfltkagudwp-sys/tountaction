using UnityEngine;

/// <summary>
/// Straight-line bullet with explicit sweep hit detection (no rigidbody).
/// Hits anything with Health except its owner; dodge i-frames let it pass through.
/// Walls/obstacles stop it, unless an ImpactSurface reflects it: a reflected bullet
/// bounces once, can hit its own shooter, and only hurts enemies.
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] Color reflectedColor = new Color(1f, 0.6f, 1f);

    const float SurfaceOffset = 0.05f;

    Transform owner;
    Vector3 dir;
    float speed;
    float radius;
    float maxDistance;
    float damageToPlayer;
    float damageToEnemy;
    float traveled;
    bool reflected;
    // Set for the first step after a bounce, when we start right next to the mirror.
    bool justReflected;

    public bool IsReflected => reflected;

    public void Init(Transform owner, Vector3 dir, float speed, float radius, float maxDistance, float damageToPlayer, float damageToEnemy)
    {
        this.owner = owner;
        this.dir = dir.normalized;
        this.speed = speed;
        this.radius = radius;
        this.maxDistance = maxDistance;
        this.damageToPlayer = damageToPlayer;
        this.damageToEnemy = damageToEnemy;
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        // Enemy bodies are triggers, so triggers must be included here.
        var hits = Physics.SphereCastAll(transform.position, radius, dir, step, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool skipStartOverlap = justReflected;
        justReflected = false;

        foreach (var h in hits)
        {
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue;

            var victim = h.collider.GetComponentInParent<Health>();
            // A trigger without Health (sensor volume etc.) is not a wall.
            if (victim == null && h.collider.isTrigger) continue;

            if (victim != null)
            {
                // Dead bodies and dodging players don't stop the bullet.
                if (victim.IsDead || victim.Invulnerable) continue;
                bool isEnemy = victim.GetComponent<ITauntable>() != null;
                // A reflected bullet belongs to the player now.
                if (reflected && !isEnemy) continue;

                float dmg = isEnemy ? damageToEnemy : damageToPlayer;
                victim.TakeDamage(new DamageInfo(dmg, owner != null ? owner.gameObject : gameObject, dir));
                Destroy(gameObject);
                return;
            }

            // Starting overlapped with a surface: only expected right after a bounce (we sit just off the
            // mirror). Anywhere else it means we began inside a wall, so the bullet dies there.
            if (h.distance <= 0f && h.point == Vector3.zero)
            {
                if (skipStartOverlap) continue;
                Destroy(gameObject);
                return;
            }

            var surface = h.collider.GetComponentInParent<ImpactSurface>();
            if (!reflected && surface != null && surface.ReflectProjectiles)
            {
                Reflect(h, surface);
                return;
            }

            // Wall or obstacle.
            Destroy(gameObject);
            return;
        }

        transform.position += dir * step;
        traveled += step;
        if (traveled >= maxDistance) Destroy(gameObject);
    }

    void Reflect(RaycastHit hit, ImpactSurface surface)
    {
        Vector3 normal = hit.normal; normal.y = 0f; normal.Normalize();
        traveled += hit.distance;
        transform.position = transform.position + dir * hit.distance + normal * SurfaceOffset;
        dir = Vector3.Reflect(dir, normal);
        dir.y = 0f; dir.Normalize();

        reflected = true;
        justReflected = true;
        owner = null; // the shooter can now be hit by its own bullet
        damageToEnemy = surface.ReflectedDamage;
        damageToPlayer = 0f;

        var r = GetComponent<Renderer>();
        if (r != null) r.material.SetColor("_BaseColor", reflectedColor);
    }
}
