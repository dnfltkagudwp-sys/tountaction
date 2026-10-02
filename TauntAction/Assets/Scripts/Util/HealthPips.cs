using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// One small segment per HP point above the character's head, facing the camera.
/// Lost points stay as dark segments; a hit flashes the segments white for a moment.
/// </summary>
[RequireComponent(typeof(Health))]
public class HealthPips : MonoBehaviour
{
    [SerializeField] Color color = new Color(1f, 0.8f, 0.25f);
    [SerializeField] Color emptyColor = new Color(0.15f, 0.15f, 0.17f);
    [SerializeField] Color outlineColor = new Color(0.02f, 0.02f, 0.03f);
    [SerializeField] Vector2 pipSize = new Vector2(0.32f, 0.16f);
    [SerializeField] float gap = 0.07f;
    [SerializeField] float outline = 0.04f;
    [Tooltip("Height above the top of the body.")]
    [SerializeField] float headroom = 0.35f;
    [SerializeField] float flashTime = 0.15f;

    Health health;
    Transform root;
    Material[] fills;
    float flashTimer;
    int shown = -1;

    void Awake()
    {
        health = GetComponent<Health>();
        health.Damaged += _ => flashTimer = flashTime;
        health.Died += _ => { if (root != null) root.gameObject.SetActive(false); };
    }

    void Start()
    {
        // Built in Start so the body's size is final (models are set up in Awake).
        float top = 1f;
        var bodies = BodyVisual.Find(transform);
        if (bodies.Length > 0)
        {
            Bounds b = bodies[0].bounds;
            foreach (var r in bodies) b.Encapsulate(r.bounds);
            top = b.max.y - transform.position.y;
        }

        root = new GameObject("HealthPips").transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(0f, top + headroom, 0f);

        int n = Mathf.Max(1, Mathf.RoundToInt(health.Max));
        fills = new Material[n];
        float width = n * pipSize.x + (n - 1) * gap;
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        for (int i = 0; i < n; i++)
        {
            float x = -width * 0.5f + pipSize.x * 0.5f + i * (pipSize.x + gap);
            Quad("Outline", x, 0.001f, pipSize + Vector2.one * outline * 2f, new Material(unlit) { color = outlineColor }, unlit);
            fills[i] = new Material(unlit);
            Quad("Pip" + i, x, 0f, pipSize, fills[i], unlit);
        }
    }

    void Quad(string name, float x, float z, Vector2 size, Material mat, Shader unlit)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Destroy(q.GetComponent<Collider>());
        q.transform.SetParent(root, false);
        q.transform.localPosition = new Vector3(x, 0f, z);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        var r = q.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        mat.SetColor("_BaseColor", mat.color);
    }

    void LateUpdate()
    {
        if (root == null) return;
        var cam = Camera.main;
        if (cam != null) root.rotation = cam.transform.rotation; // face the screen

        bool flashing = flashTimer > 0f;
        if (flashing) flashTimer -= Time.deltaTime;
        int hp = Mathf.CeilToInt(health.Current);
        if (hp == shown && !flashing && !wasFlashing) return;
        wasFlashing = flashing;
        shown = hp;
        for (int i = 0; i < fills.Length; i++)
            fills[i].SetColor("_BaseColor", flashing ? Color.white : i < hp ? color : emptyColor);
    }

    bool wasFlashing;

    void OnDestroy()
    {
        if (fills != null) foreach (var m in fills) if (m != null) Destroy(m);
    }
}
