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
        _npcInteractionUI = FindObjectOfType<NPCInteractionUI>(true);
        SetupButtons();
        SubscribeToEvents();
        RefreshResourceHUD();
        UpdateLordInfo();
        RequestBackgroundArt();
        // Auto-open the NPC list drawer so characters are visible immediately
        // instead of hiding behind the "NPCs" button.
        if (_npcListPanel != null)
        {
            _npcListPanel.SetActive(true);
            PopulateNPCList();
        }
        // NPC 3D spawning is handled by CastleScene3D (world space)
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
                if (_woodText != null) _woodText.text = FormatResource(newVal, rm.MaxWood);
                break;
            case ResourceManager.ResourceType.Food:
                if (_foodText != null) _foodText.text = FormatResource(newVal, rm.MaxFood);
                break;
            case ResourceManager.ResourceType.Gold:
                if (_goldText != null) _goldText.text = FormatResource(newVal, rm.MaxGold);
                break;
            case ResourceManager.ResourceType.Population:
                if (_populationText != null) _populationText.text = $"{newVal}/{rm.MaxPopulation}";
                break;
        }
    }

    private string FormatResource(int value, int max) => $"{value:N0}";

    private void RefreshResourceHUD()
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;
        if (_woodText != null) _woodText.text = FormatResource(rm.Wood, rm.MaxWood);
        if (_foodText != null) _foodText.text = FormatResource(rm.Food, rm.MaxFood);
        if (_goldText != null) _goldText.text = FormatResource(rm.Gold, rm.MaxGold);
        if (_populationText != null) _populationText.text = $"{rm.Population}/{rm.MaxPopulation}";
    }

    private void UpdateLordInfo()
    {
        var gm = GameManager.Instance;
        if (_lordTitleText != null)
            _lordTitleText.text = $"{gm?.LordTitle} {gm?.PlayerName}";
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
        if (npcs == null) return;

        // Ensure a vertical layout on the parent so cards stack nicely.
        var vlg = _npcListContent.GetComponent<VerticalLayoutGroup>();
        if (vlg == null)
        {
            vlg = _npcListContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(12, 12, 12, 12);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
        }
        var csf = _npcListContent.GetComponent<ContentSizeFitter>();
        if (csf == null)
        {
            csf = _npcListContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

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
        // Card root
        var card = new GameObject($"NPCCard_{npc.Id}");
        card.transform.SetParent(_npcListContent, false);
        var cardRT = card.AddComponent<RectTransform>();
        cardRT.sizeDelta = new Vector2(0, 130);
        var cardBg = card.AddComponent<Image>();
        cardBg.color = new Color(0.13f, 0.11f, 0.18f, 0.96f);

        var layoutElem = card.AddComponent<LayoutElement>();
        layoutElem.preferredHeight = 130;
        layoutElem.minHeight = 130;

        // Portrait (Gemini fills this at runtime)
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(card.transform, false);
        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.color = ProfessionColor(npc.Profession); // placeholder tint
        var portraitRT = portraitGO.GetComponent<RectTransform>();
        portraitRT.anchorMin = new Vector2(0, 0); portraitRT.anchorMax = new Vector2(0, 1);
        portraitRT.pivot = new Vector2(0, 0.5f);
        portraitRT.offsetMin = new Vector2(10, 10); portraitRT.offsetMax = new Vector2(120, -10);
        portraitRT.sizeDelta = new Vector2(110, 0);

        RequestPortrait(npc, portraitImg);

        // Name
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(card.transform, false);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = npc.Name;
        nameText.fontSize = 24;
        nameText.color = new Color(1f, 0.9f, 0.6f);
        nameText.fontStyle = FontStyles.Bold;
        nameText.raycastTarget = false;
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 1); nameRT.anchorMax = new Vector2(1, 1);
        nameRT.pivot = new Vector2(0, 1);
        nameRT.offsetMin = new Vector2(135, -40); nameRT.offsetMax = new Vector2(-10, -10);

        // Profession
        var profGO = new GameObject("Profession");
        profGO.transform.SetParent(card.transform, false);
        var profText = profGO.AddComponent<TextMeshProUGUI>();
        string profKey = $"profession_{npc.Profession.ToString().ToLower()}";
        profText.text = LocalizationManager.Instance?.Get(profKey) ?? npc.Profession.ToString();
        profText.fontSize = 18;
        profText.color = new Color(0.75f, 0.75f, 0.8f);
        profText.raycastTarget = false;
        var profRT = profGO.GetComponent<RectTransform>();
        profRT.anchorMin = new Vector2(0, 1); profRT.anchorMax = new Vector2(1, 1);
        profRT.pivot = new Vector2(0, 1);
        profRT.offsetMin = new Vector2(135, -65); profRT.offsetMax = new Vector2(-10, -40);

        // Mood bar (simple filled rect)
        var moodBg = new GameObject("MoodBarBg");
        moodBg.transform.SetParent(card.transform, false);
        var moodBgImg = moodBg.AddComponent<Image>();
        moodBgImg.color = new Color(0.2f, 0.2f, 0.25f);
        moodBgImg.raycastTarget = false;
        var moodBgRT = moodBg.GetComponent<RectTransform>();
        moodBgRT.anchorMin = new Vector2(0, 0); moodBgRT.anchorMax = new Vector2(1, 0);
        moodBgRT.pivot = new Vector2(0, 0);
        moodBgRT.offsetMin = new Vector2(135, 15); moodBgRT.offsetMax = new Vector2(-110, 28);

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
        moodFillRT.offsetMin = Vector2.zero; moodFillRT.offsetMax = Vector2.zero;

        // Talk button
        var talkGO = new GameObject("TalkButton");
        talkGO.transform.SetParent(card.transform, false);
        var talkImg = talkGO.AddComponent<Image>();
        talkImg.color = new Color(0.3f, 0.55f, 0.25f);
        var talkBtn = talkGO.AddComponent<Button>();
        talkBtn.targetGraphic = talkImg;
        var talkRT = talkGO.GetComponent<RectTransform>();
        talkRT.anchorMin = new Vector2(1, 0); talkRT.anchorMax = new Vector2(1, 0);
        talkRT.pivot = new Vector2(1, 0);
        talkRT.offsetMin = new Vector2(-100, 10); talkRT.offsetMax = new Vector2(-10, 50);
        talkRT.sizeDelta = new Vector2(90, 40);

        var talkLabelGO = new GameObject("Label");
        talkLabelGO.transform.SetParent(talkGO.transform, false);
        var talkLabel = talkLabelGO.AddComponent<TextMeshProUGUI>();
        talkLabel.text = LocalizationManager.Instance?.Get("btn_talk") ?? "Talk";
        talkLabel.fontSize = 18;
        talkLabel.alignment = TextAlignmentOptions.Center;
        talkLabel.color = Color.white;
        talkLabel.raycastTarget = false;
        var talkLabelRT = talkLabelGO.GetComponent<RectTransform>();
        talkLabelRT.anchorMin = Vector2.zero; talkLabelRT.anchorMax = Vector2.one;
        talkLabelRT.offsetMin = Vector2.zero; talkLabelRT.offsetMax = Vector2.zero;

        string capturedId = npc.Id;
        talkBtn.onClick.AddListener(() =>
        {
            _npcListPanel.SetActive(false);
            _npcInteractionUI?.OpenForNPC(capturedId);
        });
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
    /// Kicks off Gemini portrait generation for a single NPC card. Swaps the sprite
    /// onto the provided Image when ready; caches per-NPC id.
    /// </summary>
    private void RequestPortrait(NPCManager.NPCData npc, Image target)
    {
        if (target == null || GeminiImageClient.Instance == null) return;

        string profKey = $"profession_{npc.Profession.ToString().ToLower()}";
        string professionLoc = LocalizationManager.Instance?.Get(profKey) ?? npc.Profession.ToString();
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
        costText.text = $"🪵 {building.WoodCost}   💰 {building.GoldCost}";
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
