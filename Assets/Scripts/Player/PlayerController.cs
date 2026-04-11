using UnityEngine;

/// <summary>
/// M16 roaming pivot — free-movement chibi player controller.
///
/// Design (locked in research_history/milestone_16_roaming_pivot_plan.md):
/// - Direct transform-based movement. No Rigidbody, no CharacterController,
///   no NavMesh. Updates position once per frame from raw input.
/// - Collision: short-distance Physics.Raycast against a "Walls" layer
///   before applying movement. Blocked → slide along the wall tangent.
/// - 2D billboard visual. A SpriteRenderer on a child transform ("Sprite")
///   shows one of 8 sprites (4 directions × 2 walk frames), swapped based
///   on the current facing and a ping-pong walk timer while moving.
/// - Input disabled while dialogue/menu/paused — we listen to GameManager
///   state changes instead of polling for a flag each frame.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public enum Facing { South = 0, North = 1, East = 2, West = 3 }

    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;
    [SerializeField] private LayerMask _wallLayer;
    [Tooltip("How far ahead to probe for walls. Slightly bigger than " +
             "one frame's max travel at _walkSpeed so we never miss a wall.")]
    [SerializeField] private float _raycastDistance = 0.35f;

    [Header("Visual")]
    [Tooltip("Child SpriteRenderer that holds the billboard sprite.")]
    [SerializeField] private SpriteRenderer _sprite;
    [Tooltip("8 sprites indexed as [facing * 2 + frame]. " +
             "Order: S0 S1 N0 N1 E0 E1 W0 W1.")]
    [SerializeField] private Sprite[] _directionSprites = new Sprite[8];
    [SerializeField] private float _walkFrameDuration = 0.2f;

    private Facing _facing = Facing.South;
    private int _walkFrame;
    private float _walkFrameTimer;
    private bool _inputLocked;

    private void OnEnable()
    {
        // Lock input during non-gameplay states. The state change event
        // fires exactly once per transition — cheaper than polling a flag
        // every frame from Update.
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
        // Only free-roam in Castle / WorldMap. Dialogue, events, battle,
        // pause, loading, menus all block movement.
        _inputLocked = gm.CurrentState != GameManager.GameState.Castle
                    && gm.CurrentState != GameManager.GameState.WorldMap;
    }

    private void Update()
    {
        if (_inputLocked) { ApplyIdleFrame(); return; }

        // Raw axis input so diagonals aren't smoothed by Unity's input damping.
        Vector2 input = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (input.sqrMagnitude < 0.0001f)
        {
            ApplyIdleFrame();
            return;
        }

        // Camera-relative movement: pressing "up" should always walk toward
        // the top of the screen regardless of how the follow camera is yawed.
        // We project the camera's forward/right onto the horizontal plane.
        Vector3 camF = Vector3.ProjectOnPlane(Camera.main != null
            ? Camera.main.transform.forward : Vector3.forward, Vector3.up).normalized;
        Vector3 camR = Vector3.ProjectOnPlane(Camera.main != null
            ? Camera.main.transform.right : Vector3.right, Vector3.up).normalized;
        Vector3 moveDir = (camR * input.x + camF * input.y).normalized;

        // Wall raycast from chest height so ground-hugging colliders don't
        // shadow the ray. Slide along the wall if blocked.
        Vector3 probeOrigin = transform.position + Vector3.up * 0.5f;
        if (Physics.Raycast(probeOrigin, moveDir, out RaycastHit hit,
                            _raycastDistance, _wallLayer))
        {
            // Project moveDir onto the plane perpendicular to hit.normal
            // and use that as the slide direction. If the slide itself
            // also hits, the player just stops at the corner.
            Vector3 slide = Vector3.ProjectOnPlane(moveDir, hit.normal);
            if (Physics.Raycast(probeOrigin, slide.normalized, _raycastDistance, _wallLayer))
                moveDir = Vector3.zero;
            else
                moveDir = slide.normalized;
        }

        transform.position += moveDir * (_walkSpeed * Time.deltaTime);

        // Facing: pick the axis-aligned direction closest to our actual move.
        // For 2D billboards, we don't bother with diagonal sprites — the
        // dominant axis wins.
        UpdateFacing(moveDir);
        AdvanceWalkFrame();
    }

    private void UpdateFacing(Vector3 moveDir)
    {
        if (moveDir.sqrMagnitude < 0.0001f) return;
        // Camera-screen-space heuristic: compare dot against camera right
        // and camera forward to assign N/S/E/W.
        Vector3 camF = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        Vector3 camR = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        camF.y = camR.y = 0;
        camF.Normalize(); camR.Normalize();

        float dF = Vector3.Dot(moveDir, camF);
        float dR = Vector3.Dot(moveDir, camR);

        if (Mathf.Abs(dR) > Mathf.Abs(dF))
            _facing = dR > 0 ? Facing.East : Facing.West;
        else
            _facing = dF > 0 ? Facing.North : Facing.South;
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
