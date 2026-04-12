using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Generates 2D portrait/icon textures for UI panels at runtime.
/// The game world itself uses 3D primitives (see CastleScene3D).
/// Use this only for screen-space UI: NPC dialogue portraits, building menu icons.
/// </summary>
public static class PlaceholderArtGenerator
{
    // Cache to avoid regenerating the same texture every frame
    private static readonly Dictionary<string, Texture2D> _cache = new();
    private static readonly Dictionary<string, Sprite> _spriteCache = new();

    // ─────────────────────────────────────────────────────────────
    //  NPC SPRITES
    // ─────────────────────────────────────────────────────────────

    /// <summary>Returns a placeholder sprite for an NPC by profession.</summary>
    public static Sprite GetNPCSprite(NPCPersona.NPCProfession profession)
    {
        string key = $"npc_{profession}";
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        Color bodyColor = profession switch
        {
            NPCPersona.NPCProfession.Soldier  => new Color(0.5f, 0.25f, 0.15f),
            NPCPersona.NPCProfession.Farmer   => new Color(0.4f, 0.6f,  0.25f),
            NPCPersona.NPCProfession.Merchant => new Color(0.7f, 0.55f, 0.1f),
            NPCPersona.NPCProfession.Vassal   => new Color(0.4f, 0.3f,  0.6f),
            NPCPersona.NPCProfession.Scholar  => new Color(0.3f, 0.5f,  0.7f),
            NPCPersona.NPCProfession.Priest   => new Color(0.8f, 0.8f,  0.75f),
            NPCPersona.NPCProfession.Spy      => new Color(0.15f,0.15f, 0.2f),
            _                                  => new Color(0.5f, 0.5f,  0.5f),
        };

        var tex = GenerateCharacterTexture(bodyColor, 64, 96);
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0f), 100f);
        _spriteCache[key] = sprite;
        return sprite;
    }

    /// <summary>Returns a placeholder portrait sprite (square headshot).</summary>
    public static Sprite GetNPCPortrait(NPCPersona.NPCProfession profession, string npcName = "")
    {
        string key = $"portrait_{profession}_{npcName}";
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        Color c = GetProfessionColor(profession);
        var tex = new Texture2D(128, 128);
        var pixels = new Color[128 * 128];

        // Fill background
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = c * 0.7f;

        // Head circle
        for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
            {
                float dx = (x - 64) / 30f;
                float dy = (y - 80) / 30f;
                if (dx * dx + dy * dy < 1f)
                    pixels[y * 128 + x] = new Color(0.85f, 0.72f, 0.58f);
            }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;

        var sprite = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 100f);
        _spriteCache[key] = sprite;
        return sprite;
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILDING ICONS
    // ─────────────────────────────────────────────────────────────

    /// <summary>Returns a placeholder icon for a building type.</summary>
    public static Sprite GetBuildingIcon(BuildingManager.BuildingType type)
    {
        string key = $"building_{type}";
        if (_spriteCache.TryGetValue(key, out var cached)) return cached;

        Color c = type switch
        {
            BuildingManager.BuildingType.Sawmill   => new Color(0.5f, 0.3f, 0.1f),
            BuildingManager.BuildingType.Farm      => new Color(0.3f, 0.6f, 0.2f),
            BuildingManager.BuildingType.Market    => new Color(0.7f, 0.5f, 0.1f),
            BuildingManager.BuildingType.Mine      => new Color(0.4f, 0.4f, 0.5f),
            BuildingManager.BuildingType.Barracks  => new Color(0.5f, 0.2f, 0.1f),
            BuildingManager.BuildingType.Archery   => new Color(0.4f, 0.3f, 0.2f),
            BuildingManager.BuildingType.Stable    => new Color(0.5f, 0.35f, 0.15f),
            BuildingManager.BuildingType.Watchtower=> new Color(0.3f, 0.3f, 0.45f),
            BuildingManager.BuildingType.Warehouse => new Color(0.45f, 0.35f, 0.2f),
            BuildingManager.BuildingType.Granary   => new Color(0.6f, 0.5f, 0.2f),
            BuildingManager.BuildingType.Well      => new Color(0.2f, 0.4f, 0.6f),
            BuildingManager.BuildingType.Hospital  => new Color(0.7f, 0.2f, 0.2f),
            BuildingManager.BuildingType.ThroneRoom=> new Color(0.6f, 0.45f, 0.05f),
            BuildingManager.BuildingType.Library   => new Color(0.3f, 0.2f, 0.5f),
            BuildingManager.BuildingType.MageTower => new Color(0.5f, 0.2f, 0.7f),
            BuildingManager.BuildingType.CastleWalls=>new Color(0.4f, 0.4f, 0.45f),
            _                                       => Color.gray,
        };

        var tex = GenerateBuildingTexture(c, 64, 64);
        var sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        _spriteCache[key] = sprite;
        return sprite;
    }

    // ─────────────────────────────────────────────────────────────
    //  BACKGROUND GRADIENT
    // ─────────────────────────────────────────────────────────────

    /// <summary>Creates a vertical gradient texture for use as castle/map backgrounds.</summary>
    public static Texture2D GetCastleBackground()
    {
        const string key = "bg_castle";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        int w = 4, h = 256;
        var tex = new Texture2D(w, h);
        for (int y = 0; y < h; y++)
        {
            float t = y / (float)h;
            Color c = Color.Lerp(
                new Color(0.05f, 0.04f, 0.08f),   // dark bottom
                new Color(0.15f, 0.12f, 0.25f),   // lighter top (sky)
                t
            );
            for (int x = 0; x < w; x++)
                tex.SetPixel(x, y, c);
        }
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        _cache[key] = tex;
        return tex;
    }

    /// <summary>Applies placeholder sprites to all Image components in the scene that use the tag "PlaceholderNPC".</summary>
    public static void ApplyPlaceholderSpritesToScene()
    {
        // NPC portraits — look for Image components tagged or named "NPCPortrait"
        var portraits = Object.FindObjectsOfType<Image>();
        foreach (var img in portraits)
        {
            if (img.sprite != null) continue; // Already has a sprite

            if (img.gameObject.name.Contains("Portrait") || img.gameObject.name.Contains("Avatar"))
            {
                // Default to Soldier portrait if we can't determine profession
                img.sprite = GetNPCPortrait(NPCPersona.NPCProfession.Vassal);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  INTERNAL GENERATORS
    // ─────────────────────────────────────────────────────────────

    static Texture2D GenerateCharacterTexture(Color bodyColor, int w, int h)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];

        Color transparent = new Color(0, 0, 0, 0);
        Color skinColor   = new Color(0.85f, 0.72f, 0.58f);
        Color shadowColor = bodyColor * 0.6f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;  // 0-1
                float ny = y / (float)h;  // 0-1

                Color c = transparent;

                // Body (lower 60%)
                if (ny < 0.60f && nx > 0.15f && nx < 0.85f)
                {
                    c = bodyColor;
                    if (nx < 0.2f || nx > 0.8f) c = shadowColor; // sides darker
                }

                // Head (upper 20%)
                if (ny > 0.75f && ny < 0.98f)
                {
                    float dx = nx - 0.5f;
                    float dy = ny - 0.85f;
                    if (dx * dx / 0.04f + dy * dy / 0.025f < 1f)
                        c = skinColor;
                }

                pixels[y * w + x] = c;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    static Texture2D GenerateBuildingTexture(Color mainColor, int w, int h)
    {
        var tex = new Texture2D(w, h);
        var pixels = new Color[w * h];
        Color roofColor   = mainColor * 0.7f;
        Color windowColor = new Color(0.9f, 0.85f, 0.5f, 0.9f);
        Color doorColor   = new Color(0.2f, 0.12f, 0.05f);

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float nx = x / (float)w;
                float ny = y / (float)h;
                Color c = new Color(0, 0, 0, 0);

                bool inWall = nx > 0.05f && nx < 0.95f && ny < 0.65f;
                bool inRoof = ny > 0.55f && (ny - 0.55f) < (0.5f - Mathf.Abs(nx - 0.5f));
                bool inWindow = (Mathf.Abs(nx - 0.3f) < 0.08f || Mathf.Abs(nx - 0.7f) < 0.08f)
                                 && ny > 0.3f && ny < 0.5f;
                bool inDoor = Mathf.Abs(nx - 0.5f) < 0.1f && ny < 0.25f;

                if (inRoof)  c = roofColor;
                if (inWall)  c = mainColor;
                if (inWindow) c = windowColor;
                if (inDoor)  c = doorColor;

                pixels[y * w + x] = c;
            }

        tex.SetPixels(pixels);
        tex.Apply();
        tex.filterMode = FilterMode.Point;
        return tex;
    }

    static Color GetProfessionColor(NPCPersona.NPCProfession profession)
    {
        return profession switch
        {
            NPCPersona.NPCProfession.Soldier  => new Color(0.5f, 0.25f, 0.15f),
            NPCPersona.NPCProfession.Farmer   => new Color(0.35f, 0.55f, 0.2f),
            NPCPersona.NPCProfession.Merchant => new Color(0.65f, 0.5f, 0.1f),
            NPCPersona.NPCProfession.Vassal   => new Color(0.4f, 0.3f, 0.6f),
            NPCPersona.NPCProfession.Scholar  => new Color(0.25f, 0.45f, 0.65f),
            NPCPersona.NPCProfession.Priest   => new Color(0.75f, 0.75f, 0.7f),
            NPCPersona.NPCProfession.Spy      => new Color(0.15f, 0.15f, 0.2f),
            _                                  => Color.gray,
        };
    }

    /// <summary>Clears all cached textures and sprites (call when unloading).</summary>
    public static void ClearCache()
    {
        foreach (var tex in _cache.Values)
            if (tex != null) Object.Destroy(tex);
        _cache.Clear();
        _spriteCache.Clear();
    }
}
