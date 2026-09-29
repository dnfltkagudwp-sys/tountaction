using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float moveSpeed = 9f;
    [SerializeField] float turnSpeed = 720f;

    [Header("Reference")]
    [Tooltip("Movement directions are relative to this camera's yaw. Falls back to Camera.main.")]
    [SerializeField] Transform cameraReference;

    CharacterController controller;

    public Vector3 Velocity { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        var health = GetComponent<Health>();
        if (health != null) health.Died += _ => enabled = false;
        if (cameraReference == null && Camera.main != null)
            cameraReference = Camera.main.transform;
    }

    void Update()
    {
        Vector2 input = ReadMoveInput();
        if (input.sqrMagnitude > 1f) input.Normalize();

        Vector3 dir = CameraRelative(input);
        Velocity = dir * moveSpeed;

        // Keep the player pinned to the floor plane.
        controller.Move(Velocity * Time.deltaTime + Vector3.down * 0.01f);

        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }

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
