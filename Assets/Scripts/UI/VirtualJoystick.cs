using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// M16 roaming pivot — on-screen virtual joystick for mobile touch input.
///
/// Spawns its own Canvas + knob/background images at runtime via
/// RuntimeInitializeOnLoadMethod so no scene YAML edit is needed.
/// PlayerController reads InputVector each frame.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    /// <summary>Normalized direction vector. Zero when not touching.</summary>
    public Vector2 InputVector { get; private set; }

    private RectTransform _background;
    private RectTransform _knob;
    private float _radius;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (FindFirstObjectByType<VirtualJoystick>() != null) return;

        // Root canvas — screen-space overlay, renders above the game world
        // but below DialogueBoxUI (sortingOrder 5000).
        var canvasGO = new GameObject("VirtualJoystickCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution =
            new Vector2(1080, 1920);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Ensure EventSystem exists (required for touch/pointer events).
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Object.DontDestroyOnLoad(es);
        }

        // Background circle — try Kenney mobile control sprite first.
        float bgSize = 160f;
        var bgGO = new GameObject("JoystickBG");
        bgGO.transform.SetParent(canvasGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>();
        var bgSprite = Resources.Load<Sprite>("Art/UI/Joystick/joystick_bg");
        if (bgSprite != null)
        {
            bgImg.sprite = bgSprite;
            bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        }
        else
        {
            bgImg.color = new Color(1f, 1f, 1f, 0.15f);
        }
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0f, 0f);
        bgRT.anchorMax = new Vector2(0f, 0f);
        bgRT.pivot = new Vector2(0.5f, 0.5f);
        bgRT.anchoredPosition = new Vector2(100f, 100f);
        bgRT.sizeDelta = new Vector2(bgSize, bgSize);

        // Knob — try Kenney mobile control sprite first.
        float knobSize = 60f;
        var knobGO = new GameObject("JoystickKnob");
        knobGO.transform.SetParent(bgGO.transform, false);
        var knobImg = knobGO.AddComponent<Image>();
        var knobSprite = Resources.Load<Sprite>("Art/UI/Joystick/joystick_knob");
        if (knobSprite != null)
        {
            knobImg.sprite = knobSprite;
            knobImg.color = new Color(1f, 1f, 1f, 0.4f);
        }
        else
        {
            knobImg.color = new Color(1f, 1f, 1f, 0.4f);
        }
        var knobRT = knobGO.GetComponent<RectTransform>();
        knobRT.anchorMin = new Vector2(0.5f, 0.5f);
        knobRT.anchorMax = new Vector2(0.5f, 0.5f);
        knobRT.pivot = new Vector2(0.5f, 0.5f);
        knobRT.anchoredPosition = Vector2.zero;
        knobRT.sizeDelta = new Vector2(knobSize, knobSize);

        // Attach the VirtualJoystick component to the background.
        var joystick = bgGO.AddComponent<VirtualJoystick>();
        joystick._background = bgRT;
        joystick._knob = knobRT;
        joystick._radius = bgSize * 0.5f;

        Object.DontDestroyOnLoad(canvasGO);
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _background, eventData.position, eventData.pressEventCamera, out localPoint);

        // Clamp to radius.
        Vector2 clamped = Vector2.ClampMagnitude(localPoint, _radius);
        _knob.anchoredPosition = clamped;
        InputVector = clamped / _radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _knob.anchoredPosition = Vector2.zero;
        InputVector = Vector2.zero;
    }
}
