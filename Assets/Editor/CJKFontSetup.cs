using UnityEngine;
using UnityEditor;
using TMPro;
using System.IO;

/// <summary>
/// Generates a dynamic TMP_FontAsset from NotoSansKR-Regular.otf and wires it
/// as a fallback on LiberationSans SDF so Korean/Japanese/Chinese glyphs render
/// correctly in every TMP label across the project.
///
/// Run via:
///   Menu: "LittleLordMajesty > Generate CJK Font Asset"
///   CLI:  Unity -batchmode -executeMethod CJKFontSetup.GenerateFontAsset -quit
/// </summary>
public static class CJKFontSetup
{
    private const string SOURCE_FONT_PATH = "Assets/Fonts/NotoSansKR-Regular.otf";
    private const string FONT_ASSET_PATH  = "Assets/Fonts/NotoSansKR SDF.asset";
    private const string LIBERATION_PATH  = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

    [MenuItem("LittleLordMajesty/Generate CJK Font Asset")]
    public static void GenerateFontAsset()
    {
        if (!File.Exists(SOURCE_FONT_PATH))
        {
            Debug.LogError($"[CJKFontSetup] Source font not found at {SOURCE_FONT_PATH}");
            return;
        }

        // Force import as a Unity Font asset
        AssetDatabase.ImportAsset(SOURCE_FONT_PATH, ImportAssetOptions.ForceUpdate);
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SOURCE_FONT_PATH);
        if (sourceFont == null)
        {
            Debug.LogError($"[CJKFontSetup] Failed to load Font at {SOURCE_FONT_PATH}");
            return;
        }

        // Create or refresh the dynamic TMP font asset.
        // Dynamic atlas: glyphs get rasterized on demand at runtime — perfect for CJK
        // where pre-baking 11k+ glyphs would blow the atlas budget.
        TMP_FontAsset cjkAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_ASSET_PATH);
        if (cjkAsset == null)
        {
            cjkAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                samplingPointSize: 90,
                atlasPadding: 9,
                renderMode: UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            AssetDatabase.CreateAsset(cjkAsset, FONT_ASSET_PATH);
            Debug.Log($"[CJKFontSetup] Created {FONT_ASSET_PATH}");
        }
        else
        {
            Debug.Log($"[CJKFontSetup] Reusing existing {FONT_ASSET_PATH}");
        }

        // Wire as fallback on LiberationSans SDF so any missing glyph on the default
        // font falls through to Noto Sans KR automatically.
        var liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LIBERATION_PATH);
        if (liberation == null)
        {
            Debug.LogError($"[CJKFontSetup] LiberationSans SDF not found at {LIBERATION_PATH}");
            return;
        }

        if (liberation.fallbackFontAssetTable == null)
            liberation.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();

        if (!liberation.fallbackFontAssetTable.Contains(cjkAsset))
        {
            liberation.fallbackFontAssetTable.Add(cjkAsset);
            EditorUtility.SetDirty(liberation);
            Debug.Log("[CJKFontSetup] Added NotoSansKR as fallback on LiberationSans SDF");
        }
        else
        {
            Debug.Log("[CJKFontSetup] NotoSansKR already registered as fallback");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[CJKFontSetup] Done. Korean/Japanese/Chinese glyphs will now render.");
    }
}
