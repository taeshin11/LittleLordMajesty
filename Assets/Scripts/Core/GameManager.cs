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
        _dayCycleCoroutine = StartCoroutine(DayCycleCoroutine());
    }

    private void OnDestroy()
    {
        if (_dayCycleCoroutine != null)
            StopCoroutine(_dayCycleCoroutine);
    }

    private void Update()
    {
        if (_currentState != GameState.Paused && _currentState != GameState.MainMenu)
            PlayTimeSeconds += Time.deltaTime;
    }

    private void InitializeSystems()
    {
        // Systems auto-find or create themselves
        if (ResourceManager == null)
            ResourceManager = gameObject.AddComponent<ResourceManager>();
        if (NPCManager == null)
            NPCManager = gameObject.AddComponent<NPCManager>();
        if (EventManager == null)
            EventManager = gameObject.AddComponent<EventManager>();
    }

    public void SetGameState(GameState newState)
    {
        GameState oldState = _currentState;
        _currentState = newState;
        OnGameStateChanged?.Invoke(oldState, newState);
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
        while (true)
        {
            yield return new WaitForSeconds(dayDuration);
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
        PlayerName = playerName;
        Day = 1;
        Year = 1;
        PlayTimeSeconds = 0f;
        LordTitle = "Little Lord";
        ResourceManager?.ResetToDefault();
        NPCManager?.InitializeStartingNPCs();
        SetGameState(GameState.Castle);
    }
}
