using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using TMPro;

/// <summary>
/// Entry point for the game. Initializes managers and builds runtime UI.
/// No dependency on scene-wired SerializedFields — everything created from code.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject _splashScreen;
    [SerializeField] private Slider _loadingBar;
    [SerializeField] private TextMeshProUGUI _loadingText;

    private static bool _hasBooted = false;
    private string _statusText = "Booting...";

    private void Start()
    {
        if (_hasBooted) return;
        _hasBooted = true;
        StartCoroutine(BootSequence());
    }

    // Always-visible debug overlay
    private void OnGUI()
    {
        GUI.color = Color.yellow;
        GUI.Label(new Rect(10, 10, 600, 30), _statusText);
    }

    private IEnumerator BootSequence()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        _statusText = "Creating managers...";
        yield return null;

        // Create managers in dependency order
        EnsureManager<LocalizationManager>("LocalizationManager");
        SetProgress(0.2f, "Localization...");
        yield return null;

        EnsureManager<GeminiAPIClient>("GeminiAPIClient");
        SetProgress(0.4f, "AI system...");
        yield return null;

        EnsureManager<TTSManager>("TTSManager");
        EnsureManager<FirebaseManager>("FirebaseManager");
        SetProgress(0.6f, "Cloud services...");
        yield return null;

        // GameConfig
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null) config.LoadFromEnvironment();

        EnsureManager<GameManager>("GameManager");
        SetProgress(0.8f, "Game systems...");
        yield return null;

        _statusText = "Loading game world...";
        SetProgress(1.0f, "Ready!");
        yield return new WaitForSeconds(0.3f);

        // Hide splash
        if (_splashScreen != null) _splashScreen.SetActive(false);

        // Try loading Game scene
        _statusText = "Loading Game scene...";
        yield return null;

        var asyncLoad = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
        if (asyncLoad != null)
        {
            while (!asyncLoad.isDone)
            {
                _statusText = $"Loading scene... {asyncLoad.progress * 100:F0}%";
                yield return null;
            }
            _statusText = "Game scene loaded!";
            yield return null; // Wait for Awake/Start
            yield return null; // Extra frame

            // Check if UIManager exists
            if (UIManager.Instance != null)
            {
                _statusText = "UIManager found, setting MainMenu...";
                GameManager.Instance?.SetGameState(GameManager.GameState.MainMenu);
                _statusText = "Running!";
                yield break;
            }
            _statusText = "UIManager NOT found — building fallback UI...";
        }
        else
        {
            _statusText = "Game scene not in build — building fallback UI...";
        }

        // FALLBACK: Build UI from code if scene loading failed
        yield return null;
        BuildFallbackMainMenu();
    }

    private void BuildFallbackMainMenu()
    {
        _statusText = "Fallback UI active";

        // Create Canvas
        var canvasGO = new GameObject("FallbackCanvas");
        DontDestroyOnLoad(canvasGO);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Dark background
        var bg = CreateUIElement<Image>(canvasGO.transform, "Background");
        bg.color = new Color(0.05f, 0.04f, 0.08f);
        var bgRT = bg.rectTransform;
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        // Title
        var titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        var title = titleGO.AddComponent<TextMeshProUGUI>();
        title.text = "Little Lord\nMajesty";
        title.fontSize = 72;
        title.alignment = TextAlignmentOptions.Center;
        title.color = new Color(0.9f, 0.75f, 0.2f);
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0.2f, 0.6f); titleRT.anchorMax = new Vector2(0.8f, 0.85f);
        titleRT.offsetMin = Vector2.zero; titleRT.offsetMax = Vector2.zero;

        // Subtitle
        var subGO = new GameObject("Subtitle");
        subGO.transform.SetParent(canvasGO.transform, false);
        var sub = subGO.AddComponent<TextMeshProUGUI>();
        sub.text = "AI-Powered Kingdom Sim";
        sub.fontSize = 24;
        sub.alignment = TextAlignmentOptions.Center;
        sub.color = new Color(0.6f, 0.6f, 0.7f);
        var subRT = sub.rectTransform;
        subRT.anchorMin = new Vector2(0.25f, 0.55f); subRT.anchorMax = new Vector2(0.75f, 0.6f);
        subRT.offsetMin = Vector2.zero; subRT.offsetMax = Vector2.zero;

        // New Game button
        CreateButton(canvasGO.transform, "New Game", new Color(0.3f, 0.6f, 0.2f),
            new Vector2(0.3f, 0.35f), new Vector2(0.7f, 0.45f), () =>
        {
            _statusText = "Starting new game...";
            Destroy(canvasGO);
            GameManager.Instance?.NewGame("Lord Player");
        });

        // Continue button
        CreateButton(canvasGO.transform, "Continue", new Color(0.2f, 0.3f, 0.6f),
            new Vector2(0.3f, 0.22f), new Vector2(0.7f, 0.32f), () =>
        {
            if (SaveSystem.HasSaveFile())
            {
                Destroy(canvasGO);
                GameManager.Instance?.LoadGame();
            }
        });

        // Info text
        var infoGO = new GameObject("Info");
        infoGO.transform.SetParent(canvasGO.transform, false);
        var info = infoGO.AddComponent<TextMeshProUGUI>();
        info.text = "WebGL Alpha Build";
        info.fontSize = 18;
        info.alignment = TextAlignmentOptions.Center;
        info.color = new Color(0.4f, 0.4f, 0.4f);
        var infoRT = info.rectTransform;
        infoRT.anchorMin = new Vector2(0.3f, 0.05f); infoRT.anchorMax = new Vector2(0.7f, 0.1f);
        infoRT.offsetMin = Vector2.zero; infoRT.offsetMax = Vector2.zero;
    }

    private void CreateButton(Transform parent, string text, Color color,
        Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction onClick)
    {
        var btnGO = new GameObject(text + "Button");
        btnGO.transform.SetParent(parent, false);
        var img = btnGO.AddComponent<Image>();
        img.color = color;
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var rt = btnGO.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 32;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        var labelRT = label.rectTransform;
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
    }

    private T CreateUIElement<T>(Transform parent, string name) where T : Graphic
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }

    private void SetProgress(float value, string text)
    {
        if (_loadingBar != null) _loadingBar.value = value;
        if (_loadingText != null) _loadingText.text = text;
        _statusText = text;
    }

    private void EnsureManager<T>(string name) where T : Component
    {
        if (FindObjectOfType<T>() != null) return;
        var go = new GameObject(name);
        go.AddComponent<T>();
        DontDestroyOnLoad(go);
    }
}
