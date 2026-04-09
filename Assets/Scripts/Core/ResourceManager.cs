using UnityEngine;
using System;

/// <summary>
/// Manages all game resources: Wood, Food, Gold, Population.
/// Handles production, consumption, and trade.
/// </summary>
public class ResourceManager : MonoBehaviour
{
    [Header("Current Resources")]
    [SerializeField] private int _wood = 100;
    [SerializeField] private int _food = 200;
    [SerializeField] private int _gold = 50;
    [SerializeField] private int _population = 10;

    [Header("Max Capacity")]
    [SerializeField] private int _maxWood = 1000;
    [SerializeField] private int _maxFood = 1000;
    [SerializeField] private int _maxGold = 9999;
    [SerializeField] private int _maxPopulation = 100;

    [Header("Daily Production (base)")]
    [SerializeField] private int _woodPerDay = 10;
    [SerializeField] private int _foodPerDay = 15;
    [SerializeField] private int _goldPerDay = 5;

    [Header("Daily Consumption")]
    [SerializeField] private int _foodPerPersonPerDay = 2;

    // Public accessors
    public int Wood => _wood;
    public int Food => _food;
    public int Gold => _gold;
    public int Population => _population;
    public int MaxWood => _maxWood;
    public int MaxFood => _maxFood;
    public int MaxGold => _maxGold;
    public int MaxPopulation => _maxPopulation;

    // Bonuses from buildings (modified by BuildingManager)
    public float WoodProductionMultiplier = 1f;
    public float FoodProductionMultiplier = 1f;
    public float GoldProductionMultiplier = 1f;

    public event Action<ResourceType, int, int> OnResourceChanged; // type, old, new
    public event Action OnResourcesCritical;

    public enum ResourceType { Wood, Food, Gold, Population }

    public void ResetToDefault()
    {
        _wood = 100; _food = 200; _gold = 50; _population = 10;
        NotifyAll();
    }

    public void SetResources(int wood, int food, int gold, int population)
    {
        _wood = Mathf.Clamp(wood, 0, _maxWood);
        _food = Mathf.Clamp(food, 0, _maxFood);
        _gold = Mathf.Clamp(gold, 0, _maxGold);
        _population = Mathf.Clamp(population, 0, _maxPopulation);
        NotifyAll();
    }

    public void ProcessDailyProduction()
    {
        int woodProd = Mathf.RoundToInt(_woodPerDay * WoodProductionMultiplier);
        int foodProd = Mathf.RoundToInt(_foodPerDay * FoodProductionMultiplier);
        int goldProd = Mathf.RoundToInt(_goldPerDay * GoldProductionMultiplier);
        int foodCons = _population * _foodPerPersonPerDay;

        AddResource(ResourceType.Wood, woodProd);
        AddResource(ResourceType.Gold, goldProd);
        AddResource(ResourceType.Food, foodProd - foodCons);

        CheckCriticalResources();
        Debug.Log($"[Resources] Daily: +{woodProd}W +{foodProd}F(−{foodCons}) +{goldProd}G | Now: {_wood}W {_food}F {_gold}G");
    }

    public bool CanAfford(int wood = 0, int food = 0, int gold = 0) =>
        _wood >= wood && _food >= food && _gold >= gold;

    public bool TrySpend(int wood = 0, int food = 0, int gold = 0)
    {
        if (_wood < wood || _food < food || _gold < gold) return false;
        if (wood > 0) AddResource(ResourceType.Wood, -wood);
        if (food > 0) AddResource(ResourceType.Food, -food);
        if (gold > 0) AddResource(ResourceType.Gold, -gold);
        return true;
    }

    public void AddResource(ResourceType type, int amount)
    {
        int old;
        switch (type)
        {
            case ResourceType.Wood:
                old = _wood;
                _wood = Mathf.Clamp(_wood + amount, 0, _maxWood);
                OnResourceChanged?.Invoke(type, old, _wood);
                break;
            case ResourceType.Food:
                old = _food;
                _food = Mathf.Clamp(_food + amount, 0, _maxFood);
                OnResourceChanged?.Invoke(type, old, _food);
                break;
            case ResourceType.Gold:
                old = _gold;
                _gold = Mathf.Clamp(_gold + amount, 0, _maxGold);
                OnResourceChanged?.Invoke(type, old, _gold);
                break;
            case ResourceType.Population:
                old = _population;
                _population = Mathf.Clamp(_population + amount, 0, _maxPopulation);
                OnResourceChanged?.Invoke(type, old, _population);
                break;
        }
    }

    public void UpgradeStorage(ResourceType type, int additionalCapacity)
    {
        switch (type)
        {
            case ResourceType.Wood: _maxWood += additionalCapacity; break;
            case ResourceType.Food: _maxFood += additionalCapacity; break;
            case ResourceType.Gold: _maxGold += additionalCapacity; break;
            case ResourceType.Population: _maxPopulation += additionalCapacity; break;
        }
    }

    private void CheckCriticalResources()
    {
        bool critical = _food < _population * 5 || _wood < 10;
        if (critical) OnResourcesCritical?.Invoke();
    }

    private void NotifyAll()
    {
        OnResourceChanged?.Invoke(ResourceType.Wood, 0, _wood);
        OnResourceChanged?.Invoke(ResourceType.Food, 0, _food);
        OnResourceChanged?.Invoke(ResourceType.Gold, 0, _gold);
        OnResourceChanged?.Invoke(ResourceType.Population, 0, _population);
    }

    public float GetResourcePercent(ResourceType type)
    {
        return type switch
        {
            ResourceType.Wood => (float)_wood / _maxWood,
            ResourceType.Food => (float)_food / _maxFood,
            ResourceType.Gold => (float)_gold / _maxGold,
            ResourceType.Population => (float)_population / _maxPopulation,
            _ => 0f
        };
    }
}
