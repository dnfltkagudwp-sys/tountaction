using UnityEngine;

/// <summary>
/// Plays the model's clips from gameplay events. The controller holds the states Idle (loop) and
/// Attack / Hit / Taunt (each returns to Idle). Movement itself stays on ProceduralMotion.
/// Enemy: the attack clip starts with the windup and is sped up so its release lands when the attack fires.
/// </summary>
public class CharacterAnimator : MonoBehaviour
{
    public const string IdleState = "Idle";
    public const string AttackState = "Attack";
    public const string HitState = "Hit";
    public const string TauntState = "Taunt";
    public const string SpeedParam = "ActionSpeed";

    [Tooltip("Where in the attack clip (0..1) the shot/throw is released.")]
    [SerializeField, Range(0f, 1f)] float attackRelease = 0.6f;
    [SerializeField] float tauntSpeed = 1.5f;
    [SerializeField] float crossFade = 0.08f;

    Animator animator;
    EnemyBase enemy;
    Health health;
    PlayerTaunt taunt;
    EnemyBase.State lastState;
    float attackLength;

    void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) { enabled = false; return; }

        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name.EndsWith("_" + AttackState)) attackLength = clip.length;

        enemy = GetComponent<EnemyBase>();
        health = GetComponent<Health>();
        taunt = GetComponent<PlayerTaunt>();
        if (health != null) { health.Damaged += OnDamaged; health.Died += OnDied; }
        if (taunt != null) taunt.Taunted += OnTaunted;
    }

    void OnDestroy()
    {
        if (health != null) { health.Damaged -= OnDamaged; health.Died -= OnDied; }
        if (taunt != null) taunt.Taunted -= OnTaunted;
    }

    void Update()
    {
        if (enemy == null) return;
        var state = enemy.Current;
        if (state == lastState) return;
        if (state == EnemyBase.State.Windup && attackLength > 0f)
        {
            float release = attackLength * attackRelease;
            Play(AttackState, release / Mathf.Max(0.05f, enemy.WindupDuration));
        }
        lastState = state;
    }

    void OnDamaged(DamageInfo _)
    {
        // Don't cut an enemy's attack telegraph short; the squash in ProceduralMotion still shows the hit.
        if (enemy != null && (enemy.Current == EnemyBase.State.Windup || enemy.Current == EnemyBase.State.Attack)) return;
        Play(HitState, 1.3f);
    }

    void OnTaunted() => Play(TauntState, tauntSpeed);

    void OnDied(Health _) => animator.speed = 0f; // freeze the last pose

    void Play(string state, float speed)
    {
        animator.SetFloat(SpeedParam, speed);
        animator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
    }
}
