using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// M16 roaming pivot — minimal HUD showing resources and day count.
/// Procedurally built, no scene YAML needed. Updates from ResourceManager events.
/// </summary>
public class RoamingHUD : MonoBehaviour
{
    public static RoamingHUD Instance { get; private set; }

    private TextMeshProUGUI _resourceText;
    private TextMeshProUGUI _dayText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (Instance != null) return;
        var host = new GameObject("RoamingHUD");
        host.AddComponent<RoamingHUD>();
        Object.DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildLayout();
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += OnStateChanged;
            GameManager.Instance.OnDayChanged += OnDayChanged;
        }
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= OnStateChanged;
            GameManager.Instance.OnDayChanged -= OnDayChanged;
        }
    }

    private void OnStateChanged(GameManager.GameState old, GameManager.GameState state)
    {
        bool visible = state == GameManager.GameState.Castle || state == GameManager.GameState.WorldMap;
        if (_resourceText != null) _resourceText.transform.parent.gameObject.SetActive(visible);
        if (visible) Refresh();
    }

    private void OnDayChanged(int day) => Refresh();

    private void Start()
    {
        // Delayed subscribe in case GameManager isn't ready in Awake
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged += OnStateChanged;
            GameManager.Instance.OnDayChanged += OnDayChanged;
        }
        InvokeRepeating(nameof(Refresh), 1f, 2f);
    }

    private void BuildLayout()
    {
        var canvasGO = new GameObject("HUDCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Top bar background
        var barGO = new GameObject("TopBar");
        barGO.transform.SetParent(canvasGO.transform, false);
        var barImg = barGO.AddComponent<Image>();
        barImg.color = new Color(0.15f, 0.1f, 0.05f, 0.8f);
        var barRT = barGO.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0f, 1f);
        barRT.anchorMax = new Vector2(1f, 1f);
        barRT.pivot = new Vector2(0.5f, 1f);
        barRT.anchoredPosition = Vector2.zero;
        barRT.sizeDelta = new Vector2(0f, 60f);

        // Resource text (left side)
        var resGO = new GameObject("Resources");
        resGO.transform.SetParent(barGO.transform, false);
        _resourceText = resGO.AddComponent<TextMeshProUGUI>();
        _resourceText.fontSize = 22;
        _resourceText.color = new Color(0.95f, 0.85f, 0.6f);
        _resourceText.alignment = TextAlignmentOptions.MidlineLeft;
        _resourceText.richText = false;
        _resourceText.enableWordWrapping = false;
        var resRT = _resourceText.rectTransform;
        resRT.anchorMin = new Vector2(0f, 0f);
        resRT.anchorMax = new Vector2(0.7f, 1f);
        resRT.offsetMin = new Vector2(20f, 5f);
        resRT.offsetMax = new Vector2(0f, -5f);

        // Day text (right side)
        var dayGO = new GameObject("Day");
        dayGO.transform.SetParent(barGO.transform, false);
        _dayText = dayGO.AddComponent<TextMeshProUGUI>();
        _dayText.fontSize = 22;
        _dayText.color = new Color(0.8f, 0.9f, 1f);
        _dayText.alignment = TextAlignmentOptions.MidlineRight;
        _dayText.richText = false;
        _dayText.enableWordWrapping = false;
        var dayRT = _dayText.rectTransform;
        dayRT.anchorMin = new Vector2(0.7f, 0f);
        dayRT.anchorMax = new Vector2(1f, 1f);
        dayRT.offsetMin = new Vector2(0f, 5f);
        dayRT.offsetMax = new Vector2(-20f, -5f);
    }

    private void Refresh()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var rm = gm.ResourceManager;
        if (rm != null && _resourceText != null)
        {
            _resourceText.text = $"Wood: {rm.Wood}  Food: {rm.Food}  Gold: {rm.Gold}  Pop: {rm.Population}";
        }

        if (_dayText != null)
        {
            _dayText.text = gm.GetFormattedDate();
        }
    }
}
