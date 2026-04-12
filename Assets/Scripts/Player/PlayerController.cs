using UnityEngine;

/// <summary>
/// 3D free-movement player controller on the XZ plane.
///
/// Direct transform-based movement. No Rigidbody, no CharacterController.
/// Supports keyboard (WASD/arrows) and touch (via VirtualJoystick).
/// The player is a procedural 3D character (capsule body + sphere head).
/// Rotation faces movement direction for visual feedback.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public enum Facing { Down = 0, Up = 1, Right = 2, Left = 3 }

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;

    [Header("Visual")]
    [SerializeField] private float _bobFrequency = 8f;
    [SerializeField] private float _bobAmplitude = 0.06f;

    private Transform _visualRoot;
    private CharacterController _cc;
    private Facing _facing = Facing.Down;
    private bool _inputLocked;
    private float _bobTimer;

    /// <summary>
    /// Runtime configuration — called by RoamingBootstrap after building
    /// the procedural 3D character model.
    /// </summary>
    public void ConfigureAtRuntime(float walkSpeed, Transform visualRoot)
    {
        if (walkSpeed > 0f) _walkSpeed = walkSpeed;
        _visualRoot = visualRoot;
        // Add CharacterController for physics-based collision
        _cc = gameObject.GetComponent<CharacterController>();
        if (_cc == null)
        {
            _cc = gameObject.AddComponent<CharacterController>();
            _cc.height = 1.8f;
            _cc.radius = 0.3f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _cc.slopeLimit = 45f;
            _cc.stepOffset = 0.3f;
        }
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
            _bobTimer = 0f;
            ResetBob();
            return;
        }

        // Movement on XZ plane (Y is up) — CharacterController handles collision
        Vector3 moveDir = new Vector3(input.x, 0f, input.y).normalized;
        Vector3 motion = moveDir * (_walkSpeed * Time.deltaTime);
        if (_cc != null)
            _cc.Move(motion + Vector3.down * 9.8f * Time.deltaTime); // gravity
        else
            transform.position += motion;

        // Rotate character to face movement direction
        if (moveDir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }

        UpdateFacing(input);
        ApplyWalkBob();
    }

    private void UpdateFacing(Vector2 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;

        if (Mathf.Abs(moveDir.x) > Mathf.Abs(moveDir.y))
            _facing = moveDir.x > 0 ? Facing.Right : Facing.Left;
        else
            _facing = moveDir.y > 0 ? Facing.Up : Facing.Down;
    }

    private void ApplyWalkBob()
    {
        if (_visualRoot == null) return;
        _bobTimer += Time.deltaTime * _bobFrequency;
        float bob = Mathf.Abs(Mathf.Sin(_bobTimer * Mathf.PI * 2f)) * _bobAmplitude;
        _visualRoot.localPosition = new Vector3(0f, bob, 0f);
    }

    private void ResetBob()
    {
        if (_visualRoot == null) return;
        _visualRoot.localPosition = Vector3.zero;
    }
}
