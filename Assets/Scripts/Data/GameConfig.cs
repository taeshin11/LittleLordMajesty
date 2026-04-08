using UnityEngine;

/// <summary>
/// ScriptableObject for API keys and game configuration.
/// Place instance at Resources/Config/GameConfig
/// DO NOT commit with real API keys - use environment variables or Unity secrets vault.
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "LLM/Game Config")]
public class GameConfig : ScriptableObject
{
    [Header("API Keys - DO NOT COMMIT")]
    [Tooltip("Gemini 1.5 Flash API Key from Google AI Studio")]
    public string GeminiAPIKey = "";

    [Tooltip("Google Cloud API Key for TTS")]
    public string GoogleCloudAPIKey = "";

    [Tooltip("Firebase Web API Key")]
    public string FirebaseAPIKey = "";

    [Header("Firebase Config")]
    public string FirebaseProjectId = "";
    public string FirebaseAuthDomain = "";
    public string FirebaseDatabaseURL = "";
    public string FirebaseStorageBucket = "";

    [Header("Feature Flags")]
    public bool EnableTTS = true;
    public bool EnableFirebase = true;
    public bool EnableDebugLogs = false;

    [Header("Game Settings")]
    [Range(0.5f, 3.0f)] public float TTSSpeakingRate = 1.0f;
    public int MaxCacheResponseCount = 500;
    public int MaxTTSCacheFileSizeMB = 100;

    [Header("AI Settings")]
    [Range(0.1f, 1.0f)] public float GeminiTemperature = 0.85f;
    [Range(64, 1024)] public int GeminiMaxOutputTokens = 512;

    /// <summary>
    /// Load API keys from environment (for CI/CD or secure builds).
    /// </summary>
    public void LoadFromEnvironment()
    {
        string geminiKey = System.Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        string gcpKey = System.Environment.GetEnvironmentVariable("GCP_API_KEY");

        if (!string.IsNullOrEmpty(geminiKey)) GeminiAPIKey = geminiKey;
        if (!string.IsNullOrEmpty(gcpKey)) GoogleCloudAPIKey = gcpKey;

        if (EnableDebugLogs)
            Debug.Log("[GameConfig] Loaded keys from environment.");
    }
}
