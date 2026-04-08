using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Central UI theme/color palette for consistent modern pixel-art medieval aesthetic.
/// Apply via UIThemeApplier component on UI elements.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "LLM/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Background Colors")]
    public Color BackgroundDeep = new Color(0.13f, 0.09f, 0.20f, 1f);     // #211733 deep purple-black
    public Color BackgroundPanel = new Color(0.18f, 0.13f, 0.27f, 0.95f); // panel bg
    public Color BackgroundCard = new Color(0.22f, 0.16f, 0.33f, 1f);     // card bg

    [Header("Brand Colors")]
    public Color PrimaryPurple = new Color(0.48f, 0.37f, 0.65f, 1f);  // #7B5EA7 royal purple
    public Color AccentGold = new Color(0.94f, 0.75f, 0.25f, 1f);     // #F0C040 gold
    public Color AccentSilver = new Color(0.75f, 0.78f, 0.82f, 1f);   // silver

    [Header("Status Colors")]
    public Color SuccessGreen = new Color(0.24f, 0.74f, 0.39f, 1f);   // #3EBD63
    public Color DangerRed = new Color(0.88f, 0.19f, 0.19f, 1f);      // #E03030
    public Color WarningOrange = new Color(0.95f, 0.55f, 0.10f, 1f);  // warning
    public Color InfoBlue = new Color(0.25f, 0.55f, 0.90f, 1f);       // info

    [Header("Text Colors")]
    public Color TextPrimary = new Color(0.95f, 0.93f, 0.88f, 1f);    // warm white
    public Color TextSecondary = new Color(0.65f, 0.62f, 0.70f, 1f);  // muted
    public Color TextDisabled = new Color(0.40f, 0.38f, 0.45f, 1f);   // disabled

    [Header("Resource Colors")]
    public Color WoodColor = new Color(0.65f, 0.45f, 0.25f, 1f);      // brown
    public Color FoodColor = new Color(0.40f, 0.75f, 0.25f, 1f);      // green
    public Color GoldColor = new Color(0.94f, 0.75f, 0.25f, 1f);      // gold
    public Color PopColor = new Color(0.40f, 0.70f, 0.90f, 1f);       // blue

    [Header("Button Styles")]
    public Color ButtonPrimary = new Color(0.48f, 0.37f, 0.65f, 1f);
    public Color ButtonPrimaryHover = new Color(0.58f, 0.47f, 0.75f, 1f);
    public Color ButtonDanger = new Color(0.70f, 0.15f, 0.15f, 1f);
    public Color ButtonDisabled = new Color(0.30f, 0.28f, 0.35f, 1f);

    [Header("Border/Separator")]
    public Color BorderColor = new Color(0.48f, 0.37f, 0.65f, 0.4f);
    public Color SeparatorColor = new Color(1f, 1f, 1f, 0.08f);

    [Header("Severity Colors")]
    public Color SeverityMinor = new Color(0.80f, 0.80f, 0.30f, 1f);
    public Color SeverityModerate = new Color(1.00f, 0.60f, 0.20f, 1f);
    public Color SeveritySevere = new Color(1.00f, 0.30f, 0.10f, 1f);
    public Color SeverityCritical = new Color(1.00f, 0.00f, 0.00f, 1f);

    [Header("Fonts")]
    public TMP_FontAsset PixelFont;    // Retro pixel font for titles
    public TMP_FontAsset UIFont;       // Clean sans-serif for body text

    [Header("Pixel Art Settings")]
    public FilterMode SpriteFilterMode = FilterMode.Point; // Crisp pixel art
    public int PixelsPerUnit = 32;

    public static UITheme Load()
    {
        return Resources.Load<UITheme>("UI/UITheme");
    }

    public Color GetSeverityColor(EventManager.EventSeverity severity) => severity switch
    {
        EventManager.EventSeverity.Minor => SeverityMinor,
        EventManager.EventSeverity.Moderate => SeverityModerate,
        EventManager.EventSeverity.Severe => SeveritySevere,
        EventManager.EventSeverity.Critical => SeverityCritical,
        _ => TextPrimary
    };

    public Color GetResourceColor(ResourceManager.ResourceType type) => type switch
    {
        ResourceManager.ResourceType.Wood => WoodColor,
        ResourceManager.ResourceType.Food => FoodColor,
        ResourceManager.ResourceType.Gold => GoldColor,
        ResourceManager.ResourceType.Population => PopColor,
        _ => TextPrimary
    };
}

/// <summary>
/// Applies the UITheme to a UI element. Attach to buttons, panels, text.
/// </summary>
public class UIThemeApplier : MonoBehaviour
{
    public enum ThemeRole
    {
        Background, Panel, Card,
        TextPrimary, TextSecondary, TextDisabled,
        ButtonPrimary, ButtonDanger, ButtonDisabled,
        AccentGold, AccentPurple,
        Border
    }

    [SerializeField] private ThemeRole _role = ThemeRole.Panel;
    [SerializeField] private bool _applyOnEnable = true;

    private void OnEnable()
    {
        if (_applyOnEnable) ApplyTheme();
    }

    public void ApplyTheme()
    {
        var theme = UITheme.Load();
        if (theme == null) return;

        Color color = _role switch
        {
            ThemeRole.Background => theme.BackgroundDeep,
            ThemeRole.Panel => theme.BackgroundPanel,
            ThemeRole.Card => theme.BackgroundCard,
            ThemeRole.TextPrimary => theme.TextPrimary,
            ThemeRole.TextSecondary => theme.TextSecondary,
            ThemeRole.TextDisabled => theme.TextDisabled,
            ThemeRole.ButtonPrimary => theme.ButtonPrimary,
            ThemeRole.ButtonDanger => theme.ButtonDanger,
            ThemeRole.ButtonDisabled => theme.ButtonDisabled,
            ThemeRole.AccentGold => theme.AccentGold,
            ThemeRole.AccentPurple => theme.PrimaryPurple,
            ThemeRole.Border => theme.BorderColor,
            _ => Color.white
        };

        var image = GetComponent<Image>();
        if (image != null) { image.color = color; return; }

        var tmp = GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.color = color;
    }
}
