using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings panel - Language, TTS, Audio volumes, display options.
/// All changes persist via PlayerPrefs.
/// </summary>
public class SettingsUI : MonoBehaviour
{
    [Header("Language")]
    [SerializeField] private TMP_Dropdown _languageDropdown;

    [Header("Audio")]
    [SerializeField] private Slider _musicSlider;
    [SerializeField] private Slider _sfxSlider;
    [SerializeField] private Toggle _ttsToggle;

    [Header("Display")]
    [SerializeField] private Toggle _pixelArtToggle;
    [SerializeField] private Toggle _screenShakeToggle;

    [Header("Stats (API Usage)")]
    [SerializeField] private TextMeshProUGUI _apiCallsText;
    [SerializeField] private TextMeshProUGUI _cacheHitsText;
    [SerializeField] private TextMeshProUGUI _ttsCacheSizeText;

    [Header("Buttons")]
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _clearTTSCacheButton;

    private void OnEnable()
    {
        LoadCurrentSettings();
        RefreshStats();
    }

    private void Start()
    {
        SetupLanguageDropdown();

        if (_saveButton != null) _saveButton.onClick.AddListener(SaveSettings);
        if (_closeButton != null) _closeButton.onClick.AddListener(() => gameObject.SetActive(false));
        if (_clearTTSCacheButton != null) _clearTTSCacheButton.onClick.AddListener(OnClearTTSCache);

        if (_languageDropdown != null) _languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        if (_musicSlider != null) _musicSlider.onValueChanged.AddListener(v => AudioListener.volume = v);
    }

    private void SetupLanguageDropdown()
    {
        if (_languageDropdown == null) return;
        _languageDropdown.ClearOptions();
        _languageDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "English", "한국어", "日本語", "中文", "Français", "Deutsch", "Español"
        });

        int currentLang = (int)(LocalizationManager.Instance?.CurrentLanguage
                                ?? LocalizationManager.Language.English);
        _languageDropdown.value = currentLang;
    }

    private void LoadCurrentSettings()
    {
        if (_musicSlider != null) _musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
        if (_sfxSlider != null) _sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1.0f);
        if (_ttsToggle != null) _ttsToggle.isOn = PlayerPrefs.GetInt("TTS_Enabled", 1) == 1;
        if (_screenShakeToggle != null) _screenShakeToggle.isOn = PlayerPrefs.GetInt("ScreenShake", 1) == 1;
    }

    private void SaveSettings()
    {
        if (_musicSlider != null) PlayerPrefs.SetFloat("MusicVolume", _musicSlider.value);
        if (_sfxSlider != null) PlayerPrefs.SetFloat("SFXVolume", _sfxSlider.value);
        if (_ttsToggle != null)
        {
            PlayerPrefs.SetInt("TTS_Enabled", _ttsToggle.isOn ? 1 : 0);
            TTSManager.Instance?.SetEnabled(_ttsToggle.isOn);
        }
        if (_screenShakeToggle != null) PlayerPrefs.SetInt("ScreenShake", _screenShakeToggle.isOn ? 1 : 0);
        PlayerPrefs.Save();

        gameObject.SetActive(false);
    }

    private void OnLanguageChanged(int index)
    {
        var language = (LocalizationManager.Language)index;
        LocalizationManager.Instance?.SetLanguage(language);
    }

    private void RefreshStats()
    {
        var gemini = GeminiAPIClient.Instance;
        if (_apiCallsText != null && gemini != null)
            _apiCallsText.text = $"Gemini API Calls: {gemini.GetRequestCount()} (Cache: {gemini.GetCacheHits()})";

        var tts = TTSManager.Instance;
        if (tts != null)
        {
            var (calls, hits, sizeBytes) = tts.GetStats();
            if (_cacheHitsText != null) _cacheHitsText.text = $"TTS Cache Hits: {hits}/{calls + hits}";
            if (_ttsCacheSizeText != null)
                _ttsCacheSizeText.text = $"TTS Cache: {sizeBytes / 1024f:F1} KB";
        }
    }

    private void OnClearTTSCache()
    {
        TTSManager.Instance?.ClearCache();
        RefreshStats();
    }
}
