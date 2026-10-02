using UnityEngine;

/// <summary>
/// Code-driven body motion on the "Visual" child: hop/lean while moving, breathing at rest,
/// crouch + shake on windup, lean on attack, wobble when stunned, squash on hit, stretch on dash.
/// Reads EnemyBase / PlayerMotor / Health when present. Bones under the model stay free for an Animator.
/// </summary>
public class ProceduralMotion : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] float hopHeight = 0.12f;
    [Tooltip("Hops per meter traveled.")]
    [SerializeField] float hopsPerMeter = 0.55f;
    [SerializeField] float leanPerSpeed = 1.5f;
    [SerializeField] float maxLean = 14f;
    [SerializeField] float swayAngle = 4f;

    [Header("Rest")]
    [SerializeField] float breatheAmount = 0.025f;
    [SerializeField] float breatheSpeed = 2.2f;

    [Header("Enemy states")]
    [SerializeField] float windupCrouch = 0.12f;
    [SerializeField] float windupShake = 0.06f;
    [SerializeField] float attackLean = 18f;
    [SerializeField] float stunWobble = 10f;

    [Header("Hit / dash")]
    [SerializeField] float hitSquash = 0.2f;
    [SerializeField] float hitTime = 0.18f;
    [SerializeField] float dashLean = 22f;
    [SerializeField] float dashStretch = 0.15f;

    [Header("Taunt (player)")]
    [Tooltip("One full twirl when a taunt lands; reads from above where an arm gesture doesn't.")]
    [SerializeField] float spinTime = 0.4f;
    [Tooltip("The skirt flares out wider during the twirl.")]
    [SerializeField] float spinFlare = 0.18f;

    Transform visual;
    Vector3 restPos;
    Quaternion restRot;
    Vector3 restScale;
    float footY; // pivot for leaning, in the root's local space

    EnemyBase enemy;
    PlayerMotor player;
    Health health;

    Vector3 lastPos;
    float speed;
    float hopPhase;
    float lean;
    float hitTimer;
    float spinTimer;
    PlayerTaunt taunt;

    void Awake()
    {
        visual = transform.Find(BodyVisual.ChildName);
        if (visual == null) { enabled = false; return; }
        restPos = visual.localPosition;
        restRot = visual.localRotation;
        restScale = visual.localScale;

        float minY = float.MaxValue;
        foreach (var r in BodyVisual.Find(transform)) minY = Mathf.Min(minY, r.bounds.min.y);
        footY = minY < float.MaxValue ? transform.InverseTransformPoint(new Vector3(0f, minY, 0f)).y : restPos.y;

        enemy = GetComponent<EnemyBase>();
        player = GetComponent<PlayerMotor>();
        health = GetComponent<Health>();
        if (health != null) health.Damaged += OnDamaged;
        taunt = GetComponent<PlayerTaunt>();
        if (taunt != null) taunt.Taunted += Spin;
        lastPos = transform.position;
    }

    void OnDestroy()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (taunt != null) taunt.Taunted -= Spin;
    }

    void OnDamaged(DamageInfo _) => hitTimer = hitTime;

    /// <summary>One quick full turn of the body (taunt).</summary>
    public void Spin() => spinTimer = spinTime;

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        Vector3 delta = transform.position - lastPos; delta.y = 0f;
        lastPos = transform.position;
        float rawSpeed = delta.magnitude / dt;
        speed = Mathf.Lerp(speed, rawSpeed, 1f - Mathf.Exp(-12f * dt));
        float t = Time.time;

        bool dead = health != null && health.IsDead;
        bool dashing = player != null && player.IsDashing;
        var state = enemy != null ? enemy.Current : EnemyBase.State.Idle;

        Vector3 pos = Vector3.zero;   // local offset
        float pitch = 0f, roll = 0f;  // degrees, around the feet
        Vector3 scale = Vector3.one;

        if (!dead)
        {
            // Moving: hop + lean into the move. Charging gets a fixed lean instead of hops.
            float moveBlend = Mathf.Clamp01(speed / 2f);
            if (state == EnemyBase.State.Attack && enemy != null)
            {
                pitch += attackLean;
            }
            else if (!dashing && moveBlend > 0.01f)
            {
                hopPhase += rawSpeed * dt * hopsPerMeter * Mathf.PI;
                pos.y += Mathf.Abs(Mathf.Sin(hopPhase)) * hopHeight * moveBlend;
                roll += Mathf.Sin(hopPhase) * swayAngle * moveBlend;
            }
            else hopPhase = 0f;

            float wantLean = dashing ? dashLean : Mathf.Min(maxLean, speed * leanPerSpeed);
            lean = Mathf.Lerp(lean, wantLean, 1f - Mathf.Exp(-14f * dt));
            pitch += lean;

            // Rest: breathe.
            float breathe = Mathf.Sin(t * breatheSpeed) * breatheAmount * (1f - moveBlend);
            scale.y += breathe;
            scale.x -= breathe * 0.5f; scale.z -= breathe * 0.5f;

            switch (state)
            {
                case EnemyBase.State.Windup:
                    float p = enemy.WindupProgress;
                    scale.y -= windupCrouch * p;
                    scale.x += windupCrouch * 0.5f * p; scale.z += windupCrouch * 0.5f * p;
                    pos.x += (Mathf.PerlinNoise(t * 35f, 0f) - 0.5f) * 2f * windupShake * p;
                    pos.z += (Mathf.PerlinNoise(0f, t * 35f) - 0.5f) * 2f * windupShake * p;
                    break;
                case EnemyBase.State.Stunned:
                    roll += Mathf.Sin(t * 14f) * stunWobble;
                    break;
            }

            if (dashing)
            {
                scale.z += dashStretch; scale.y -= dashStretch * 0.5f; scale.x -= dashStretch * 0.5f;
            }
        }

        if (hitTimer > 0f)
        {
            hitTimer -= dt;
            float h = Mathf.Sin(Mathf.Clamp01(hitTimer / hitTime) * Mathf.PI) * hitSquash;
            scale.y -= h; scale.x += h * 0.5f; scale.z += h * 0.5f;
        }

        float yaw = 0f;
        if (spinTimer > 0f)
        {
            spinTimer -= dt;
            float s = 1f - Mathf.Clamp01(spinTimer / spinTime);
            yaw = (1f - (1f - s) * (1f - s)) * 360f; // ease-out: fast start, settles facing forward
            float flare = Mathf.Sin(s * Mathf.PI) * spinFlare;
            scale.x += flare; scale.z += flare;
        }

        // Lean around the feet so the body tips over instead of sliding through the floor.
        Quaternion tilt = Quaternion.Euler(pitch, yaw, -roll);
        Vector3 pivot = new Vector3(restPos.x, footY, restPos.z);
        visual.localPosition = pivot + tilt * (restPos - pivot) + pos;
        visual.localRotation = tilt * restRot;
        visual.localScale = Vector3.Scale(restScale, scale);
    }
}
