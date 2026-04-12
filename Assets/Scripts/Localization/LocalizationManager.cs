using UnityEngine;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TMPro;

/// <summary>
/// Dynamic localization system. NO hardcoded text - everything through this manager.
/// Supports: English, Korean, Japanese, Chinese, French, German, Spanish.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public static LocalizationManager Instance { get; private set; }

    public enum Language
    {
        English,
        Korean,
        Japanese,
        Chinese,
        French,
        German,
        Spanish
    }

    private Language _currentLanguage = Language.Korean;
    public Language CurrentLanguage => _currentLanguage;
    public string CurrentLanguageCode => GetLanguageCode(_currentLanguage);

    private Dictionary<string, string> _localizedTexts = new();
    public event Action<Language> OnLanguageChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        DetectSystemLanguage();
        LoadLanguage(_currentLanguage);
    }

    private void DetectSystemLanguage()
    {
        _currentLanguage = Application.systemLanguage switch
        {
            SystemLanguage.Korean => Language.Korean,
            SystemLanguage.Japanese => Language.Japanese,
            SystemLanguage.ChineseSimplified or SystemLanguage.ChineseTraditional => Language.Chinese,
            SystemLanguage.French => Language.French,
            SystemLanguage.German => Language.German,
            SystemLanguage.Spanish => Language.Spanish,
            _ => Language.English
        };

        // Check player preference override
        string saved = PlayerPrefs.GetString("SelectedLanguage", "");
        if (!string.IsNullOrEmpty(saved) && Enum.TryParse<Language>(saved, out var savedLang))
            _currentLanguage = savedLang;

        // Safety belt: if the default TMP font can't render the detected language's glyphs
        // (e.g. CJK font asset not yet generated on this machine), fall back to English.
        // Otherwise the entire UI renders as empty boxes. This guard is a no-op once
        // CJKFontSetup has wired NotoSansKR as a fallback on LiberationSans SDF.
        if (IsCJK(_currentLanguage) && !CanRenderCJK())
        {
            Debug.LogWarning($"[Localization] Detected {_currentLanguage} but no CJK font fallback is wired. " +
                             "Defaulting to English. Run 'LittleLordMajesty > Generate CJK Font Asset' in the Editor.");
            _currentLanguage = Language.English;
        }
    }

    private static bool IsCJK(Language lang) =>
        lang == Language.Korean || lang == Language.Japanese || lang == Language.Chinese;

    private static bool CanRenderCJK()
    {
        // LiberationSans SDF lives in TextMesh Pro's Resources folder — load it and
        // ask whether '가' (U+AC00, the first Hangul syllable) resolves via any fallback.
        //
        // IMPORTANT (m15): HasCharacter(searchFallbacks: true) can return true
        // against a Dynamic-atlas fallback even when no glyph is actually baked,
        // because TMP "trusts" the dynamic rasterizer to supply it on demand.
        // On WebGL/IL2CPP that path routes through TMP_FontAsset.TryAddCharacterInternal
        // → FreeType bridge (partially stripped) → `null function` wasm crash.
        // So we require the glyph to already exist in a STATIC atlas somewhere
        // in the fallback chain — dynamic fallbacks are treated as "no".
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null) return false;
        if (HasCharacterInStaticChain(font, '\uAC00')) return true;
        return false;
    }

    private static bool HasCharacterInStaticChain(TMP_FontAsset font, uint unicode, int depth = 0)
    {
        if (font == null || depth > 4) return false;
        // Only trust the glyph if it's literally already in a static atlas's
        // character lookup table. Dynamic atlases are treated as "no" because
        // their HasCharacter(true) return value promises FreeType will
        // rasterize on demand — but on WebGL/IL2CPP that FreeType bridge is
        // partially stripped and crashes the wasm runtime.
        if (font.atlasPopulationMode == AtlasPopulationMode.Static
            && font.characterLookupTable != null
            && font.characterLookupTable.ContainsKey(unicode))
        {
            return true;
        }
        if (font.fallbackFontAssetTable != null)
        {
            foreach (var fb in font.fallbackFontAssetTable)
                if (HasCharacterInStaticChain(fb, unicode, depth + 1)) return true;
        }
        return false;
    }

    public void SetLanguage(Language language)
    {
        _currentLanguage = language;
        PlayerPrefs.SetString("SelectedLanguage", language.ToString());
        LoadLanguage(language);
        OnLanguageChanged?.Invoke(language);
    }

    private void LoadLanguage(Language language)
    {
        string code = GetLanguageCode(language);
        var asset = Resources.Load<TextAsset>($"Localization/{code}");
        if (asset != null)
        {
            _localizedTexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(asset.text)
                              ?? new Dictionary<string, string>();
            Debug.Log($"[Localization] Loaded {_localizedTexts.Count} strings for {language}");
        }
        else
        {
            Debug.LogWarning($"[Localization] No file for {language}, falling back to key names.");
            _localizedTexts.Clear();
        }
    }

    public string Get(string key, params object[] args)
    {
        if (_localizedTexts.TryGetValue(key, out string value))
        {
            return args.Length > 0 ? string.Format(value, args) : value;
        }

        // Fallback to English
        if (_currentLanguage != Language.English)
        {
            var enAsset = Resources.Load<TextAsset>("Localization/en");
            if (enAsset != null)
            {
                var enDict = JsonConvert.DeserializeObject<Dictionary<string, string>>(enAsset.text);
                if (enDict != null && enDict.TryGetValue(key, out string enVal))
                    return args.Length > 0 ? string.Format(enVal, args) : enVal;
            }
        }

        Debug.LogWarning($"[Localization] Missing key: {key}");
        return key; // Return key itself as last resort
    }

    public string GetFormattedDate(int day, int year)
    {
        return _currentLanguage switch
        {
            Language.Korean => $"{year}년 {day}일",
            Language.Japanese => $"{year}年{day}日",
            Language.Chinese => $"第{year}年第{day}天",
            _ => $"Year {year}, Day {day}"
        };
    }

    public string GetLanguageCode(Language lang) => lang switch
    {
        Language.Korean => "ko",
        Language.Japanese => "ja",
        Language.Chinese => "zh",
        Language.French => "fr",
        Language.German => "de",
        Language.Spanish => "es",
        _ => "en"
    };

    public string GetTTSLanguageCode(Language lang) => lang switch
    {
        Language.Korean => "ko-KR",
        Language.Japanese => "ja-JP",
        Language.Chinese => "cmn-CN",
        Language.French => "fr-FR",
        Language.German => "de-DE",
        Language.Spanish => "es-ES",
        _ => "en-US"
    };

    public string GetTTSVoiceForLanguage(Language lang) => lang switch
    {
        Language.Korean => "ko-KR-Neural2-A",
        Language.Japanese => "ja-JP-Neural2-B",
        Language.Chinese => "cmn-CN-Wavenet-A",
        Language.French => "fr-FR-Neural2-A",
        Language.German => "de-DE-Neural2-B",
        Language.Spanish => "es-ES-Neural2-A",
        _ => "en-US-Neural2-D"
    };
}
