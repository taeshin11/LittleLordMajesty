using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System;
using System.Collections;

/// <summary>
/// Central UI manager. Handles responsive layout for Mobile & Tablet,
/// screen transitions, HUD updates, and panel management.
/// Modern dark medieval aesthetic with pixel art elements.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Root Panels")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _castleViewPanel;
    [SerializeField] private GameObject _worldMapPanel;
    [SerializeField] private GameObject _dialoguePanel;
    [SerializeField] private GameObject _eventPanel;
    [SerializeField] private GameObject _pausePanel;
    [SerializeField] private GameObject _loadingPanel;
    [SerializeField] private GameObject _settingsPanel;

    [Header("HUD Elements")]
    [SerializeField] private TextMeshProUGUI _woodText;
    [SerializeField] private TextMeshProUGUI _foodText;
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _populationText;
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private TextMeshProUGUI _lordTitleText;
    [SerializeField] private Slider _woodBar;
    [SerializeField] private Slider _foodBar;
    [SerializeField] private Slider _goldBar;

    [Header("Dialogue Panel")]
    [SerializeField] private TextMeshProUGUI _npcNameText;
    [SerializeField] private TextMeshProUGUI _npcDialogueText;
    [SerializeField] private TMP_InputField _playerInputField;
    [SerializeField] private Button _sendCommandButton;
    [SerializeField] private GameObject _thinkingIndicator;
    [SerializeField] private Image _npcPortrait;

    [Header("Event Panel")]
    [SerializeField] private TextMeshProUGUI _eventTitleText;
    [SerializeField] private TextMeshProUGUI _eventDescText;
    [SerializeField] private TMP_InputField _eventResponseField;
    [SerializeField] private Button _eventSubmitButton;
    [SerializeField] private Image _eventIcon;

    [Header("Responsive UI")]
    [SerializeField] private CanvasScaler _canvasScaler;
    private bool _isTablet;

    // Tablet breakpoint: width/height ratio closer to 4:3
    private const float TABLET_RATIO_THRESHOLD = 0.75f;
    private const float REFERENCE_PHONE_WIDTH = 1080f;
    private const float REFERENCE_PHONE_HEIGHT = 1920f;
    private const float REFERENCE_TABLET_WIDTH = 1536f;
    private const float REFERENCE_TABLET_HEIGHT = 2048f;

    private string _currentActiveNPCId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Defensive: ensure there's a working EventSystem + input module at runtime.
        // The project had `activeInputHandler: -1` (corrupted) for a while which left
        // EventSystem.currentInputModule null in WebGL builds — every button click was
        // silently dropped because no module was processing pointer events. This guard
        // creates or re-enables a StandaloneInputModule on the scene's EventSystem so
        // the game stays clickable even on fresh clones with stale ProjectSettings.
        var es = EventSystem.current ?? FindObjectOfType<EventSystem>();
        if (es == null)
        {
            var esGO = new GameObject("EventSystem");
            es = esGO.AddComponent<EventSystem>();
            esGO.AddComponent<StandaloneInputModule>();
            Debug.LogWarning("[UIManager] No EventSystem in scene — created one at runtime.");
        }
        else if (es.currentInputModule == null)
        {
            var existing = es.GetComponent<StandaloneInputModule>();
            if (existing == null)
            {
                es.gameObject.AddComponent<StandaloneInputModule>();
                Debug.LogWarning("[UIManager] EventSystem had no input module — added StandaloneInputModule.");
            }
            else if (!existing.enabled)
            {
                existing.enabled = true;
                Debug.LogWarning("[UIManager] Re-enabled disabled StandaloneInputModule.");
            }
        }

        DetectDeviceType();
        ApplyResponsiveLayout();
    }

    private void Start()
    {
        SubscribeToEvents();
        ShowPanel(GameManager.GameState.MainMenu);
    }

    private void DetectDeviceType()
    {
        float aspect = (float)Screen.width / Screen.height;
        _isTablet = aspect >= TABLET_RATIO_THRESHOLD;
        Debug.Log($"[UI] Device: {(_isTablet ? "Tablet" : "Phone")} ({Screen.width}x{Screen.height}, ratio: {aspect:F2})");
    }

    private void ApplyResponsiveLayout()
    {
        if (_canvasScaler == null) return;
        _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        if (_isTablet)
        {
            _canvasScaler.referenceResolution = new Vector2(REFERENCE_TABLET_WIDTH, REFERENCE_TABLET_HEIGHT);
            _canvasScaler.matchWidthOrHeight = 0.5f;
        }
        else
        {
            _canvasScaler.referenceResolution = new Vector2(REFERENCE_PHONE_WIDTH, REFERENCE_PHONE_HEIGHT);
            _canvasScaler.matchWidthOrHeight = 0.5f;
        }
    }

    private void SubscribeToEvents()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            GameManager.Instance.OnDayChanged += OnDayChanged;
        }

        if (GameManager.Instance?.ResourceManager != null)
            GameManager.Instance.ResourceManager.OnResourceChanged += OnResourceChanged;

        if (GameManager.Instance?.EventManager != null)
        {
            GameManager.Instance.EventManager.OnNewEvent += OnNewEvent;
            GameManager.Instance.EventManager.OnEventResolved += OnEventResolved;
        }
    }

    private void OnGameStateChanged(GameManager.GameState oldState, GameManager.GameState newState)
    {
        ShowPanel(newState);
    }

    public void ShowPanel(GameManager.GameState state)
    {
        // Hide all panels first
        SetPanelActive(_mainMenuPanel, false);
        SetPanelActive(_castleViewPanel, false);
        SetPanelActive(_worldMapPanel, false);
        SetPanelActive(_dialoguePanel, false);
        SetPanelActive(_eventPanel, false);
        SetPanelActive(_pausePanel, false);

        switch (state)
        {
            case GameManager.GameState.MainMenu:
                SetPanelActive(_mainMenuPanel, true);
                break;
            case GameManager.GameState.Castle:
                SetPanelActive(_castleViewPanel, true);
                break;
            case GameManager.GameState.WorldMap:
                SetPanelActive(_worldMapPanel, true);
                break;
            case GameManager.GameState.Dialogue:
                SetPanelActive(_castleViewPanel, true); // Keep castle in background
                SetPanelActive(_dialoguePanel, true);
                break;
            case GameManager.GameState.Event:
                SetPanelActive(_castleViewPanel, true);
                SetPanelActive(_eventPanel, true);
                break;
            case GameManager.GameState.Paused:
                SetPanelActive(_pausePanel, true);
                break;
            case GameManager.GameState.Loading:
                SetPanelActive(_loadingPanel, true);
                break;
            case GameManager.GameState.GameOver:
            case GameManager.GameState.Victory:
                SetPanelActive(_mainMenuPanel, true); // fallback until dedicated panels exist
                break;
        }
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void OnResourceChanged(ResourceManager.ResourceType type, int oldVal, int newVal)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        switch (type)
        {
            case ResourceManager.ResourceType.Wood:
                UpdateResourceUI(_woodText, _woodBar, newVal, rm.MaxWood, "🪵");
                break;
            case ResourceManager.ResourceType.Food:
                UpdateResourceUI(_foodText, _foodBar, newVal, rm.MaxFood, "🌾");
                break;
            case ResourceManager.ResourceType.Gold:
                UpdateResourceUI(_goldText, _goldBar, newVal, rm.MaxGold, "💰");
                break;
            case ResourceManager.ResourceType.Population:
                if (_populationText != null)
                    _populationText.text = $"👥 {newVal}/{rm.MaxPopulation}";
                break;
        }
    }

    private void UpdateResourceUI(TextMeshProUGUI text, Slider bar, int value, int max, string icon)
    {
        if (text != null) text.text = $"{icon} {value:N0}";
        if (bar != null) bar.value = max > 0 ? (float)value / max : 0f;
    }

    private void OnDayChanged(int day)
    {
        if (_dateText != null)
            _dateText.text = GameManager.Instance?.GetFormattedDate() ?? "";
        if (_lordTitleText != null)
            _lordTitleText.text = $"{GameManager.Instance?.LordTitle} {GameManager.Instance?.PlayerName}";
    }

    // ========== DIALOGUE SYSTEM ==========

    public void OpenDialogue(string npcId)
    {
        _currentActiveNPCId = npcId;
        var npc = NPCManager.Instance?.GetNPC(npcId);
        if (npc == null) return;

        if (_npcNameText != null) _npcNameText.text = npc.Name;
        if (_npcDialogueText != null) _npcDialogueText.text = "";
        if (_playerInputField != null)
        {
            _playerInputField.text = "";
            _playerInputField.ActivateInputField();
        }

        GameManager.Instance?.SetGameState(GameManager.GameState.Dialogue);

        // Tutorial: complete "talk to NPC" step
        TutorialSystem.Instance?.CompleteCurrentStep("talk_to_aldric");
    }

    public void OnSendCommandClicked()
    {
        if (_playerInputField == null || string.IsNullOrWhiteSpace(_playerInputField.text)) return;
        string command = _playerInputField.text.Trim();
        _playerInputField.text = "";
        SendCommandToNPC(command);
    }

    private void SendCommandToNPC(string command)
    {
        if (string.IsNullOrEmpty(_currentActiveNPCId)) return;

        SetThinking(true);

        // Tutorial: complete "issue command" step
        TutorialSystem.Instance?.CompleteCurrentStep("issue_command");

        NPCManager.Instance?.IssueCommandToNPC(_currentActiveNPCId, command, response =>
        {
            SetThinking(false);
            DisplayNPCResponse(response);
        });
    }

    private Coroutine _activeTypewriter;

    private void DisplayNPCResponse(string response)
    {
        if (_npcDialogueText == null) return;
        if (_activeTypewriter != null) StopCoroutine(_activeTypewriter);
        _activeTypewriter = StartCoroutine(TypewriterEffect(_npcDialogueText, response, 0.03f));
    }

    private IEnumerator TypewriterEffect(TextMeshProUGUI target, string fullText, float charDelay)
    {
        target.text = fullText;
        target.maxVisibleCharacters = 0;
        for (int i = 1; i <= fullText.Length; i++)
        {
            target.maxVisibleCharacters = i;
            yield return new WaitForSeconds(charDelay);
        }
        _activeTypewriter = null;
    }

    private void SetThinking(bool isThinking)
    {
        if (_thinkingIndicator != null) _thinkingIndicator.SetActive(isThinking);
        if (_sendCommandButton != null) _sendCommandButton.interactable = !isThinking;
    }

    public void CloseDialogue()
    {
        _currentActiveNPCId = null;
        GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
    }

    // ========== EVENT SYSTEM ==========

    private void OnNewEvent(EventManager.GameEvent ev)
    {
        ShowEventPanel(ev);
    }

    private void OnEventResolved(EventManager.GameEvent ev)
    {
        if (_eventPanel != null && !EventManager.Instance.HasActiveEvents())
            SetPanelActive(_eventPanel, false);
    }

    private void ShowEventPanel(EventManager.GameEvent ev)
    {
        if (_eventTitleText != null) _eventTitleText.text = ev.Title;
        if (_eventDescText != null) _eventDescText.text = ev.Description;
        if (_eventResponseField != null) _eventResponseField.text = "";

        // Color severity
        if (_eventTitleText != null)
        {
            _eventTitleText.color = ev.Severity switch
            {
                EventManager.EventSeverity.Minor => new Color(0.8f, 0.8f, 0.3f),
                EventManager.EventSeverity.Moderate => new Color(1f, 0.6f, 0.2f),
                EventManager.EventSeverity.Severe => new Color(1f, 0.3f, 0.1f),
                EventManager.EventSeverity.Critical => new Color(1f, 0f, 0f),
                _ => Color.white
            };
        }
    }

    public void OnEventResponseSubmit()
    {
        var activeEvents = EventManager.Instance?.GetActiveEvents();
        if (activeEvents == null || activeEvents.Count == 0 || _eventResponseField == null) return;

        string response = _eventResponseField.text.Trim();
        if (string.IsNullOrEmpty(response)) return;

        var ev = activeEvents[0];
        _eventResponseField.interactable = false;

        EventManager.Instance?.RespondToEvent(ev.EventId, response, (outcome, success) =>
        {
            _eventResponseField.interactable = true;
            if (_eventDescText != null)
                _eventDescText.text = $"<color={(success ? "green" : "red")}>{outcome}</color>";

            StartCoroutine(DelayedPanelClose(2f));
        });
    }

    private IEnumerator DelayedPanelClose(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetPanelActive(_eventPanel, false);
    }

    // ========== LOADING ==========

    public void ShowLoading(bool show) => SetPanelActive(_loadingPanel, show);
    public void ShowSettings() => SetPanelActive(_settingsPanel, true);
    public void HideSettings() => SetPanelActive(_settingsPanel, false);

    // ========== MAIN MENU ==========

    public void OnStartNewGame()
    {
        ShowLoading(true);
        // Player name input would go here - for now use default
        GameManager.Instance?.NewGame("Lord Player");
        ShowLoading(false);
    }

    public void OnContinueGame()
    {
        ShowLoading(true);
        GameManager.Instance?.LoadGame();
        ShowLoading(false);
    }

    public void OnQuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnGameStateChanged -= OnGameStateChanged;
            gm.OnDayChanged -= OnDayChanged;
            if (gm.ResourceManager != null)
                gm.ResourceManager.OnResourceChanged -= OnResourceChanged;
            if (gm.EventManager != null)
            {
                gm.EventManager.OnNewEvent -= OnNewEvent;
                gm.EventManager.OnEventResolved -= OnEventResolved;
            }
        }
    }
}
