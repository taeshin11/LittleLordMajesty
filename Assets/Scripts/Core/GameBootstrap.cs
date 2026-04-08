using UnityEngine;
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
    [SerializeField] private UnityEngine.UI.Slider _loadingBar;
    [SerializeField] private TMPro.TextMeshProUGUI _loadingText;

    private void Start()
    {
        StartCoroutine(BootSequence());
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
            Debug.LogWarning("[Bootstrap] GameConfig not found at Resources/Config/GameConfig. Create one!");
        }

        yield return SetLoadingProgress(0.85f, "Preparing castle...");

        EnsureManager<GameManager>(_gameManagerPrefab, "GameManager");

        yield return SetLoadingProgress(1.0f, "Ready!");
        yield return new WaitForSeconds(0.5f);

        // Hide splash and go to main menu
        if (_splashScreen != null) _splashScreen.SetActive(false);

        // Load main menu scene or activate UI
        if (GameManager.Instance != null)
            GameManager.Instance.SetGameState(GameManager.GameState.MainMenu);

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
