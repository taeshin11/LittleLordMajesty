using UnityEngine;

/// <summary>
/// M16 roaming pivot — 2D sprite renderer for NPCs using Anokolisa Pixel Crawler.
///
/// NPCs use front-facing sprite sheets (Idle animation, no directional variants).
/// Animates between frames to give life to the NPC. Sits alongside NPCDailyRoutine
/// which owns world position and Lerp-based waypoint movement.
/// </summary>
[DefaultExecutionOrder(50)]
public class NPCBillboard : MonoBehaviour
{
    [SerializeField] private string _spriteType; // Knight, Rogue, Wizzard
    [SerializeField] private SpriteRenderer _sprite;
    [SerializeField] private float _frameDuration = 0.25f;

    private Sprite[] _idleFrames;
    private int _frame;
    private float _frameTimer;

    public void SetSpriteType(string spriteType)
    {
        _spriteType = spriteType;
        LoadSprites();
        ApplySprite();
    }

    public void AssignSpriteRenderer(SpriteRenderer sr)
    {
        _sprite = sr;
        ApplySprite();
    }

    // Keep the old API name for compatibility with RoamingBootstrap
    public void SetCharacterId(string id)
    {
        SetSpriteType(PixelCrawlerSprites.GetNPCSpriteType(id));
    }

    private void Awake()
    {
        LoadSprites();
    }

    private void LoadSprites()
    {
        if (string.IsNullOrEmpty(_spriteType)) return;
        var set = PixelCrawlerSprites.LoadNPCSprites(_spriteType);
        _idleFrames = set.Idle;
    }

    private void LateUpdate()
    {
        if (_idleFrames == null || _idleFrames.Length == 0) return;

        _frameTimer += Time.deltaTime;
        if (_frameTimer >= _frameDuration)
        {
            _frameTimer = 0f;
            _frame = (_frame + 1) % _idleFrames.Length;
        }
        ApplySprite();
    }

    private void ApplySprite()
    {
        if (_sprite == null || _idleFrames == null || _idleFrames.Length == 0) return;
        int idx = _frame % _idleFrames.Length;
        if (_idleFrames[idx] != null) _sprite.sprite = _idleFrames[idx];
    }
}
