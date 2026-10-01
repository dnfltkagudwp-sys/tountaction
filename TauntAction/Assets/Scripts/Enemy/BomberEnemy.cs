using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marks the target's spot (ring follows during windup, locks just before the throw), lobs a bomb there,
/// and the blast hits everyone inside, enemies and itself included. Lobbed over cover.
/// </summary>
public class BomberEnemy : EnemyBase
{
    [Header("Bomb")]
    [Tooltip("Farther targets get the bomb at this distance along the same direction.")]
    [SerializeField] float throwRange = 12f;
    [SerializeField] float flightTime = 0.9f;
    [SerializeField] float arcHeight = 4f;
    [SerializeField] float blastRadius = 2.5f;

    [Header("Telegraph")]
    [SerializeField] Color aimMarkerColor = new Color(0.5f, 0.25f, 0.6f);
    [SerializeField] Color bombMarkerStartColor = new Color(0.7f, 0.3f, 0.9f);
    [SerializeField] Color bombMarkerEndColor = new Color(1f, 0.5f, 1f);
    [SerializeField] Color bombColor = new Color(0.25f, 0.1f, 0.3f);

    const float FloorY = 0.06f;

    LineRenderer aimMarker;
    Material bombMat;
    Vector3 aimPoint;

    public override string StateLabel => Current == State.Attack ? "Throw" : Current.ToString();

    protected override void Awake()
    {
        base.Awake();
        aimMarker = RingVisual.Create("BomberAimMarker", null, blastRadius, 0.05f, aimMarkerColor);
        aimMarker.gameObject.SetActive(false);
        bombMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        bombMat.SetColor("_BaseColor", bombColor);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (aimMarker != null) Destroy(aimMarker.gameObject);
    }

    protected override void OnDiedExtra() => aimMarker.gameObject.SetActive(false);

    protected override void OnEnterState(State next)
    {
        aimMarker.gameObject.SetActive(next == State.Windup);
        if (next == State.Windup) UpdateAimPoint();
    }

    protected override void OnWindupTick(float progress)
    {
        // Follow the target until the lock, then the ring stays put: that's where the bomb lands.
        if (stateTimer > AimLockTime) UpdateAimPoint();
        aimMarker.transform.position = new Vector3(aimPoint.x, FloorY, aimPoint.z);
    }

    protected override void UpdateAttack()
    {
        Throw();
        EnterState(State.Recover, recoverTime);
    }

    void UpdateAimPoint()
    {
        if (target == null) return;
        Vector3 to = Flat(target.position - transform.position);
        if (to.magnitude > throwRange) to = to.normalized * throwRange;
        aimPoint = transform.position + to;
    }

    void Throw()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Bomb";
        DestroyImmediate(go.GetComponent<Collider>()); // must not register as an obstacle
        go.transform.localScale = Vector3.one * 0.6f;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = bombMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.AddComponent<LobbedBomb>().Init(gameObject, transform.position + Vector3.up * 0.8f, aimPoint, flightTime, arcHeight, blastRadius,
                                          damageToPlayer, damageToEnemy, bombMarkerStartColor, bombMarkerEndColor);
    }
}
