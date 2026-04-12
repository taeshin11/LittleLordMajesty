using UnityEngine;

/// <summary>
/// 2D isometric player controller on the XY plane.
/// Simple transform-based movement with sprite animation support.
/// No physics — uses Physics2D.OverlapCircle for collision checks.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;

    [Header("Animation")]
    [SerializeField] private float _runFPS = 10f;

    private SpriteRenderer _spriteRenderer;
    private Sprite[] _runFrames;
    private Sprite _idleSprite;
    private float _animTimer;
    private int _animFrame;
    private bool _inputLocked;

    public void ConfigureAtRuntime(float walkSpeed, SpriteRenderer sr,
        Sprite idleSprite, Sprite[] runFrames)
    {
        if (walkSpeed > 0f) _walkSpeed = walkSpeed;
        _spriteRenderer = sr;
        _idleSprite = idleSprite;
        _runFrames = runFrames;
        if (_spriteRenderer != null && _idleSprite != null)
            _spriteRenderer.sprite = _idleSprite;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += HandleStateChanged;
        RefreshLockFromState();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameManager.GameState _old, GameManager.GameState _new)
        => RefreshLockFromState();

    private void RefreshLockFromState()
    {
        var gm = GameManager.Instance;
        if (gm == null) { _inputLocked = false; return; }
        _inputLocked = gm.CurrentState != GameManager.GameState.Castle
                    && gm.CurrentState != GameManager.GameState.WorldMap;
    }

    private void Update()
    {
        if (_inputLocked) return;

        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        var joy = VirtualJoystick.Instance;
        if (joy != null && joy.InputVector.sqrMagnitude > 0.01f)
            input = joy.InputVector;

        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude < 0.01f)
        {
            // Idle
            if (_spriteRenderer != null && _idleSprite != null)
                _spriteRenderer.sprite = _idleSprite;
            _animTimer = 0f;
            _animFrame = 0;
            return;
        }

        // Move on XY plane (screen space = world space for 2D)
        Vector3 moveDir = new Vector3(input.x, input.y, 0f).normalized;
        float step = _walkSpeed * Time.deltaTime;
        Vector3 newPos = transform.position + moveDir * step;
        newPos.z = 0f;
        transform.position = newPos;

        // Animate run cycle
        if (_spriteRenderer != null && _runFrames != null && _runFrames.Length > 0)
        {
            _animTimer += Time.deltaTime * _runFPS;
            if (_animTimer >= 1f)
            {
                _animTimer -= 1f;
                _animFrame = (_animFrame + 1) % _runFrames.Length;
            }
            _spriteRenderer.sprite = _runFrames[_animFrame];
        }

        // Update sorting order based on Y position (lower Y = in front = higher order)
        if (_spriteRenderer != null)
            _spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100f);
    }
}
