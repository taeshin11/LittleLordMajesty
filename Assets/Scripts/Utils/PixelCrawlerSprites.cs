using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility to load and slice Anokolisa Pixel Crawler sprite sheets at runtime.
/// Sprite sheets are horizontal strips — each frame is frameSize x frameSize.
/// Caches sliced sprites to avoid repeat work.
/// </summary>
public static class PixelCrawlerSprites
{
    private static readonly Dictionary<string, Sprite[]> _cache = new();

    /// <summary>
    /// Load a sprite sheet from Resources and slice it into individual frames.
    /// </summary>
    /// <param name="resourcePath">Path relative to Resources/ (no extension)</param>
    /// <param name="frameSize">Width and height of each frame in pixels</param>
    /// <returns>Array of sprites, one per frame, left-to-right</returns>
    public static Sprite[] LoadSheet(string resourcePath, int frameSize)
    {
        string key = $"{resourcePath}@{frameSize}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null)
        {
            Debug.LogWarning($"[PixelCrawlerSprites] Missing texture: {resourcePath}");
            return System.Array.Empty<Sprite>();
        }

        int frameCount = tex.width / frameSize;
        var sprites = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            var rect = new Rect(i * frameSize, 0, frameSize, tex.height);
            sprites[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), frameSize);
        }

        _cache[key] = sprites;
        return sprites;
    }

    /// <summary>
    /// Load player character sprites (Body_A). Returns idle and walk arrays
    /// for Down, Side, and Up directions.
    /// </summary>
    public static PlayerSpriteSet LoadPlayerSprites()
    {
        return new PlayerSpriteSet
        {
            IdleDown = LoadSheet("Art/PixelCrawler/Player/Idle_Down", 64),
            IdleSide = LoadSheet("Art/PixelCrawler/Player/Idle_Side", 64),
            IdleUp   = LoadSheet("Art/PixelCrawler/Player/Idle_Up",   64),
            WalkDown = LoadSheet("Art/PixelCrawler/Player/Walk_Down", 64),
            WalkSide = LoadSheet("Art/PixelCrawler/Player/Walk_Side", 64),
            WalkUp   = LoadSheet("Art/PixelCrawler/Player/Walk_Up",   64),
        };
    }

    /// <summary>
    /// Load NPC sprites (Knight, Rogue, or Wizzard). Front-facing only.
    /// </summary>
    public static NPCSpriteSet LoadNPCSprites(string npcType)
    {
        return new NPCSpriteSet
        {
            Idle = LoadSheet($"Art/PixelCrawler/NPCs/{npcType}/Idle", 32),
        };
    }

    /// <summary>
    /// Map NPC id to Anokolisa NPC sprite type.
    /// </summary>
    public static string GetNPCSpriteType(string npcId)
    {
        return npcId switch
        {
            "vassal_01"   => "Knight",
            "soldier_01"  => "Knight",
            "farmer_01"   => "Rogue",
            "merchant_01" => "Wizzard",
            _ => "Knight",
        };
    }

    /// <summary>
    /// Get a tint color to differentiate NPCs that share the same sprite type.
    /// </summary>
    public static Color GetNPCTint(string npcId)
    {
        return npcId switch
        {
            "vassal_01"   => Color.white,
            "soldier_01"  => new Color(0.85f, 0.92f, 1f),    // Slight blue tint
            "farmer_01"   => new Color(1f, 0.95f, 0.85f),    // Warm tint
            "merchant_01" => new Color(0.95f, 0.85f, 1f),    // Purple tint
            _ => Color.white,
        };
    }

    public class PlayerSpriteSet
    {
        public Sprite[] IdleDown, IdleSide, IdleUp;
        public Sprite[] WalkDown, WalkSide, WalkUp;
    }

    public class NPCSpriteSet
    {
        public Sprite[] Idle;
    }
}
