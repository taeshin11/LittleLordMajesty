using UnityEngine;

/// <summary>
/// 2D orthographic follow camera for top-down pixel art world.
///
/// Camera sits at z=-10 looking at z=0. Smooth follow on XY plane.
/// Designed for 16x16 Kenney Tiny Town/Dungeon tiles at PPU=16.
/// </summary>
[DefaultExecutionOrder(100)]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;

    [Tooltip("Orthographic size — smaller = more zoomed in.")]
    [SerializeField] private float _orthoSize = 5f;

    [Tooltip("Higher = snappier follow.")]
    [SerializeField] private float _smooth = 5f;

    [Tooltip("Camera Z position (behind the sprite plane).")]
    [SerializeField] private float _cameraZ = -10f;

    private void Start()
    {
        var cam = GetComponent<Camera>();
        if (cam != null)
        {
            cam.orthographic = true;
            cam.orthographicSize = _orthoSize;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
        }
        // Face straight forward along Z (no rotation)
        transform.rotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (_target == null) return;

        // Keep ortho size in sync
        var cam = GetComponent<Camera>();
        if (cam != null && cam.orthographicSize != _orthoSize)
            cam.orthographicSize = _orthoSize;

        // No rotation — flat 2D view
        transform.rotation = Quaternion.identity;

        // Follow target on XY, keep fixed Z
        Vector3 desired = new Vector3(_target.position.x, _target.position.y, _cameraZ);

        float t = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }

    public void SetTarget(Transform target) => _target = target;
    public void SetOrthoSize(float size) => _orthoSize = size;
    public float OrthoSize => _orthoSize;
}
