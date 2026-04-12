using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D orthographic follow camera.
///
/// Tracks the target in XY, keeping a fixed Z offset. Uses the same
/// exponential smoothing as the original 3D version for jitter-free
/// frame-rate-independent follow. LateUpdate ensures we read the
/// target's post-input position for that frame.
/// </summary>
[DefaultExecutionOrder(100)]
public class FollowCamera : MonoBehaviour
{
    [SerializeField] private Transform _target;
    [Tooltip("How much world-space is visible vertically. " +
             "5 is tight on the player, 8 gives a neighbourhood feel.")]
    [SerializeField] private float _orthoSize = 5.5f;
    [Tooltip("Higher = snappier follow. 5 feels good at 60 FPS.")]
    [SerializeField] private float _smooth = 5f;
    [Tooltip("Camera Z position (negative = in front of the scene).")]
    [SerializeField] private float _cameraZ = -10f;

    private void LateUpdate()
    {
        if (_target == null) return;

        Vector3 desired = new Vector3(_target.position.x, _target.position.y, _cameraZ);
        float t = 1f - Mathf.Exp(-_smooth * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desired, t);
    }

    /// <summary>
    /// Assigned at scene build time by RoamingBootstrap.
    /// </summary>
    public void SetTarget(Transform target) => _target = target;

    /// <summary>
    /// Allows runtime ortho size adjustment (e.g. for zoom or aspect fix).
    /// </summary>
    public void SetOrthoSize(float size) => _orthoSize = size;

    /// <summary>
    /// Returns the configured ortho size so the camera builder can read it.
    /// </summary>
    public float OrthoSize => _orthoSize;
}
