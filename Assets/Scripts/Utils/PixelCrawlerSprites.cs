using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility to load Kenney RPG Urban Pack sprites at runtime.
/// Each sprite is a separate 16x16 PNG file loaded from Resources/Art/Kenney/.
/// Provides player and NPC directional sprites (front, back, left, right).
/// Replaces the old Anokolisa sheet-slicing approach.
/// </summary>
public static class PixelCrawlerSprites
{
    private static readonly Dictionary<string, Sprite> _cache = new();

    // 16 PPU = 1 Kenney tile (16px) per world unit.
    public const float PPU = 16f;

    /// <summary>
    /// Load a single sprite PNG from Resources/Art/Kenney/.
    /// </summary>
    public static Sprite LoadSprite(string name)
    {
        if (_cache.TryGetValue(name, out var cached)) return cached;

        string path = $"Art/Kenney/{name}";
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogWarning($"[KenneySprites] Missing texture: {path}");
            return null;
        }

        var sprite = Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), PPU);
        sprite.name = name;
        _cache[name] = sprite;
        return sprite;
    }

    /// <summary>
    /// Load player character sprites — 4 directions, 1 frame each.
    /// </summary>
    public static PlayerSpriteSet LoadPlayerSprites()
    {
        return new PlayerSpriteSet
        {
            Front = LoadSprite("player_front"),
            Back  = LoadSprite("player_back"),
            Left  = LoadSprite("player_left"),
            Right = LoadSprite("player_right"),
        };
    }

    /// <summary>
    /// Load NPC sprites — 4 directions, 1 frame each.
    /// NPC id maps directly to file names (e.g. vassal_01_front).
    /// </summary>
    public static NPCSpriteSet LoadNPCSprites(string npcId)
    {
        return new NPCSpriteSet
        {
            Front = LoadSprite($"{npcId}_front"),
            Back  = LoadSprite($"{npcId}_back"),
            Left  = LoadSprite($"{npcId}_left"),
            Right = LoadSprite($"{npcId}_right"),
        };
    }

    /// <summary>
    /// Get front-facing sprite for an NPC (for portraits, etc.)
    /// </summary>
    public static Sprite GetNPCPortrait(string npcId)
    {
        return LoadSprite($"{npcId}_front");
    }

    public class PlayerSpriteSet
    {
        public Sprite Front, Back, Left, Right;

        public Sprite GetFacing(int facing)
        {
            return facing switch
            {
                0 => Front, // Down
                1 => Back,  // Up
                2 => Right,
                3 => Left,
                _ => Front,
            };
        }
    }

    public class NPCSpriteSet
    {
        public Sprite Front, Back, Left, Right;

        public Sprite GetFacing(int facing)
        {
            return facing switch
            {
                0 => Front,
                1 => Back,
                2 => Right,
                3 => Left,
                _ => Front,
            };
        }
    }
}
