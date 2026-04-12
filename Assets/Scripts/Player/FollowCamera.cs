using UnityEngine;

/// <summary>
/// 3D perspective follow camera — isometric-style top-down view.
///
/// Tracks the target on the XZ plane with a fixed height and pitch angle.
/// Gives the 3/4 top-down RPG perspective (Diablo / Baldur's Gate style).
/// Uses exponential smoothing for jitter-free frame-rate-independent follow.
/// </summary>
[DefaultExecutionOrder(100)]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [Tooltip("Height above the target.")]
    [SerializeField] private float _height = 45f;
    [Tooltip("Horizontal distance behind the target.")]
    [SerializeField] private float _distance = 28f;
    [Tooltip("Pitch angle in degrees (45 = classic isometric).")]
    [SerializeField] private float _pitch = 45f;
    [Tooltip("Yaw rotation around target in degrees (0 = looking south).")]
    [SerializeField] private float _yaw = 0f;
    [Tooltip("Higher = snappier follow. 5 feels good at 60 FPS.")]
    [SerializeField] private float _smooth = 5f;

    private void LateUpdate()
    {
        if (_target == null) return;

        float rad = _pitch * Mathf.Deg2Rad;
        float yawRad = _yaw * Mathf.Deg2Rad;

        // Camera offset: behind and above the target
        Vector3 offset = new Vector3(
            Mathf.Sin(yawRad) * _distance,
            _height,
            -Mathf.Cos(yawRad) * _distance
        );

        Vector3 desired = _target.position + offset;
        float t = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
        transform.LookAt(_target.position + Vector3.up * 1f); // Look slightly above ground
    }

    /// <summary>Assigned at scene build time by RoamingBootstrap.</summary>
    public void SetTarget(Transform target) => _target = target;

    /// <summary>Adjust camera distance at runtime.</summary>
    public void SetDistance(float dist) => _distance = dist;

    /// <summary>Adjust camera height at runtime.</summary>
    public void SetHeight(float h) => _height = h;
}
