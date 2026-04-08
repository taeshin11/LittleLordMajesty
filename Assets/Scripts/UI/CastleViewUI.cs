using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Main castle view - shows NPCs as pixel sprites, buildings, and resource HUD.
/// Tap NPCs to interact. Tap buildings to construct/upgrade.
/// </summary>
public class CastleViewUI : MonoBehaviour
{
    [Header("Castle Scene")]
    [SerializeField] private Transform _npcContainer;
    [SerializeField] private GameObject _npcSpritePrefab;
    [SerializeField] private Transform _buildingContainer;
    [SerializeField] private GameObject _buildingSlotPrefab;

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

    private Dictionary<string, NPCSpriteController> _npcSprites = new();

    private void Start()
    {
        SetupButtons();
        SubscribeToEvents();
        RefreshNPCSprites();
        RefreshResourceHUD();
        UpdateLordInfo();
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

        if (gm?.NPCManager != null)
        {
            gm.NPCManager.OnNPCAdded += OnNPCAdded;
            gm.NPCManager.OnNPCDialogue += OnNPCDialogue;
        }

        if (gm != null)
        {
            gm.OnDayChanged += _ => UpdateLordInfo();
        }
    }

    private void RefreshNPCSprites()
    {
        if (_npcContainer == null || _npcSpritePrefab == null) return;
        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null) return;

        foreach (var npc in npcs)
        {
            if (!_npcSprites.ContainsKey(npc.Id))
                SpawnNPCSprite(npc);
        }
    }

    private void SpawnNPCSprite(NPCManager.NPCData npc)
    {
        var sprite = Instantiate(_npcSpritePrefab, _npcContainer);

        // Position based on NPC data
        var rt = sprite.GetComponent<RectTransform>();
        if (rt != null) rt.anchoredPosition = npc.Position * 100f;

        var ctrl = sprite.GetComponent<NPCSpriteController>();
        if (ctrl != null)
        {
            ctrl.Initialize(npc, () => OnNPCTapped(npc.Id));
            _npcSprites[npc.Id] = ctrl;
        }
    }

    private void OnNPCTapped(string npcId)
    {
        UIManager.Instance?.OpenDialogue(npcId);
        NPCInteractionUI interactionUI = FindObjectOfType<NPCInteractionUI>(true);
        interactionUI?.OpenForNPC(npcId);
    }

    private void OnNPCAdded(NPCManager.NPCData npc) => SpawnNPCSprite(npc);

    private void OnNPCDialogue(string npcId, string text)
    {
        // Show speech bubble above NPC
        if (_npcSprites.TryGetValue(npcId, out var ctrl))
            ctrl.ShowSpeechBubble(text, 3f);
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
        if (_npcListContent == null || _npcListItemPrefab == null) return;
        foreach (Transform child in _npcListContent) Destroy(child.gameObject);

        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null) return;

        foreach (var npc in npcs)
        {
            var item = Instantiate(_npcListItemPrefab, _npcListContent);
            var nameText = item.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var taskText = item.transform.Find("Task")?.GetComponent<TextMeshProUGUI>();
            var moodBar = item.transform.Find("MoodBar")?.GetComponent<Slider>();
            var talkBtn = item.transform.Find("TalkButton")?.GetComponent<Button>();

            if (nameText != null) nameText.text = npc.Name;
            if (taskText != null) taskText.text = npc.CurrentTask ?? "Idle";
            if (moodBar != null) moodBar.value = npc.MoodScore / 100f;

            string capturedId = npc.Id;
            talkBtn?.onClick.AddListener(() =>
            {
                _npcListPanel.SetActive(false);
                OnNPCTapped(capturedId);
            });
        }
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
        if (_buildingMenuContent == null || _buildingMenuItemPrefab == null) return;
        foreach (Transform child in _buildingMenuContent) Destroy(child.gameObject);

        var buildings = BuildingManager.Instance?.GetAvailableBuildings();
        if (buildings == null) return;

        foreach (var building in buildings)
        {
            var item = Instantiate(_buildingMenuItemPrefab, _buildingMenuContent);
            var nameText = item.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var costText = item.transform.Find("Cost")?.GetComponent<TextMeshProUGUI>();
            var descText = item.transform.Find("Description")?.GetComponent<TextMeshProUGUI>();
            var buildBtn = item.transform.Find("BuildButton")?.GetComponent<Button>();

            if (nameText != null) nameText.text = building.Name;
            if (costText != null) costText.text = $"🪵{building.WoodCost} 💰{building.GoldCost}";
            if (descText != null)
                descText.text = LocalizationManager.Instance?.Get(building.DescriptionKey) ?? "";

            var capturedType = building.Type;
            buildBtn?.onClick.AddListener(() =>
            {
                BuildingManager.Instance?.TryBuild(capturedType, built =>
                {
                    ShowNotification($"{built.Name} constructed!");
                    ToggleBuildingMenu();
                    ToggleBuildingMenu(); // Refresh
                });
            });
        }
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
}

/// <summary>
/// Controller for NPC pixel sprite in the castle view.
/// </summary>
public class NPCSpriteController : MonoBehaviour
{
    [SerializeField] private Image _spriteImage;
    [SerializeField] private Button _button;
    [SerializeField] private GameObject _speechBubble;
    [SerializeField] private TextMeshProUGUI _speechText;
    [SerializeField] private Animator _animator;

    private string _npcId;

    public void Initialize(NPCManager.NPCData npc, System.Action onTap)
    {
        _npcId = npc.Id;
        _button?.onClick.AddListener(() => onTap?.Invoke());

        // Set sprite color by profession
        if (_spriteImage != null)
        {
            _spriteImage.color = npc.Profession switch
            {
                NPCPersona.NPCProfession.Soldier => new Color(0.6f, 0.3f, 0.2f),
                NPCPersona.NPCProfession.Farmer => new Color(0.5f, 0.7f, 0.3f),
                NPCPersona.NPCProfession.Merchant => new Color(0.8f, 0.7f, 0.1f),
                NPCPersona.NPCProfession.Vassal => new Color(0.5f, 0.4f, 0.7f),
                _ => Color.white
            };
        }
    }

    public void ShowSpeechBubble(string text, float duration)
    {
        if (_speechBubble == null) return;
        _speechBubble.SetActive(true);
        if (_speechText != null) _speechText.text = text.Length > 60 ? text.Substring(0, 60) + "..." : text;
        StartCoroutine(HideBubbleAfter(duration));
    }

    private System.Collections.IEnumerator HideBubbleAfter(float t)
    {
        yield return new WaitForSeconds(t);
        if (_speechBubble != null) _speechBubble.SetActive(false);
    }
}
