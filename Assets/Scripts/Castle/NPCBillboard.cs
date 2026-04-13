using UnityEngine;

/// <summary>
/// 2D top-down NPC visual controller for Tiny Dungeon character sprites.
///
/// Single 16x16 sprite per character (no animation frames).
/// Flips sprite horizontally when moving left.
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

    /// <summary>
    /// Set the idle sprite only (for single-tile characters with no animation).
    /// </summary>
    public void SetIdleSprite(Sprite idle)
    {
        _idleSprite = idle;
        _runFrames = null;
    }

    /// <summary>
    /// Legacy compatibility — set run frames if available.
    /// </summary>
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
        _spriteRenderer.sortingOrder = 10000 - Mathf.RoundToInt(transform.position.y * 10f);

        // Flip sprite based on horizontal movement direction
        if (_isMoving && Mathf.Abs(delta.x) > 0.001f)
            _spriteRenderer.flipX = delta.x < 0f;

        // Animate if run frames exist
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
        else if (!_isMoving && _idleSprite != null)
        {
            _spriteRenderer.sprite = _idleSprite;
            _animTimer = 0f;
            _animFrame = 0;
        }
    }
}
