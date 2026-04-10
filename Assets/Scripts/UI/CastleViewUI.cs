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
        // NPC 3D spawning is handled by CastleScene3D (world space)
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
            if (taskText != null)
                taskText.text = string.IsNullOrEmpty(npc.CurrentTask)
                    ? (LocalizationManager.Instance?.Get("npc_idle") ?? "Idle")
                    : npc.CurrentTask;
            if (moodBar != null) moodBar.value = npc.MoodScore / 100f;

            string capturedId = npc.Id;
            talkBtn?.onClick.AddListener(() =>
            {
                _npcListPanel.SetActive(false);
                _npcInteractionUI?.OpenForNPC(capturedId);
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
                    string msg = LocalizationManager.Instance?.Get("building_constructed", built.Name)
                                 ?? $"{built.Name} constructed!";
                    ShowNotification(msg);
                    // Refresh building menu contents without toggle flicker
                    PopulateBuildingMenu();
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
