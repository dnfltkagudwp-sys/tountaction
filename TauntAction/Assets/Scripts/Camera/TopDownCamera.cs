using UnityEngine;

public class TopDownCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;

    [Header("Framing")]
    [SerializeField, Range(40f, 75f)] float pitch = 55f;
    [SerializeField] float yaw = 0f;
    [SerializeField] float distance = 24f;
    [SerializeField] Vector3 targetOffset = Vector3.zero;

    [Header("Follow")]
    [Tooltip("0 = fixed camera at the arena center. 1 = fully locked to the target.")]
    [SerializeField, Range(0f, 1f)] float followAmount = 0.35f;
    [SerializeField] float smoothTime = 0.15f;
    [Tooltip("Follow is clamped so the focus point stays within this half-extent of the arena center.")]
    [SerializeField] float clampHalfExtent = 6f;

    Vector3 velocity;

    void LateUpdate()
    {
        Vector3 focus = targetOffset;
        if (target != null)
        {
            Vector3 p = target.position * followAmount;
            p.x = Mathf.Clamp(p.x, -clampHalfExtent, clampHalfExtent);
            p.z = Mathf.Clamp(p.z, -clampHalfExtent, clampHalfExtent);
            p.y = 0f;
            focus += p;
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desired = focus - rot * Vector3.forward * distance;

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
        transform.rotation = rot;
    }

    void OnValidate()
    {
        // Snap in edit mode so Inspector tweaks are visible immediately.
        if (!Application.isPlaying)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(targetOffset - rot * Vector3.forward * distance, rot);
        }
    }
}
