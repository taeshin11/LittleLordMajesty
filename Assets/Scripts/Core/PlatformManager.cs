using UnityEngine;
using System.IO;

/// <summary>
/// Abstracts platform differences between Mobile (Android/iOS) and Desktop (Steam/PC).
/// All systems that need platform-specific paths or features go through here.
/// </summary>
public static class PlatformManager
{
    public enum Platform { Mobile, Desktop }

    public static Platform Current
    {
        get
        {
#if UNITY_ANDROID || UNITY_IOS
            return Platform.Mobile;
#else
            return Platform.Desktop;
#endif
        }
    }

    public static bool IsMobile  => Current == Platform.Mobile;
    public static bool IsDesktop => Current == Platform.Desktop;
    public static bool IsSteam   => IsDesktop; // Extend with Steamworks check later

    // ── Save paths ────────────────────────────────────────────────

    /// <summary>Returns platform-appropriate persistent data directory.</summary>
    public static string SaveDirectory
    {
        get
        {
#if UNITY_STANDALONE_WIN
            // Windows: %APPDATA%/LittleLordMajesty
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData),
                "LittleLordMajesty");
#elif UNITY_STANDALONE_OSX
            return Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                "Library/Application Support/LittleLordMajesty");
#else
            return Application.persistentDataPath;
#endif
        }
    }

    // ── Display ───────────────────────────────────────────────────

    /// <summary>Reference resolution for CanvasScaler — portrait mobile vs landscape desktop.</summary>
    public static Vector2 ReferenceResolution => IsMobile
        ? new Vector2(1080, 1920)
        : new Vector2(1920, 1080);

    public static float CanvasMatchWidthOrHeight => IsMobile ? 0.5f : 0.5f;

    /// <summary>Minimum touch target size in pixels (Apple HIG: 44pt).</summary>
    public static float MinTouchTargetPx => IsMobile ? 88f : 32f;

    // ── Input ─────────────────────────────────────────────────────

    public static bool UsesTouchInput => Input.touchSupported && IsMobile;

    // ── Features ──────────────────────────────────────────────────

    /// <summary>True when running on a platform that can show a fullscreen/windowed toggle.</summary>
    public static bool SupportsWindowMode => IsDesktop;

    /// <summary>True on platforms with a Steam overlay available (future Steamworks integration).</summary>
    public static bool SteamAvailable => false; // Set to true after Steamworks.NET is integrated

    // ── Helpers ───────────────────────────────────────────────────

    public static void EnsureSaveDirectory()
    {
        if (!Directory.Exists(SaveDirectory))
            Directory.CreateDirectory(SaveDirectory);
    }
}
