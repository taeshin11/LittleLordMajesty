using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D free-movement player controller.
///
/// Direct transform-based movement in the XY plane. No Rigidbody, no
/// CharacterController. Supports keyboard (WASD/arrows) and touch
/// (via VirtualJoystick). 3-direction sprite system (Down/Side/Up)
/// with horizontal flip for left/right, using Anokolisa Pixel Crawler sheets.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public enum Facing { Down = 0, Up = 1, Right = 2, Left = 3 }

    public class RuntimeConfig
    {
        public SpriteRenderer Sprite;
        public PixelCrawlerSprites.PlayerSpriteSet Sprites;
        public float WalkSpeed;
    }

    public void ConfigureAtRuntime(RuntimeConfig cfg)
    {
        if (cfg == null) return;
        _sprite = cfg.Sprite;
        _sprites = cfg.Sprites;
        if (cfg.WalkSpeed > 0f) _walkSpeed = cfg.WalkSpeed;
        ApplySprite(false);
    }

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private float _frameDuration = 0.12f;

    private PixelCrawlerSprites.PlayerSpriteSet _sprites;
    private Facing _facing = Facing.Down;
    private int _frame;
    private float _frameTimer;
    private bool _inputLocked;

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
    {
        RefreshLockFromState();
    }

    private void RefreshLockFromState()
    {
        var gm = GameManager.Instance;
        if (gm == null) { _inputLocked = false; return; }
        _inputLocked = gm.CurrentState != GameManager.GameState.Castle
                    && gm.CurrentState != GameManager.GameState.WorldMap;
    }

    private void Update()
    {
        if (_inputLocked) { ApplySprite(false); return; }

        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        var joy = VirtualJoystick.Instance;
        if (joy != null && joy.InputVector.sqrMagnitude > 0.01f)
            input = joy.InputVector;

        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude < 0.01f)
        {
            _frame = 0;
            _frameTimer = 0f;
            ApplySprite(false);
            return;
        }

        Vector2 moveDir = input.normalized;
        transform.position += new Vector3(moveDir.x, moveDir.y, 0f) * (_walkSpeed * Time.deltaTime);

        UpdateFacing(moveDir);
        AdvanceFrame();
        ApplySprite(true);
    }

    private void UpdateFacing(Vector2 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;

        if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
            _facing = moveDir.x > 0 ? Facing.Right : Facing.Left;
        else
            _facing = moveDir.y > 0 ? Facing.Up : Facing.Down;
    }

    private void AdvanceFrame()
    {
        _frameTimer += Time.deltaTime;
        if (_frameTimer >= _frameDuration)
        {
            _frameTimer = 0f;
            // Get current walk array to know frame count
            var arr = GetWalkArray();
            if (arr != null && arr.Length > 0)
                _frame = (_frame + 1) % arr.Length;
        }
    }

    private Sprite[] GetWalkArray()
    {
        if (_sprites == null) return null;
        return _facing switch
        {
            Facing.Down => _sprites.WalkDown,
            Facing.Up => _sprites.WalkUp,
            Facing.Right or Facing.Left => _sprites.WalkSide,
            _ => _sprites.WalkDown,
        };
    }

    private Sprite[] GetIdleArray()
    {
        if (_sprites == null) return null;
        return _facing switch
        {
            Facing.Down => _sprites.IdleDown,
            Facing.Up => _sprites.IdleUp,
            Facing.Right or Facing.Left => _sprites.IdleSide,
            _ => _sprites.IdleDown,
        };
    }

    private void ApplySprite(bool walking)
    {
        if (_sprite == null || _sprites == null) return;

        var arr = walking ? GetWalkArray() : GetIdleArray();
        if (arr == null || arr.Length == 0) return;

        int idx = _frame % arr.Length;
        if (arr[idx] != null) _sprite.sprite = arr[idx];

        // Flip horizontally for Left facing (Side sheets face right)
        _sprite.flipX = _facing == Facing.Left;
    }
}
