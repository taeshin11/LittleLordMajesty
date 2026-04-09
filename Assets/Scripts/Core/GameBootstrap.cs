using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Entry point for the game. Attached to the Bootstrap scene's root object.
/// Initializes all singleton managers in the correct order and transitions to Main Menu.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [Header("Manager Prefabs")]
    [SerializeField] private GameObject _gameManagerPrefab;
    [SerializeField] private GameObject _geminiAPIPrefab;
    [SerializeField] private GameObject _ttsPrefab;
    [SerializeField] private GameObject _localizationPrefab;
    [SerializeField] private GameObject _firebasePrefab;

    [SerializeField] private GameObject _splashScreen;
    [SerializeField] private Slider _loadingBar;
    [SerializeField] private TMPro.TextMeshProUGUI _loadingText;

    // Debug overlay for WebGL — shows boot progress on screen
    private string _debugLog = "";

    private void Start()
    {
        Application.logMessageReceived += OnLogMessage;
        StartCoroutine(BootSequence());
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception)
            _debugLog += $"[{type}] {message}\n";
    }

    // Show debug info on screen for WebGL troubleshooting
    private void OnGUI()
    {
        if (string.IsNullOrEmpty(_debugLog)) return;
        GUI.color = Color.red;
        GUI.Label(new Rect(10, 10, Screen.width - 20, Screen.height / 2), _debugLog);
    }

    private IEnumerator BootSequence()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        yield return SetLoadingProgress(0.1f, "Initializing...");

        // Create managers in dependency order
        EnsureManager<LocalizationManager>(_localizationPrefab, "LocalizationManager");
        yield return SetLoadingProgress(0.25f, "Loading localization...");

        EnsureManager<GeminiAPIClient>(_geminiAPIPrefab, "GeminiAPIClient");
        yield return SetLoadingProgress(0.40f, "Connecting AI...");

        EnsureManager<TTSManager>(_ttsPrefab, "TTSManager");
        yield return SetLoadingProgress(0.55f, "Loading voice system...");

        EnsureManager<FirebaseManager>(_firebasePrefab, "FirebaseManager");
        yield return SetLoadingProgress(0.70f, "Connecting cloud...");

        // GameConfig check
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null)
        {
            config.LoadFromEnvironment();
            Debug.Log("[Bootstrap] GameConfig loaded.");
        }
        else
        {
            Debug.LogWarning("[Bootstrap] GameConfig not found at Resources/Config/GameConfig.");
        }

        yield return SetLoadingProgress(0.85f, "Preparing castle...");

        EnsureManager<GameManager>(_gameManagerPrefab, "GameManager");

        yield return SetLoadingProgress(1.0f, "Loading game world...");
        yield return new WaitForSeconds(0.3f);

        // Load Game.unity which contains UIManager, CastleScene3D, and all UI panels
        Debug.Log("[Bootstrap] Loading Game scene...");
        var asyncLoad = SceneManager.LoadSceneAsync("Game");
        if (asyncLoad == null)
        {
            Debug.LogError("[Bootstrap] Failed to load Game scene! Is it in Build Settings?");
            yield break;
        }

        while (!asyncLoad.isDone)
            yield return null;

        Debug.Log("[Bootstrap] Game scene loaded.");

        // Wait one frame for all Awake/Start to run
        yield return null;

        // Check UIManager
        if (UIManager.Instance == null)
        {
            Debug.LogError("[Bootstrap] UIManager.Instance is null after Game scene loaded!");
            yield break;
        }

        // Set initial state
        if (GameManager.Instance != null)
        {
            Debug.Log("[Bootstrap] Setting MainMenu state...");
            GameManager.Instance.SetGameState(GameManager.GameState.MainMenu);
        }

        Debug.Log("[Bootstrap] Game initialized successfully.");
    }

    private IEnumerator SetLoadingProgress(float target, string message)
    {
        if (_loadingText != null) _loadingText.text = message;
        if (_loadingBar != null)
        {
            float start = _loadingBar.value;
            float elapsed = 0f;
            while (elapsed < 0.3f)
            {
                elapsed += Time.deltaTime;
                _loadingBar.value = Mathf.Lerp(start, target, elapsed / 0.3f);
                yield return null;
            }
            _loadingBar.value = target;
        }
        else
        {
            yield return null;
        }
    }

    private void EnsureManager<T>(GameObject prefab, string name) where T : Component
    {
        if (FindObjectOfType<T>() != null) return;

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
        }
        else
        {
            go = new GameObject(name);
            go.AddComponent<T>();
        }
        DontDestroyOnLoad(go);
    }
}
