using UnityEngine;
using System;

/// <summary>
/// Steam integration via Facepunch.Steamworks.
/// Install: https://github.com/Facepunch/Facepunch.Steamworks
/// Add Facepunch.Steamworks.dll to Assets/Plugins/ before enabling USE_STEAM.
///
/// Usage:
///   SteamManager.UnlockAchievement("FIRST_NPC");
///   SteamManager.SubmitScore("MostTerritories", 5);
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static SteamManager Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private uint _appId = 480; // Replace with real Steam AppID

    public bool IsInitialized { get; private set; }

    // ─── Achievement IDs (match Steam dashboard) ──────────────────
    public const string ACH_FIRST_NPC       = "ACH_FIRST_NPC";
    public const string ACH_FIRST_BUILDING  = "ACH_FIRST_BUILDING";
    public const string ACH_FIRST_CONQUEST  = "ACH_FIRST_CONQUEST";
    public const string ACH_ALLY_MADE       = "ACH_ALLY_MADE";
    public const string ACH_SPY_SENT        = "ACH_SPY_SENT";
    public const string ACH_10_DAYS         = "ACH_10_DAYS";
    public const string ACH_RICH_LORD       = "ACH_RICH_LORD";   // 1000 gold
    public const string ACH_FULL_LOYALTY    = "ACH_FULL_LOYALTY";
    public const string ACH_VICTORY         = "ACH_VICTORY";
    public const string ACH_CONQUEROR       = "ACH_CONQUEROR";   // 10 territories

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if USE_STEAM
        InitializeSteam();
#else
        Debug.Log("[SteamManager] Steam disabled. Add USE_STEAM scripting symbol to enable.");
#endif
    }

#if USE_STEAM
    private void InitializeSteam()
    {
        try
        {
            Steamworks.SteamClient.Init(_appId);
            IsInitialized = true;
            Debug.Log($"[SteamManager] Initialized. User: {Steamworks.SteamClient.Name}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SteamManager] Init failed (Steam not running?): {ex.Message}");
            IsInitialized = false;
        }
    }

    private void Update()
    {
        if (IsInitialized)
            Steamworks.SteamClient.RunCallbacks();
    }

    private void OnDestroy()
    {
        if (IsInitialized)
            Steamworks.SteamClient.Shutdown();
    }
#endif

    // ─── Public API ───────────────────────────────────────────────

    public static void UnlockAchievement(string achievementId)
    {
#if USE_STEAM
        if (Instance == null || !Instance.IsInitialized) return;
        try
        {
            var ach = new Steamworks.Data.Achievement(achievementId);
            if (!ach.State)
            {
                ach.Trigger();
                Debug.Log($"[SteamManager] Achievement unlocked: {achievementId}");
                ToastNotification.Show($"Achievement unlocked!", ToastNotification.ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SteamManager] Achievement failed: {ex.Message}");
        }
#endif
    }

    public static void SubmitScore(string leaderboardName, int score)
    {
#if USE_STEAM
        if (Instance == null || !Instance.IsInitialized) return;
        Instance.SubmitScoreAsync(leaderboardName, score);
#endif
    }

#if USE_STEAM
    private async void SubmitScoreAsync(string leaderboardName, int score)
    {
        try
        {
            var board = await Steamworks.SteamUserStats.FindLeaderboardAsync(leaderboardName);
            if (board.HasValue)
                await board.Value.SubmitScoreAsync(score);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SteamManager] Leaderboard submit failed: {ex.Message}");
        }
    }
#endif

    // ─── Game event hooks (called from game systems) ──────────────

    public static void OnFirstNPCDialogue()     => UnlockAchievement(ACH_FIRST_NPC);
    public static void OnFirstBuildingBuilt()   => UnlockAchievement(ACH_FIRST_BUILDING);
    public static void OnTerritoryConquered()   => UnlockAchievement(ACH_FIRST_CONQUEST);
    public static void OnAllianceMade()         => UnlockAchievement(ACH_ALLY_MADE);
    public static void OnSpySent()              => UnlockAchievement(ACH_SPY_SENT);
    public static void OnVictory()              => UnlockAchievement(ACH_VICTORY);

    public static void OnDayReached(int day)
    {
        if (day >= 10) UnlockAchievement(ACH_10_DAYS);
    }

    public static void OnGoldReached(int gold)
    {
        if (gold >= 1000) UnlockAchievement(ACH_RICH_LORD);
    }

    public static void OnTerritoriesConquered(int count)
    {
        if (count >= 10) UnlockAchievement(ACH_CONQUEROR);
        SubmitScore("MostTerritories", count);
    }
}
