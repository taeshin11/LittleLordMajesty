using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;

/// <summary>
/// Firebase Spark Plan integration - pure REST API calls, no heavy SDK.
/// Handles: Authentication, Firestore data sync, Leaderboards.
/// Uses Firebase REST API to avoid importing the full SDK.
/// </summary>
public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    private string _apiKey;
    private string _projectId;
    private string _databaseURL;
    private string _authToken;
    private string _userId;
    private bool _isAuthenticated;

    private const string AUTH_URL = "https://identitytoolkit.googleapis.com/v1/accounts:signInAnonymously";
    private const string FIRESTORE_BASE = "https://firestore.googleapis.com/v1/projects/{0}/databases/(default)/documents";

    public bool IsAuthenticated => _isAuthenticated;
    public string UserId => _userId;
    public event Action<bool> OnAuthChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadConfig();
    }

    private void Start()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config?.EnableFirebase == true)
            StartCoroutine(AuthenticateAnonymously());
    }

    private void LoadConfig()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null)
        {
            _apiKey = config.FirebaseAPIKey;
            _projectId = config.FirebaseProjectId;
            _databaseURL = config.FirebaseDatabaseURL;
        }
    }

    private IEnumerator AuthenticateAnonymously()
    {
        if (string.IsNullOrEmpty(_apiKey))
        {
            Debug.LogWarning("[Firebase] No API key configured.");
            yield break;
        }

        string url = $"{AUTH_URL}?key={_apiKey}";
        string body = "{\"returnSecureToken\": true}";

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            var response = JsonConvert.DeserializeObject<AuthResponse>(req.downloadHandler.text);
            _authToken = response.idToken;
            _userId = response.localId;
            _isAuthenticated = true;
            Debug.Log($"[Firebase] Authenticated as {_userId}");
            OnAuthChanged?.Invoke(true);

            // Refresh token before expiry
            float expiresIn = float.Parse(response.expiresIn) - 60f;
            StartCoroutine(RefreshTokenAfter(expiresIn));
        }
        else
        {
            Debug.LogError($"[Firebase] Auth failed: {req.error}");
        }
    }

    private IEnumerator RefreshTokenAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        yield return AuthenticateAnonymously();
    }

    [Serializable]
    private class AuthResponse
    {
        public string idToken;
        public string localId;
        public string expiresIn;
    }

    /// <summary>
    /// Saves player save data to Firestore.
    /// </summary>
    public void SaveToCloud(SaveSystem.SaveData data, Action<bool> onComplete = null)
    {
        if (!_isAuthenticated) { onComplete?.Invoke(false); return; }
        StartCoroutine(SaveToFirestore(data, onComplete));
    }

    private IEnumerator SaveToFirestore(SaveSystem.SaveData data, Action<bool> onComplete)
    {
        string url = $"{string.Format(FIRESTORE_BASE, _projectId)}/saves/{_userId}";
        var firestoreDoc = ConvertToFirestoreDocument(data);
        string json = JsonConvert.SerializeObject(firestoreDoc);

        using var req = new UnityWebRequest(url, "PATCH"); // PATCH = upsert
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {_authToken}");

        yield return req.SendWebRequest();

        bool success = req.result == UnityWebRequest.Result.Success;
        if (success) Debug.Log("[Firebase] Cloud save successful");
        else Debug.LogError($"[Firebase] Save failed: {req.error}");
        onComplete?.Invoke(success);
    }

    private object ConvertToFirestoreDocument(SaveSystem.SaveData data)
    {
        return new
        {
            fields = new
            {
                playerName = new { stringValue = data.PlayerName },
                lordTitle = new { stringValue = data.LordTitle },
                day = new { integerValue = data.Day.ToString() },
                year = new { integerValue = data.Year.ToString() },
                gold = new { integerValue = data.Gold.ToString() },
                territories = new { integerValue = data.TerritoriesOwned.ToString() },
                timestamp = new { stringValue = data.SaveTimestamp },
                gameVersion = new { stringValue = data.GameVersion }
            }
        };
    }

    /// <summary>
    /// Posts score to leaderboard.
    /// </summary>
    public void PostLeaderboardScore(int score, string category = "territory_count")
    {
        if (!_isAuthenticated) return;
        StartCoroutine(PostScore(score, category));
    }

    private IEnumerator PostScore(int score, string category)
    {
        string url = $"{string.Format(FIRESTORE_BASE, _projectId)}/leaderboard/{_userId}_{category}";
        var doc = new
        {
            fields = new
            {
                userId = new { stringValue = _userId },
                playerName = new { stringValue = GameManager.Instance?.PlayerName ?? "Unknown" },
                score = new { integerValue = score.ToString() },
                category = new { stringValue = category },
                timestamp = new { stringValue = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") }
            }
        };
        string json = JsonConvert.SerializeObject(doc);

        using var req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {_authToken}");

        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
            Debug.Log($"[Firebase] Leaderboard score posted: {score}");
    }
}
