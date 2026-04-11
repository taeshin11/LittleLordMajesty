using UnityEngine;

/// <summary>
/// M16 roaming pivot — third-person isometric follow camera.
///
/// Keeps a fixed offset from the target (the player) and soft-lerps toward
/// it each LateUpdate. No collision, no clipping correction in M16 — if the
/// camera pokes through a wall, we'll add a short rear-raycast pushback in a
/// polish pass. LateUpdate (not Update) ensures we follow the target's
/// post-input position for that frame, eliminating 1-frame jitter.
/// </summary>
[DefaultExecutionOrder(100)]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [Tooltip("World-space offset from target. Default gives a 3-quarter " +
             "isometric framing (above, behind, looking down).")]
    [SerializeField] private Vector3 _offset = new Vector3(0f, 10f, -7f);
    [Tooltip("Higher = snappier follow. 5 feels good at 60 FPS.")]
    [SerializeField] private float _smooth = 5f;
    [Tooltip("Point the camera at target.position + this. Slight upward " +
             "lift means the avatar sits in the lower-third, not dead center.")]
    [SerializeField] private Vector3 _lookOffset = new Vector3(0f, 1f, 0f);

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desired = _target.position + _offset;
        // Vector3.Lerp with a dt-scaled t gives frame-rate-independent
        // smoothing. This is the "exponential smoothing" pattern — the
        // actual approach rate matches _smooth regardless of frame time.
        float t = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);

        transform.LookAt(_target.position + _lookOffset);
    }

    /// <summary>
    /// Assigned at scene build time by SceneAutoBuilder or at runtime when
    /// the player avatar spawns (e.g. after a scene load).
    /// </summary>
    public void SetTarget(Transform target) => _target = target;
}
