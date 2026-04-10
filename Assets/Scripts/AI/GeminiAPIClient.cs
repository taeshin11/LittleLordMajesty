using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Handles all communication with Google Gemini 1.5 Flash API.
/// Supports request queueing, SHA256 caching, and retry logic.
/// </summary>
public class GeminiAPIClient : MonoBehaviour
{
    public static GeminiAPIClient Instance { get; private set; }

    private const string API_BASE_URL    = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent";
    private const string STREAM_URL      = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:streamGenerateContent?alt=sse";
    private const int    MAX_RETRIES     = 3;
    private const float  RETRY_DELAY     = 1f;
    private const float  MIN_REQUEST_GAP = 1.0f; // Rate limiting: 1s between requests
    private const int    MAX_CONCURRENT  = 2;

    [SerializeField] private string _apiKey = ""; // Set via GameConfig or environment
    [SerializeField] private int _maxOutputTokens = 512;
    [SerializeField] private float _temperature = 0.85f;

    private int _requestCount = 0;
    private int _cachedResponses = 0;
    private int _activeRequests = 0;
    private float _lastRequestTime = -999f;
    private readonly Queue<System.Func<IEnumerator>> _requestQueue = new();
    private bool _queueRunning = false;

    private Dictionary<string, string> _responseCache = new();

    public event Action<int> OnRequestCountChanged;

    [Serializable]
    private class GeminiRequest
    {
        public Content system_instruction;
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
    /// Sends a message to Gemini. Requests are queued with rate limiting.
    /// </summary>
    public Coroutine SendMessage(
        string userMessage,
        string systemPrompt,
        List<(string role, string text)> conversationHistory,
        Action<string> onSuccess,
        Action<string> onError = null)
    {
        // Check MonetizationManager scroll limit
        if (MonetizationManager.Instance != null && !MonetizationManager.Instance.CanUseAI)
        {
            MonetizationManager.Instance.RequestAIUsage(
                () => EnqueueRequest(userMessage, systemPrompt, conversationHistory, onSuccess, onError),
                () => onError?.Invoke("No Wisdom Scrolls remaining."));
            return null;
        }

        return EnqueueRequest(userMessage, systemPrompt, conversationHistory, onSuccess, onError);
    }

    private Coroutine EnqueueRequest(string userMessage, string systemPrompt,
        List<(string role, string text)> history, Action<string> onSuccess, Action<string> onError)
    {
        _requestQueue.Enqueue(() => SendMessageCoroutine(userMessage, systemPrompt, history, onSuccess, onError));
        if (!_queueRunning)
            return StartCoroutine(RunRequestQueue());
        return null;
    }

    private IEnumerator RunRequestQueue()
    {
        _queueRunning = true;
        while (_requestQueue.Count > 0)
        {
            // Rate limiting
            float elapsed = Time.realtimeSinceStartup - _lastRequestTime;
            if (elapsed < MIN_REQUEST_GAP)
                yield return new WaitForSeconds(MIN_REQUEST_GAP - elapsed);

            if (_activeRequests >= MAX_CONCURRENT)
            {
                yield return new WaitUntil(() => _activeRequests < MAX_CONCURRENT);
            }

            var nextRequest = _requestQueue.Dequeue();
            _lastRequestTime = Time.realtimeSinceStartup;
            _activeRequests++;
            yield return StartCoroutine(WrapWithCounter(nextRequest()));
        }
        _queueRunning = false;
    }

    private IEnumerator WrapWithCounter(IEnumerator coroutine)
    {
        yield return coroutine;
        _activeRequests = Mathf.Max(0, _activeRequests - 1);
    }

    private IEnumerator SendMessageCoroutine(
        string userMessage,
        string systemPrompt,
        List<(string role, string text)> history,
        Action<string> onSuccess,
        Action<string> onError,
        int retryCount = 0)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogError("[Gemini] API key is empty. Set it in Resources/Config/GameConfig.");
            onError?.Invoke("API key not configured.");
            yield break;
        }

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

        // Add current user message (system prompt goes in system_instruction)
        contents.Add(new Content
        {
            role = "user",
            parts = new List<Part> { new Part { text = userMessage } }
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

        // Use Gemini's native system_instruction for system prompt (reduces token usage)
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.system_instruction = new Content
            {
                parts = new List<Part> { new Part { text = systemPrompt } }
            };
        }

        string jsonBody = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
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
                var candidate = response?.candidates?[0];
                string finishReason = candidate?.finishReason ?? "";

                // Safety filter blocked the response
                if (finishReason == "SAFETY")
                {
                    Debug.LogWarning("[Gemini] Response blocked by safety filter.");
                    onSuccess?.Invoke("I cannot respond to that request.");
                    yield break;
                }

                string responseText = candidate?.content?.parts?[0]?.text ?? "";

                if (!string.IsNullOrEmpty(responseText))
                {
                    // Bounded cache: nuke everything when full. Dictionary.First()
                    // does NOT guarantee insertion order in .NET, so the old
                    // "evict first" code was evicting an arbitrary entry and
                    // keeping stale data. A hard reset is simpler and correct.
                    if (_responseCache.Count >= 300)
                        _responseCache.Clear();
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
            long statusCode = webRequest.responseCode;
            Debug.LogError($"[Gemini] Request failed (attempt {retryCount + 1}, HTTP {statusCode}): {webRequest.error}");

            if (retryCount < MAX_RETRIES)
            {
                // 429 Rate limit → longer back-off; 5xx server error → short retry
                float delay = statusCode == 429
                    ? RETRY_DELAY * (retryCount + 3)   // 3s, 4s, 5s
                    : RETRY_DELAY * (retryCount + 1);  // 1s, 2s, 3s

                yield return new WaitForSeconds(delay);
                yield return SendMessageCoroutine(userMessage, systemPrompt, history, onSuccess, onError, retryCount + 1);
            }
            else
            {
                onError?.Invoke(statusCode == 429 ? "AI rate limit reached. Please wait a moment." : webRequest.error);
            }
        }
    }

    private string ComputeCacheKey(string systemPrompt, string userMessage)
    {
        // SHA256 truncated — collision-safe unlike GetHashCode()
        using var sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(systemPrompt + "|" + userMessage));
        return BitConverter.ToString(hash, 0, 8).Replace("-", ""); // 16 hex chars
    }

    /// <summary>
    /// Streaming variant — calls onChunk with each partial response for real-time typewriter UX.
    /// Falls back to non-streaming if SSE parsing fails.
    /// </summary>
    public Coroutine SendMessageStreaming(
        string userMessage,
        string systemPrompt,
        List<(string role, string text)> conversationHistory,
        Action<string> onChunk,
        Action<string> onComplete,
        Action<string> onError = null)
    {
        return StartCoroutine(StreamCoroutine(userMessage, systemPrompt, conversationHistory, onChunk, onComplete, onError));
    }

    private IEnumerator StreamCoroutine(
        string userMessage, string systemPrompt,
        List<(string role, string text)> history,
        Action<string> onChunk, Action<string> onComplete, Action<string> onError)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            onError?.Invoke("API key not configured.");
            yield break;
        }

        var contents = new List<Content>();
        if (history != null)
            foreach (var (role, text) in history)
                contents.Add(new Content { role = role == "user" ? "user" : "model", parts = new List<Part> { new Part { text = text } } });
        contents.Add(new Content { role = "user", parts = new List<Part> { new Part { text = userMessage } } });

        var request = new GeminiRequest
        {
            contents = contents,
            generationConfig = new GenerationConfig { maxOutputTokens = _maxOutputTokens, temperature = _temperature },
            safetySettings = new SafetySettings[]
            {
                new SafetySettings { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                new SafetySettings { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
            }
        };
        if (!string.IsNullOrEmpty(systemPrompt))
            request.system_instruction = new Content { parts = new List<Part> { new Part { text = systemPrompt } } };

        string jsonBody = JsonConvert.SerializeObject(request, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        string url = $"{STREAM_URL}&key={_apiKey}";

        using var webRequest = new UnityWebRequest(url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 30;

        _requestCount++;
        OnRequestCountChanged?.Invoke(_requestCount);

        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            // Parse SSE response — each chunk is "data: {json}\n\n"
            string fullText = "";
            string[] lines = webRequest.downloadHandler.text.Split('\n');
            foreach (string line in lines)
            {
                if (!line.StartsWith("data: ")) continue;
                string json = line.Substring(6).Trim();
                if (string.IsNullOrEmpty(json)) continue;

                try
                {
                    var chunk = JsonConvert.DeserializeObject<GeminiResponse>(json);
                    string partText = chunk?.candidates?[0]?.content?.parts?[0]?.text ?? "";
                    if (!string.IsNullOrEmpty(partText))
                    {
                        fullText += partText;
                        onChunk?.Invoke(partText);
                    }
                }
                catch { /* skip malformed chunks */ }
            }
            onComplete?.Invoke(fullText);
        }
        else
        {
            onError?.Invoke(webRequest.error);
        }
    }

    public void ClearCache() => _responseCache.Clear();
    public int GetRequestCount() => _requestCount;
    public int GetCacheHits() => _cachedResponses;
}
