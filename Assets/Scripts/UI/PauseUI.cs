using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Pause menu: Resume / Save / Main Menu. Wires click handlers at runtime and
/// localizes button labels.
/// </summary>
public class PauseUI : MonoBehaviour
{
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _resumeLabel;
    [SerializeField] private TextMeshProUGUI _saveLabel;
    [SerializeField] private TextMeshProUGUI _mainMenuLabel;

    private void Start()
    {
        var loc = LocalizationManager.Instance;
        if (_titleText != null) _titleText.text = loc?.Get("pause_title") ?? "PAUSED";
        if (_resumeLabel != null) _resumeLabel.text = loc?.Get("pause_resume") ?? "Resume";
        if (_saveLabel != null) _saveLabel.text = loc?.Get("pause_save") ?? "Save";
        if (_mainMenuLabel != null) _mainMenuLabel.text = loc?.Get("pause_main_menu") ?? "Main Menu";

        if (_resumeButton != null) _resumeButton.onClick.AddListener(OnResume);
        if (_saveButton != null) _saveButton.onClick.AddListener(OnSave);
        if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
    }

    private void OnResume()
    {
        // Return to the gameplay state we were in before pause. Castle is the
        // default/resume target.
        GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
    }

    private void OnSave()
    {
        SaveSystem.Save();
        // Show a toast via CastleViewUI if available, otherwise just return to play.
        var castle = FindFirstObjectByType<CastleViewUI>(FindObjectsInactive.Include);
        castle?.ShowNotification(LocalizationManager.Instance?.Get("game_saved") ?? "Game Saved");
        GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
    }

    private void OnMainMenu()
    {
        GameManager.Instance?.SetGameState(GameManager.GameState.MainMenu);
    }
}
