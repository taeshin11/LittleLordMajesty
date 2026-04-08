using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Anno 1404 스타일 인구 계급 시스템.
///
/// 계급: Serf(농노) → Peasant(농민) → Citizen(시민) → Burgher(부르주아) → Noble(귀족)
/// 각 계급은 특정 수요(needs)를 충족해야 다음 계급으로 승격.
/// 계급이 높을수록 세금 수입 증가 + 고급 건물 요건 해금.
///
/// 불만이 쌓이면 시민 대표 NPC가 찾아와 항의.
/// </summary>
public class PopulationClassSystem : MonoBehaviour
{
    public static PopulationClassSystem Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  ENUMS & DATA
    // ─────────────────────────────────────────────────────────────

    public enum PopClass
    {
        Serf    = 0,
        Peasant = 1,
        Citizen = 2,
        Burgher = 3,
        Noble   = 4
    }

    [Serializable]
    public class PopTier
    {
        public PopClass Class;
        public int      Count;        // 이 계급의 인구 수
        public float    Satisfaction; // 0-100
        public float    TaxRate;      // 1인당 금화/일
        public string[] Needs;        // 충족해야 할 수요 목록
        public string[] NextNeeds;    // 다음 계급 승격 조건
    }

    [Serializable]
    public class NeedStatus
    {
        public string Need;
        public bool   IsMet;
        public string HowToMeet; // 플레이어 안내 텍스트
    }

    // ─────────────────────────────────────────────────────────────
    //  TIER DEFINITIONS
    // ─────────────────────────────────────────────────────────────

    private readonly Dictionary<PopClass, PopTier> _tiers = new()
    {
        [PopClass.Serf] = new PopTier
        {
            Class   = PopClass.Serf,
            Count   = 20,
            TaxRate = 0.5f,
            Needs   = new[] { "food" },
            NextNeeds = new[] { "food", "water" }
        },
        [PopClass.Peasant] = new PopTier
        {
            Class   = PopClass.Peasant,
            Count   = 0,
            TaxRate = 1.5f,
            Needs   = new[] { "food", "water" },
            NextNeeds = new[] { "bread", "ale", "cloth" }
        },
        [PopClass.Citizen] = new PopTier
        {
            Class   = PopClass.Citizen,
            Count   = 0,
            TaxRate = 4f,
            Needs   = new[] { "bread", "ale", "cloth" },
            NextNeeds = new[] { "bread", "ale", "cloth", "weapons_security", "church" }
        },
        [PopClass.Burgher] = new PopTier
        {
            Class   = PopClass.Burgher,
            Count   = 0,
            TaxRate = 9f,
            Needs   = new[] { "bread", "ale", "cloth", "weapons_security", "church" },
            NextNeeds = new[] { "bread", "ale", "cloth", "spices", "silk", "jewelry", "library_access" }
        },
        [PopClass.Noble] = new PopTier
        {
            Class   = PopClass.Noble,
            Count   = 0,
            TaxRate = 20f,
            Needs   = new[] { "bread", "ale", "cloth", "spices", "silk", "jewelry", "library_access" },
            NextNeeds = Array.Empty<string>()
        }
    };

    // 현재 충족된 수요 목록
    private readonly HashSet<string> _metNeeds = new();

    public event Action<PopClass, int> OnTierChanged;     // 계급, 새 인구 수
    public event Action<string>        OnComplaint;        // 시민 대표의 불만 메시지
    public event Action<float>         OnTaxCollected;     // 수집된 세금

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // 건물/생산 이벤트 구독
        if (BuildingManager.Instance != null)
        {
            BuildingManager.Instance.OnBuildingConstructed += OnBuildingBuilt;
            BuildingManager.Instance.OnBuildingUpgraded    += OnBuildingBuilt;
        }

        if (ProductionChainManager.Instance != null)
            ProductionChainManager.Instance.OnGoodsProduced += OnGoodsProduced;

        // 매일 세금 징수 및 만족도 업데이트
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged += OnDayAdvanced;
    }

    private void OnDestroy()
    {
        if (BuildingManager.Instance != null)
        {
            BuildingManager.Instance.OnBuildingConstructed -= OnBuildingBuilt;
            BuildingManager.Instance.OnBuildingUpgraded    -= OnBuildingBuilt;
        }
        if (ProductionChainManager.Instance != null)
            ProductionChainManager.Instance.OnGoodsProduced -= OnGoodsProduced;
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged -= OnDayAdvanced;
    }

    // ─────────────────────────────────────────────────────────────
    //  NEEDS FULFILLMENT
    // ─────────────────────────────────────────────────────────────

    private void OnGoodsProduced(string goodsType, int amount)
    {
        _metNeeds.Add(goodsType);
        EvaluateUpgrades();
    }

    private void OnBuildingBuilt(BuildingManager.BuildingData building)
    {
        // 건물이 특정 수요를 충족
        switch (building.Type)
        {
            case BuildingManager.BuildingType.Well:         _metNeeds.Add("water"); break;
            case BuildingManager.BuildingType.Market:       _metNeeds.Add("market_access"); break;
            case BuildingManager.BuildingType.Hospital:     _metNeeds.Add("healthcare"); break;
            case BuildingManager.BuildingType.Library:      _metNeeds.Add("library_access"); break;
            case BuildingManager.BuildingType.CastleWalls:  _metNeeds.Add("weapons_security"); break;
        }
        EvaluateUpgrades();
    }

    public void FulfillNeed(string need)
    {
        _metNeeds.Add(need);
        EvaluateUpgrades();
    }

    // ─────────────────────────────────────────────────────────────
    //  POPULATION UPGRADE LOGIC
    // ─────────────────────────────────────────────────────────────

    private void EvaluateUpgrades()
    {
        bool changed = false;
        foreach (var tier in _tiers.Values)
        {
            if (tier.Count == 0) continue;
            if (tier.Class == PopClass.Noble) continue;

            var nextClass = (PopClass)((int)tier.Class + 1);
            var nextTier  = _tiers[nextClass];

            // 다음 계급 수요가 모두 충족됐는지 확인
            bool canUpgrade = true;
            foreach (var need in tier.NextNeeds)
                if (!_metNeeds.Contains(need)) { canUpgrade = false; break; }

            if (canUpgrade && tier.Count > 0)
            {
                // 인구 10% 씩 상위 계급으로 이동
                int upgrading = Mathf.Max(1, tier.Count / 10);
                tier.Count      -= upgrading;
                nextTier.Count  += upgrading;
                tier.Satisfaction = Mathf.Min(100f, tier.Satisfaction + 10f);
                changed = true;

                OnTierChanged?.Invoke(nextClass, nextTier.Count);
                Debug.Log($"[Population] {upgrading} people upgraded to {nextClass}!");
            }
        }

        if (changed)
        {
            // ResourceManager의 최대 인구 업데이트
            GameManager.Instance?.ResourceManager?.UpgradeStorage(
                ResourceManager.ResourceType.Population, 0);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  DAILY TICK
    // ─────────────────────────────────────────────────────────────

    private void OnDayAdvanced(int day)
    {
        CollectTaxes();
        UpdateSatisfaction();
        CheckForComplaints(day);
    }

    private void CollectTaxes()
    {
        float totalTax = 0;
        foreach (var tier in _tiers.Values)
            totalTax += tier.Count * tier.TaxRate;

        if (totalTax > 0)
        {
            int gold = Mathf.RoundToInt(totalTax);
            GameManager.Instance?.ResourceManager?.AddResource(
                ResourceManager.ResourceType.Gold, gold);
            OnTaxCollected?.Invoke(totalTax);
            Debug.Log($"[Population] Tax collected: {gold} gold");
        }
    }

    private void UpdateSatisfaction()
    {
        foreach (var tier in _tiers.Values)
        {
            if (tier.Count == 0) continue;
            bool allNeedsMet = true;
            foreach (var need in tier.Needs)
                if (!_metNeeds.Contains(need)) { allNeedsMet = false; break; }

            tier.Satisfaction += allNeedsMet ? 2f : -5f;
            tier.Satisfaction  = Mathf.Clamp(tier.Satisfaction, 0f, 100f);
        }
    }

    private void CheckForComplaints(int day)
    {
        // 불만도가 낮으면 시민 대표가 찾아옴
        foreach (var tier in _tiers.Values)
        {
            if (tier.Count == 0) continue;
            if (tier.Satisfaction < 30f && day % 3 == 0)
            {
                StartCoroutine(GenerateComplaintNPC(tier));
                break;
            }
        }
    }

    private IEnumerator GenerateComplaintNPC(PopTier tier)
    {
        if (GeminiAPIClient.Instance == null)
        {
            OnComplaint?.Invoke($"The {tier.Class}s are growing restless. Their needs are not being met.");
            yield break;
        }

        var unmetNeeds = new List<string>();
        foreach (var need in tier.Needs)
            if (!_metNeeds.Contains(need)) unmetNeeds.Add(need);

        string prompt =
            $"You are a {tier.Class} representative visiting the lord. " +
            $"Satisfaction: {tier.Satisfaction:F0}%. Population: {tier.Count}. " +
            $"Unmet needs: {string.Join(", ", unmetNeeds)}. " +
            $"Complain to the lord in 2 sentences. Be dramatic but not rude.";

        bool done = false; string complaint = "";
        GeminiAPIClient.Instance.SendMessage(prompt, "", null,
            r => { complaint = r; done = true; },
            _ => { complaint = $"My lord, the {tier.Class}s are suffering!"; done = true; });

        float t = 10f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        OnComplaint?.Invoke(complaint);
        TTSManager.Instance?.Speak(complaint);

        // EventManager에 등록해서 UI에 표시
        GameManager.Instance?.EventManager?.TriggerManualEvent(
            $"Petition from {tier.Class}s", complaint, EventManager.EventSeverity.Minor);
    }

    // ─────────────────────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────────────────────

    public int TotalPopulation
    {
        get { int t = 0; foreach (var tier in _tiers.Values) t += tier.Count; return t; }
    }

    public PopTier GetTier(PopClass c) => _tiers.TryGetValue(c, out var t) ? t : null;
    public Dictionary<PopClass, PopTier> GetAllTiers() => new(_tiers);

    public List<NeedStatus> GetNeedStatusForClass(PopClass c)
    {
        var tier = GetTier(c);
        if (tier == null) return new();
        var result = new List<NeedStatus>();
        foreach (var need in tier.NextNeeds)
        {
            result.Add(new NeedStatus
            {
                Need  = need,
                IsMet = _metNeeds.Contains(need),
                HowToMeet = GetNeedHint(need)
            });
        }
        return result;
    }

    private string GetNeedHint(string need) => need switch
    {
        "bread"            => "Build Windmill + Bakery",
        "ale"              => "Build Hop Farm + Brewery",
        "cloth"            => "Build Sheep Farm + Weaving Mill",
        "water"            => "Build Well",
        "weapons_security" => "Build Castle Walls",
        "library_access"   => "Build Library",
        "church"           => "Build Chapel (new building)",
        "spices"           => "Trade route or Market level 3",
        _                  => "Fulfill through construction or trade"
    };
}
