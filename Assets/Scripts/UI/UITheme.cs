using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Warm medieval charm palette.
/// Inspired by cozy tavern + Stardew Valley + modern mobile game aesthetics.
/// Dark but warm — candlelit parchment, amber gold, sage green.
/// </summary>
[CreateAssetMenu(fileName = "UITheme", menuName = "LLM/UI Theme")]
public class UITheme : ScriptableObject
{
    [Header("Backgrounds — Warm Candlelit Dark")]
    public Color BackgroundDeep  = new Color(0.10f, 0.07f, 0.04f, 1.00f); // #1A120A near-black warm brown
    public Color BackgroundPanel = new Color(0.17f, 0.11f, 0.06f, 0.97f); // #2C1D0F dark parchment
    public Color BackgroundCard  = new Color(0.22f, 0.15f, 0.08f, 1.00f); // #391E0D rich leather

    [Header("Primary Accent — Amber Gold")]
    public Color PrimaryGold     = new Color(0.96f, 0.72f, 0.16f, 1.00f); // #F5B829 warm amber
    public Color PrimaryGoldDim  = new Color(0.70f, 0.52f, 0.12f, 1.00f); // dimmed gold
    public Color AccentCream     = new Color(0.96f, 0.91f, 0.78f, 1.00f); // #F5E7C6 parchment cream

    [Header("Secondary Accents")]
    public Color AccentSage      = new Color(0.44f, 0.64f, 0.37f, 1.00f); // #71A35E sage green
    public Color AccentSkyBlue   = new Color(0.38f, 0.66f, 0.82f, 1.00f); // #61A8D1 sky blue
    public Color AccentRose      = new Color(0.85f, 0.42f, 0.42f, 1.00f); // #D96B6B muted rose
    public Color AccentLavender  = new Color(0.62f, 0.50f, 0.78f, 1.00f); // #9E80C7 soft purple

    [Header("Status")]
    public Color SuccessGreen    = new Color(0.38f, 0.76f, 0.38f, 1.00f); // #62C262
    public Color DangerRed       = new Color(0.86f, 0.25f, 0.25f, 1.00f); // #DB4040
    public Color WarningAmber    = new Color(0.96f, 0.65f, 0.10f, 1.00f); // #F5A61A
    public Color InfoBlue        = new Color(0.30f, 0.60f, 0.92f, 1.00f); // #4D99EB

    [Header("Text")]
    public Color TextPrimary     = new Color(0.96f, 0.91f, 0.80f, 1.00f); // warm white cream
    public Color TextSecondary   = new Color(0.70f, 0.64f, 0.52f, 1.00f); // aged parchment
    public Color TextDisabled    = new Color(0.42f, 0.38f, 0.30f, 1.00f); // faded ink
    public Color TextGold        = new Color(0.96f, 0.78f, 0.28f, 1.00f); // title gold

    [Header("Resources")]
    public Color WoodColor       = new Color(0.67f, 0.48f, 0.25f, 1.00f); // warm oak
    public Color FoodColor       = new Color(0.50f, 0.80f, 0.30f, 1.00f); // fresh green
    public Color GoldColor       = new Color(0.96f, 0.72f, 0.16f, 1.00f); // coin gold
    public Color PopColor        = new Color(0.45f, 0.72f, 0.92f, 1.00f); // blue

    [Header("Buttons")]
    public Color ButtonPrimary      = new Color(0.55f, 0.38f, 0.14f, 1.00f); // warm brown
    public Color ButtonPrimaryHover = new Color(0.68f, 0.50f, 0.22f, 1.00f);
    public Color ButtonSuccess      = new Color(0.28f, 0.56f, 0.24f, 1.00f); // forest green
    public Color ButtonDanger       = new Color(0.62f, 0.16f, 0.16f, 1.00f); // dark crimson
    public Color ButtonDisabled     = new Color(0.28f, 0.22f, 0.14f, 1.00f);
    public Color ButtonGold         = new Color(0.70f, 0.52f, 0.10f, 1.00f); // golden CTA

    [Header("Borders & Decorations")]
    public Color BorderGold      = new Color(0.75f, 0.55f, 0.18f, 0.60f); // subtle gold border
    public Color BorderSoft      = new Color(1.00f, 0.90f, 0.70f, 0.12f); // very faint cream
    public Color SeparatorColor  = new Color(1.00f, 0.85f, 0.60f, 0.10f);
    public Color ShadowColor     = new Color(0.00f, 0.00f, 0.00f, 0.45f);

    [Header("Severity")]
    public Color SeverityMinor    = new Color(0.85f, 0.80f, 0.25f, 1.00f);
    public Color SeverityModerate = new Color(0.96f, 0.58f, 0.15f, 1.00f);
    public Color SeveritySevere   = new Color(0.90f, 0.28f, 0.12f, 1.00f);
    public Color SeverityCritical = new Color(0.95f, 0.05f, 0.05f, 1.00f);

    [Header("Fonts")]
    public TMP_FontAsset TitleFont;  // Display/title — bold, medieval-ish
    public TMP_FontAsset BodyFont;   // Readable body text
    public TMP_FontAsset AccentFont; // Small caps, UI labels

    [Header("Corner Radius (for shader/9-slice)")]
    public float CornerRadiusSmall  = 6f;
    public float CornerRadiusMedium = 12f;
    public float CornerRadiusLarge  = 20f;

    // ─────────────────────────────────────────────────────────────

    public static UITheme Load() => Resources.Load<UITheme>("Config/UITheme");

    public Color GetSeverityColor(EventManager.EventSeverity s) => s switch
    {
        EventManager.EventSeverity.Minor    => SeverityMinor,
        EventManager.EventSeverity.Moderate => SeverityModerate,
        EventManager.EventSeverity.Severe   => SeveritySevere,
        EventManager.EventSeverity.Critical => SeverityCritical,
        _ => TextPrimary
    };

    public Color GetResourceColor(ResourceManager.ResourceType t) => t switch
    {
        ResourceManager.ResourceType.Wood       => WoodColor,
        ResourceManager.ResourceType.Food       => FoodColor,
        ResourceManager.ResourceType.Gold       => GoldColor,
        ResourceManager.ResourceType.Population => PopColor,
        _ => TextPrimary
    };

    public Color GetProfessionColor(NPCPersona.NPCProfession p) => p switch
    {
        NPCPersona.NPCProfession.Soldier  => AccentRose,
        NPCPersona.NPCProfession.Farmer   => AccentSage,
        NPCPersona.NPCProfession.Merchant => PrimaryGold,
        NPCPersona.NPCProfession.Vassal   => AccentLavender,
        NPCPersona.NPCProfession.Scholar  => AccentSkyBlue,
        NPCPersona.NPCProfession.Priest   => AccentCream,
        NPCPersona.NPCProfession.Spy      => new Color(0.25f, 0.25f, 0.30f, 1f),
        _ => TextSecondary
    };
}

/// <summary>
/// Applies theme colors to UI elements automatically.
/// Attach to any Image or TextMeshProUGUI.
/// </summary>
public class UIThemeApplier : MonoBehaviour
{
    public enum ThemeRole
    {
        // Backgrounds
        BgDeep, BgPanel, BgCard,
        // Text
        TextPrimary, TextSecondary, TextDisabled, TextGold,
        // Buttons
        BtnPrimary, BtnSuccess, BtnDanger, BtnDisabled, BtnGold,
        // Accents
        Gold, Sage, SkyBlue, Rose, Lavender, Cream,
        // Resources
        Wood, Food, GoldRes, Population,
        // Borders
        BorderGold, BorderSoft
    }

    [SerializeField] private ThemeRole _role = ThemeRole.BgPanel;

    private void OnEnable()  => ApplyTheme();
    private void Start()     => ApplyTheme();

    public void ApplyTheme()
    {
        var t = UITheme.Load();
        if (t == null) return;

        Color c = _role switch
        {
            ThemeRole.BgDeep      => t.BackgroundDeep,
            ThemeRole.BgPanel     => t.BackgroundPanel,
            ThemeRole.BgCard      => t.BackgroundCard,
            ThemeRole.TextPrimary => t.TextPrimary,
            ThemeRole.TextSecondary=> t.TextSecondary,
            ThemeRole.TextDisabled => t.TextDisabled,
            ThemeRole.TextGold    => t.TextGold,
            ThemeRole.BtnPrimary  => t.ButtonPrimary,
            ThemeRole.BtnSuccess  => t.ButtonSuccess,
            ThemeRole.BtnDanger   => t.ButtonDanger,
            ThemeRole.BtnDisabled => t.ButtonDisabled,
            ThemeRole.BtnGold     => t.ButtonGold,
            ThemeRole.Gold        => t.PrimaryGold,
            ThemeRole.Sage        => t.AccentSage,
            ThemeRole.SkyBlue     => t.AccentSkyBlue,
            ThemeRole.Rose        => t.AccentRose,
            ThemeRole.Lavender    => t.AccentLavender,
            ThemeRole.Cream       => t.AccentCream,
            ThemeRole.Wood        => t.WoodColor,
            ThemeRole.Food        => t.FoodColor,
            ThemeRole.GoldRes     => t.GoldColor,
            ThemeRole.Population  => t.PopColor,
            ThemeRole.BorderGold  => t.BorderGold,
            ThemeRole.BorderSoft  => t.BorderSoft,
            _ => Color.white
        };

        var img = GetComponent<Image>();
        if (img != null) { img.color = c; return; }
        var tmp = GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.color = c;
    }
}
