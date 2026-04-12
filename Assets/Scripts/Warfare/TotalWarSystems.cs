using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;

// ═══════════════════════════════════════════════════════════════════════════
//  TOTAL WAR SYSTEM BUNDLE
//
//  4 systems in one file (shared namespace, low coupling):
//
//  1. MoraleSpeechSystem  — Pre-battle speech → morale buff
//  2. GeneralTraitSystem  — Battle experience evolves NPC system prompts
//  3. BattleCommandSystem — Natural language tactical orders → NavMesh AI
//  4. GovernorSystem      — Province governors with loyalty/rebellion
// ═══════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────
//  1. MORALE SPEECH SYSTEM
// ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// 출정 연설 시스템. 전투 전 영주가 병사들에게 연설.
/// Gemini가 연설의 감동도/설득력을 분석 → 사기 버프 적용.
/// </summary>
public class MoraleSpeechSystem : MonoBehaviour
{
    public static MoraleSpeechSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Serializable]
    public class SpeechResult
    {
        public float MoraleBonus;       // 0.0 - 0.5 (전투력 보정)
        public string SoldierReaction;  // Gemini가 생성한 병사들의 반응
        public string SpeechRating;     // "Legendary" / "Inspiring" / "Decent" / "Weak"
    }

    public event Action<SpeechResult> OnSpeechDelivered;

    public void DeliverSpeech(string speechText, Action<SpeechResult> onResult)
    {
        StartCoroutine(DoEvaluateSpeech(speechText, onResult));
    }

    private IEnumerator DoEvaluateSpeech(string speech, Action<SpeechResult> onResult)
    {
        if (GeminiAPIClient.Instance == null)
        {
            var fallback = new SpeechResult { MoraleBonus = 0.1f, SpeechRating = "Decent",
                SoldierReaction = "The soldiers nod quietly." };
            onResult?.Invoke(fallback);
            yield break;
        }

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(
            $"Evaluate this pre-battle speech by a lord: \"{speech}\"\n\n" +
            $"Rate it and describe the soldiers' reaction. " +
            $"JSON: {{\"moraleBonus\": float 0.0-0.5, \"rating\": string, \"soldierReaction\": string (2 sentences)}}",
            "You are a battle historian judging the effectiveness of a lord's speech.",
            null,
            r => { reply = r; done = true; },
            _ => { reply = ""; done = true; });

        float t = 10f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        SpeechResult result;
        try
        {
            result = JsonConvert.DeserializeObject<SpeechResult>(reply)
                     ?? new SpeechResult { MoraleBonus = 0.1f, SpeechRating = "Decent" };
        }
        catch { result = new SpeechResult { MoraleBonus = 0.15f, SpeechRating = "Decent",
                SoldierReaction = "The troops are ready." }; }

        // 사기 버프를 PlayerPrefs에 저장 (전투 시스템에서 참조)
        PlayerPrefs.SetFloat("BattleMoraleBonus", result.MoraleBonus);
        PlayerPrefs.SetString("BattleMoraleRating", result.SpeechRating);

        OnSpeechDelivered?.Invoke(result);
        onResult?.Invoke(result);
    }
}

// ─────────────────────────────────────────────────────────────────────────
//  2. GENERAL TRAIT SYSTEM
// ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// 장군 특성 진화 시스템.
/// 전투 경험이 NPC의 System Prompt에 특성을 추가.
/// 장군이 불 공격에서 살아남으면 [불 트라우마] 또는 [화공 방어 전문가] 특성 획득.
/// </summary>
public class GeneralTraitSystem : MonoBehaviour
{
    public static GeneralTraitSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Serializable]
    public class NPCTrait
    {
        public string TraitId;
        public string TraitName;
        public string TraitDescription;
        public string SystemPromptAddition; // 이 특성이 NPC의 System Prompt에 추가하는 텍스트
        public bool   IsPositive;
        public int    AcquiredDay;
    }

    // 각 NPC가 획득한 특성들 (npcId → 특성 목록)
    private readonly Dictionary<string, List<NPCTrait>> _npcTraits = new();

    public event Action<string, NPCTrait> OnTraitAcquired; // npcId, trait

    /// <summary>전투 결과를 바탕으로 NPC 특성 생성</summary>
    public void ProcessBattleExperience(
        string npcId,
        string battleContext,   // "defended against fire attack", "won siege", "lost cavalry battle"
        Action<NPCTrait> onTraitGranted = null)
    {
        StartCoroutine(DoProcessBattleExperience(npcId, battleContext, onTraitGranted));
    }

    private IEnumerator DoProcessBattleExperience(string npcId, string context,
        Action<NPCTrait> onGranted)
    {
        if (GeminiAPIClient.Instance == null) yield break;

        var npc = NPCManager.Instance?.GetNPC(npcId);
        if (npc == null) yield break;

        var existingTraits = GetTraitsForNPC(npcId);
        string existingTraitsText = existingTraits.Count > 0
            ? string.Join(", ", existingTraits.ConvertAll(t => t.TraitName))
            : "none";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(
            $"A military NPC named {npc.Name} ({npc.Profession}) just experienced: \"{context}\"\n" +
            $"Their existing traits: {existingTraitsText}\n\n" +
            $"Grant ONE new personality trait based on this experience. " +
            $"Be creative — it could be a trauma, a new skill, a phobia, or a strength.\n" +
            $"JSON: {{\"traitName\": string, \"description\": string, " +
            $"\"systemPromptAddition\": string (10-20 words added to NPC's system prompt), " +
            $"\"isPositive\": bool}}",
            "You are a game designer creating NPC personality traits.",
            null,
            r => { reply = r; done = true; },
            _ => { done = true; });

        float t = 10f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        try
        {
            var traitData = JsonConvert.DeserializeObject<TraitGenerationResult>(reply);
            if (traitData == null) yield break;

            var trait = new NPCTrait
            {
                TraitId              = Guid.NewGuid().ToString("N")[..8],
                TraitName            = traitData.traitName,
                TraitDescription     = traitData.description,
                SystemPromptAddition = traitData.systemPromptAddition,
                IsPositive           = traitData.isPositive,
                AcquiredDay          = GameManager.Instance?.Day ?? 0
            };

            if (!_npcTraits.ContainsKey(npcId)) _npcTraits[npcId] = new List<NPCTrait>();
            _npcTraits[npcId].Add(trait);

            // NPC 페르소나에 특성 주입 (PlayerPrefs 경유)
            string existing = PlayerPrefs.GetString($"NPC_Traits_{npcId}", "");
            PlayerPrefs.SetString($"NPC_Traits_{npcId}",
                existing + $"\n[TRAIT: {trait.TraitName}] {trait.SystemPromptAddition}");

            OnTraitAcquired?.Invoke(npcId, trait);
            onGranted?.Invoke(trait);

            ToastNotification.Show(
                $"{npc.Name} gained trait: {trait.TraitName}");
        }
        catch (Exception e) { Debug.LogWarning($"[Trait] Parse failed: {e.Message}"); }
    }

    [Serializable] private class TraitGenerationResult
    {
        public string traitName;
        public string description;
        public string systemPromptAddition;
        public bool   isPositive;
    }

    public List<NPCTrait> GetTraitsForNPC(string npcId) =>
        _npcTraits.TryGetValue(npcId, out var t) ? new List<NPCTrait>(t) : new List<NPCTrait>();
}

// ─────────────────────────────────────────────────────────────────────────
//  3. BATTLE COMMAND SYSTEM
// ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// 전술 지시 시스템. 자연어 명령 → 전투 AI 행동 변환.
/// "보병은 방패벽, 기병은 숲 뒤에서 기다렸다가 측면 공격"
/// → 부대별 전술 태그로 변환 → NavMesh AI에게 행동 지시
/// </summary>
public class BattleCommandSystem : MonoBehaviour
{
    public static BattleCommandSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public enum UnitTactic
    {
        Advance,       // 전진
        Hold,          // 현재 위치 사수
        ShieldWall,    // 방패벽 (방어력+, 이동력-)
        Flank,         // 측면 우회
        Ambush,        // 매복
        Retreat,       // 후퇴
        ChargeCalvary, // 기병 돌격
        Volley,        // 화살 집중 사격
        Scatter        // 분산
    }

    [Serializable]
    public class BattleOrder
    {
        public string UnitType;     // "infantry", "archer", "cavalry"
        public UnitTactic Tactic;
        public string TargetLocation; // "left flank", "center", "forest", "enemy rear"
        public float  TimingDelay;   // 초 후 실행 (0 = 즉시)
        public string Explanation;   // Gemini의 전술 해설
    }

    public event Action<List<BattleOrder>> OnOrdersIssued;

    public void IssueTacticalOrders(string commandText, Action<List<BattleOrder>> onOrders)
    {
        StartCoroutine(DoIssueOrders(commandText, onOrders));
    }

    private IEnumerator DoIssueOrders(string commandText, Action<List<BattleOrder>> onOrders)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onOrders?.Invoke(new List<BattleOrder> {
                new() { UnitType = "infantry", Tactic = UnitTactic.Advance, TargetLocation = "center" }
            });
            yield break;
        }

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(
            $"Translate this battle command into unit orders: \"{commandText}\"\n\n" +
            $"Output JSON array: [{{\"unitType\": string, \"tactic\": string, " +
            $"\"targetLocation\": string, \"timingDelay\": float, \"explanation\": string}}]\n" +
            $"Valid tactics: Advance, Hold, ShieldWall, Flank, Ambush, Retreat, ChargeCalvary, Volley, Scatter",
            "You are a medieval battle tactician converting lord's orders into unit actions.",
            null,
            r => { reply = r; done = true; },
            _ => { done = true; });

        float t = 10f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        List<BattleOrder> orders;
        try { orders = JsonConvert.DeserializeObject<List<BattleOrder>>(reply) ?? new List<BattleOrder>(); }
        catch { orders = new List<BattleOrder> {
            new() { UnitType = "all", Tactic = UnitTactic.Advance, TargetLocation = "forward" } }; }

        OnOrdersIssued?.Invoke(orders);
        onOrders?.Invoke(orders);
    }
}

// ─────────────────────────────────────────────────────────────────────────
//  4. GOVERNOR SYSTEM
// ─────────────────────────────────────────────────────────────────────────

/// <summary>
/// 속주 총독 시스템. 정복한 영토에 AI 총독 임명.
/// 세금 착취 과하면 충성도 감소 → 독립 선언 → 진압 또는 협상.
/// </summary>
public class GovernorSystem : MonoBehaviour
{
    public static GovernorSystem Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    [Serializable]
    public class Province
    {
        public string ProvinceId;
        public string ProvinceName;
        public int    TerritoryIndex;

        public string GovernorNPCId;    // NPC 참조
        public string GovernorName;
        public int    LoyaltyToLord;    // 0-100
        public int    TaxDemand;        // 영주가 요구하는 세금/일 (금화)
        public int    Unrest;           // 불만도 (높을수록 반란 위험)
        public int    GarrisonSize;     // 주둔 병력
        public bool   IsRebelling;
        public string RebellionMessage; // 독립 선언 메시지
    }

    private readonly List<Province> _provinces = new();

    public event Action<Province> OnRebellionDeclared;
    public event Action<Province> OnProvinceRecaptured;

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged += OnDayAdvanced;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnDayChanged -= OnDayAdvanced;
    }

    public Province AddProvince(string name, int territoryIndex, string governorNPCId)
    {
        var npc = NPCManager.Instance?.GetNPC(governorNPCId);
        var province = new Province
        {
            ProvinceId    = Guid.NewGuid().ToString("N")[..8],
            ProvinceName  = name,
            TerritoryIndex = territoryIndex,
            GovernorNPCId = governorNPCId,
            GovernorName  = npc?.Name ?? "Unknown Governor",
            LoyaltyToLord = 70,
            TaxDemand     = 50,
            Unrest        = 10,
            GarrisonSize  = 5
        };
        _provinces.Add(province);
        return province;
    }

    private void OnDayAdvanced(int day)
    {
        foreach (var province in _provinces)
        {
            if (province.IsRebelling) continue;

            // 세금 징수
            GameManager.Instance?.ResourceManager?.AddResource(
                ResourceManager.ResourceType.Gold, province.TaxDemand);

            // 과세 → 불만도 증가
            int fair = 50;
            if (province.TaxDemand > fair)
                province.Unrest += (province.TaxDemand - fair) / 10;
            else
                province.Unrest = Mathf.Max(0, province.Unrest - 2);

            // 충성도 감소
            if (province.Unrest > 50)
                province.LoyaltyToLord = Mathf.Max(0, province.LoyaltyToLord - 3);

            // 반란 판정 (충성도 < 20, 랜덤)
            if (province.LoyaltyToLord < 20 && UnityEngine.Random.value < 0.1f)
                StartCoroutine(TriggerRebellion(province));
        }
    }

    private IEnumerator TriggerRebellion(Province province)
    {
        province.IsRebelling = true;

        if (GeminiAPIClient.Instance != null)
        {
            bool done = false; string message = "";
            GeminiAPIClient.Instance.SendMessage(
                $"Governor {province.GovernorName} of {province.ProvinceName} is declaring independence. " +
                $"Loyalty: {province.LoyaltyToLord}/100, Unrest: {province.Unrest}/100, " +
                $"Tax demanded: {province.TaxDemand}/day. " +
                $"Write their 2-sentence declaration of independence. Be dramatic.",
                "You are a defiant medieval governor breaking from an oppressive lord.",
                null,
                r => { message = r; done = true; },
                _ => { message = $"{province.GovernorName} declares independence!"; done = true; });

            float t = 10f;
            while (!done && t > 0) { t -= Time.deltaTime; yield return null; }
            province.RebellionMessage = message;
        }
        else
        {
            province.RebellionMessage =
                $"My lord! Governor {province.GovernorName} of {province.ProvinceName} " +
                $"has declared independence! Your excessive taxation has driven them to revolt!";
        }

        OnRebellionDeclared?.Invoke(province);
        GameManager.Instance?.EventManager?.TriggerManualEvent(
            $"Rebellion in {province.ProvinceName}!",
            province.RebellionMessage,
            EventManager.EventSeverity.Critical);
    }

    public void NegotiateWithGovernor(Province province, string playerMessage,
        Action<string, bool> onResponse)
    {
        StartCoroutine(DoNegotiateGovernor(province, playerMessage, onResponse));
    }

    private IEnumerator DoNegotiateGovernor(Province province, string message,
        Action<string, bool> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("The governor considers your words...", false);
            yield break;
        }

        var npc = NPCManager.Instance?.GetNPC(province.GovernorNPCId);
        string systemPrompt =
            $"You are Governor {province.GovernorName} who has declared independence.\n" +
            $"Your grievances: tax={province.TaxDemand}/day, loyalty={province.LoyaltyToLord}/100, unrest={province.Unrest}.\n" +
            $"You will stand down ONLY if: the lord reduces tax by 40%+ OR sends 10+ garrison troops " +
            $"OR makes a sincere heartfelt apology.\n" +
            $"End with [STAND_DOWN] if you accept, [REFUSE] if you hold.\n" +
            (npc != null ? $"Your character: {npc.BackgroundStory}" : "");

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(message, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = "The governor remains defiant."; done = true; });

        float t = 12f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        bool accepted = reply.Contains("[STAND_DOWN]");
        string cleanReply = System.Text.RegularExpressions.Regex.Replace(reply, @"\[.*?\]", "").Trim();

        if (accepted)
        {
            province.IsRebelling = false;
            province.LoyaltyToLord += 20;
            province.Unrest = Mathf.Max(0, province.Unrest - 30);
            OnProvinceRecaptured?.Invoke(province);
        }

        TTSManager.Instance?.Speak(cleanReply);
        onResponse?.Invoke(cleanReply, accepted);
    }

    public List<Province> GetAllProvinces() => new(_provinces);
    public List<Province> GetRebellingProvinces() => _provinces.FindAll(p => p.IsRebelling);
}
