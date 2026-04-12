using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D free-movement player controller.
///
/// Direct transform-based movement in the XY plane. No Rigidbody, no
/// CharacterController. Supports keyboard (WASD/arrows) and touch
/// (via VirtualJoystick). 4-direction sprite system using Kenney RPG
/// Urban Pack individual tile PNGs (front/back/left/right).
/// Simulates walk by toggling a slight Y offset on the sprite.
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
    [SerializeField] private float _bobFrequency = 8f;
    [SerializeField] private float _bobAmplitude = 0.04f;

    private PixelCrawlerSprites.PlayerSpriteSet _sprites;
    private Facing _facing = Facing.Down;
    private bool _inputLocked;
    private float _bobTimer;

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
            _bobTimer = 0f;
            ApplySprite(false);
            return;
        }

        Vector2 moveDir = input.normalized;
        transform.position += new Vector3(moveDir.x, moveDir.y, 0f) * (_walkSpeed * Time.deltaTime);

        UpdateFacing(moveDir);
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

    private void ApplySprite(bool walking)
    {
        if (_sprite == null || _sprites == null) return;

        var s = _sprites.GetFacing((int)_facing);
        if (s != null) _sprite.sprite = s;

        // No flipX needed — left and right are separate sprites
        _sprite.flipX = false;

        // Walk bob: slight Y oscillation on the sprite child to simulate stepping
        if (walking)
        {
            _bobTimer += Time.deltaTime * _bobFrequency;
            float bob = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * _bobAmplitude;
            _sprite.transform.localPosition = new Vector3(0f, bob, 0f);
        }
        else
        {
            _sprite.transform.localPosition = Vector3.zero;
        }
    }
}
