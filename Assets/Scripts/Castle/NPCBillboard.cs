using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D sprite renderer for NPCs using Kenney RPG Urban Pack.
///
/// NPCs have 4 directional sprites (front, back, left, right). Facing is
/// inferred from movement delta each frame. When idle, defaults to front-facing.
/// Simulates walk with a slight Y bob like the player.
/// </summary>
[DefaultExecutionOrder(50)]
public class NPCBillboard : MonoBehaviour
{
    [SerializeField] private string _npcId;
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private float _bobFrequency = 6f;
    [SerializeField] private float _bobAmplitude = 0.03f;

    private PixelCrawlerSprites.NPCSpriteSet _sprites;
    private int _facing; // 0=front, 1=back, 2=right, 3=left
    private Vector3 _lastPos;
    private float _bobTimer;
    private bool _isMoving;

    public void SetCharacterId(string id)
    {
        _npcId = id;
        LoadSprites();
        ApplySprite();
    }

    public void AssignSpriteRenderer(SpriteRenderer sr)
    {
        _sprite = sr;
        ApplySprite();
    }

    /// <summary>
    /// Get the front-facing sprite for use as a portrait.
    /// </summary>
    public Sprite GetPortraitSprite()
    {
        return _sprites?.Front;
    }

    private void Awake()
    {
        LoadSprites();
    }

    private void Start()
    {
        _lastPos = transform.position;
    }

    private void LoadSprites()
    {
        if (string.IsNullOrEmpty(_npcId)) return;
        _sprites = PixelCrawlerSprites.LoadNPCSprites(_npcId);
    }

    private void LateUpdate()
    {
        if (_sprites == null) return;

        // Infer facing from movement delta
        Vector3 delta = transform.position - _lastPos;
        _lastPos = transform.position;

        _isMoving = delta.sqrMagnitude > 0.0001f;

        if (_isMoving)
        {
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                _facing = delta.x > 0 ? 2 : 3; // right : left
            else
                _facing = delta.y > 0 ? 1 : 0; // back(up) : front(down)
        }

        // Walk bob
        if (_isMoving && _sprite != null)
        {
            _bobTimer += Time.deltaTime * _bobFrequency;
            float bob = Mathf.Sin(_bobTimer * Mathf.PI * 2f) * _bobAmplitude;
            _sprite.transform.localPosition = new Vector3(0f, bob, 0f);
        }
        else if (_sprite != null)
        {
            _bobTimer = 0f;
            _sprite.transform.localPosition = Vector3.zero;
        }

        ApplySprite();
    }

    private void ApplySprite()
    {
        if (_sprite == null || _sprites == null) return;
        var s = _sprites.GetFacing(_facing);
        if (s != null) _sprite.sprite = s;
        _sprite.flipX = false;
    }
}
