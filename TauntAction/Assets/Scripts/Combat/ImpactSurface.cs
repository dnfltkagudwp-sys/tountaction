using UnityEngine;

/// <summary>
/// Environment piece that reacts when an enemy attack runs into it.
/// Plain colliders without this act as normal walls (stop + stun, no damage).
/// Shooter projectiles (reflect / explode) hook in here later.
/// </summary>
public class ImpactSurface : MonoBehaviour
{
    [Header("Charge impact")]
    [Tooltip("Damage the charging enemy deals to itself on impact. Keep below an enemy-on-enemy hit.")]
    [SerializeField] float chargeSelfDamage = 1f;
    [Tooltip("Stun duration override for the attacker. Negative = use the attacker's own wall stun.")]
    [SerializeField] float stunTimeOverride = -1f;

    [Header("Projectile impact")]
    [Tooltip("Bounce enemy bullets off this surface (once). A reflected bullet only hurts enemies.")]
    [SerializeField] bool reflectProjectiles = false;
    [Tooltip("Damage a reflected bullet deals to enemies. Keep below an enemy-on-enemy hit.")]
    [SerializeField] float reflectedDamage = 1f;

    public float ChargeSelfDamage => chargeSelfDamage;
    public float StunTimeOverride => stunTimeOverride;
    public bool ReflectProjectiles => reflectProjectiles;
    public float ReflectedDamage => reflectedDamage;
}
