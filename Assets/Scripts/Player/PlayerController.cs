using UnityEngine;

/// <summary>
/// 3D free-movement player controller on the XZ plane.
/// Simple transform-based movement with Y=0 lock. No physics.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;

    [Header("Visual")]
    [SerializeField] private float _bobFrequency = 8f;
    [SerializeField] private float _bobAmplitude = 0.06f;

    private Transform _visualRoot;
    private bool _inputLocked;
    private float _bobTimer;

    public void ConfigureAtRuntime(float walkSpeed, Transform visualRoot)
    {
        if (walkSpeed > 0f) _walkSpeed = walkSpeed;
        _visualRoot = visualRoot;
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
        // Always lock Y to ground
        var pos = transform.position;
        if (pos.y != 0f) { pos.y = 0f; transform.position = pos; }

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
            if (_visualRoot != null) _visualRoot.localPosition = Vector3.zero;
            return;
        }

        // Rotate input by camera yaw so joystick "up" = screen "up"
        float camYaw = Camera.main != null ? Camera.main.transform.eulerAngles.y : 0f;
        Vector3 rawDir = new Vector3(input.x, 0f, input.y);
        Vector3 moveDir = (Quaternion.Euler(0f, camYaw, 0f) * rawDir).normalized;
        float step = _walkSpeed * Time.deltaTime;
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Check from waist height

        // Try full movement first
        Vector3 newPos = transform.position + moveDir * step;
        if (!Physics.SphereCast(origin, 0.3f, moveDir, out _, step + 0.1f))
        {
            newPos.y = 0f;
            transform.position = newPos;
        }
        else
        {
            // Blocked — try sliding along X or Z axis separately
            Vector3 slideX = new Vector3(moveDir.x, 0f, 0f);
            if (slideX.sqrMagnitude > 0.01f && !Physics.SphereCast(origin, 0.3f, slideX.normalized, out _, step + 0.1f))
            {
                var p = transform.position + slideX.normalized * step;
                p.y = 0f;
                transform.position = p;
            }
            else
            {
                Vector3 slideZ = new Vector3(0f, 0f, moveDir.z);
                if (slideZ.sqrMagnitude > 0.01f && !Physics.SphereCast(origin, 0.3f, slideZ.normalized, out _, step + 0.1f))
                {
                    var p = transform.position + slideZ.normalized * step;
                    p.y = 0f;
                    transform.position = p;
                }
            }
        }

        // Face movement direction
        if (moveDir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.Euler(0f,
                Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg, 0f);

        // Walk bob
        if (_visualRoot != null)
        {
            _bobTimer += Time.deltaTime * _bobFrequency;
            float bob = Mathf.Abs(Mathf.Sin(_bobTimer * Mathf.PI * 2f)) * _bobAmplitude;
            _visualRoot.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
