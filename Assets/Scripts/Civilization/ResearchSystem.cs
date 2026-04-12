using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 문명 5 스타일: 학자 NPC 기반 테크트리 연구 시스템
///
/// 수석 학자 NPC에게 자연어로 연구 지시:
/// "오크 가죽을 뚫을 수 있는 뾰족한 무기를 연구해"
/// → Gemini가 관련 기술을 결정하고 연구 완료 후 보고
///
/// 기술 종류: 군사, 경제, 건설, 외교, 자연
/// </summary>
public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    public enum TechCategory { Military, Economy, Construction, Diplomacy, Nature }
    public enum TechStatus   { Locked, Available, Researching, Completed }

    [Serializable]
    public class Technology
    {
        public string Id;
        public string NameKey;             // Localization key for display name
        public string DescriptionKey;      // Localization key for description
        public TechCategory Category;
        public TechStatus   Status;
        public string[]     Prerequisites; // 선행 기술 ID
        public int          ResearchDays;  // 연구에 걸리는 인게임 일수
        public int          DaysElapsed;
        public string       Bonus;         // 효과 설명 (TODO: localize — not on alpha test path)
        public string[]     Unlocks;       // 해금되는 건물/기능

        public string LocalizedName =>
            LocalizationManager.Instance?.Get(NameKey) ?? Id;
        public string LocalizedDescription =>
            LocalizationManager.Instance?.Get(DescriptionKey) ?? "";
    }

    // ─────────────────────────────────────────────────────────────
    //  TECH TREE DEFINITION
    // ─────────────────────────────────────────────────────────────

    private static readonly Technology[] TechTree =
    {
        // ── 군사 ──────────────────────────────────────────────────
        new() { Id="iron_working",    NameKey="tech_iron_working_name",    DescriptionKey="tech_iron_working_desc",    Category=TechCategory.Military,
                Status=TechStatus.Available, Prerequisites=Array.Empty<string>(),
                ResearchDays=5, Bonus="Soldier attack +15%", Unlocks=new[]{"Iron Smelter"} },

        new() { Id="crossbow",        NameKey="tech_crossbow_name",        DescriptionKey="tech_crossbow_desc",        Category=TechCategory.Military,
                Status=TechStatus.Locked, Prerequisites=new[]{"iron_working"},
                ResearchDays=8, Bonus="Archer range +50%, damage +25%", Unlocks=new[]{"Archery Range Upgrade"} },

        new() { Id="siege_weapons",   NameKey="tech_siege_weapons_name",   DescriptionKey="tech_siege_weapons_desc",   Category=TechCategory.Military,
                Status=TechStatus.Locked, Prerequisites=new[]{"crossbow", "masonry"},
                ResearchDays=12, Bonus="Siege attack +50%", Unlocks=new[]{"Siege Workshop"} },

        new() { Id="cavalry",         NameKey="tech_cavalry_name",         DescriptionKey="tech_cavalry_desc",         Category=TechCategory.Military,
                Status=TechStatus.Locked, Prerequisites=new[]{"iron_working"},
                ResearchDays=7, Bonus="Scout speed x2, cavalry damage +30%", Unlocks=new[]{"Stable Upgrade"} },

        // ── 경제 ──────────────────────────────────────────────────
        new() { Id="trade_routes",    NameKey="tech_trade_routes_name",    DescriptionKey="tech_trade_routes_desc",    Category=TechCategory.Economy,
                Status=TechStatus.Available, Prerequisites=Array.Empty<string>(),
                ResearchDays=4, Bonus="Gold production +20%", Unlocks=new[]{"Trading Post"} },

        new() { Id="banking",         NameKey="tech_banking_name",         DescriptionKey="tech_banking_desc",         Category=TechCategory.Economy,
                Status=TechStatus.Locked, Prerequisites=new[]{"trade_routes"},
                ResearchDays=8, Bonus="Tax collection +25%, max gold capacity x2", Unlocks=new[]{"Bank"} },

        new() { Id="guilds",          NameKey="tech_guilds_name",          DescriptionKey="tech_guilds_desc",          Category=TechCategory.Economy,
                Status=TechStatus.Locked, Prerequisites=new[]{"banking"},
                ResearchDays=6, Bonus="Market output +30%, burgher pop growth +20%", Unlocks=new[]{"Guild Hall"} },

        // ── 건설 ──────────────────────────────────────────────────
        new() { Id="masonry",         NameKey="tech_masonry_name",         DescriptionKey="tech_masonry_desc",         Category=TechCategory.Construction,
                Status=TechStatus.Available, Prerequisites=Array.Empty<string>(),
                ResearchDays=4, Bonus="Castle walls defense +30%", Unlocks=new[]{"Stone Quarry"} },

        new() { Id="architecture",    NameKey="tech_architecture_name",    DescriptionKey="tech_architecture_desc",    Category=TechCategory.Construction,
                Status=TechStatus.Locked, Prerequisites=new[]{"masonry"},
                ResearchDays=7, Bonus="All buildings capacity +20%", Unlocks=new[]{"Cathedral"} },

        // ── 외교 ──────────────────────────────────────────────────
        new() { Id="writing",         NameKey="tech_writing_name",         DescriptionKey="tech_writing_desc",         Category=TechCategory.Diplomacy,
                Status=TechStatus.Available, Prerequisites=Array.Empty<string>(),
                ResearchDays=3, Bonus="Diplomatic reputation +10", Unlocks=new[]{"Library"} },

        new() { Id="code_of_laws",    NameKey="tech_code_of_laws_name",    DescriptionKey="tech_code_of_laws_desc",    Category=TechCategory.Diplomacy,
                Status=TechStatus.Locked, Prerequisites=new[]{"writing"},
                ResearchDays=8, Bonus="NPC loyalty +15, all satisfaction +10%", Unlocks=new[]{"Courthouse"} },

        // ── 자연/마법 ─────────────────────────────────────────────
        new() { Id="herbalism",       NameKey="tech_herbalism_name",       DescriptionKey="tech_herbalism_desc",       Category=TechCategory.Nature,
                Status=TechStatus.Available, Prerequisites=Array.Empty<string>(),
                ResearchDays=4, Bonus="Food storage +30%, hospital effectiveness +50%", Unlocks=new[]{"Herbalist"} },

        new() { Id="astrology",       NameKey="tech_astrology_name",       DescriptionKey="tech_astrology_desc",       Category=TechCategory.Nature,
                Status=TechStatus.Locked, Prerequisites=new[]{"writing", "herbalism"},
                ResearchDays=6, Bonus="Event warning +3 days advance notice", Unlocks=new[]{"Observatory"} },
    };

    private readonly Dictionary<string, Technology> _techs = new();
    private Technology _currentResearch;
    private int _researchDaysSpent;

    public event Action<Technology> OnResearchCompleted;
    public event Action<Technology> OnResearchStarted;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        foreach (var t in TechTree)
            _techs[t.Id] = t;
    }

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

    // ─────────────────────────────────────────────────────────────
    //  NATURAL LANGUAGE RESEARCH ORDER (학자 NPC에게 명령)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 학자에게 자연어로 연구 지시.
    /// Gemini가 가장 적합한 기술 선택 후 연구 시작.
    /// </summary>
    public void IssueResearchOrder(string naturalLanguageOrder, Action<string> onScholarResponse)
    {
        StartCoroutine(DoResearchOrder(naturalLanguageOrder, onScholarResponse));
    }

    private IEnumerator DoResearchOrder(string order, Action<string> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke(LocalizationManager.Instance?.Get("research_scholar_no_llm")
                               ?? "The scholar nods and retreats to the library...");
            yield break;
        }

        var available = GetAvailableTechs();
        var techList  = string.Join("\n", available.ConvertAll(t =>
            $"- {t.Id}: {t.LocalizedName} ({t.ResearchDays} days) — {t.LocalizedDescription}"));

        string prompt =
            $"You are the chief scholar. The lord says: \"{order}\"\n\n" +
            $"Available technologies:\n{techList}\n\n" +
            $"Choose the single most relevant technology. " +
            $"Reply with JSON: {{\"techId\": \"id\", \"explanation\": \"2-sentence scholar response\"}}";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(prompt, "", null,
            r => { reply = r; done = true; },
            _ => { reply = ""; done = true; });

        float t = 15f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        string explanation = StartResearchFromLLMResponse(reply);
        onResponse?.Invoke(explanation);
    }

    private string StartResearchFromLLMResponse(string llmReply)
    {
        try
        {
            var data = Newtonsoft.Json.JsonConvert.DeserializeObject<ResearchChoice>(llmReply);
            if (data != null && !string.IsNullOrEmpty(data.techId))
            {
                if (StartResearch(data.techId))
                    return data.explanation ??
                           (LocalizationManager.Instance?.Get("research_started_default", data.techId)
                            ?? $"Research on {data.techId} has begun.");
            }
        }
        catch { }
        return LocalizationManager.Instance?.Get("research_scholar_fallback")
               ?? "The scholar considers many possibilities and begins a promising study...";
    }

    [Serializable] private class ResearchChoice
    {
        public string techId;
        public string explanation;
    }

    // ─────────────────────────────────────────────────────────────
    //  RESEARCH MANAGEMENT
    // ─────────────────────────────────────────────────────────────

    public bool StartResearch(string techId)
    {
        if (_currentResearch != null) return false; // Already researching
        if (!_techs.TryGetValue(techId, out var tech)) return false;
        if (tech.Status != TechStatus.Available) return false;

        _currentResearch   = tech;
        _researchDaysSpent = 0;
        tech.Status        = TechStatus.Researching;

        OnResearchStarted?.Invoke(tech);
        Debug.Log($"[Research] Started: {tech.Id}");
        return true;
    }

    private void OnDayAdvanced(int day)
    {
        if (_currentResearch == null) return;

        _researchDaysSpent++;
        _currentResearch.DaysElapsed = _researchDaysSpent;

        if (_researchDaysSpent >= _currentResearch.ResearchDays)
        {
            CompleteResearch(_currentResearch);
        }
    }

    private void CompleteResearch(Technology tech)
    {
        tech.Status = TechStatus.Completed;
        _currentResearch = null;
        _researchDaysSpent = 0;

        // 선행 조건이 된 기술들 해금
        foreach (var t in _techs.Values)
        {
            if (t.Status != TechStatus.Locked) continue;
            bool prereqsMet = true;
            foreach (var prereq in t.Prerequisites)
                if (!_techs.TryGetValue(prereq, out var p) || p.Status != TechStatus.Completed)
                { prereqsMet = false; break; }
            if (prereqsMet) t.Status = TechStatus.Available;
        }

        OnResearchCompleted?.Invoke(tech);
        Debug.Log($"[Research] Completed: {tech.Id}! Bonus: {tech.Bonus}");

        string toast = LocalizationManager.Instance?.Get("research_completed_toast", tech.LocalizedName, tech.Bonus)
                       ?? $"Research complete: {tech.LocalizedName}\n{tech.Bonus}";
        ToastNotification.Show(toast);

        // 학자 NPC 보고
        TriggerScholarReport(tech);
    }

    private void TriggerScholarReport(Technology tech)
    {
        var loc = LocalizationManager.Instance;
        string report = loc?.Get("research_scholar_report", tech.LocalizedName, tech.Bonus)
                        ?? $"My lord! We have mastered {tech.LocalizedName}. {tech.Bonus}";
        string title = loc?.Get("research_event_title", tech.LocalizedName)
                       ?? $"Research Complete: {tech.LocalizedName}";
        GameManager.Instance?.EventManager?.TriggerManualEvent(title, report, EventManager.EventSeverity.Minor);
        TTSManager.Instance?.Speak(report);
    }

    // ─────────────────────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────────────────────

    public List<Technology> GetAvailableTechs() =>
        new List<Technology>(_techs.Values).FindAll(t => t.Status == TechStatus.Available);

    public List<Technology> GetCompletedTechs() =>
        new List<Technology>(_techs.Values).FindAll(t => t.Status == TechStatus.Completed);

    public Technology GetCurrentResearch() => _currentResearch;

    public float GetResearchProgress() =>
        _currentResearch == null ? 0
        : (float)_researchDaysSpent / _currentResearch.ResearchDays;

    public bool IsTechCompleted(string id) =>
        _techs.TryGetValue(id, out var t) && t.Status == TechStatus.Completed;
}
