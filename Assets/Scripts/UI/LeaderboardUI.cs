using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Global leaderboard panel. Fetches top scores from Firebase Firestore.
/// Shows: PlayerName, TerritoryCount, LordTitle, PlayTime
/// </summary>
public class LeaderboardUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform _entriesContainer;
    [SerializeField] private GameObject _entryPrefab;
    [SerializeField] private TextMeshProUGUI _playerRankText;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private GameObject _loadingSpinner;
    [SerializeField] private TextMeshProUGUI _errorText;

    [Header("Tabs")]
    [SerializeField] private Button _territoriesTab;
    [SerializeField] private Button _goldTab;
    [SerializeField] private Button _daysTab;

    private string _currentCategory = "territory_count";
    private bool _isLoading;

    private void Start()
    {
        if (_refreshButton != null) _refreshButton.onClick.AddListener(Refresh);
        if (_territoriesTab != null) _territoriesTab.onClick.AddListener(() => SwitchTab("territory_count"));
        if (_goldTab != null) _goldTab.onClick.AddListener(() => SwitchTab("gold"));
        if (_daysTab != null) _daysTab.onClick.AddListener(() => SwitchTab("days_survived"));
    }

    private void OnEnable() => Refresh();

    private void SwitchTab(string category)
    {
        _currentCategory = category;
        Refresh();
    }

    private void Refresh()
    {
        if (_isLoading) return;
        StartCoroutine(FetchLeaderboard());
    }

    private IEnumerator FetchLeaderboard()
    {
        _isLoading = true;
        SetLoading(true);
        if (_errorText != null) _errorText.gameObject.SetActive(false);

        var firebase = FirebaseManager.Instance;
        if (firebase == null || !firebase.IsAuthenticated)
        {
            ShowError("Cloud not connected.");
            yield break;
        }

        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config == null || string.IsNullOrEmpty(config.FirebaseProjectId))
        {
            ShowError("Firebase not configured.");
            yield break;
        }

        string token = "";
        // Get token via reflection (simplification - in production use proper auth)
        var tokenField = typeof(FirebaseManager).GetField("_authToken",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (tokenField != null) token = (string)tokenField.GetValue(firebase);

        string url = $"https://firestore.googleapis.com/v1/projects/{config.FirebaseProjectId}" +
                     $"/databases/(default)/documents:runQuery";

        var query = new
        {
            structuredQuery = new
            {
                from = new[] { new { collectionId = "leaderboard" } },
                where = new
                {
                    fieldFilter = new
                    {
                        field = new { fieldPath = "category" },
                        op = "EQUAL",
                        value = new { stringValue = _currentCategory }
                    }
                },
                orderBy = new[] { new { field = new { fieldPath = "score" }, direction = "DESCENDING" } },
                limit = 20
            }
        };

        string json = JsonConvert.SerializeObject(query);

        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("Authorization", $"Bearer {token}");

        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            PopulateFromJSON(req.downloadHandler.text);
        }
        else
        {
            ShowError($"Failed to load: {req.error}");
        }

        SetLoading(false);
        _isLoading = false;
    }

    private void PopulateFromJSON(string json)
    {
        // Clear existing entries
        foreach (Transform child in _entriesContainer) Destroy(child.gameObject);

        try
        {
            var results = JsonConvert.DeserializeObject<List<FirestoreQueryResult>>(json);
            int rank = 1;

            foreach (var result in results)
            {
                if (result?.document?.fields == null) continue;
                var fields = result.document.fields;

                string playerName = fields.TryGetValue("playerName", out var pn) ? pn.stringValue : "Unknown";
                string score = fields.TryGetValue("score", out var sc) ? sc.integerValue : "0";

                var entry = Instantiate(_entryPrefab, _entriesContainer);
                var rankText = entry.transform.Find("Rank")?.GetComponent<TextMeshProUGUI>();
                var nameText = entry.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
                var scoreText = entry.transform.Find("Score")?.GetComponent<TextMeshProUGUI>();

                if (rankText != null) rankText.text = $"#{rank}";
                if (nameText != null) nameText.text = playerName;
                if (scoreText != null) scoreText.text = score;

                // Highlight current player
                if (playerName == GameManager.Instance?.PlayerName)
                {
                    if (_playerRankText != null) _playerRankText.text = $"Your Rank: #{rank}";
                    var bg = entry.GetComponent<Image>();
                    if (bg != null) bg.color = new Color(0.5f, 0.4f, 0.7f, 0.3f);
                }

                rank++;
            }
        }
        catch (Exception e)
        {
            ShowError($"Parse error: {e.Message}");
        }
    }

    private void ShowError(string msg)
    {
        SetLoading(false);
        if (_errorText != null)
        {
            _errorText.text = msg;
            _errorText.gameObject.SetActive(true);
        }
        _isLoading = false;
    }

    private void SetLoading(bool loading)
    {
        if (_loadingSpinner != null) _loadingSpinner.SetActive(loading);
        if (_refreshButton != null) _refreshButton.interactable = !loading;
    }

    [Serializable]
    private class FirestoreQueryResult
    {
        public FirestoreDocument document;
    }

    [Serializable]
    private class FirestoreDocument
    {
        public Dictionary<string, FirestoreValue> fields;
    }

    [Serializable]
    private class FirestoreValue
    {
        public string stringValue;
        public string integerValue;
    }
}
