using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

/// <summary>
/// Main castle view - shows NPCs as pixel sprites, buildings, and resource HUD.
/// Tap NPCs to interact. Tap buildings to construct/upgrade.
/// </summary>
public class CastleViewUI : MonoBehaviour
{
    // 3D castle scene is rendered by CastleScene3D (world space).
    // This UI is a screen-space overlay — no 2D NPC/building sprites here.

    [Header("Background Art (Gemini-generated)")]
    [SerializeField] private Image _backgroundArt;

    [Header("HUD Top Bar")]
    [SerializeField] private TextMeshProUGUI _lordTitleText;
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private Button _menuButton;
    [SerializeField] private Button _worldMapButton;
    [SerializeField] private Button _settingsButton;

    [Header("Resource HUD")]
    [SerializeField] private TextMeshProUGUI _woodText;
    [SerializeField] private TextMeshProUGUI _foodText;
    [SerializeField] private TextMeshProUGUI _goldText;
    [SerializeField] private TextMeshProUGUI _populationText;

    [Header("Notification Banner")]
    [SerializeField] private GameObject _notificationBanner;
    [SerializeField] private TextMeshProUGUI _notificationText;

    [Header("Bottom Action Bar")]
    [SerializeField] private Button _buildButton;
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _npcListButton;

    [Header("NPC List Drawer")]
    [SerializeField] private GameObject _npcListPanel;
    [SerializeField] private Transform _npcListContent;
    [SerializeField] private GameObject _npcListItemPrefab;

    [Header("Building Menu")]
    [SerializeField] private GameObject _buildingMenuPanel;
    [SerializeField] private Transform _buildingMenuContent;
    [SerializeField] private GameObject _buildingMenuItemPrefab;

    private NPCInteractionUI _npcInteractionUI;
    private Action<int> _dayChangedHandler;

    private void Start()
    {
        // Defensive: every step is wrapped so a single failure doesn't take
        // out the whole castle entry. Logged warnings tell us which step
        // tripped without crashing the wasm runtime. Crash-bisect logs tag
        // each successful step so we can see which one was the LAST to
        // run before the wasm null-function crash.
        Debug.Log("[Crash-Bisect] CastleViewUI.Start entry");

#if UNITY_WEBGL && !UNITY_EDITOR
        // Crash-bisect 13: disable ActionBar + ObjectiveText. Everything
        // else enabled. Process of elimination: attempts 11 & 12 both left
        // ObjectiveText enabled and both crashed; attempt 9 had it off and
        // did not. If this fixes it we have nailed the culprit.
        try
        {
            if (_buildButton != null && _buildButton.transform.parent != null)
                _buildButton.transform.parent.gameObject.SetActive(false);
            var panel = transform;
            var objective = panel.Find("ObjectiveText");
            if (objective != null) { objective.gameObject.SetActive(false); Debug.Log("[Crash-Bisect] Disabled ObjectiveText"); }
        }
        catch (System.Exception e) { Debug.LogError($"[Crash-Bisect] Disable: {e.Message}"); }
#endif

        try { _npcInteractionUI = FindFirstObjectByType<NPCInteractionUI>(FindObjectsInactive.Include); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] Find NPCInteractionUI: {e.Message}"); }
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after FindNPCInteractionUI");

        try { SetupButtons(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] SetupButtons: {e.Message}"); }
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after SetupButtons");

        try { SubscribeToEvents(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] SubscribeToEvents: {e.Message}"); }
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after SubscribeToEvents");

        try { RefreshResourceHUD(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] RefreshResourceHUD: {e.Message}"); }
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after RefreshResourceHUD");

        try { UpdateLordInfo(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] UpdateLordInfo: {e.Message}"); }
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after UpdateLordInfo");

        // Crash-bisect: SKIP RequestBackgroundArt on WebGL during this iteration.
        // Gemini JsonConvert.SerializeObject of anonymous types hits IL2CPP
        // reflection stripping and is a suspect for the post-Start render crash.
#if !UNITY_WEBGL || UNITY_EDITOR
        try { RequestBackgroundArt(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] RequestBackgroundArt: {e.Message}"); }
#else
        Debug.Log("[Crash-Bisect] CastleViewUI Start: SKIPPED RequestBackgroundArt on WebGL");
#endif
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after RequestBackgroundArt");

        // Crash-bisect: SKIP PopulateNPCList on WebGL for this iteration.
        // Dynamic UI spawning + RequestPortrait(Gemini) calls are suspects.
#if !UNITY_WEBGL || UNITY_EDITOR
        try
        {
            if (_npcListPanel != null)
            {
                _npcListPanel.SetActive(true);
                PopulateNPCList();
            }
        }
        catch (System.Exception e) { Debug.LogError($"[CastleView] PopulateNPCList: {e.Message}\n{e.StackTrace}"); }
#else
        Debug.Log("[Crash-Bisect] CastleViewUI Start: SKIPPED PopulateNPCList on WebGL");
#endif
        Debug.Log("[Crash-Bisect] CastleViewUI Start: after PopulateNPCList");

#if !UNITY_WEBGL || UNITY_EDITOR
        try { ShowWelcomeHint(); }
        catch (System.Exception e) { Debug.LogError($"[CastleView] ShowWelcomeHint: {e.Message}"); }
#else
        Debug.Log("[Crash-Bisect] CastleViewUI Start: SKIPPED ShowWelcomeHint on WebGL");
#endif
        Debug.Log("[Crash-Bisect] CastleViewUI Start: COMPLETE");
    }

    private void ShowWelcomeHint()
    {
        string msg = LocalizationManager.Instance?.Get("welcome_hint")
                     ?? "Welcome to your castle! Tap an NPC card to give commands, or use Build to construct buildings.";
        ShowNotification(msg);
    }

    /// <summary>
    /// Kicks off Gemini background art generation for the castle courtyard. Cached
    /// to disk after first generation so returning to the Castle scene is instant.
    /// </summary>
    private void RequestBackgroundArt()
    {
        if (_backgroundArt == null || GeminiImageClient.Instance == null) return;

        const string prompt =
            "Medieval fantasy castle courtyard at golden hour, painterly oil-painting style, " +
            "warm torchlight, stone walls, wooden scaffolding, distant towers, dramatic sky, " +
            "atmospheric depth, no characters, cinematic wide establishing shot, detailed background art.";

        var targetImage = _backgroundArt;
        GeminiImageClient.Instance.GenerateImage(prompt,
            onSuccess: tex =>
            {
                if (targetImage == null) return;
                var sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                targetImage.sprite = sprite;
                targetImage.color = new Color(1f, 1f, 1f, 0.85f); // slight dim for HUD legibility
                targetImage.preserveAspect = false;
            },
            onError: err => Debug.LogWarning($"[CastleView] Background art generation failed: {err}"));
    }

    private void SetupButtons()
    {
        if (_worldMapButton != null) _worldMapButton.onClick.AddListener(() =>
            GameManager.Instance?.SetGameState(GameManager.GameState.WorldMap));
        if (_buildButton != null) _buildButton.onClick.AddListener(ToggleBuildingMenu);
        if (_saveButton != null) _saveButton.onClick.AddListener(OnSaveClicked);
        if (_npcListButton != null) _npcListButton.onClick.AddListener(ToggleNPCList);
        if (_menuButton != null) _menuButton.onClick.AddListener(() =>
            GameManager.Instance?.TogglePause());

        // Apply localized labels to the action-bar buttons at runtime. The
        // scene-baked labels are English defaults; without this pass a user
        // who switched to ko/ja/zh/de/es/fr would still see English words.
        var loc = LocalizationManager.Instance;
        if (loc != null)
        {
            SetButtonLabel(_buildButton,    loc.Get("btn_build"));
            SetButtonLabel(_saveButton,     loc.Get("btn_save"));
            SetButtonLabel(_npcListButton,  loc.Get("btn_npcs"));
            SetButtonLabel(_worldMapButton, loc.Get("btn_map"));
            SetButtonLabel(_settingsButton, loc.Get("btn_options"));
            SetButtonLabel(_menuButton,     loc.Get("btn_pause"));
        }
    }

    /// <summary>
    /// Writes a label string into the first TextMeshProUGUI child of a
    /// button, defensively no-op on null. Centralised so the action-bar
    /// wiring stays a single line per button.
    /// </summary>
    private static void SetButtonLabel(Button btn, string text)
    {
        if (btn == null || string.IsNullOrEmpty(text)) return;
        var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmp != null) tmp.text = text;
    }

    private void SubscribeToEvents()
    {
        var gm = GameManager.Instance;
        if (gm?.ResourceManager != null)
            gm.ResourceManager.OnResourceChanged += OnResourceChanged;

        if (gm != null)
        {
            _dayChangedHandler = _ => UpdateLordInfo();
            gm.OnDayChanged += _dayChangedHandler;
        }
    }

    private void OnResourceChanged(ResourceManager.ResourceType type, int oldVal, int newVal)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        switch (type)
        {
            case ResourceManager.ResourceType.Wood:
                if (_woodText != null) _woodText.text = FormatNamedResource("hud_wood", newVal);
                break;
            case ResourceManager.ResourceType.Food:
                if (_foodText != null) _foodText.text = FormatNamedResource("hud_food", newVal);
                break;
            case ResourceManager.ResourceType.Gold:
                if (_goldText != null) _goldText.text = FormatNamedResource("hud_gold", newVal);
                break;
            case ResourceManager.ResourceType.Population:
                if (_populationText != null)
                    _populationText.text = $"{LocName("hud_population", "Pop")} {newVal}/{rm.MaxPopulation}";
                break;
        }
    }

    /// <summary>
    /// Formats a resource like "Wood 500" / "Holz 500" / "Gold 1,240" — the
    /// prefix is resolved via LocalizationManager so the HUD matches the
    /// player's language. Falls back to the English word if no translation
    /// is registered.
    /// </summary>
    private string FormatNamedResource(string locKey, int value) =>
        $"{LocName(locKey, locKey)} {value:N0}";

    private static string LocName(string key, string fallback) =>
        LocalizationManager.Instance?.Get(key) ?? fallback;

    private void RefreshResourceHUD()
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;
        if (_woodText != null) _woodText.text = FormatNamedResource("hud_wood", rm.Wood);
        if (_foodText != null) _foodText.text = FormatNamedResource("hud_food", rm.Food);
        if (_goldText != null) _goldText.text = FormatNamedResource("hud_gold", rm.Gold);
        if (_populationText != null)
            _populationText.text = $"{LocName("hud_population", "Pop")} {rm.Population}/{rm.MaxPopulation}";
    }

    private static string FormatLordDisplay(string title, string name)
    {
        // Avoid "Little Lord Lord" when the player used the default name "Lord"
        // (or any name that duplicates the current title, e.g. "Baron Baron").
        if (string.IsNullOrEmpty(name)) return title ?? "";
        if (string.IsNullOrEmpty(title)) return name;
        if (string.Equals(name, title, System.StringComparison.OrdinalIgnoreCase))
            return title;
        if (title.EndsWith(name, System.StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(title, System.StringComparison.OrdinalIgnoreCase))
            return title;
        return $"{title} {name}";
    }

    private void UpdateLordInfo()
    {
        var gm = GameManager.Instance;
        if (_lordTitleText != null)
            _lordTitleText.text = FormatLordDisplay(gm?.LordTitle, gm?.PlayerName);
        if (_dateText != null)
            _dateText.text = gm?.GetFormattedDate() ?? "";
    }

    private void ToggleNPCList()
    {
        if (_npcListPanel == null) return;
        bool show = !_npcListPanel.activeSelf;
        _npcListPanel.SetActive(show);
        if (show) PopulateNPCList();
    }

    private void PopulateNPCList()
    {
        if (_npcListContent == null) return;
        foreach (Transform child in _npcListContent) Destroy(child.gameObject);

        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null || npcs.Count == 0) return;

        // Grid layout: 4 columns, auto-wrap. Each card is large and prominent so
        // NPCs are the VISUAL CENTERPIECE of the castle view — no more hidden
        // drawer, no more "can't see the characters" feedback.
        var grid = _npcListContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = _npcListContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize   = new Vector2(240, 280);
            grid.spacing    = new Vector2(20, 20);
            grid.padding    = new RectOffset(20, 20, 20, 20);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            grid.childAlignment  = TextAnchor.MiddleCenter;
        }
        // Remove any leftover vertical/content-size fitter from previous builds
        var oldVlg = _npcListContent.GetComponent<VerticalLayoutGroup>();
        if (oldVlg != null) Destroy(oldVlg);
        var oldCsf = _npcListContent.GetComponent<ContentSizeFitter>();
        if (oldCsf != null) Destroy(oldCsf);

        foreach (var npc in npcs)
            BuildNPCCard(npc);
    }

    /// <summary>
    /// Builds a rich NPC card procedurally: portrait slot (filled by Gemini later),
    /// name, profession, mood bar, and a Talk button. Replaces the old prefab-based
    /// path which relied on a prefab that was a skeleton.
    /// </summary>
    private void BuildNPCCard(NPCManager.NPCData npc)
    {
        const float CARD_W = 240f;
        const float CARD_H = 280f;

        // Card root — GridLayoutGroup controls size via cellSize, but we still set
        // explicit sizeDelta + LayoutElement as a belt-and-suspenders measure.
        var card = new GameObject($"NPCCard_{npc.Id}");
        card.transform.SetParent(_npcListContent, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(CARD_W, CARD_H);
        var cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.14f, 0.12f, 0.20f, 0.96f);

        var layoutElem = card.AddComponent<LayoutElement>();
        layoutElem.preferredWidth  = CARD_W;
        layoutElem.preferredHeight = CARD_H;
        layoutElem.minWidth  = CARD_W;
        layoutElem.minHeight = CARD_H;

        // Frame border — ornamental gold border around the card
        var frameGO = new GameObject("Frame");
        frameGO.transform.SetParent(card.transform, false);
        var frameImg = frameGO.AddComponent<Image>();
        frameImg.color = new Color(0.6f, 0.45f, 0.15f, 0.9f);
        frameImg.raycastTarget = false;
        var frameRT = frameGO.GetComponent<RectTransform>();
        frameRT.anchorMin = Vector2.zero; frameRT.anchorMax = Vector2.one;
        frameRT.offsetMin = new Vector2(-3, -3); frameRT.offsetMax = new Vector2(3, 3);
        frameGO.transform.SetAsFirstSibling(); // render behind the card bg

        // Big portrait area at the TOP of the card — square, prominent
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(card.transform, false);
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.color = ProfessionColor(npc.Profession);
        portraitImg.raycastTarget = false;
        var portraitRT = portraitGO.GetComponent<RectTransform>();
        portraitRT.anchorMin = new Vector2(0.5f, 1);
        portraitRT.anchorMax = new Vector2(0.5f, 1);
        portraitRT.pivot     = new Vector2(0.5f, 1);
        portraitRT.anchoredPosition = new Vector2(0, -12);
        portraitRT.sizeDelta = new Vector2(160, 160);

        // Big initial letter overlay on the portrait — visible even without Gemini art.
        // Uses the first character of the localized profession name if it's Latin,
        // otherwise a Latin letter lookup based on the enum.
        var initialGO = new GameObject("Initial");
        initialGO.transform.SetParent(portraitGO.transform, false);
        var initialText = initialGO.AddComponent<TextMeshProUGUI>();
        initialText.text = ProfessionInitial(npc.Profession);
        initialText.fontSize = 90;
        initialText.fontStyle = FontStyles.Bold;
        initialText.color = new Color(1f, 1f, 1f, 0.92f);
        initialText.alignment = TextAlignmentOptions.Center;
        initialText.enableWordWrapping = false;
        initialText.raycastTarget = false;
        var initialRT = initialGO.GetComponent<RectTransform>();
        initialRT.anchorMin = Vector2.zero; initialRT.anchorMax = Vector2.one;
        initialRT.offsetMin = Vector2.zero; initialRT.offsetMax = Vector2.zero;

        // Kick off Gemini portrait generation in the background — when ready, it
        // replaces the colored-circle placeholder. If Gemini isn't available
        // (no API key), the letter-on-color placeholder stays and the card is
        // still clearly readable.
        RequestPortrait(npc, portraitImg, initialText);

        // Name below portrait
        var nameText = CreateCardLabel(card.transform, "Name", npc.Name,
            fontSize: 22, bold: true, color: new Color(1f, 0.92f, 0.65f));
        nameText.alignment = TextAlignmentOptions.Center;
        var nameRT = nameText.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0);
        nameRT.anchorMax = new Vector2(1, 0);
        nameRT.pivot     = new Vector2(0.5f, 0);
        nameRT.anchoredPosition = new Vector2(0, 82);
        nameRT.sizeDelta = new Vector2(0, 30);

        // Profession label under the name (cached localization key — no per-card alloc)
        string profKey = NPCManager.GetProfessionLocKey(npc.Profession);
        string professionText = LocalizationManager.Instance?.Get(profKey) ?? npc.Profession.ToString();
        var profText = CreateCardLabel(card.transform, "Profession", professionText,
            fontSize: 17, bold: false, color: new Color(0.78f, 0.78f, 0.85f));
        profText.alignment = TextAlignmentOptions.Center;
        var profRT = profText.GetComponent<RectTransform>();
        profRT.anchorMin = new Vector2(0, 0);
        profRT.anchorMax = new Vector2(1, 0);
        profRT.pivot     = new Vector2(0.5f, 0);
        profRT.anchoredPosition = new Vector2(0, 58);
        profRT.sizeDelta = new Vector2(0, 24);

        // Mood bar background — sits under the profession label
        var moodBg = new GameObject("MoodBarBg");
        moodBg.transform.SetParent(card.transform, false);
        var moodBgImg = moodBg.AddComponent<Image>();
        moodBgImg.color = new Color(0.22f, 0.22f, 0.28f);
        moodBgImg.raycastTarget = false;
        var moodBgRT = moodBg.GetComponent<RectTransform>();
        moodBgRT.anchorMin = new Vector2(0, 0);
        moodBgRT.anchorMax = new Vector2(0, 0);
        moodBgRT.pivot     = new Vector2(0, 0);
        moodBgRT.anchoredPosition = new Vector2(16, 46);
        moodBgRT.sizeDelta = new Vector2(CARD_W - 32, 10);

        var moodFill = new GameObject("MoodBarFill");
        moodFill.transform.SetParent(moodBg.transform, false);
        var moodFillImg = moodFill.AddComponent<Image>();
        moodFillImg.color = npc.MoodScore > 60 ? new Color(0.3f, 0.8f, 0.35f)
                          : npc.MoodScore > 30 ? new Color(0.85f, 0.7f, 0.15f)
                          : new Color(0.85f, 0.25f, 0.2f);
        moodFillImg.raycastTarget = false;
        var moodFillRT = moodFill.GetComponent<RectTransform>();
        moodFillRT.anchorMin = Vector2.zero;
        moodFillRT.anchorMax = new Vector2(Mathf.Clamp01(npc.MoodScore / 100f), 1f);
        moodFillRT.offsetMin = Vector2.zero;
        moodFillRT.offsetMax = Vector2.zero;

        // Full-width Talk button at the bottom
        var talkGO = new GameObject("TalkButton");
        talkGO.transform.SetParent(card.transform, false);
        var talkImg = talkGO.AddComponent<Image>();
        talkImg.color = new Color(0.28f, 0.58f, 0.26f);
        var talkBtn = talkGO.AddComponent<Button>();
        talkBtn.targetGraphic = talkImg;
        var talkColors = talkBtn.colors;
        talkColors.highlightedColor = new Color(0.38f, 0.72f, 0.36f);
        talkColors.pressedColor     = new Color(0.20f, 0.45f, 0.18f);
        talkBtn.colors = talkColors;

        var talkRT = talkGO.GetComponent<RectTransform>();
        talkRT.anchorMin = new Vector2(0, 0);
        talkRT.anchorMax = new Vector2(1, 0);
        talkRT.pivot     = new Vector2(0.5f, 0);
        talkRT.anchoredPosition = new Vector2(0, 8);
        talkRT.sizeDelta = new Vector2(-24, 34);

        var talkLabel = CreateCardLabel(talkGO.transform, "Label",
            LocalizationManager.Instance?.Get("btn_talk") ?? "Talk",
            fontSize: 18, bold: true, color: Color.white);
        talkLabel.alignment = TextAlignmentOptions.Center;
        var talkLabelRT = talkLabel.GetComponent<RectTransform>();
        talkLabelRT.anchorMin = Vector2.zero;
        talkLabelRT.anchorMax = Vector2.one;
        talkLabelRT.offsetMin = Vector2.zero;
        talkLabelRT.offsetMax = Vector2.zero;

        string capturedId = npc.Id;
        talkBtn.onClick.AddListener(() =>
        {
            _npcInteractionUI?.OpenForNPC(capturedId);
        });
    }

    /// <summary>
    /// Returns a single letter to display on the portrait placeholder until
    /// Gemini fills in a real character art. Using English initials means the
    /// letter renders reliably even when the CJK font fallback hasn't loaded.
    /// </summary>
    private static string ProfessionInitial(NPCPersona.NPCProfession p) => p switch
    {
        // NOTE: Keep every glyph in the basic Latin range. LiberationSans SDF has no
        // glyph for ⚔/✝/♥/⚙/➤/☰, and TMP's dynamic font-fallback path hits a null
        // function pointer on WebGL IL2CPP → "RuntimeError: null function".
        NPCPersona.NPCProfession.Soldier          => "So",
        NPCPersona.NPCProfession.Farmer           => "F",
        NPCPersona.NPCProfession.Merchant         => "M",
        NPCPersona.NPCProfession.Vassal           => "V",
        NPCPersona.NPCProfession.Scholar          => "S",
        NPCPersona.NPCProfession.Priest           => "P",
        NPCPersona.NPCProfession.Spy              => "?",
        NPCPersona.NPCProfession.Blacksmith       => "B",
        NPCPersona.NPCProfession.Healer           => "H",
        NPCPersona.NPCProfession.Scout            => "Sc",
        NPCPersona.NPCProfession.Guard            => "G",
        NPCPersona.NPCProfession.Builder          => "Bu",
        NPCPersona.NPCProfession.Mage             => "M",
        NPCPersona.NPCProfession.MysteriousVisitor=> "?",
        _                                          => "N",
    };

    /// <summary>
    /// Helper: creates a TMP label with word-wrap DISABLED. Critical — without
    /// this, a label whose RectTransform width can't be resolved at build time
    /// (because the parent hasn't run its layout pass yet) ends up 1 pixel wide
    /// and TMP wraps every character to its own line.
    /// </summary>
    private static TextMeshProUGUI CreateCardLabel(Transform parent, string name, string text,
        int fontSize, bool bold, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Color ProfessionColor(NPCPersona.NPCProfession p) => p switch
    {
        NPCPersona.NPCProfession.Soldier     => new Color(0.55f, 0.25f, 0.20f),
        NPCPersona.NPCProfession.Farmer      => new Color(0.35f, 0.60f, 0.25f),
        NPCPersona.NPCProfession.Merchant    => new Color(0.75f, 0.55f, 0.15f),
        NPCPersona.NPCProfession.Vassal      => new Color(0.45f, 0.30f, 0.65f),
        NPCPersona.NPCProfession.Scholar     => new Color(0.25f, 0.45f, 0.70f),
        NPCPersona.NPCProfession.Priest      => new Color(0.80f, 0.80f, 0.75f),
        NPCPersona.NPCProfession.Spy         => new Color(0.20f, 0.20f, 0.25f),
        _                                     => new Color(0.4f, 0.4f, 0.45f),
    };

    /// <summary>
    /// Kicks off Gemini portrait generation for a single NPC card. When ready,
    /// swaps the sprite onto the provided Image and fades out the placeholder
    /// initial letter. No-op (placeholder remains) if GeminiImageClient isn't
    /// available — typical for CI builds where the API key isn't injected.
    /// </summary>
    private void RequestPortrait(NPCManager.NPCData npc, Image target, TextMeshProUGUI initialLabel = null)
    {
        if (target == null || GeminiImageClient.Instance == null) return;

        string prompt =
            $"Square medieval fantasy character portrait of {npc.Name}, a {npc.Profession} in a small lord's castle. " +
            $"Head-and-shoulders framing, painterly oil-painting style, warm torchlight, earthy colors, " +
            $"detailed face, subtle castle stone background. Character ID: {npc.Id}.";

        GeminiImageClient.Instance.GenerateImage(prompt,
            onSuccess: tex =>
            {
                if (target == null) return;
                target.sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                target.color = Color.white;
                target.preserveAspect = true;
                // Hide the placeholder initial letter now that real art is in
                if (initialLabel != null) initialLabel.enabled = false;
            },
            onError: err => Debug.LogWarning($"[CastleView] Portrait failed for {npc.Name}: {err}"));
    }

    private void ToggleBuildingMenu()
    {
        if (_buildingMenuPanel == null) return;
        bool show = !_buildingMenuPanel.activeSelf;
        _buildingMenuPanel.SetActive(show);
        if (show) PopulateBuildingMenu();
    }

    private void PopulateBuildingMenu()
    {
        if (_buildingMenuContent == null) return;
        foreach (Transform child in _buildingMenuContent) Destroy(child.gameObject);

        var buildings = BuildingManager.Instance?.GetAvailableBuildings();
        if (buildings == null) return;

        // Ensure a grid layout on the parent so building cards arrange nicely.
        var grid = _buildingMenuContent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = _buildingMenuContent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(300, 140);
            grid.spacing = new Vector2(16, 16);
            grid.padding = new RectOffset(16, 16, 16, 16);
        }

        foreach (var building in buildings)
            BuildBuildingCard(building);
    }

    private void BuildBuildingCard(BuildingManager.BuildingData building)
    {
        var card = new GameObject($"BuildingCard_{building.Type}");
        card.transform.SetParent(_buildingMenuContent, false);
        var cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.14f, 0.12f, 0.20f, 0.97f);

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = LocalizationManager.Instance?.Get($"building_{building.Type.ToString().ToLower()}") ?? building.Name;
        nameText.fontSize = 22;
        nameText.fontStyle = FontStyles.Bold;
        nameText.color = new Color(1f, 0.9f, 0.6f);
        nameText.raycastTarget = false;
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 1); nameRT.anchorMax = new Vector2(1, 1);
        nameRT.pivot = new Vector2(0, 1);
        nameRT.offsetMin = new Vector2(12, -36); nameRT.offsetMax = new Vector2(-12, -8);

        // Cost
        var costGO = new GameObject("Cost");
        costGO.transform.SetParent(card.transform, false);
        var costText = costGO.AddComponent<TextMeshProUGUI>();
        // Plain ASCII labels — emoji glyphs (wood log, money bag) are missing from
        // LiberationSans SDF and trip the WebGL IL2CPP TMP dynamic-font null crash.
        costText.text = $"Wood {building.WoodCost}   Gold {building.GoldCost}";
        costText.fontSize = 17;
        costText.color = new Color(0.85f, 0.8f, 0.65f);
        costText.raycastTarget = false;
        var costRT = costGO.GetComponent<RectTransform>();
        costRT.anchorMin = new Vector2(0, 1); costRT.anchorMax = new Vector2(1, 1);
        costRT.pivot = new Vector2(0, 1);
        costRT.offsetMin = new Vector2(12, -60); costRT.offsetMax = new Vector2(-12, -36);

        // Description
        var descGO = new GameObject("Description");
        descGO.transform.SetParent(card.transform, false);
        var descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.text = LocalizationManager.Instance?.Get(building.DescriptionKey) ?? "";
        descText.fontSize = 14;
        descText.color = new Color(0.75f, 0.75f, 0.8f);
        descText.enableWordWrapping = true;
        descText.raycastTarget = false;
        var descRT = descGO.GetComponent<RectTransform>();
        descRT.anchorMin = new Vector2(0, 0); descRT.anchorMax = new Vector2(1, 1);
        descRT.offsetMin = new Vector2(12, 46); descRT.offsetMax = new Vector2(-12, -62);

        // Build button
        var btnGO = new GameObject("BuildButton");
        btnGO.transform.SetParent(card.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.25f, 0.5f, 0.3f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0, 0); btnRT.anchorMax = new Vector2(1, 0);
        btnRT.pivot = new Vector2(0.5f, 0);
        btnRT.offsetMin = new Vector2(12, 8); btnRT.offsetMax = new Vector2(-12, 40);

        var btnLblGO = new GameObject("Label");
        btnLblGO.transform.SetParent(btnGO.transform, false);
        var btnLbl = btnLblGO.AddComponent<TextMeshProUGUI>();
        btnLbl.text = LocalizationManager.Instance?.Get("btn_build") ?? "Build";
        btnLbl.fontSize = 17;
        btnLbl.alignment = TextAlignmentOptions.Center;
        btnLbl.color = Color.white;
        btnLbl.raycastTarget = false;
        var btnLblRT = btnLblGO.GetComponent<RectTransform>();
        btnLblRT.anchorMin = Vector2.zero; btnLblRT.anchorMax = Vector2.one;
        btnLblRT.offsetMin = Vector2.zero; btnLblRT.offsetMax = Vector2.zero;

        var capturedType = building.Type;
        btn.onClick.AddListener(() =>
        {
            BuildingManager.Instance?.TryBuild(capturedType, built =>
            {
                string msg = LocalizationManager.Instance?.Get("building_constructed", built.Name)
                             ?? $"{built.Name} constructed!";
                ShowNotification(msg);
                PopulateBuildingMenu();
            });
        });
    }

    public void ShowNotification(string message)
    {
        if (_notificationBanner == null) return;
        _notificationBanner.SetActive(true);
        if (_notificationText != null) _notificationText.text = message;
        StartCoroutine(HideNotificationAfter(3f));
    }

    private System.Collections.IEnumerator HideNotificationAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_notificationBanner != null) _notificationBanner.SetActive(false);
    }

    private void OnSaveClicked()
    {
        GameManager.Instance?.SaveGame();
        ShowNotification(LocalizationManager.Instance?.Get("game_saved") ?? "Game Saved");
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm?.ResourceManager != null)
            gm.ResourceManager.OnResourceChanged -= OnResourceChanged;
        if (gm != null && _dayChangedHandler != null)
            gm.OnDayChanged -= _dayChangedHandler;
    }

    // Called by KeyboardShortcuts on PC
    public void ToggleBuildingMenuFromKeyboard() => ToggleBuildingMenu();
    public void ToggleNPCListFromKeyboard() => ToggleNPCList();
}

// NPCSpriteController removed — 3D NPCs are handled by NPC3DClickHandler in CastleScene3D.
