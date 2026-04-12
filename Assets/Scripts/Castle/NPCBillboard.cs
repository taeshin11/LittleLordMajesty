using UnityEngine;

/// <summary>
/// 2D isometric NPC visual controller — manages the sprite-based character.
///
/// Handles run animation when moving, idle sprite when stationary.
/// Updates sortingOrder based on Y position for correct draw order.
/// </summary>
[DefaultExecutionOrder(50)]
public class NPCBillboard : MonoBehaviour
{
    [SerializeField] private string _npcId;
    [SerializeField] private float _runFPS = 10f;

    private SpriteRenderer _spriteRenderer;
    private Sprite _idleSprite;
    private Sprite[] _runFrames;
    private Vector3 _lastPos;
    private float _animTimer;
    private int _animFrame;
    private bool _isMoving;

    public void SetCharacterId(string id)
    {
        _npcId = id;
    }

    public void AssignSpriteRenderer(SpriteRenderer sr)
    {
        _spriteRenderer = sr;
    }

    public void SetRunFrames(Sprite idle, Sprite[] runFrames)
    {
        _idleSprite = idle;
        _runFrames = runFrames;
    }

    /// <summary>
    /// Legacy compatibility — no-op for 2D mode (visual root not needed).
    /// </summary>
    public void AssignVisualRoot(Transform root) { }

    /// <summary>
    /// Get a placeholder portrait sprite for dialogue UI.
    /// Returns the idle sprite if available.
    /// </summary>
    public Sprite GetPortraitSprite()
    {
        return _idleSprite;
    }

    private void Start()
    {
        _lastPos = transform.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - _lastPos;
        _lastPos = transform.position;

        // Only consider XY movement
        Vector2 deltaXY = new Vector2(delta.x, delta.y);
        _isMoving = deltaXY.sqrMagnitude > 0.0001f;

        if (_spriteRenderer == null) return;

        // Update sorting order based on Y position (lower Y = in front)
        _spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);

        // Animate
        if (_isMoving && _runFrames != null && _runFrames.Length > 0)
        {
            _animTimer += Time.deltaTime * _runFPS;
            if (_animTimer >= 1f)
            {
                _animTimer -= 1f;
                _animFrame = (_animFrame + 1) % _runFrames.Length;
            }
            if (_runFrames[_animFrame] != null)
                _spriteRenderer.sprite = _runFrames[_animFrame];
        }
        else if (_idleSprite != null)
        {
            _spriteRenderer.sprite = _idleSprite;
            _animTimer = 0f;
            _animFrame = 0;
        }
    }
}
