using UnityEngine;

/// <summary>
/// Loads pre-generated NPC portraits from Assets/Resources/Art/Generated/.
/// SDXL backgrounds retired in M16 pivot — only portraits remain.
/// </summary>
public static class LocalArtBank
{
    private const string ART_ROOT = "Art/Generated/";

    /// <summary>
    /// Returns the pre-generated portrait sprite for the given NPC id (e.g.
    /// "vassal_01"), or null if not baked.
    /// </summary>
    public static Sprite GetNPCPortrait(string npcId)
    {
        if (string.IsNullOrEmpty(npcId)) return null;
        return LoadAsSprite($"portrait_{npcId}");
    }

    // Stubs kept to avoid breaking callers — return null (callers already handle null).
    public static Sprite GetMainMenuBackground() => null;
    public static Sprite GetCastleBackground() => null;
    public static Sprite GetWorldMapBackground() => null;
    public static Sprite GetBattleFieldBackground() => null;

    private static Sprite LoadAsSprite(string name)
    {
        var tex = Resources.Load<Texture2D>(ART_ROOT + name);
        if (tex == null) return null;
        return Sprite.Create(tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f));
    }
}
