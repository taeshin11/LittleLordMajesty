using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Building tech tree system. Buildings boost production, capacity, and unlock new features.
/// </summary>
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    public enum BuildingType
    {
        // Production
        Sawmill,       // +Wood production
        Farm,          // +Food production
        Market,        // +Gold production
        Mine,          // +Gold production (advanced)

        // Military
        Barracks,      // Train soldiers
        Archery,       // Train archers
        Stable,        // Train cavalry
        Watchtower,    // Advance warning of raids

        // Infrastructure
        Warehouse,     // +Storage capacity
        Granary,       // +Food storage
        Well,          // Reduces fire risk
        Hospital,      // NPC mood recovery

        // Special
        ThroneRoom,    // Diplomacy bonuses
        Library,       // Research/tech
        MageTower,     // Special abilities
        CastleWalls    // Defense rating
    }

    [Serializable]
    public class BuildingData
    {
        public BuildingType Type;
        public string Name;
        public string DescriptionKey; // Localization key
        public int Level;
        public int MaxLevel;
        public bool IsBuilt;

        public int WoodCost;
        public int FoodCost;
        public int GoldCost;
        public int BuildTimeDays;

        public float ProductionBonus;
        public int CapacityBonus;
        public int DefenseBonus;
        public int PopulationBonus;
    }

    private List<BuildingData> _buildings = new();
    private Dictionary<BuildingType, bool> _buildingUnlocks = new();

    public event Action<BuildingData> OnBuildingConstructed;
    public event Action<BuildingData> OnBuildingUpgraded;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeBuildingTree();
    }

    private void InitializeBuildingTree()
    {
        _buildings.AddRange(new[]
        {
            new BuildingData { Type = BuildingType.Sawmill, Name = "Sawmill", DescriptionKey = "building_sawmill_desc",
                Level = 0, MaxLevel = 3, WoodCost = 50, GoldCost = 20, BuildTimeDays = 2,
                ProductionBonus = 5f, CapacityBonus = 0 },

            new BuildingData { Type = BuildingType.Farm, Name = "Farm", DescriptionKey = "building_farm_desc",
                Level = 0, MaxLevel = 3, WoodCost = 30, GoldCost = 10, BuildTimeDays = 1,
                ProductionBonus = 10f, PopulationBonus = 5 },

            new BuildingData { Type = BuildingType.Market, Name = "Market", DescriptionKey = "building_market_desc",
                Level = 0, MaxLevel = 3, WoodCost = 40, GoldCost = 30, BuildTimeDays = 3,
                ProductionBonus = 3f },

            new BuildingData { Type = BuildingType.Barracks, Name = "Barracks", DescriptionKey = "building_barracks_desc",
                Level = 0, MaxLevel = 5, WoodCost = 80, GoldCost = 50, BuildTimeDays = 5,
                DefenseBonus = 10, CapacityBonus = 10 },

            new BuildingData { Type = BuildingType.Watchtower, Name = "Watchtower", DescriptionKey = "building_watchtower_desc",
                Level = 0, MaxLevel = 3, WoodCost = 60, GoldCost = 30, BuildTimeDays = 3,
                DefenseBonus = 15 },

            new BuildingData { Type = BuildingType.Warehouse, Name = "Warehouse", DescriptionKey = "building_warehouse_desc",
                Level = 0, MaxLevel = 3, WoodCost = 70, GoldCost = 25, BuildTimeDays = 4,
                CapacityBonus = 200 },

            new BuildingData { Type = BuildingType.Hospital, Name = "Hospital", DescriptionKey = "building_hospital_desc",
                Level = 0, MaxLevel = 2, WoodCost = 50, GoldCost = 60, BuildTimeDays = 4,
                ProductionBonus = 0f },

            new BuildingData { Type = BuildingType.CastleWalls, Name = "Castle Walls", DescriptionKey = "building_walls_desc",
                Level = 0, MaxLevel = 5, WoodCost = 150, GoldCost = 100, BuildTimeDays = 10,
                DefenseBonus = 25 }
        });
    }

    public bool TryBuild(BuildingType type, Action<BuildingData> onComplete = null)
    {
        var building = GetBuilding(type);
        if (building == null || building.IsBuilt) return false;

        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return false;

        if (!rm.TrySpend(building.WoodCost, building.FoodCost, building.GoldCost))
        {
            Debug.LogWarning($"[Buildings] Cannot afford {building.Name}");
            return false;
        }

        // Simulate build time (in a real game, use a coroutine with day advancement)
        building.IsBuilt = true;
        building.Level = 1;
        ApplyBuildingBonus(building);
        OnBuildingConstructed?.Invoke(building);
        onComplete?.Invoke(building);

        Debug.Log($"[Buildings] Built: {building.Name}");
        return true;
    }

    public bool TryUpgrade(BuildingType type)
    {
        var building = GetBuilding(type);
        if (building == null || !building.IsBuilt || building.Level >= building.MaxLevel) return false;

        int upgradeCost = Mathf.RoundToInt(building.GoldCost * building.Level * 0.5f);
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null || !rm.TrySpend(gold: upgradeCost)) return false;

        building.Level++;
        ApplyBuildingBonus(building);
        OnBuildingUpgraded?.Invoke(building);
        return true;
    }

    private void ApplyBuildingBonus(BuildingData building)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        float levelMult = building.Level * 0.5f; // Each level gives 50% more

        switch (building.Type)
        {
            case BuildingType.Sawmill:
                rm.WoodProductionMultiplier += building.ProductionBonus * levelMult * 0.01f;
                break;
            case BuildingType.Farm:
                rm.FoodProductionMultiplier += building.ProductionBonus * levelMult * 0.01f;
                rm.UpgradeStorage(ResourceManager.ResourceType.Population, building.PopulationBonus);
                break;
            case BuildingType.Market:
                rm.GoldProductionMultiplier += building.ProductionBonus * levelMult * 0.01f;
                break;
            case BuildingType.Warehouse:
                rm.UpgradeStorage(ResourceManager.ResourceType.Wood, building.CapacityBonus);
                rm.UpgradeStorage(ResourceManager.ResourceType.Food, building.CapacityBonus);
                break;
        }
    }

    public BuildingData GetBuilding(BuildingType type) => _buildings.Find(b => b.Type == type);
    public List<BuildingData> GetAllBuildings() => new List<BuildingData>(_buildings);
    public List<BuildingData> GetBuiltBuildings() => _buildings.FindAll(b => b.IsBuilt);
    public List<BuildingData> GetAvailableBuildings() => _buildings.FindAll(b => !b.IsBuilt);
}
