using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Central game manager - singleton that persists across scenes.
/// Coordinates all major systems and manages game state.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState
    {
        MainMenu,
        Loading,
        Castle,       // Internal affairs view
        WorldMap,     // Territory conquest view
        Battle,       // Active siege/defense
        Dialogue,     // NPC interaction
        Event,        // Crisis/unexpected event
        Paused,
        GameOver,
        Victory
    }

    [Header("Game State")]
    [SerializeField] private GameState _currentState = GameState.MainMenu;
    public GameState CurrentState => _currentState;
    private GameState _prepauseState = GameState.Castle; // Restored on unpause
    private Coroutine _dayCycleCoroutine;

    [Header("Player Progress")]
    public string LordTitle = "Little Lord";
    public string PlayerName = "Stranger";
    public int Day = 1;
    public int Year = 1;
    public float PlayTimeSeconds = 0f;

    [Header("Systems")]
    public ResourceManager ResourceManager;
    public NPCManager NPCManager;
    public EventManager EventManager;
    public WorldMapManager WorldMapManager;

    public event Action<GameState, GameState> OnGameStateChanged;
    public event Action<int> OnDayChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeSystems();
    }

    private void Start()
    {
        // DayCycle starts only when entering Castle/WorldMap, not during MainMenu
    }

    private void OnDestroy()
    {
        if (_dayCycleCoroutine != null)
            StopCoroutine(_dayCycleCoroutine);
    }

    // Crash-bisect: count frames after first entering Castle so the log tells us
    // exactly which frame the null-function crash fires on. We also tag the
    // "about to run PlayTimeSeconds++" line so stack order is unambiguous.
    private int _framesSinceCastleEntry = -1;

    private void Update()
    {
        if (_currentState == GameState.Castle && _framesSinceCastleEntry < 8)
        {
            _framesSinceCastleEntry++;
            Debug.Log($"[Crash-Bisect] Castle frame #{_framesSinceCastleEntry} Update");
        }
        if (_currentState != GameState.Paused && _currentState != GameState.MainMenu)
            PlayTimeSeconds += Time.deltaTime;
    }

    private void LateUpdate()
    {
        if (_currentState == GameState.Castle && _framesSinceCastleEntry < 8)
            Debug.Log($"[Crash-Bisect] Castle frame #{_framesSinceCastleEntry} LateUpdate");
    }

#if !UNITY_EDITOR
    // WebGL debug overlay — shows game state to diagnose black screen.
    // GUIStyle is lazily cached (GUI.skin is not safe to touch before first OnGUI),
    // and the Canvas reference is cached to avoid per-frame FindObjectOfType allocations.
    private static GUIStyle _debugStyle;
    private Canvas _cachedCanvas;
    private float _nextCanvasRefresh;

    private void OnGUI()
    {
        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle(GUI.skin.label) { fontSize = 18 };
            _debugStyle.normal.textColor = Color.yellow;
        }
        if (_cachedCanvas == null && Time.unscaledTime >= _nextCanvasRefresh)
        {
            _cachedCanvas = FindFirstObjectByType<Canvas>();
            _nextCanvasRefresh = Time.unscaledTime + 1f;
        }
        float y = 10;
        GUI.Label(new Rect(10, y, 800, 30), "State: " + _currentState, _debugStyle); y += 25;
        GUI.Label(new Rect(10, y, 800, 30), "UIManager: " + (UIManager.Instance != null ? "OK" : "NULL"), _debugStyle); y += 25;
        GUI.Label(new Rect(10, y, 800, 30), "Scene: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, _debugStyle); y += 25;
        GUI.Label(new Rect(10, y, 800, 30), "Cameras: " + Camera.allCamerasCount, _debugStyle); y += 25;
        GUI.Label(new Rect(10, y, 800, 30), _cachedCanvas != null ? "Canvas: " + _cachedCanvas.name + " " + _cachedCanvas.renderMode : "Canvas: NULL", _debugStyle);
    }
#endif

    private void InitializeSystems()
    {
        // Systems auto-find or create themselves
        if (ResourceManager == null)
            ResourceManager = gameObject.AddComponent<ResourceManager>();
        if (NPCManager == null)
            NPCManager = gameObject.AddComponent<NPCManager>();
        if (EventManager == null)
            EventManager = gameObject.AddComponent<EventManager>();
        if (WorldMapManager == null)
            WorldMapManager = gameObject.AddComponent<WorldMapManager>();
    }

    public void SetGameState(GameState newState)
    {
        GameState oldState = _currentState;
        _currentState = newState;
        OnGameStateChanged?.Invoke(oldState, newState);

        // Start/stop day cycle based on gameplay state
        bool needsDayCycle = newState == GameState.Castle || newState == GameState.WorldMap
                          || newState == GameState.Dialogue || newState == GameState.Event;
        if (needsDayCycle && _dayCycleCoroutine == null)
            _dayCycleCoroutine = StartCoroutine(DayCycleCoroutine());
        else if (!needsDayCycle && _dayCycleCoroutine != null)
        {
            StopCoroutine(_dayCycleCoroutine);
            _dayCycleCoroutine = null;
        }

        Debug.Log($"[GameManager] State: {oldState} -> {newState}");
    }

    public void TogglePause()
    {
        if (_currentState == GameState.Paused)
        {
            SetGameState(_prepauseState);
            Time.timeScale = 1f;
        }
        else
        {
            _prepauseState = _currentState;
            SetGameState(GameState.Paused);
            Time.timeScale = 0f;
        }
    }

    private IEnumerator DayCycleCoroutine()
    {
        float dayDuration = 300f; // 5 real minutes = 1 in-game day
        while (this != null && enabled)
        {
            yield return new WaitForSeconds(dayDuration);
            if (this == null || !enabled) yield break;
            AdvanceDay();
        }
    }

    public void AdvanceDay()
    {
        Day++;
        if (Day > 365)
        {
            Day = 1;
            Year++;
        }
        OnDayChanged?.Invoke(Day);
        EventManager?.CheckDailyEvents(Day, Year);
        ResourceManager?.ProcessDailyProduction();
        SteamManager.OnDayReached(Day);
        if (ResourceManager != null) SteamManager.OnGoldReached(ResourceManager.Gold);
        Debug.Log($"[GameManager] Day {Day}, Year {Year}");
    }

    /// <summary>Updates the lord title based on territory count.</summary>
    public void UpdateLordTitle(int territoryCount)
    {
        if (territoryCount >= 10) LordTitle = "Majesty";
        else if (territoryCount >= 7) LordTitle = "High Lord";
        else if (territoryCount >= 5) LordTitle = "Lord";
        else if (territoryCount >= 3) LordTitle = "Baron";
        else LordTitle = "Little Lord";
    }

    public string GetFormattedDate()
    {
        return LocalizationManager.Instance != null
            ? LocalizationManager.Instance.GetFormattedDate(Day, Year)
            : $"Year {Year}, Day {Day}";
    }

    public void SaveGame() => SaveSystem.Save();
    public void LoadGame() => SaveSystem.Load();

    public void NewGame(string playerName)
    {
        // Verbose diagnostic logging during the NewGame flow — every step is
        // a candidate for the wasm "null function" runtime crash, so we tag
        // each one and the Playwright console-log scraper can pinpoint which
        // step was the LAST to log before the crash.
        Debug.Log("[NewGame] STEP 1: setting fields");
        PlayerName = playerName;
        Day = 1;
        Year = 1;
        PlayTimeSeconds = 0f;
        LordTitle = "Little Lord";

        Debug.Log("[NewGame] STEP 2: ResourceManager.ResetToDefault");
        try { ResourceManager?.ResetToDefault(); }
        catch (System.Exception e) { Debug.LogError($"[NewGame] ResetToDefault: {e}"); }

        Debug.Log("[NewGame] STEP 3: NPCManager.InitializeStartingNPCs");
        try { NPCManager?.InitializeStartingNPCs(); }
        catch (System.Exception e) { Debug.LogError($"[NewGame] InitializeStartingNPCs: {e}"); }

        Debug.Log("[NewGame] STEP 4: EventManager.ClearActiveEvents");
        try { EventManager?.ClearActiveEvents(); }
        catch (System.Exception e) { Debug.LogError($"[NewGame] ClearActiveEvents: {e}"); }

        Debug.Log("[NewGame] STEP 5: SetGameState(Castle)");
        try { SetGameState(GameState.Castle); }
        catch (System.Exception e) { Debug.LogError($"[NewGame] SetGameState: {e}"); }

        Debug.Log("[NewGame] STEP 6: Tutorial reset+start");
#if !UNITY_WEBGL || UNITY_EDITOR
        if (TutorialSystem.Instance != null)
        {
            try { TutorialSystem.Instance.ResetTutorial(); }
            catch (System.Exception e) { Debug.LogError($"[NewGame] ResetTutorial: {e}"); }
            try { TutorialSystem.Instance.StartTutorial(); }
            catch (System.Exception e) { Debug.LogError($"[NewGame] StartTutorial: {e}"); }
        }
#else
        // WebGL: skip tutorial. Activating the TutorialOverlay (which has
        // Outline on its dialogue box + many child TMP labels) intermittently
        // trips the IL2CPP wasm "null function" crash on the first render.
        // The tutorial is a nice-to-have; the core gameplay works fine
        // without it on WebGL. Re-enable when the root cause is isolated.
        Debug.Log("[GameManager] Skipping Tutorial on WebGL (wasm crash workaround)");
#endif
        Debug.Log("[NewGame] STEP 7: NewGame() complete");
    }
}
