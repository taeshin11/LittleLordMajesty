using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// In-game debug console. Visible only in development builds and Editor.
/// Toggle with triple-tap anywhere or the ` key.
/// Displays Unity log messages and accepts cheat commands.
/// </summary>
public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [Header("UI References — auto-created if null")]
    [SerializeField] private Canvas       _canvas;
    [SerializeField] private GameObject   _panel;
    [SerializeField] private ScrollRect   _scrollRect;
    [SerializeField] private Transform    _logContent;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private Button       _closeButton;

    private const int MAX_LINES = 200;
    private readonly Queue<string> _logLines = new();
    private readonly List<GameObject> _lineObjects = new();

    private bool  _visible;
    private int   _tapCount;
    private float _tapResetTimer;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.logMessageReceived += OnLogMessage;

        if (_panel == null) BuildUI();
        Hide();
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    private static int _bisectUpdateCount = 0;
    private void Update()
    {
        if (_bisectUpdateCount < 6) { _bisectUpdateCount++; Debug.Log($"[Crash-Bisect] DebugConsole.Update #{_bisectUpdateCount}"); }
        // Keyboard toggle
        if (Input.GetKeyDown(KeyCode.BackQuote)) Toggle();

        // Triple-tap toggle
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            _tapCount++;
            _tapResetTimer = 0.5f;
        }
        if (_tapResetTimer > 0)
        {
            _tapResetTimer -= Time.unscaledDeltaTime;
            if (_tapResetTimer <= 0) _tapCount = 0;
        }
        if (_tapCount >= 5) { Toggle(); _tapCount = 0; }
    }

    // ─────────────────────────────────────────────────────────────
    //  LOG RECEIVER
    // ─────────────────────────────────────────────────────────────

    private void OnLogMessage(string message, string stackTrace, LogType type)
    {
        string prefix = type switch
        {
            LogType.Error   => "<color=#ff5555>[ERR]</color> ",
            LogType.Warning => "<color=#ffaa33>[WRN]</color> ",
            LogType.Assert  => "<color=#ff33ff>[AST]</color> ",
            _               => "<color=#aaaaff>[LOG]</color> ",
        };

        string line = prefix + message;
        _logLines.Enqueue(line);
        while (_logLines.Count > MAX_LINES) _logLines.Dequeue();

        if (_visible) AppendLineToUI(line);
    }

    // ─────────────────────────────────────────────────────────────
    //  COMMANDS
    // ─────────────────────────────────────────────────────────────

    private void ExecuteCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return;
        Log($"<color=#88ff88>> {cmd}</color>");
        cmd = cmd.Trim().ToLower();

        var parts = cmd.Split(' ');
        switch (parts[0])
        {
            case "help":
                Log("Commands: addgold [n] | addfood [n] | addwood [n] | skip_day | fps | state | npclist | clear | reload");
                break;
            case "addgold":
                int gold = parts.Length > 1 ? int.Parse(parts[1]) : 500;
                GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Gold, gold);
                Log($"Added {gold} gold.");
                break;
            case "addfood":
                int food = parts.Length > 1 ? int.Parse(parts[1]) : 500;
                GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Food, food);
                Log($"Added {food} food.");
                break;
            case "addwood":
                int wood = parts.Length > 1 ? int.Parse(parts[1]) : 500;
                GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Wood, wood);
                Log($"Added {wood} wood.");
                break;
            case "skip_day":
                GameManager.Instance?.AdvanceDay();
                Log("Day advanced.");
                break;
            case "fps":
                Log($"FPS: {(1f / Time.unscaledDeltaTime):F1} | Target: {Application.targetFrameRate}");
                break;
            case "state":
                Log($"GameState: {GameManager.Instance?.CurrentState}");
                break;
            case "npclist":
                var npcs = NPCManager.Instance?.GetAllNPCs();
                if (npcs != null) foreach (var n in npcs)
                    Log($"  {n.Id} | {n.Name} | {n.Profession} | Mood:{n.MoodScore:F0} Loyalty:{n.LoyaltyToLord}");
                break;
            case "clear":
                ClearUI();
                break;
            case "reload":
                UnityEngine.SceneManagement.SceneManager.LoadScene(0);
                break;
            default:
                Log($"<color=#ff5555>Unknown command: {parts[0]}. Type 'help' for list.</color>");
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  UI
    // ─────────────────────────────────────────────────────────────

    public void Show()
    {
        _visible = true;
        _panel?.SetActive(true);
        RefreshUI();
    }

    public void Hide()
    {
        _visible = false;
        _panel?.SetActive(false);
    }

    public void Toggle() { if (_visible) Hide(); else Show(); }

    private void RefreshUI()
    {
        ClearUI();
        foreach (var line in _logLines)
            AppendLineToUI(line);
    }

    private void AppendLineToUI(string line)
    {
        if (_logContent == null) return;

        var go = new GameObject("Line");
        go.transform.SetParent(_logContent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = line;
        tmp.fontSize = 18;
        tmp.color = Color.white;
        tmp.enableWordWrapping = true;
        var le = go.AddComponent<LayoutElement>();
        le.flexibleWidth = 1;
        var csf = go.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _lineObjects.Add(go);

        // Remove oldest if over limit
        while (_lineObjects.Count > MAX_LINES)
        {
            Destroy(_lineObjects[0]);
            _lineObjects.RemoveAt(0);
        }

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        if (_scrollRect != null)
            _scrollRect.normalizedPosition = new Vector2(0, 0);
    }

    private void ClearUI()
    {
        foreach (var go in _lineObjects) if (go) Destroy(go);
        _lineObjects.Clear();
    }

    private void Log(string msg) => Debug.Log(msg);

    // ─────────────────────────────────────────────────────────────
    //  AUTO-BUILD UI
    // ─────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("DebugConsoleCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 9999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        _panel = new GameObject("Panel");
        _panel.transform.SetParent(canvasGO.transform, false);
        var panelImg = _panel.AddComponent<Image>();
        panelImg.color = new Color(0f, 0f, 0f, 0.88f);
        var panelRT = _panel.GetComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = new Vector2(1f, 0.5f);
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        // Close button
        var closeBtnGO = new GameObject("CloseBtn");
        closeBtnGO.transform.SetParent(_panel.transform, false);
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.5f, 0.1f, 0.1f);
        _closeButton = closeBtnGO.AddComponent<Button>();
        _closeButton.targetGraphic = closeBtnImg;
        _closeButton.onClick.AddListener(Hide);
        var closeBtnRT = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(1, 1);
        closeBtnRT.anchorMax = new Vector2(1, 1);
        closeBtnRT.pivot = new Vector2(1, 1);
        closeBtnRT.anchoredPosition = Vector2.zero;
        closeBtnRT.sizeDelta = new Vector2(80, 40);
        var closeLblGO = new GameObject("Lbl");
        closeLblGO.transform.SetParent(closeBtnGO.transform, false);
        var closeLbl = closeLblGO.AddComponent<TextMeshProUGUI>();
        closeLbl.text = "X";
        closeLbl.fontSize = 24;
        closeLbl.color = Color.white;
        closeLbl.alignment = TextAlignmentOptions.Center;
        var clRT = closeLblGO.GetComponent<RectTransform>();
        clRT.anchorMin = Vector2.zero; clRT.anchorMax = Vector2.one;
        clRT.offsetMin = Vector2.zero; clRT.offsetMax = Vector2.zero;

        // Scroll view
        var scrollGO = new GameObject("Scroll");
        scrollGO.transform.SetParent(_panel.transform, false);
        _scrollRect = scrollGO.AddComponent<ScrollRect>();
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = Vector2.zero;
        scrollRT.anchorMax = new Vector2(1, 1);
        scrollRT.offsetMin = new Vector2(0, 44);
        scrollRT.offsetMax = new Vector2(0, -40);

        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        vpGO.AddComponent<RectMask2D>();
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;
        _logContent = contentRT;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 2;
        vlg.padding = new RectOffset(8, 8, 4, 4);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _scrollRect.content  = contentRT;
        _scrollRect.viewport = vpRT;
        _scrollRect.horizontal = false;
        _scrollRect.vertical   = true;

        // Input field
        var inputGO = new GameObject("InputField");
        inputGO.transform.SetParent(_panel.transform, false);
        inputGO.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f);
        _inputField = inputGO.AddComponent<TMP_InputField>();
        var inputRT = inputGO.GetComponent<RectTransform>();
        inputRT.anchorMin = new Vector2(0, 0); inputRT.anchorMax = new Vector2(1, 0);
        inputRT.pivot = new Vector2(0.5f, 0);
        inputRT.offsetMin = Vector2.zero; inputRT.offsetMax = new Vector2(0, 44);

        var textAreaGO = new GameObject("TextArea");
        textAreaGO.transform.SetParent(inputGO.transform, false);
        textAreaGO.AddComponent<RectMask2D>();
        var taRT = textAreaGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(8, 0); taRT.offsetMax = new Vector2(-8, 0);

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(textAreaGO.transform, false);
        var ph = phGO.AddComponent<TextMeshProUGUI>();
        ph.text = LocalizationManager.Instance?.Get("debug_input_placeholder") ?? "Type command... (help for list)";
        ph.fontSize = 20;
        ph.color = new Color(0.5f, 0.5f, 0.5f);
        var phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(textAreaGO.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 20;
        txt.color = Color.white;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

        _inputField.textComponent = txt;
        _inputField.placeholder = ph;
        _inputField.targetGraphic = inputGO.GetComponent<Image>();
        _inputField.onSubmit.AddListener(cmd =>
        {
            ExecuteCommand(cmd);
            _inputField.text = "";
            _inputField.ActivateInputField();
        });
    }

#else
    // Release builds: stub out everything
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    public void Show() { }
    public void Hide() { }
    public void Toggle() { }
#endif
}
