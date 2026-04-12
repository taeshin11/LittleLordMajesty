using UnityEngine;
using TMPro;

/// <summary>
/// M16 roaming pivot — world-space "Press E to Talk to X" prompt that floats
/// above the NPC's head whenever the player is in interact range.
///
/// Sits as a child of the NPC GameObject with a world-space Canvas and a
/// single TMP label. InteractionFinder.Show/Hide is the only caller.
/// </summary>
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private string _format = "E   Talk to {0}";

    /// <summary>
    /// Runtime wiring hook — called by RoamingBootstrap when building
    /// the prompt programmatically (no scene YAML edit needed).
    /// </summary>
    public void SetupRuntime(Canvas canvas, TextMeshProUGUI label)
    {
        _canvas = canvas;
        _label = label;
    }

    private void Awake()
    {
        if (_canvas != null) _canvas.gameObject.SetActive(false);
    }

    public void Show(string displayName)
    {
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(true);
        if (_label != null) _label.text = string.Format(_format, displayName);
    }

    public void Hide()
    {
        if (_canvas == null) return;
        _canvas.gameObject.SetActive(false);
    }
}
