using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
#if !UNITY_WEBGL || UNITY_EDITOR
using System.IO;
#endif
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Generates character portraits, background art, and event illustrations using
/// Gemini 2.5 Flash Image (codename "nano banana", released 2025-08).
///
/// Features:
///   - SHA256-hashed disk cache at persistentDataPath/generated_art/{hash}.png — identical
///     prompts return the cached PNG immediately, zero API cost on repeat.
///   - One-line API: GenerateImage(prompt, onSuccess, onError)
///   - Returns a Texture2D ready to assign to Image.sprite or RawImage.texture
///
/// Cost: ~$0.039/image. All generated images are SynthID-watermarked by Google.
/// </summary>
public class GeminiImageClient : MonoBehaviour
{
    public static GeminiImageClient Instance { get; private set; }

    // Gemini 2.5 Flash Image ("nano banana") — the current stable model for
    // image generation. The "-preview" variant has been removed from the API;
    // calls to it return 404. https://ai.google.dev/gemini-api/docs/models
    private const string IMAGE_MODEL = "gemini-2.5-flash-image";
    private const string API_URL =
        "https://generativelanguage.googleapis.com/v1beta/models/" + IMAGE_MODEL + ":generateContent";

    private const string CACHE_DIR_NAME = "generated_art";

    private string _apiKey = "";
    private string _cacheDir;

    // In-memory cache — always used. Disk cache is desktop/editor only.
    // WebGL has broken System.IO.File behavior under IL2CPP: calls get stripped
    // or mis-invoked with wrong signatures, producing wasm runtime errors like
    // "null function or function signature mismatch". The browser caches
    // fetch responses anyway, so skipping disk cache on WebGL loses nothing.
    private readonly Dictionary<string, Texture2D> _memoryCache = new();

    public int GeneratedCount { get; private set; }
    public int CacheHits { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadAPIKey();

#if !UNITY_WEBGL || UNITY_EDITOR
        _cacheDir = Path.Combine(Application.persistentDataPath, CACHE_DIR_NAME);
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
#endif
    }

    private void LoadAPIKey()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null && !string.IsNullOrEmpty(config.GeminiAPIKey))
            _apiKey = config.GeminiAPIKey;
        else
            Debug.LogWarning("[GeminiImage] API key not configured. Set in Resources/Config/GameConfig.");
    }

    /// <summary>
    /// Generate (or retrieve from cache) an image for the given prompt.
    /// Callbacks run on the main thread.
    /// </summary>
    public Coroutine GenerateImage(string prompt, Action<Texture2D> onSuccess, Action<string> onError = null)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            onError?.Invoke("Empty prompt");
            return null;
        }

        string hash = Sha256(prompt);

        // 1. In-memory cache hit?
        if (_memoryCache.TryGetValue(hash, out var cachedTex) && cachedTex != null)
        {
            CacheHits++;
            onSuccess?.Invoke(cachedTex);
            return null;
        }

        // 2. Disk cache hit (desktop/editor only — WebGL skips this entirely)
        string cachedPath = null;
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!string.IsNullOrEmpty(_cacheDir))
        {
            cachedPath = Path.Combine(_cacheDir, hash + ".png");
            if (File.Exists(cachedPath))
            {
                CacheHits++;
                var tex = LoadTextureFromFile(cachedPath);
                if (tex != null)
                {
                    _memoryCache[hash] = tex;
                    onSuccess?.Invoke(tex);
                    return null;
                }
                // corrupt cache — fall through and regenerate
                try { File.Delete(cachedPath); } catch { }
            }
        }
#endif

        // 3. Miss — hit the API
        return StartCoroutine(GenerateImageCoroutine(prompt, hash, cachedPath, onSuccess, onError));
    }

    private IEnumerator GenerateImageCoroutine(string prompt, string hash, string cachedPath,
        Action<Texture2D> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            onError?.Invoke("API key not configured");
            yield break;
        }

        // Gemini image-gen request shape: contents[0].parts[0].text = prompt,
        // generationConfig.responseModalities = ["TEXT", "IMAGE"]
        var requestObj = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                responseModalities = new[] { "TEXT", "IMAGE" }
            }
        };
        string jsonBody = JsonConvert.SerializeObject(requestObj);

        string url = $"{API_URL}?key={_apiKey}";
        using var req = new UnityWebRequest(url, "POST");
        byte[] body = Encoding.UTF8.GetBytes(jsonBody);
        req.uploadHandler = new UploadHandlerRaw(body);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            string err = $"HTTP {(int)req.responseCode}: {req.error}\n{req.downloadHandler?.text}";
            Debug.LogError($"[GeminiImage] {err}");
            onError?.Invoke(err);
            yield break;
        }

        // Parse response — walk candidates[0].content.parts[*] looking for inlineData.data
        byte[] pngBytes = null;
        try
        {
            var root = JObject.Parse(req.downloadHandler.text);
            var parts = root["candidates"]?[0]?["content"]?["parts"] as JArray;
            if (parts != null)
            {
                foreach (var part in parts)
                {
                    var data = part["inlineData"]?["data"]?.ToString();
                    if (!string.IsNullOrEmpty(data))
                    {
                        pngBytes = Convert.FromBase64String(data);
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[GeminiImage] Parse error: {e.Message}");
            onError?.Invoke("Parse error: " + e.Message);
            yield break;
        }

        if (pngBytes == null || pngBytes.Length == 0)
        {
            Debug.LogError("[GeminiImage] Response had no image data");
            onError?.Invoke("No image data in response");
            yield break;
        }

        // Decode PNG to texture first — this is the most important step
        var texture = new Texture2D(2, 2);
        if (!texture.LoadImage(pngBytes))
        {
            onError?.Invoke("Texture decode failed");
            yield break;
        }

        // Always keep in memory cache
        _memoryCache[hash] = texture;

#if !UNITY_WEBGL || UNITY_EDITOR
        // Disk cache only on platforms where System.IO.File works reliably.
        // WebGL is excluded because IL2CPP's System.IO stripping causes
        // "null function or function signature mismatch" wasm errors.
        if (!string.IsNullOrEmpty(cachedPath))
        {
            try { File.WriteAllBytes(cachedPath, pngBytes); }
            catch (Exception e) { Debug.LogWarning($"[GeminiImage] Cache write failed: {e.Message}"); }
        }
#endif

        GeneratedCount++;
        Debug.Log($"[GeminiImage] Generated #{GeneratedCount} ({pngBytes.Length / 1024} KB)");
        onSuccess?.Invoke(texture);
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static string Sha256(string input)
    {
        using var sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

#if !UNITY_WEBGL || UNITY_EDITOR
    private static Texture2D LoadTextureFromFile(string path)
    {
        try
        {
            byte[] fileData = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2);
            return tex.LoadImage(fileData) ? tex : null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[GeminiImage] Failed to load cached texture: {e.Message}");
            return null;
        }
    }
#endif

    /// <summary>
    /// Clear the cache. Memory cache always, disk cache on desktop/editor only.
    /// </summary>
    public void ClearCache()
    {
        _memoryCache.Clear();
#if !UNITY_WEBGL || UNITY_EDITOR
        if (!string.IsNullOrEmpty(_cacheDir) && Directory.Exists(_cacheDir))
        {
            Directory.Delete(_cacheDir, recursive: true);
            Directory.CreateDirectory(_cacheDir);
        }
#endif
        GeneratedCount = 0;
        CacheHits = 0;
        Debug.Log("[GeminiImage] Cache cleared");
    }
}
