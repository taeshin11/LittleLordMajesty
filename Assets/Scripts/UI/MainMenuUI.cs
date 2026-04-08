using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Main menu with new game, continue, settings.
/// Modern pixel-art aesthetic with animated title and background.
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _subtitleText;
    [SerializeField] private Animator _titleAnimator;

    [Header("Buttons")]
    [SerializeField] private Button _newGameButton;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _creditsButton;
    [SerializeField] private Button _quitButton;

    [Header("Name Input (New Game Flow)")]
    [SerializeField] private GameObject _nameInputPanel;
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _cancelNameButton;

    [Header("Version")]
    [SerializeField] private TextMeshProUGUI _versionText;

    [Header("Background")]
    [SerializeField] private ParticleSystem _backgroundParticles;
    [SerializeField] private Image _backgroundImage;

    private void Start()
    {
        SetupUI();
        StartCoroutine(AnimateTitle());
    }

    private void SetupUI()
    {
        // Localized text
        var loc = LocalizationManager.Instance;

        if (_titleText != null) _titleText.text = loc?.Get("app_name") ?? "Little Lord Majesty";
        if (_subtitleText != null) _subtitleText.text = "Rule the realm with a single word.";
        if (_versionText != null) _versionText.text = "v0.1.0 Alpha";

        // Button text
        SetButtonText(_newGameButton, loc?.Get("btn_new_game") ?? "New Game");
        SetButtonText(_continueButton, loc?.Get("btn_continue") ?? "Continue");
        SetButtonText(_settingsButton, loc?.Get("btn_settings") ?? "Settings");
        SetButtonText(_quitButton, loc?.Get("btn_quit") ?? "Quit");

        // Button listeners
        if (_newGameButton != null) _newGameButton.onClick.AddListener(OnNewGameClicked);
        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
            _continueButton.interactable = SaveSystem.HasSaveFile();
        }
        if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
        if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
        if (_startButton != null) _startButton.onClick.AddListener(OnStartNewGameConfirmed);
        if (_cancelNameButton != null) _cancelNameButton.onClick.AddListener(() =>
            SetPanelActive(_nameInputPanel, false));

        if (loc != null) loc.OnLanguageChanged += _ => RefreshText();
    }

    private void RefreshText()
    {
        var loc = LocalizationManager.Instance;
        if (_titleText != null) _titleText.text = loc?.Get("app_name") ?? "Little Lord Majesty";
        SetButtonText(_newGameButton, loc?.Get("btn_new_game") ?? "New Game");
        SetButtonText(_continueButton, loc?.Get("btn_continue") ?? "Continue");
        SetButtonText(_settingsButton, loc?.Get("btn_settings") ?? "Settings");
        SetButtonText(_quitButton, loc?.Get("btn_quit") ?? "Quit");
    }

    private void SetButtonText(Button btn, string text)
    {
        var tmp = btn?.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
    }

    private void OnNewGameClicked()
    {
        SetPanelActive(_nameInputPanel, true);
        _playerNameInput?.Select();
        _playerNameInput?.ActivateInputField();
    }

    private void OnStartNewGameConfirmed()
    {
        string name = _playerNameInput?.text?.Trim();
        if (string.IsNullOrEmpty(name)) name = "Lord";

        SetPanelActive(_nameInputPanel, false);
        GameManager.Instance?.NewGame(name);
    }

    private void OnContinueClicked()
    {
        GameManager.Instance?.LoadGame();
    }

    private void OnSettingsClicked()
    {
        var settings = FindObjectOfType<SettingsUI>(true);
        settings?.gameObject.SetActive(true);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator AnimateTitle()
    {
        if (_titleText == null) yield break;

        // Pulse animation
        float time = 0;
        while (true)
        {
            time += Time.deltaTime;
            float scale = 1f + Mathf.Sin(time * 1.5f) * 0.02f;
            _titleText.transform.localScale = Vector3.one * scale;
            yield return null;
        }
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}
