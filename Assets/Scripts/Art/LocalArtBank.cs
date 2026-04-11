using UnityEngine;

/// <summary>
/// Loads pre-generated art (backgrounds + NPC portraits) from
/// Assets/Resources/Art/Generated/, baked offline by
/// tools/image_gen/generate.py against a local SDXL Turbo model.
///
/// Used as the first-choice path for art so the game doesn't have to
/// hit the Gemini Image API for every castle entry / NPC tap. Falls
/// back to caller-supplied logic (Gemini, placeholder) if not found.
/// </summary>
public static class LocalArtBank
{
    private const string ART_ROOT = "Art/Generated/";

    /// <summary>Returns the pre-generated castle courtyard background, or null if missing.</summary>
    public static Sprite GetCastleBackground() => LoadAsSprite("bg_castle_courtyard");

    /// <summary>Returns the pre-generated world map background, or null if missing.</summary>
    public static Sprite GetWorldMapBackground() => LoadAsSprite("bg_world_map");

    /// <summary>Returns the pre-generated battle field background, or null if missing.</summary>
    public static Sprite GetBattleFieldBackground() => LoadAsSprite("bg_battle_field");

    /// <summary>
    /// Returns the pre-generated portrait sprite for the given NPC id (e.g.
    /// "vassal_01"), or null if not baked. Caller should fall back to a
    /// placeholder or live image generation.
    /// </summary>
    public static Sprite GetNPCPortrait(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        return LoadAsSprite($"portrait_{npcId}");
    }

    private static Sprite LoadAsSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(ART_ROOT + name);
        if (tex == null) return null;
        return Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
    }
}
