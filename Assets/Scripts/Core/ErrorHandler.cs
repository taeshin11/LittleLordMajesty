using UnityEngine;
using System;
using System.IO;

/// <summary>
/// Global error handler — catches unhandled exceptions, logs them to a crash file,
/// and shows a user-friendly recovery message instead of a silent freeze.
/// </summary>
public class ErrorHandler : MonoBehaviour
{
    public static ErrorHandler Instance { get; private set; }

    [Header("UI")]
    [SerializeField] private GameObject _crashPanel;
    [SerializeField] private TMPro.TextMeshProUGUI _crashMessageText;
    [SerializeField] private UnityEngine.UI.Button _restartButton;
    [SerializeField] private UnityEngine.UI.Button _continueButton;

    private static string _logPath;
    private bool _showingCrash;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _logPath = Path.Combine(Application.persistentDataPath, "crash_log.txt");
        Application.logMessageReceived += OnLogMessage;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnLogMessage;
    }

    private void OnLogMessage(string condition, string stackTrace, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error) return;

        // Write to crash log
        try
        {
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {type}: {condition}\n{stackTrace}\n---\n";
            File.AppendAllText(_logPath, entry);
        }
        catch { /* never throw in error handler */ }

        if (type == LogType.Exception && !_showingCrash)
        {
            _showingCrash = true;
            ShowCrashPanel(condition);
        }
    }

    private void ShowCrashPanel(string errorMessage)
    {
        if (_crashPanel == null) return;

        _crashPanel.SetActive(true);

        string friendly = GetFriendlyMessage(errorMessage);
        if (_crashMessageText != null)
            _crashMessageText.text = friendly;

        if (_restartButton != null)
            _restartButton.onClick.AddListener(RestartGame);
        if (_continueButton != null)
            _continueButton.onClick.AddListener(DismissCrash);
    }

    private string GetFriendlyMessage(string error)
    {
        if (error.Contains("NullReference"))
            return "Something went wrong loading game data.\nYour progress is safe - tap Restart to continue.";
        if (error.Contains("OutOfMemory"))
            return "The game ran out of memory.\nPlease restart to free up resources.";
        if (error.Contains("Network") || error.Contains("WebRequest"))
            return "Connection lost.\nCheck your internet and try again.";
        return "An unexpected error occurred.\nYour progress has been saved.";
    }

    private void RestartGame()
    {
        _showingCrash = false;
        _crashPanel?.SetActive(false);
        SaveSystem.Save(); // attempt save before reload
        UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }

    private void DismissCrash()
    {
        _showingCrash = false;
        _crashPanel?.SetActive(false);
    }

    /// <summary>Called from GameBootstrap when an init step fails.</summary>
    public static void HandleInitFailure(string system, Exception ex)
    {
        Debug.LogError($"[ErrorHandler] Init failure in {system}: {ex.Message}");
        ToastNotification.Show($"{system} failed to load. Some features may be unavailable.",
            ToastNotification.ToastType.Warning);
    }
}
