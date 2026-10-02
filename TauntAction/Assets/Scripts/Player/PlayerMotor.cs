using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 9f;
    [SerializeField] float turnSpeed = 720f;

    [Header("Dash")]
    [SerializeField] float dashSpeed = 25f;
    [SerializeField] float dashDuration = 0.18f;
    [SerializeField] float dashCooldown = 0.6f;
    [Tooltip("Invulnerable for this many seconds from the start of the dash.")]
    [SerializeField] float iFrameDuration = 0.12f;
    [Tooltip("While dashing, ignore collisions with anything that has Health (enemies). Walls still block.")]
    [SerializeField] bool passThroughEnemies = true;
    [SerializeField] Color iFrameColor = new Color(0.6f, 0.9f, 1f);

    [Header("Reference")]
    [Tooltip("Movement directions are relative to this camera's yaw. Falls back to Camera.main.")]
    [SerializeField] Transform cameraReference;

    CharacterController controller;
    Health health;
    Material[] bodyMats;
    Color[] baseColors;

    float dashTimer;
    float cooldownTimer;
    float iFrameTimer;
    Vector3 dashDir;
    readonly List<Collider> ignoredColliders = new List<Collider>();

    public Vector3 Velocity { get; private set; }
    public bool IsDashing => dashTimer > 0f;
    public bool IsInvulnerable => iFrameTimer > 0f;
    public float DashCooldownRemaining => Mathf.Max(0f, cooldownTimer);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        if (health != null) health.Died += _ => enabled = false;
        if (cameraReference == null && Camera.main != null)
            cameraReference = Camera.main.transform;

        bodyMats = BodyVisual.InstanceMaterials(BodyVisual.Find(transform));
        baseColors = new Color[bodyMats.Length];
        for (int i = 0; i < bodyMats.Length; i++)
            baseColors[i] = bodyMats[i].HasProperty("_BaseColor") ? bodyMats[i].GetColor("_BaseColor") : Color.white;
    }

    void OnDisable()
    {
        // Covers death mid-dash: never leave i-frames or ignored collisions behind.
        EndIFrames();
        EndDash();
    }

    void Update()
    {
        float dt = Time.deltaTime;
        cooldownTimer -= dt;

        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude > 1f) input.Normalize();
        Vector3 dir = CameraRelative(input);

        if (!IsDashing && cooldownTimer <= 0f && DashPressed())
            StartDash(dir);

        if (iFrameTimer > 0f)
        {
            iFrameTimer -= dt;
            if (iFrameTimer <= 0f) EndIFrames();
        }

        if (IsDashing)
        {
            // Direction is locked for the whole dash.
            Velocity = dashDir * dashSpeed;
            controller.Move(Velocity * dt + Vector3.down * 0.01f);

            dashTimer -= dt;
            if (dashTimer <= 0f) EndDash();
            return;
        }

        Velocity = dir * moveSpeed;

        // Keep the player pinned to the floor plane.
        controller.Move(Velocity * dt + Vector3.down * 0.01f);

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * dt);
        }
    }

    // ---- Dash -------------------------------------------------------------

    void StartDash(Vector3 inputDir)
    {
        // Input direction first; with no input, dash where the player is facing.
        dashDir = inputDir.sqrMagnitude > 0.0001f ? inputDir.normalized : Flat(transform.forward).normalized;
        transform.rotation = Quaternion.LookRotation(dashDir, Vector3.up);

        dashTimer = dashDuration;
        cooldownTimer = dashCooldown;

        if (iFrameDuration > 0f)
        {
            iFrameTimer = iFrameDuration;
            if (health != null) health.Invulnerable = true;
            BodyVisual.SetColor(bodyMats, "_BaseColor", iFrameColor);
        }

        if (passThroughEnemies) IgnoreEnemyCollisions();
    }

    void EndDash()
    {
        dashTimer = 0f;
        foreach (var c in ignoredColliders)
            if (c != null) Physics.IgnoreCollision(controller, c, false);
        ignoredColliders.Clear();
    }

    void EndIFrames()
    {
        iFrameTimer = 0f;
        if (health != null) health.Invulnerable = false;
        for (int i = 0; i < bodyMats.Length; i++)
            if (bodyMats[i].HasProperty("_BaseColor")) bodyMats[i].SetColor("_BaseColor", baseColors[i]);
    }

    void IgnoreEnemyCollisions()
    {
        foreach (var h in FindObjectsByType<Health>())
        {
            if (h == health) continue;
            foreach (var c in h.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(controller, c, true);
                ignoredColliders.Add(c);
            }
        }
    }

    static bool DashPressed()
    {
        var kb = Keyboard.current;
        return kb != null && kb.spaceKey.wasPressedThisFrame;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    static Vector2 ReadMoveInput()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    Vector3 CameraRelative(Vector2 input)
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraReference != null)
        {
            forward = Vector3.ProjectOnPlane(cameraReference.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.ProjectOnPlane(cameraReference.up, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, forward);
        }
        return forward * input.y + right * input.x;
    }
}
