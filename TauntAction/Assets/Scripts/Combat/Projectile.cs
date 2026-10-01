using UnityEngine;

/// <summary>
/// Straight-line bullet with explicit sweep hit detection (no rigidbody).
/// Hits anything with Health except its owner; dodge i-frames let it pass through.
/// Walls/obstacles stop it (ImpactSurface reactions such as reflect/explode plug in here later).
/// </summary>
public class Projectile : MonoBehaviour
{
    Transform owner;
    Vector3 dir;
    float speed;
    float radius;
    float maxDistance;
    float damageToPlayer;
    float damageToEnemy;
    float traveled;

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
                victim.TakeDamage(new DamageInfo(isEnemy ? damageToEnemy : damageToPlayer, owner != null ? owner.gameObject : gameObject, dir));
                Destroy(gameObject);
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
}
