using UnityEngine;

/// <summary>
/// Isometric orthographic follow camera — low-poly diorama style.
///
/// Orthographic projection removes perspective distortion, making the
/// scene look like a miniature toy diorama. Fixed 45° isometric angle
/// gives the classic 2.5D feel (StarCraft, RollerCoaster Tycoon).
/// </summary>
[DefaultExecutionOrder(100)]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;

    [Tooltip("Orthographic size — smaller = more zoomed in.")]
    [SerializeField] private float _orthoSize = 7f;

    [Tooltip("Isometric rotation around Y axis (45 = classic iso).")]
    [SerializeField] private float _yawAngle = 45f;

    [Tooltip("Pitch angle from horizontal (30-35 = classic iso).")]
    [SerializeField] private float _pitchAngle = 35f;

    [Tooltip("Distance from target (affects shadow/clipping, not visual size).")]
    [SerializeField] private float _distance = 30f;

    [Tooltip("Higher = snappier follow.")]
    [SerializeField] private float _smooth = 5f;

    private void Start()
    {
        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = _orthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
        }
        // Set fixed rotation immediately
        transform.rotation = Quaternion.Euler(_pitchAngle, _yawAngle, 0f);
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // Fixed isometric rotation
        Quaternion rot = Quaternion.Euler(_pitchAngle, _yawAngle, 0f);
        transform.rotation = rot;

        // Position: offset from target along the camera's back direction
        Vector3 offset = rot * new Vector3(0f, 0f, -_distance);
        Vector3 desired = _target.position + offset;

        float t = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }

    public void SetTarget(Transform target) => _target = target;
    public void SetOrthoSize(float size) => _orthoSize = size;
    public float OrthoSize => _orthoSize;
}
