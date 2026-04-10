using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// World Map UI - shows territories, AI lords, and conquest actions.
/// Tap territory to scout/attack. Shows battle overlay with narrative.
/// </summary>
public class WorldMapUI : MonoBehaviour
{
    [Header("Map Grid")]
    [SerializeField] private Transform _mapGridParent;
    [SerializeField] private GameObject _territoryTilePrefab;

    [Header("Territory Info Panel")]
    [SerializeField] private GameObject _territoryInfoPanel;
    [SerializeField] private TextMeshProUGUI _territoryNameText;
    [SerializeField] private TextMeshProUGUI _territoryTypeText;
    [SerializeField] private TextMeshProUGUI _defenseText;
    [SerializeField] private TextMeshProUGUI _garrisonText;
    [SerializeField] private TextMeshProUGUI _ownerText;
    [SerializeField] private TextMeshProUGUI _resourceBonusText;
    [SerializeField] private Button _scoutButton;
    [SerializeField] private Button _attackButton;
    [SerializeField] private Slider _defenseBar;
    [SerializeField] private TextMeshProUGUI _scoutCostText;

    [Header("Army Dispatch Panel")]
    [SerializeField] private GameObject _armyPanel;
    [SerializeField] private Slider _armySlider;
    [SerializeField] private TextMeshProUGUI _armyCountText;
    [SerializeField] private Button _launchSiegeButton;
    [SerializeField] private Button _cancelSiegeButton;

    [Header("Battle Overlay")]
    [SerializeField] private GameObject _battleOverlay;
    [SerializeField] private TextMeshProUGUI _battleNarrativeText;
    [SerializeField] private Image _battleResultBanner;
    [SerializeField] private TextMeshProUGUI _battleResultText;

    [Header("Legend")]
    [SerializeField] private Image _ownedColor;
    [SerializeField] private Image _hostileColor;
    [SerializeField] private Image _neutralColor;

    [Header("Navigation")]
    [SerializeField] private Button _closeButton;

    [Header("Background Art (Gemini-generated)")]
    [SerializeField] private Image _backgroundArt;

    private string _selectedTerritoryId;
    private Dictionary<string, TerritoryTile> _tileMap = new();

    private void Start()
    {
        BuildMapGrid();
        SubscribeToEvents();
        RequestBackgroundArt();

        if (_scoutButton != null) _scoutButton.onClick.AddListener(OnScoutClicked);
        if (_attackButton != null) _attackButton.onClick.AddListener(OnAttackClicked);
        if (_launchSiegeButton != null) _launchSiegeButton.onClick.AddListener(OnLaunchSiegeClicked);
        if (_cancelSiegeButton != null) _cancelSiegeButton.onClick.AddListener(() => SetPanelActive(_armyPanel, false));
        if (_closeButton != null) _closeButton.onClick.AddListener(() =>
            GameManager.Instance?.SetGameState(GameManager.GameState.Castle));
    }

    /// <summary>Generates a painterly world map background via Gemini.</summary>
    private void RequestBackgroundArt()
    {
        if (_backgroundArt == null || GeminiImageClient.Instance == null) return;

        const string prompt =
            "Ancient medieval parchment world map, hand-drawn fantasy cartography style, " +
            "aged yellow-brown paper texture, inked territorial borders, small illustrated " +
            "castles and forests, compass rose in the corner, no text labels, " +
            "highly detailed, atmospheric, suitable as a game world map background.";

        var targetImage = _backgroundArt;
        GeminiImageClient.Instance.GenerateImage(prompt,
            onSuccess: tex =>
            {
                if (targetImage == null) return;
                var sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                targetImage.sprite = sprite;
                targetImage.color = new Color(1f, 1f, 1f, 0.85f);
                targetImage.preserveAspect = false;
            },
            onError: err => Debug.LogWarning($"[WorldMap] Background art failed: {err}"));
    }

    private void BuildMapGrid()
    {
        if (_mapGridParent == null) return;
        var territories = WorldMapManager.Instance?.GetAllTerritories();
        if (territories == null || territories.Count == 0) return;

        // Ensure a GridLayoutGroup on the parent so procedural tiles auto-arrange.
        var grid = _mapGridParent.GetComponent<GridLayoutGroup>();
        if (grid == null)
        {
            grid = _mapGridParent.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(240, 240);
            grid.spacing = new Vector2(20, 20);
            grid.padding = new RectOffset(20, 20, 20, 20);
            grid.childAlignment = TextAnchor.UpperCenter;
        }

        foreach (var territory in territories)
        {
            GameObject tileGO;
            TerritoryTile tileComp;
            if (_territoryTilePrefab != null)
            {
                tileGO = Instantiate(_territoryTilePrefab, _mapGridParent);
                tileComp = tileGO.GetComponent<TerritoryTile>();
            }
            else
            {
                // Fallback: build a tile procedurally when no prefab is wired in the scene.
                (tileGO, tileComp) = CreateProceduralTile(_mapGridParent);
            }

            if (tileComp != null)
            {
                tileComp.Initialize(territory, OnTerritoryTapped);
                _tileMap[territory.TerritoryId] = tileComp;
            }
        }
    }

    /// <summary>
    /// Procedural tile builder — used when no TerritoryTile prefab is wired in the scene.
    /// Produces a square card with a colored background, territory name label, and a
    /// full-surface Button that forwards taps.
    /// </summary>
    private (GameObject go, TerritoryTile tile) CreateProceduralTile(Transform parent)
    {
        var go = new GameObject("TerritoryTile");
        go.transform.SetParent(parent, false);

        var bgImg = go.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.5f, 0.3f);

        // Ownership border — slightly inset colored frame
        var borderGO = new GameObject("OwnershipBorder");
        borderGO.transform.SetParent(go.transform, false);
        var borderImg = borderGO.AddComponent<Image>();
        borderImg.color = new Color(0.6f, 0.6f, 0.6f);
        borderImg.raycastTarget = false;
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = Vector2.zero; borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = new Vector2(-4, -4); borderRT.offsetMax = new Vector2(4, 4);
        borderGO.transform.SetAsFirstSibling(); // render behind the main tile

        // Name text
        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(go.transform, false);
        var nameText = nameGO.AddComponent<TMPro.TextMeshProUGUI>();
        nameText.text = "???";
        nameText.alignment = TMPro.TextAlignmentOptions.Center;
        nameText.fontSize = 22;
        nameText.color = Color.white;
        nameText.raycastTarget = false;
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.3f); nameRT.anchorMax = new Vector2(1, 0.7f);
        nameRT.offsetMin = new Vector2(8, 0); nameRT.offsetMax = new Vector2(-8, 0);

        // Scouted indicator (small dot, shown when IsScouted)
        var scouted = new GameObject("Scouted");
        scouted.transform.SetParent(go.transform, false);
        var scoutedImg = scouted.AddComponent<Image>();
        scoutedImg.color = new Color(1f, 0.9f, 0.3f);
        scoutedImg.raycastTarget = false;
        var scoutedRT = scouted.GetComponent<RectTransform>();
        scoutedRT.anchorMin = new Vector2(0.85f, 0.85f); scoutedRT.anchorMax = new Vector2(0.95f, 0.95f);
        scoutedRT.offsetMin = Vector2.zero; scoutedRT.offsetMax = Vector2.zero;

        // Click button uses the background image as its target graphic
        var clickBtn = go.AddComponent<Button>();
        clickBtn.targetGraphic = bgImg;
        var colors = clickBtn.colors;
        colors.highlightedColor = new Color(0.5f, 0.7f, 0.5f);
        colors.pressedColor = new Color(0.2f, 0.4f, 0.2f);
        clickBtn.colors = colors;

        var tile = go.AddComponent<TerritoryTile>();

        // Wire serialized fields via reflection (no SerializedObject in runtime builds)
        SetPrivateField(tile, "_tileImage", bgImg);
        SetPrivateField(tile, "_nameText", nameText);
        SetPrivateField(tile, "_ownershipBorder", borderImg);
        SetPrivateField(tile, "_scoutedIndicator", scouted);
        SetPrivateField(tile, "_clickButton", clickBtn);

        return (go, tile);
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        field?.SetValue(target, value);
    }

    private void SubscribeToEvents()
    {
        if (WorldMapManager.Instance != null)
        {
            WorldMapManager.Instance.OnTerritoryConquered += OnTerritoryConquered;
            WorldMapManager.Instance.OnBattleStarted += territory => ShowBattleOverlay(territory, "");
            WorldMapManager.Instance.OnBattleResolved += OnBattleResolved;
        }
    }

    private void OnTerritoryTapped(string territoryId)
    {
        _selectedTerritoryId = territoryId;
        var territory = WorldMapManager.Instance?.GetTerritory(territoryId);
        if (territory == null) return;

        ShowTerritoryInfo(territory);
    }

    private void ShowTerritoryInfo(WorldMapManager.Territory territory)
    {
        SetPanelActive(_territoryInfoPanel, true);

        if (_territoryNameText != null) _territoryNameText.text = territory.Name;
        if (_territoryTypeText != null) _territoryTypeText.text = territory.Type.ToString();

        if (territory.IsScouted)
        {
            if (_defenseText != null) _defenseText.text = $"⚔️ {territory.DefenseStrength}";
            if (_garrisonText != null) _garrisonText.text = $"🪖 {territory.Garrison}";
            if (_defenseBar != null) _defenseBar.value = territory.DefenseStrength / 100f;
        }
        else
        {
            var loc = LocalizationManager.Instance;
            if (_defenseText != null) _defenseText.text = loc?.Get("territory_unknown_defense") ?? "???";
            if (_garrisonText != null) _garrisonText.text = loc?.Get("territory_unknown_garrison") ?? "???";
        }

        if (_ownerText != null)
        {
            var loc = LocalizationManager.Instance;
            if (territory.State == WorldMapManager.ConquestState.Owned)
                _ownerText.text = loc?.Get("territory_yours") ?? "Your Territory";
            else if (!string.IsNullOrEmpty(territory.OwnerLordId))
            {
                var lord = WorldMapManager.Instance?.GetAILords().Find(l => l.LordId == territory.OwnerLordId);
                _ownerText.text = lord != null ? $"{lord.Title} {lord.Name}"
                                               : (loc?.Get("territory_owner_enemy") ?? "Enemy");
            }
            else
                _ownerText.text = loc?.Get("territory_neutral") ?? "Neutral";
        }

        if (_resourceBonusText != null)
            _resourceBonusText.text = territory.IsScouted ? $"+{territory.ResourceBonus}/day" : "?";

        if (_scoutCostText != null)
            _scoutCostText.text = LocalizationManager.Instance?.Get("territory_scout_cost", 10) ?? "Scout (10 Gold)";

        bool isOwned = territory.State == WorldMapManager.ConquestState.Owned;
        bool isHostile = territory.State == WorldMapManager.ConquestState.Hostile ||
                         territory.State == WorldMapManager.ConquestState.Neutral;

        if (_scoutButton != null) _scoutButton.interactable = !territory.IsScouted && !isOwned;
        if (_attackButton != null) _attackButton.interactable = isHostile && !isOwned;
    }

    private void OnScoutClicked()
    {
        if (string.IsNullOrEmpty(_selectedTerritoryId)) return;
        WorldMapManager.Instance?.ScoutTerritory(_selectedTerritoryId, territory =>
        {
            ShowTerritoryInfo(territory);
            if (_tileMap.TryGetValue(territory.TerritoryId, out var tile))
                tile.UpdateVisual(territory);
        });
    }

    private void OnAttackClicked()
    {
        SetPanelActive(_armyPanel, true);
        int maxSoldiers = NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count * 10 ?? 10;
        if (_armySlider != null)
        {
            _armySlider.onValueChanged.RemoveAllListeners();
            _armySlider.maxValue = maxSoldiers;
            _armySlider.value = maxSoldiers / 2;
            _armySlider.onValueChanged.AddListener(v =>
            {
                if (_armyCountText != null)
                    _armyCountText.text = LocalizationManager.Instance?.Get("territory_army_slider_format", (int)v)
                                          ?? $"{(int)v} soldiers";
            });
        }
    }

    private void OnLaunchSiegeClicked()
    {
        int force = _armySlider != null ? (int)_armySlider.value : 10;
        SetPanelActive(_armyPanel, false);
        SetPanelActive(_territoryInfoPanel, false);

        WorldMapManager.Instance?.LaunchSiege(_selectedTerritoryId, force, (won, narrative) =>
        {
            ShowBattleResult(won, narrative);
        });
    }

    private void ShowBattleOverlay(WorldMapManager.Territory territory, string message)
    {
        SetPanelActive(_battleOverlay, true);
        if (_battleNarrativeText != null)
            _battleNarrativeText.text = LocalizationManager.Instance?.Get("battle_commencing", territory.Name)
                                       ?? $"Siege of {territory.Name} begins!";
    }

    private void ShowBattleResult(bool playerWon, string narrative)
    {
        if (_battleNarrativeText != null)
            StartCoroutine(TypewriterEffect(_battleNarrativeText, narrative, 0.03f));

        if (_battleResultText != null)
        {
            _battleResultText.text = playerWon
                ? LocalizationManager.Instance?.Get("battle_victory") ?? "VICTORY!"
                : LocalizationManager.Instance?.Get("battle_defeat") ?? "DEFEAT...";
            _battleResultText.color = playerWon ? Color.yellow : Color.red;
        }

        if (_battleResultBanner != null)
            _battleResultBanner.color = playerWon
                ? new Color(0.8f, 0.6f, 0f, 0.9f)
                : new Color(0.6f, 0f, 0f, 0.9f);

        StartCoroutine(HideBattleOverlayAfterDelay(5f));
    }

    private IEnumerator HideBattleOverlayAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetPanelActive(_battleOverlay, false);
        RefreshAllTiles();
    }

    private void OnTerritoryConquered(WorldMapManager.Territory territory)
    {
        if (_tileMap.TryGetValue(territory.TerritoryId, out var tile))
            tile.UpdateVisual(territory);
    }

    private void OnBattleResolved(string territoryId, bool playerWon)
    {
        if (_tileMap.TryGetValue(territoryId, out var tile))
        {
            var t = WorldMapManager.Instance?.GetTerritory(territoryId);
            if (t != null) tile.UpdateVisual(t);
        }
    }

    private void RefreshAllTiles()
    {
        foreach (var kvp in _tileMap)
        {
            var t = WorldMapManager.Instance?.GetTerritory(kvp.Key);
            if (t != null) kvp.Value.UpdateVisual(t);
        }
    }

    private IEnumerator TypewriterEffect(TextMeshProUGUI tmp, string fullText, float delay)
    {
        tmp.text = "";
        foreach (char c in fullText)
        {
            tmp.text += c;
            yield return new WaitForSeconds(delay);
        }
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }
}

/// <summary>
/// Individual territory tile on the world map.
/// </summary>
public class TerritoryTile : MonoBehaviour
{
    [SerializeField] private Image _tileImage;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private Image _ownershipBorder;
    [SerializeField] private GameObject _scoutedIndicator;
    [SerializeField] private Button _clickButton;

    private string _territoryId;

    public void Initialize(WorldMapManager.Territory territory, System.Action<string> onTap)
    {
        _territoryId = territory.TerritoryId;
        UpdateVisual(territory);
        _clickButton?.onClick.AddListener(() => onTap?.Invoke(_territoryId));
    }

    public void UpdateVisual(WorldMapManager.Territory territory)
    {
        if (_nameText != null) _nameText.text = territory.IsScouted ? territory.Name : "???";
        if (_scoutedIndicator != null) _scoutedIndicator.SetActive(territory.IsScouted);

        if (_ownershipBorder != null)
        {
            _ownershipBorder.color = territory.State switch
            {
                WorldMapManager.ConquestState.Owned => new Color(0.2f, 0.8f, 0.2f),
                WorldMapManager.ConquestState.Hostile => new Color(0.9f, 0.2f, 0.1f),
                WorldMapManager.ConquestState.Allied => new Color(0.2f, 0.4f, 0.9f),
                _ => new Color(0.6f, 0.6f, 0.6f)
            };
        }

        if (_tileImage != null)
        {
            _tileImage.color = territory.Type switch
            {
                WorldMapManager.TerritoryType.Forest => new Color(0.1f, 0.4f, 0.1f),
                WorldMapManager.TerritoryType.Mountain => new Color(0.5f, 0.4f, 0.35f),
                WorldMapManager.TerritoryType.River => new Color(0.2f, 0.4f, 0.8f),
                WorldMapManager.TerritoryType.Village => new Color(0.7f, 0.65f, 0.4f),
                WorldMapManager.TerritoryType.Castle => new Color(0.4f, 0.35f, 0.5f),
                _ => new Color(0.6f, 0.7f, 0.3f) // Plains
            };
        }
    }
}
