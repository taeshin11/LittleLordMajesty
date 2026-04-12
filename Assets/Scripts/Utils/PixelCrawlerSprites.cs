using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// DEPRECATED — 2D sprite loading from Kenney RPG Urban Pack.
/// The game has pivoted to 3D procedural primitives. This class remains
/// as a stub so any lingering references compile without error.
/// All methods return null. The old Art/Kenney/ resources are no longer used.
/// </summary>
public static class PixelCrawlerSprites
{
    public const float PPU = 16f;

    public static Sprite LoadSprite(string name) => null;

    public static PlayerSpriteSet LoadPlayerSprites() => new PlayerSpriteSet();

    public static NPCSpriteSet LoadNPCSprites(string npcId) => new NPCSpriteSet();

    public static Sprite GetNPCPortrait(string npcId) => null;

    public class PlayerSpriteSet
    {
        public Sprite Front, Back, Left, Right;
        public Sprite GetFacing(int facing) => null;
    }

    public class NPCSpriteSet
    {
        public Sprite Front, Back, Left, Right;
        public Sprite GetFacing(int facing) => null;
    }
}
