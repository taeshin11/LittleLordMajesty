using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D billboard sprite renderer for NPCs.
///
/// Sits alongside the existing NPCDailyRoutine (which owns world position
/// and Lerp-based waypoint movement) and just handles the visual layer:
/// - Tracks per-frame position delta to infer facing and "am I walking?"
/// - Swaps one of 8 sprites (4 directions × 2 walk frames) accordingly
/// - Yaw-aligns a child quad to the camera so the sprite always faces the
///   viewer regardless of world rotation
///
/// NPC ids are resolved against Resources/Art/Sprites/ using the same layout
/// that generate_sprites.py emits: <npc_id>_<dir>_<frame>.png
/// </summary>
[DefaultExecutionOrder(50)]
public class NPCBillboard : MonoBehaviour
{
    public enum Facing { South = 0, North = 1, East = 2, West = 3 }

    [Header("Resources")]
    [Tooltip("Character id used as the sprite filename prefix " +
             "(e.g. 'vassal_01'). Must match tools/image_gen/generate_sprites.py.")]
    [SerializeField] private string _characterId;

    [Tooltip("Child SpriteRenderer that shows the billboard sprite.")]
    [SerializeField] private SpriteRenderer _sprite;

    [Header("Motion detection")]
    [Tooltip("Squared position delta per frame below which we consider " +
             "the NPC 'idle'. Keeps us from animating during rounding jitter.")]
    [SerializeField] private float _motionThresholdSq = 0.0001f;

    [SerializeField] private float _walkFrameDuration = 0.22f;

    private Sprite[] _directionSprites;      // [facing*2 + frame], size 8
    private Facing _facing = Facing.South;
    private int _walkFrame;
    private float _walkFrameTimer;
    private Vector3 _lastPosition;

    /// <summary>Public setter so scene wiring can swap the id post-spawn.</summary>
    public void SetCharacterId(string id)
    {
        _characterId = id;
        LoadSprites();
        ApplySprite();
    }

    /// <summary>Runtime wiring hook so CastleScene3D can point us at the
    /// SpriteRenderer it just spawned without exposing the private field.</summary>
    public void AssignSpriteRenderer(SpriteRenderer sr)
    {
        _sprite = sr;
        ApplySprite();
    }

    private void Awake()
    {
        LoadSprites();
        _lastPosition = transform.position;
    }

    private void LoadSprites()
    {
        _directionSprites = new Sprite[8];
        if (string.IsNullOrEmpty(_characterId)) return;
        // Order must match PlayerController: S0 S1 N0 N1 E0 E1 W0 W1.
        // generate_sprites.py emits <id>_<dir>_<frame>.png with dir in {s,n,e,w}.
        string[] dirs = { "s", "n", "e", "w" };
        for (int d = 0; d < 4; d++)
        {
            for (int f = 0; f < 2; f++)
            {
                string path = $"Art/Sprites/{_characterId}_{dirs[d]}_{f}";
                var s = Resources.Load<Sprite>(path);
                _directionSprites[d * 2 + f] = s;
                if (s == null)
                    Debug.LogWarning($"[NPCBillboard] Missing sprite: {path}");
            }
        }
    }

    private void LateUpdate()
    {
        // Yaw-align the sprite holder to the camera so the quad always
        // faces the viewer. Kept in LateUpdate so the follow camera has
        // already moved this frame.
        if (_sprite != null && Camera.main != null)
        {
            Vector3 camPos = Camera.main.transform.position;
            Vector3 forward = _sprite.transform.position - camPos;
            forward.y = 0;
            if (forward.sqrMagnitude > 0.0001f)
                _sprite.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        // Infer motion from position delta. NPCDailyRoutine owns movement,
        // so we don't touch transform.position here.
        Vector3 delta = transform.position - _lastPosition;
        delta.y = 0;
        _lastPosition = transform.position;

        if (delta.sqrMagnitude < _motionThresholdSq)
        {
            _walkFrame = 0;
            _walkFrameTimer = 0f;
            ApplySprite();
            return;
        }

        // Facing derivation identical to PlayerController — pick the
        // cardinal direction closest to the motion vector in camera space.
        Vector3 camF = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
        Vector3 camR = Camera.main != null ? Camera.main.transform.right   : Vector3.right;
        camF.y = camR.y = 0; camF.Normalize(); camR.Normalize();
        float dF = Vector3.Dot(delta.normalized, camF);
        float dR = Vector3.Dot(delta.normalized, camR);
        if (Mathf.Abs(dR) > Mathf.Abs(dF))
            _facing = dR > 0 ? Facing.East : Facing.West;
        else
            _facing = dF > 0 ? Facing.North : Facing.South;

        _walkFrameTimer += Time.deltaTime;
        if (_walkFrameTimer >= _walkFrameDuration)
        {
            _walkFrameTimer = 0f;
            _walkFrame = 1 - _walkFrame;
        }
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
