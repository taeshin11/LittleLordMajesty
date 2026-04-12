using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D free-movement player controller.
///
/// Direct transform-based movement in the XY plane. No Rigidbody, no
/// CharacterController. Supports keyboard (WASD/arrows) and touch
/// (via VirtualJoystick). 4-direction sprite swapping with 2-frame
/// walk animation.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public enum Facing { South = 0, North = 1, East = 2, West = 3 }

    /// <summary>
    /// Runtime wiring payload used by RoamingBootstrap.
    /// </summary>
    public class RuntimeConfig
    {
        public SpriteRenderer Sprite;
        public Sprite[] DirectionSprites;
        public float WalkSpeed;
    }

    public void ConfigureAtRuntime(RuntimeConfig cfg)
    {
        if (cfg == null) return;
        _sprite = cfg.Sprite;
        _directionSprites = cfg.DirectionSprites;
        if (cfg.WalkSpeed > 0f) _walkSpeed = cfg.WalkSpeed;
        ApplySprite();
    }

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private Sprite[] _directionSprites = new Sprite[8];
    [SerializeField] private float _walkFrameDuration = 0.2f;

    private Facing _facing = Facing.South;
    private int _walkFrame;
    private float _walkFrameTimer;
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
        if (_inputLocked) { ApplyIdleFrame(); return; }

        // Combine keyboard + virtual joystick input.
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));

        // Overlay virtual joystick input if active.
        var joy = VirtualJoystick.Instance;
        if (joy != null && joy.InputVector.sqrMagnitude > 0.01f)
            input = joy.InputVector;

        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude < 0.01f)
        {
            ApplyIdleFrame();
            return;
        }

        // 2D movement: direct XY, no camera projection needed.
        Vector2 moveDir = input.normalized;
        transform.position += new Vector3(moveDir.x, moveDir.y, 0f) * (_walkSpeed * Time.deltaTime);

        UpdateFacing(moveDir);
        AdvanceWalkFrame();
    }

    private void UpdateFacing(Vector2 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;

        // Pick dominant axis.
        if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
            _facing = moveDir.x > 0 ? Facing.East : Facing.West;
        else
            _facing = moveDir.y > 0 ? Facing.North : Facing.South;
    }

    private void AdvanceWalkFrame()
    {
        _walkFrameTimer += Time.deltaTime;
        if (_walkFrameTimer >= _walkFrameDuration)
        {
            _walkFrameTimer = 0f;
            _walkFrame = 1 - _walkFrame;
        }
        ApplySprite();
    }

    private void ApplyIdleFrame()
    {
        _walkFrame = 0;
        _walkFrameTimer = 0f;
        ApplySprite();
    }

    private void ApplySprite()
    {
        if (_sprite == null || _directionSprites == null) return;
        int idx = (int)_facing * 2 + _walkFrame;
        if (idx < 0 || idx >= _directionSprites.Length) return;
        var s = _directionSprites[idx];
        if (s != null) _sprite.sprite = s;
    }
}
