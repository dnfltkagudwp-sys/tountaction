using System;
using UnityEngine;

/// <summary>
/// Shared health for Player and Enemies. Damage is applied explicitly by attackers
/// (no rigidbody/physics callbacks), so anything can hurt anything.
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] float maxHealth = 3f;

    [Header("Hit invulnerability")]
    [Tooltip("After taking damage, ignore all damage for this long (0 = off). Player uses this; enemies usually don't.")]
    [SerializeField] float hitInvulnerableTime = 0f;
    [SerializeField] float blinkInterval = 0.08f;

    [Header("Death")]
    [SerializeField] bool destroyOnDeath = true;
    [SerializeField] float destroyDelay = 0.4f;

    [Header("Debug")]
    [SerializeField] bool showLabel = true;
    [SerializeField] Vector3 labelOffset = new Vector3(0f, 2.2f, 0f);

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>When true, incoming damage is ignored (dodge i-frames).</summary>
    public bool Invulnerable { get; set; }

    /// <summary>True during the post-hit invulnerability window.</summary>
    public bool IsHitInvulnerable => hitInvulnerableTimer > 0f;

    public event Action<DamageInfo> Damaged;
    public event Action<Health> Died;

    float hitInvulnerableTimer;
    Renderer bodyRenderer;

    void Awake()
    {
        Current = maxHealth;
        bodyRenderer = GetComponent<Renderer>();
    }

    void Update()
    {
        if (hitInvulnerableTimer <= 0f) return;

        hitInvulnerableTimer -= Time.deltaTime;
        if (bodyRenderer == null) return;
        bodyRenderer.enabled = hitInvulnerableTimer <= 0f || Mathf.Repeat(hitInvulnerableTimer, blinkInterval * 2f) > blinkInterval;
    }

    /// <summary>Back to full HP (e.g. entering a new room). Does not revive the dead.</summary>
    public void ResetHealth()
    {
        if (IsDead) return;
        Current = maxHealth;
        hitInvulnerableTimer = 0f;
        if (bodyRenderer != null) bodyRenderer.enabled = true;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || Invulnerable || IsHitInvulnerable || info.Amount <= 0f) return;

        Current = Mathf.Max(0f, Current - info.Amount);
        Damaged?.Invoke(info);
        Debug.Log($"[{name}] took {info.Amount} from {(info.Source != null ? info.Source.name : "?")} -> {Current}/{maxHealth}");

        if (Current <= 0f) Die();
        else if (hitInvulnerableTime > 0f) hitInvulnerableTimer = hitInvulnerableTime;
    }

    void Die()
    {
        IsDead = true;
        hitInvulnerableTimer = 0f;
        if (bodyRenderer != null) bodyRenderer.enabled = true;
        Debug.Log($"[{name}] died");
        Died?.Invoke(this);
        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }

    void OnGUI()
    {
        if (!showLabel || IsDead) return;
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 sp = cam.WorldToScreenPoint(transform.position + labelOffset);
        if (sp.z < 0f) return;

        var rect = new Rect(sp.x - 30f, Screen.height - sp.y - 10f, 60f, 20f);
        var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUI.Label(rect, $"{Current:0.#}/{maxHealth:0.#}", style);
    }
}
