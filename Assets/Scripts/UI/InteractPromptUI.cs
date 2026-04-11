using UnityEngine;
using TMPro;

/// <summary>
/// M16 roaming pivot — world-space "Press E to Talk to X" prompt that floats
/// above the NPC's head whenever the player is in interact range.
///
/// Sits as a child of the NPC GameObject with a world-space Canvas and a
/// single TMP label. LateUpdate keeps the canvas yaw-aligned to the camera
/// so the text always faces the viewer. InteractionFinder.Show/Hide is the
/// only caller — we don't do our own proximity check.
/// </summary>
public class InteractPromptUI : MonoBehaviour
{
    [SerializeField] private Canvas  _canvas;
    [SerializeField] private TextMeshProUGUI _label;
    [Tooltip("Format string. {0} = NPC display name.")]
    [SerializeField] private string _format = "E   Talk to {0}";

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

    private void LateUpdate()
    {
        if (_canvas == null || Camera.main == null) return;
        // Billboard the canvas toward the camera so the text stays legible
        // regardless of NPC facing or camera yaw. We use LookRotation with
        // a +forward (camera-to-canvas) direction to avoid the usual
        // "transform.LookAt" mirror-flip when the camera passes behind.
        Vector3 toCam = _canvas.transform.position - Camera.main.transform.position;
        toCam.y = 0;
        if (toCam.sqrMagnitude > 0.0001f)
            _canvas.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
    }
}
