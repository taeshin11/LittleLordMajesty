using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Anno 1404 스타일 생산 체인 시스템.
///
/// 1차 자원 → 가공 → 최종 상품 → 백성 만족도 향상
/// 백성 만족도가 오르면 세금 수입 증가, 인구 계급 상승
///
/// 내정관 NPC에게 자연어로 명령 가능:
/// "밀가루가 남으니 빵집을 지어서 시민들 배부르게 해줘"
/// → Gemini가 명령을 파악하고 생산 체인 최적화 실행
/// </summary>
public class ProductionChainManager : MonoBehaviour
{
    public static ProductionChainManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  PRODUCTION CHAIN DEFINITIONS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class ProductionNode
    {
        public string Id;
        public string Name;
        public string BuildingRequired;   // 필요 건물 타입
        public bool   IsBuilt;

        // 입력 → 출력
        public ResourceAmount[] Inputs;
        public ResourceAmount   Output;
        public float            CycleTimeSecs; // 1사이클 생산 시간

        public float ElapsedSecs;  // 현재 사이클 경과 시간
        public int   WorkerCount;  // 배치된 일꾼 수 (0 = 가동 중지)
        public int   StoredOutput; // 창고에 쌓인 출력 재화
    }

    [Serializable]
    public class ResourceAmount
    {
        public string Type;   // "wheat", "flour", "bread", "wood", "iron", "ale", "cloth"
        public int    Amount;
    }

    // 생산 체인 정의 (Anno 1404 스타일)
    private static readonly ProductionNode[] ChainTemplates =
    {
        // ── 식량 체인 ─────────────────────────────────────────────
        new() {
            Id = "wheat_field",
            Name = "Wheat Field",
            BuildingRequired = "Farm",
            Inputs = Array.Empty<ResourceAmount>(),
            Output = new() { Type = "wheat", Amount = 5 },
            CycleTimeSecs = 60f
        },
        new() {
            Id = "windmill",
            Name = "Windmill",
            BuildingRequired = "Warehouse",
            Inputs = new[] { new ResourceAmount { Type = "wheat", Amount = 4 } },
            Output = new() { Type = "flour", Amount = 3 },
            CycleTimeSecs = 45f
        },
        new() {
            Id = "bakery",
            Name = "Bakery",
            BuildingRequired = "Market",
            Inputs = new[] { new ResourceAmount { Type = "flour", Amount = 3 } },
            Output = new() { Type = "bread", Amount = 5 },
            CycleTimeSecs = 40f
        },
        // ── 음료 체인 ─────────────────────────────────────────────
        new() {
            Id = "hop_farm",
            Name = "Hop Farm",
            BuildingRequired = "Farm",
            Inputs = Array.Empty<ResourceAmount>(),
            Output = new() { Type = "hops", Amount = 4 },
            CycleTimeSecs = 70f
        },
        new() {
            Id = "brewery",
            Name = "Brewery",
            BuildingRequired = "Market",
            Inputs = new[] { new ResourceAmount { Type = "hops", Amount = 3 } },
            Output = new() { Type = "ale", Amount = 3 },
            CycleTimeSecs = 55f
        },
        // ── 군수 체인 ─────────────────────────────────────────────
        new() {
            Id = "iron_smelter",
            Name = "Iron Smelter",
            BuildingRequired = "Mine",
            Inputs = new[] { new ResourceAmount { Type = "wood", Amount = 2 } },
            Output = new() { Type = "iron", Amount = 2 },
            CycleTimeSecs = 90f
        },
        new() {
            Id = "weaponsmith",
            Name = "Weaponsmith",
            BuildingRequired = "Barracks",
            Inputs = new[] {
                new ResourceAmount { Type = "iron", Amount = 2 },
                new ResourceAmount { Type = "wood", Amount = 1 }
            },
            Output = new() { Type = "weapons", Amount = 2 },
            CycleTimeSecs = 80f
        },
        // ── 직물 체인 ─────────────────────────────────────────────
        new() {
            Id = "sheep_farm",
            Name = "Sheep Farm",
            BuildingRequired = "Farm",
            Inputs = Array.Empty<ResourceAmount>(),
            Output = new() { Type = "wool", Amount = 3 },
            CycleTimeSecs = 80f
        },
        new() {
            Id = "weaving_mill",
            Name = "Weaving Mill",
            BuildingRequired = "Market",
            Inputs = new[] { new ResourceAmount { Type = "wool", Amount = 3 } },
            Output = new() { Type = "cloth", Amount = 2 },
            CycleTimeSecs = 65f
        },
    };

    // ─────────────────────────────────────────────────────────────
    //  RUNTIME STATE
    // ─────────────────────────────────────────────────────────────

    private readonly List<ProductionNode> _activeChains = new();
    private readonly Dictionary<string, int> _processedGoods = new(); // 가공품 재고

    public event Action<string, int> OnGoodsProduced;       // type, amount
    public event Action<string>      OnChainActivated;
    public event Action              OnChainsUpdated;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitializeAvailableChains();
    }

    private void Update()
    {
        TickProductionChains(Time.deltaTime);
    }

    private void InitializeAvailableChains()
    {
        foreach (var template in ChainTemplates)
        {
            var node = JsonConvert.DeserializeObject<ProductionNode>(
                JsonConvert.SerializeObject(template));
            node.WorkerCount = 0; // All chains start idle
            _activeChains.Add(node);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  PRODUCTION TICK
    // ─────────────────────────────────────────────────────────────

    private void TickProductionChains(float dt)
    {
        foreach (var chain in _activeChains)
        {
            if (!chain.IsBuilt || chain.WorkerCount == 0) continue;

            chain.ElapsedSecs += dt * chain.WorkerCount; // 일꾼 많을수록 빠름

            if (chain.ElapsedSecs >= chain.CycleTimeSecs)
            {
                chain.ElapsedSecs -= chain.CycleTimeSecs;
                TryProduceCycle(chain);
            }
        }
    }

    private void TryProduceCycle(ProductionNode chain)
    {
        // 입력 재료 확인
        foreach (var input in chain.Inputs)
        {
            if (!HasEnoughGoods(input.Type, input.Amount))
            {
                Debug.Log($"[Production] {chain.Name}: missing {input.Amount}x {input.Type}");
                return;
            }
        }

        // 입력 소비
        foreach (var input in chain.Inputs)
            ConsumeGoods(input.Type, input.Amount);

        // 출력 생산
        AddGoods(chain.Output.Type, chain.Output.Amount);
        chain.StoredOutput += chain.Output.Amount;
        OnGoodsProduced?.Invoke(chain.Output.Type, chain.Output.Amount);

        // 식량/특수 재화를 ResourceManager에 반영
        SyncToResourceManager(chain.Output.Type, chain.Output.Amount);
    }

    // ─────────────────────────────────────────────────────────────
    //  WORKER ASSIGNMENT (LLM 명령으로 자동화 가능)
    // ─────────────────────────────────────────────────────────────

    public void AssignWorkers(string chainId, int workerCount)
    {
        var chain = _activeChains.Find(c => c.Id == chainId);
        if (chain == null) return;
        chain.WorkerCount = Mathf.Clamp(workerCount, 0, 5);
        OnChainsUpdated?.Invoke();
    }

    /// <summary>
    /// 내정관 NPC가 LLM 명령을 해석해서 자동으로 일꾼 배치.
    /// NPC에게 자연어로 명령하면 이 메서드가 최적화를 수행.
    /// </summary>
    public void OptimizeWorkforceFromNPC(string command, Action<string> onResult)
    {
        StartCoroutine(DoOptimizeWorkforce(command, onResult));
    }

    private IEnumerator DoOptimizeWorkforce(string command, Action<string> onResult)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResult?.Invoke("The steward nods and begins reassigning workers.");
            yield break;
        }

        var status = GetProductionStatus();
        string systemPrompt =
            "You are a castle steward managing production chains. " +
            "Analyze the current status and the lord's command. " +
            "Respond with JSON: {\"changes\": [{\"chainId\": string, \"workers\": int}], \"explanation\": string}";

        string prompt = $"Lord's command: \"{command}\"\n\nCurrent status:\n{status}";

        bool done = false;
        string reply = "";
        GeminiAPIClient.Instance.SendMessage(prompt, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = ""; done = true; });

        float t = 15f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        // 응답 파싱 & 적용
        string explanation = ApplyWorkforceChanges(reply);
        OnChainsUpdated?.Invoke();
        onResult?.Invoke(explanation);
    }

    private string ApplyWorkforceChanges(string llmResponse)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<WorkforceOptimizationResult>(llmResponse);
            if (data?.changes != null)
                foreach (var change in data.changes)
                    AssignWorkers(change.chainId, change.workers);
            return data?.explanation ?? "Workers reassigned.";
        }
        catch
        {
            return "The steward rearranges the workers as best they can.";
        }
    }

    [Serializable]
    private class WorkforceOptimizationResult
    {
        public List<WorkerChange> changes;
        public string explanation;
    }
    [Serializable]
    private class WorkerChange
    {
        public string chainId;
        public int workers;
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILDING UNLOCK (건물 완공 시 연동)
    // ─────────────────────────────────────────────────────────────

    public void OnBuildingCompleted(BuildingManager.BuildingType type)
    {
        string buildingName = type.ToString();
        foreach (var chain in _activeChains)
        {
            if (chain.BuildingRequired == buildingName)
            {
                chain.IsBuilt = true;
                OnChainActivated?.Invoke(chain.Name);
                Debug.Log($"[Production] Chain unlocked: {chain.Name}");
            }
        }
        OnChainsUpdated?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  GOODS MANAGEMENT
    // ─────────────────────────────────────────────────────────────

    private bool HasEnoughGoods(string type, int amount)
    {
        // 기본 자원은 ResourceManager에서 확인
        var rm = GameManager.Instance?.ResourceManager;
        return type switch
        {
            "wood"  => rm?.Wood  >= amount,
            "food"  => rm?.Food  >= amount,
            "gold"  => rm?.Gold  >= amount,
            _       => _processedGoods.TryGetValue(type, out int stock) && stock >= amount
        };
    }

    private void ConsumeGoods(string type, int amount)
    {
        var rm = GameManager.Instance?.ResourceManager;
        switch (type)
        {
            case "wood": rm?.TrySpend(amount, 0, 0); break;
            case "food": rm?.TrySpend(0, amount, 0); break;
            case "gold": rm?.TrySpend(0, 0, amount); break;
            default:
                if (_processedGoods.ContainsKey(type))
                    _processedGoods[type] = Mathf.Max(0, _processedGoods[type] - amount);
                break;
        }
    }

    private void AddGoods(string type, int amount)
    {
        if (!_processedGoods.ContainsKey(type)) _processedGoods[type] = 0;
        _processedGoods[type] += amount;
    }

    private void SyncToResourceManager(string goodsType, int amount)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        // 완성 식품은 식량으로 전환 (밀 자체보다 빵이 더 효율적)
        switch (goodsType)
        {
            case "bread": rm.AddResource(ResourceManager.ResourceType.Food, amount * 3); break;
            case "ale":   rm.AddResource(ResourceManager.ResourceType.Food, amount * 2); break;
            case "weapons":
                // 무기는 전투력에 직접 반영 (별도 시스템)
                PlayerPrefs.SetInt("WeaponsStock", PlayerPrefs.GetInt("WeaponsStock", 0) + amount);
                break;
        }
    }

    public int GetGoodsStock(string type) =>
        _processedGoods.TryGetValue(type, out int v) ? v : 0;

    public List<ProductionNode> GetAllChains() => new(_activeChains);
    public List<ProductionNode> GetActiveChains() => _activeChains.FindAll(c => c.IsBuilt);

    private string GetProductionStatus()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in _activeChains)
        {
            if (!c.IsBuilt) continue;
            sb.AppendLine($"{c.Name}: workers={c.WorkerCount}, stored={c.StoredOutput} {c.Output.Type}");
        }
        sb.AppendLine("\nProcessed goods stocks:");
        foreach (var kvp in _processedGoods)
            sb.AppendLine($"  {kvp.Key}: {kvp.Value}");
        return sb.ToString();
    }
}
