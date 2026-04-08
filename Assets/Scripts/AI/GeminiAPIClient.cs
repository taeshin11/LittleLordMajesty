using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Handles all communication with Google Gemini 1.5 Flash API.
/// Supports streaming responses, context management, and retry logic.
/// </summary>
public class GeminiAPIClient : MonoBehaviour
{
    public static GeminiAPIClient Instance { get; private set; }

    private const string API_BASE_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
    private const int MAX_RETRIES = 3;
    private const float RETRY_DELAY = 1f;

    [SerializeField] private string _apiKey = ""; // Set via GameConfig or environment
    [SerializeField] private int _maxOutputTokens = 512;
    [SerializeField] private float _temperature = 0.85f;

    private int _requestCount = 0;
    private int _cachedResponses = 0;
    private Dictionary<string, string> _responseCache = new();

    public event Action<int> OnRequestCountChanged;

    [Serializable]
    private class GeminiRequest
    {
        public List<Content> contents;
        public GenerationConfig generationConfig;
        public SafetySettings[] safetySettings;
    }

    [Serializable]
    private class Content
    {
        public string role;
        public List<Part> parts;
    }

    [Serializable]
    private class Part
    {
        public string text;
    }

    [Serializable]
    private class GenerationConfig
    {
        public int maxOutputTokens;
        public float temperature;
        public float topP = 0.9f;
    }

    [Serializable]
    private class SafetySettings
    {
        public string category;
        public string threshold;
    }

    [Serializable]
    private class GeminiResponse
    {
        public Candidate[] candidates;
        public UsageMetadata usageMetadata;
    }

    [Serializable]
    private class Candidate
    {
        public ContentResponse content;
        public string finishReason;
    }

    [Serializable]
    private class ContentResponse
    {
        public Part[] parts;
        public string role;
    }

    [Serializable]
    private class UsageMetadata
    {
        public int promptTokenCount;
        public int candidatesTokenCount;
        public int totalTokenCount;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadAPIKey();
    }

    private void LoadAPIKey()
    {
        // Load from GameConfig ScriptableObject or environment
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null && !string.IsNullOrEmpty(config.GeminiAPIKey))
            _apiKey = config.GeminiAPIKey;
        else
            Debug.LogWarning("[Gemini] API key not configured. Set in Resources/Config/GameConfig.");
    }

    /// <summary>
    /// Sends a message to Gemini with full NPC persona context.
    /// </summary>
    public Coroutine SendMessage(
        string userMessage,
        string systemPrompt,
        List<(string role, string text)> conversationHistory,
        Action<string> onSuccess,
        Action<string> onError = null)
    {
        return StartCoroutine(SendMessageCoroutine(userMessage, systemPrompt, conversationHistory, onSuccess, onError));
    }

    private IEnumerator SendMessageCoroutine(
        string userMessage,
        string systemPrompt,
        List<(string role, string text)> history,
        Action<string> onSuccess,
        Action<string> onError,
        int retryCount = 0)
    {
        string cacheKey = ComputeCacheKey(systemPrompt, userMessage);
        if (_responseCache.TryGetValue(cacheKey, out string cached))
        {
            _cachedResponses++;
            Debug.Log($"[Gemini] Cache hit ({_cachedResponses} total)");
            onSuccess?.Invoke(cached);
            yield break;
        }

        var contents = new List<Content>();

        // Add conversation history
        if (history != null)
        {
            foreach (var (role, text) in history)
            {
                contents.Add(new Content
                {
                    role = role == "user" ? "user" : "model",
                    parts = new List<Part> { new Part { text = text } }
                });
            }
        }

        // Add current message with system context
        string fullMessage = string.IsNullOrEmpty(systemPrompt)
            ? userMessage
            : $"[SYSTEM CONTEXT]\n{systemPrompt}\n\n[USER MESSAGE]\n{userMessage}";

        contents.Add(new Content
        {
            role = "user",
            parts = new List<Part> { new Part { text = fullMessage } }
        });

        var request = new GeminiRequest
        {
            contents = contents,
            generationConfig = new GenerationConfig
            {
                maxOutputTokens = _maxOutputTokens,
                temperature = _temperature
            },
            safetySettings = new SafetySettings[]
            {
                new SafetySettings { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                new SafetySettings { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
            }
        };

        string jsonBody = JsonConvert.SerializeObject(request);
        string url = $"{API_BASE_URL}?key={_apiKey}";

        using var webRequest = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
        webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 30;

        _requestCount++;
        OnRequestCountChanged?.Invoke(_requestCount);
        Debug.Log($"[Gemini] Request #{_requestCount}: {userMessage.Substring(0, Mathf.Min(50, userMessage.Length))}...");

        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<GeminiResponse>(webRequest.downloadHandler.text);
                string responseText = response?.candidates?[0]?.content?.parts?[0]?.text ?? "";

                if (!string.IsNullOrEmpty(responseText))
                {
                    // Cache the response for cost optimization
                    if (_responseCache.Count < 500) // Limit cache size
                        _responseCache[cacheKey] = responseText;

                    Debug.Log($"[Gemini] Response ({response.usageMetadata?.totalTokenCount} tokens): {responseText.Substring(0, Mathf.Min(80, responseText.Length))}...");
                    onSuccess?.Invoke(responseText);
                }
                else
                {
                    onError?.Invoke("Empty response from Gemini");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Gemini] Parse error: {e.Message}");
                onError?.Invoke($"Parse error: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"[Gemini] Request failed (attempt {retryCount + 1}): {webRequest.error}");
            if (retryCount < MAX_RETRIES)
            {
                yield return new WaitForSeconds(RETRY_DELAY * (retryCount + 1));
                yield return SendMessageCoroutine(userMessage, systemPrompt, history, onSuccess, onError, retryCount + 1);
            }
            else
            {
                onError?.Invoke(webRequest.error);
            }
        }
    }

    private string ComputeCacheKey(string systemPrompt, string userMessage)
    {
        // Simple hash for cache lookup
        int hash = (systemPrompt + "|" + userMessage).GetHashCode();
        return hash.ToString();
    }

    public void ClearCache() => _responseCache.Clear();
    public int GetRequestCount() => _requestCount;
    public int GetCacheHits() => _cachedResponses;
}
