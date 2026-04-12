using UnityEngine;

/// <summary>
/// 3D NPC visual controller — manages the procedural 3D character model
/// (capsule body + sphere head). Replaces the old 2D sprite billboard.
///
/// Facing is inferred from movement delta each frame. The character model
/// rotates to face its movement direction. Walk bob simulates stepping.
/// </summary>
[DefaultExecutionOrder(50)]
public class NPCBillboard : MonoBehaviour
{
    [SerializeField] private string _npcId;
    [SerializeField] private float _bobFrequency = 6f;
    [SerializeField] private float _bobAmplitude = 0.04f;

    private Transform _visualRoot;
    private Vector3 _lastPos;
    private float _bobTimer;
    private bool _isMoving;

    public void SetCharacterId(string id)
    {
        _npcId = id;
    }

    /// <summary>
    /// Assign the visual root transform (parent of body+head meshes)
    /// so we can apply walk bob to it.
    /// </summary>
    public void AssignVisualRoot(Transform root)
    {
        _visualRoot = root;
    }

    // Legacy compatibility — no-op for 3D mode
    public void AssignSpriteRenderer(SpriteRenderer sr) { }

    /// <summary>
    /// Get a placeholder portrait sprite for dialogue UI.
    /// Uses PlaceholderArtGenerator since we no longer have 2D sprites.
    /// </summary>
    public Sprite GetPortraitSprite()
    {
        return null; // Handled by PlaceholderArtGenerator / LocalArtBank now
    }

    private void Start()
    {
        _lastPos = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - _lastPos;
        _lastPos = transform.position;

        // Only consider XZ movement (ignore Y)
        Vector2 deltaXZ = new Vector2(delta.x, delta.z);
        _isMoving = deltaXZ.sqrMagnitude > 0.0001f;

        // Rotate to face movement direction
        if (_isMoving)
        {
            float angle = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        // Walk bob on the visual root
        if (_isMoving && _visualRoot != null)
        {
            _bobTimer += Time.deltaTime * _bobFrequency;
            float bob = Mathf.Abs(Mathf.Sin(_bobTimer * Mathf.PI * 2f)) * _bobAmplitude;
            _visualRoot.localPosition = new Vector3(0f, bob, 0f);
        }
        else if (_visualRoot != null)
        {
            _bobTimer = 0f;
            _visualRoot.localPosition = Vector3.zero;
        }
    }
}
