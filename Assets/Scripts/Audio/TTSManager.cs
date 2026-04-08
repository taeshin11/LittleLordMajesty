using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Google Cloud TTS integration with local file caching.
/// Once audio is generated it's cached to device storage - near-zero cost after first play.
/// </summary>
public class TTSManager : MonoBehaviour
{
    public static TTSManager Instance { get; private set; }

    private const string TTS_API_URL = "https://texttospeech.googleapis.com/v1/text:synthesize";
    private string _cachePath;
    private string _apiKey;

    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private bool _enableTTS = true;

    // Cache statistics
    private int _apiCalls = 0;
    private int _cacheHits = 0;

    // Track currently playing to avoid overlap
    private Coroutine _playingCoroutine;

    [Serializable]
    private class TTSRequest
    {
        public InputData input;
        public VoiceParams voice;
        public AudioConfigData audioConfig;
    }

    [Serializable]
    private class InputData { public string text; }

    [Serializable]
    private class VoiceParams
    {
        public string languageCode;
        public string name;
        public string ssmlGender;
    }

    [Serializable]
    private class AudioConfigData
    {
        public string audioEncoding = "MP3";
        public float speakingRate;
        public float pitch;
    }

    [Serializable]
    private class TTSResponse
    {
        public string audioContent; // base64 encoded MP3
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _cachePath = Path.Combine(Application.persistentDataPath, "tts_cache");
        Directory.CreateDirectory(_cachePath);

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        LoadAPIKey();
        Debug.Log($"[TTS] Cache path: {_cachePath}");
    }

    private void LoadAPIKey()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null)
            _apiKey = config.GoogleCloudAPIKey;
    }

    /// <summary>
    /// Speaks text using TTS. Checks cache first before making API call.
    /// </summary>
    public void Speak(string text, string voiceName = "en-US-Neural2-D",
        string languageCode = "en-US", float speakingRate = 1.0f, float pitch = 0f,
        Action onComplete = null)
    {
        if (!_enableTTS || string.IsNullOrEmpty(text)) return;
        if (_playingCoroutine != null) StopCoroutine(_playingCoroutine);
        _playingCoroutine = StartCoroutine(SpeakCoroutine(text, voiceName, languageCode, speakingRate, pitch, onComplete));
    }

    private IEnumerator SpeakCoroutine(string text, string voiceName, string languageCode,
        float speakingRate, float pitch, Action onComplete)
    {
        string cacheFile = GetCachePath(text, voiceName, speakingRate, pitch);

        if (File.Exists(cacheFile))
        {
            _cacheHits++;
            Debug.Log($"[TTS] Cache hit #{_cacheHits}: '{text.Substring(0, Mathf.Min(30, text.Length))}...'");
            yield return LoadAndPlayFromCache(cacheFile, onComplete);
        }
        else
        {
            yield return FetchAndCacheFromAPI(text, voiceName, languageCode, speakingRate, pitch, cacheFile, onComplete);
        }
    }

    private IEnumerator FetchAndCacheFromAPI(string text, string voiceName, string languageCode,
        float speakingRate, float pitch, string cacheFile, Action onComplete)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogWarning("[TTS] No API key configured.");
            onComplete?.Invoke();
            yield break;
        }

        var requestData = new TTSRequest
        {
            input = new InputData { text = text },
            voice = new VoiceParams
            {
                languageCode = languageCode,
                name = voiceName,
                ssmlGender = "NEUTRAL"
            },
            audioConfig = new AudioConfigData
            {
                audioEncoding = "MP3",
                speakingRate = speakingRate,
                pitch = pitch
            }
        };

        string json = JsonConvert.SerializeObject(requestData);
        string url = $"{TTS_API_URL}?key={_apiKey}";

        using var webRequest = new UnityWebRequest(url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 15;

        _apiCalls++;
        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            var response = JsonConvert.DeserializeObject<TTSResponse>(webRequest.downloadHandler.text);
            if (!string.IsNullOrEmpty(response?.audioContent))
            {
                byte[] audioBytes = Convert.FromBase64String(response.audioContent);
                File.WriteAllBytes(cacheFile, audioBytes);
                Debug.Log($"[TTS] API call #{_apiCalls}, cached to {Path.GetFileName(cacheFile)}");
                yield return LoadAndPlayFromCache(cacheFile, onComplete);
            }
        }
        else
        {
            Debug.LogError($"[TTS] API error: {webRequest.error}");
            onComplete?.Invoke();
        }
    }

    private IEnumerator LoadAndPlayFromCache(string filePath, Action onComplete)
    {
        string fileUrl = "file://" + filePath;
        using var req = UnityWebRequestMultimedia.GetAudioClip(fileUrl, AudioType.MPEG);
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var clip = DownloadHandlerAudioClip.GetContent(req);
            _audioSource.clip = clip;
            _audioSource.Play();
            yield return new WaitForSeconds(clip.length);
        }
        onComplete?.Invoke();
    }

    private string GetCachePath(string text, string voiceName, float rate, float pitch)
    {
        // Create a unique deterministic filename from the parameters
        string key = $"{text}|{voiceName}|{rate:F2}|{pitch:F2}";
        using var md5 = MD5.Create();
        byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
        string hashStr = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return Path.Combine(_cachePath, $"{hashStr}.mp3");
    }

    public void StopSpeaking()
    {
        if (_playingCoroutine != null) StopCoroutine(_playingCoroutine);
        _audioSource?.Stop();
    }

    public void SetEnabled(bool enabled) => _enableTTS = enabled;

    public (int apiCalls, int cacheHits, long cacheSizeBytes) GetStats()
    {
        long size = 0;
        if (Directory.Exists(_cachePath))
            foreach (var f in Directory.GetFiles(_cachePath, "*.mp3"))
                size += new FileInfo(f).Length;
        return (_apiCalls, _cacheHits, size);
    }

    public void ClearCache()
    {
        if (!Directory.Exists(_cachePath)) return;
        foreach (var f in Directory.GetFiles(_cachePath, "*.mp3"))
            File.Delete(f);
        _cacheHits = 0;
        Debug.Log("[TTS] Cache cleared.");
    }
}
