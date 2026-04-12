using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 문명 5 스타일: 영주 칙령 시스템 (Lord's Decree / Social Policy)
///
/// 영주가 국정 철학을 자연어 칙령으로 선포 → 전체 NPC의 System Prompt에 반영
/// 칙령에 따라 전 영지에 버프/디버프 발생
///
/// 예시:
/// "우리 영지는 무력을 숭상한다. 잉여 식량은 군대에 쓴다."
/// → 병력 생산 +30%, 불만도 +10%, NPC들이 군대식 말투로 변경
/// </summary>
public class LordDecreeSystem : MonoBehaviour
{
    public static LordDecreeSystem Instance { get; private set; }

    [Serializable]
    public class ActiveDecree
    {
        public string Title;
        public string FullText;       // 영주가 직접 작성한 칙령 전문
        public string PolicyArchetype; // "military", "commerce", "peace", "expansion", "culture"
        public DecreeEffects Effects;
        public long   IssuedAtUtc;
    }

    [Serializable]
    public class DecreeEffects
    {
        // 생산/경제
        public float FoodProductionMod;     // 0.2 = +20%
        public float WoodProductionMod;
        public float GoldProductionMod;
        public float TaxRateMod;

        // 군사
        public float SoldierStrengthMod;
        public float DefenseRatingMod;
        public float TrainingSpeedMod;

        // 사회
        public float SatisfactionMod;       // 음수 가능 (불만도)
        public float PopGrowthMod;

        // NPC 대화
        public string NpcToneOverride;      // "militaristic", "scholarly", "mercantile" 등
    }

    public ActiveDecree CurrentDecree { get; private set; }

    // 효과가 ResourceManager에 적용된 배율을 추적
    private bool _effectsApplied;

    public event Action<ActiveDecree> OnDecreeProclaimed;
    public event Action               OnDecreeRevoked;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ─────────────────────────────────────────────────────────────
    //  PROCLAIM DECREE
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 영주가 칙령 선포. 자동으로 아키타입 감지 + 효과 적용.
    /// </summary>
    public void ProclaimDecree(string title, string fullText)
    {
        // 이전 칙령 효과 제거
        if (CurrentDecree != null && _effectsApplied)
            RevokeEffects(CurrentDecree.Effects);

        var archetype = DetectArchetype(fullText);
        var effects   = GenerateEffects(archetype, fullText);

        CurrentDecree = new ActiveDecree
        {
            Title          = title,
            FullText       = fullText,
            PolicyArchetype = archetype,
            Effects        = effects,
            IssuedAtUtc    = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        ApplyEffects(effects);
        _effectsApplied = true;

        OnDecreeProclaimed?.Invoke(CurrentDecree);
        Debug.Log($"[Decree] \"{title}\" proclaimed. Archetype: {archetype}");

        // 모든 NPC 페르소나에 칙령 컨텍스트 추가
        NotifyNPCs(title, fullText, effects.NpcToneOverride);
    }

    public void RevokeDecree()
    {
        if (CurrentDecree == null) return;
        RevokeEffects(CurrentDecree.Effects);
        _effectsApplied = false;
        CurrentDecree = null;
        OnDecreeRevoked?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  ARCHETYPE DETECTION (키워드 기반)
    // ─────────────────────────────────────────────────────────────

    private string DetectArchetype(string text)
    {
        text = text.ToLower();
        int militaryScore  = CountKeywords(text, "군대", "무력", "전쟁", "병사", "공격", "방어", "military", "war", "soldier");
        int commerceScore  = CountKeywords(text, "무역", "상업", "금화", "세금", "시장", "trade", "commerce", "gold", "tax");
        int peaceScore     = CountKeywords(text, "평화", "화합", "번영", "행복", "peace", "harmony", "prosperity");
        int cultureScore   = CountKeywords(text, "문화", "학문", "예술", "교육", "library", "scholar", "culture", "art");
        int expansionScore = CountKeywords(text, "정복", "영토", "확장", "점령", "conquest", "territory", "expand");

        var scores = new Dictionary<string, int>
        {
            ["military"]  = militaryScore,
            ["commerce"]  = commerceScore,
            ["peace"]     = peaceScore,
            ["culture"]   = cultureScore,
            ["expansion"] = expansionScore
        };

        string best = "peace";
        int max = 0;
        foreach (var kvp in scores)
            if (kvp.Value > max) { max = kvp.Value; best = kvp.Key; }

        return best;
    }

    private int CountKeywords(string text, params string[] keywords)
    {
        int count = 0;
        foreach (var kw in keywords)
            if (text.Contains(kw)) count++;
        return count;
    }

    // ─────────────────────────────────────────────────────────────
    //  EFFECT GENERATION
    // ─────────────────────────────────────────────────────────────

    private DecreeEffects GenerateEffects(string archetype, string fullText)
    {
        return archetype switch
        {
            "military" => new DecreeEffects
            {
                SoldierStrengthMod  =  0.30f,
                DefenseRatingMod    =  0.20f,
                TrainingSpeedMod    =  0.25f,
                FoodProductionMod   = -0.10f, // 식량을 군대에 투자
                SatisfactionMod     = -0.10f, // 백성 불만 소폭 증가
                NpcToneOverride     = "militaristic and direct"
            },
            "commerce" => new DecreeEffects
            {
                GoldProductionMod   =  0.30f,
                TaxRateMod          =  0.20f,
                TrainingSpeedMod    = -0.10f,
                SatisfactionMod     =  0.05f,
                NpcToneOverride     = "merchant-like, numbers-focused"
            },
            "peace" => new DecreeEffects
            {
                SatisfactionMod     =  0.20f,
                PopGrowthMod        =  0.15f,
                FoodProductionMod   =  0.10f,
                SoldierStrengthMod  = -0.10f,
                NpcToneOverride     = "warm and cooperative"
            },
            "culture" => new DecreeEffects
            {
                GoldProductionMod   =  0.15f,   // 지식 = 부
                SatisfactionMod     =  0.15f,
                TrainingSpeedMod    = -0.05f,
                NpcToneOverride     = "scholarly and eloquent"
            },
            "expansion" => new DecreeEffects
            {
                SoldierStrengthMod  =  0.20f,
                DefenseRatingMod    =  0.10f,
                SatisfactionMod     = -0.05f,
                FoodProductionMod   = -0.05f,
                NpcToneOverride     = "ambitious and conquest-driven"
            },
            _ => new DecreeEffects()
        };
    }

    // ─────────────────────────────────────────────────────────────
    //  APPLY / REVOKE EFFECTS
    // ─────────────────────────────────────────────────────────────

    private void ApplyEffects(DecreeEffects e)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        if (e.FoodProductionMod != 0) rm.FoodProductionMultiplier *= (1 + e.FoodProductionMod);
        if (e.WoodProductionMod != 0) rm.WoodProductionMultiplier *= (1 + e.WoodProductionMod);
        if (e.GoldProductionMod != 0) rm.GoldProductionMultiplier *= (1 + e.GoldProductionMod);
    }

    private void RevokeEffects(DecreeEffects e)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        if (e.FoodProductionMod != 0) rm.FoodProductionMultiplier /= (1 + e.FoodProductionMod);
        if (e.WoodProductionMod != 0) rm.WoodProductionMultiplier /= (1 + e.WoodProductionMod);
        if (e.GoldProductionMod != 0) rm.GoldProductionMultiplier /= (1 + e.GoldProductionMod);
    }

    private void NotifyNPCs(string decreeTitle, string fullText, string toneOverride)
    {
        // NPCPersonaSystem에 칙령 컨텍스트 주입
        // NPC들이 다음 대화부터 칙령 내용을 인식
        var context = $"\n\n[LORD'S DECREE — \"{decreeTitle}\"]\n{fullText}\n" +
                      $"Speak in a {toneOverride} tone as mandated by the decree.";

        // GlobalNPCContext에 저장 (NPCPersonaSystem이 system prompt에 append)
        PlayerPrefs.SetString("ActiveDecreeContext", context);
        PlayerPrefs.SetString("ActiveDecreeTitle", decreeTitle);
    }

    // ─────────────────────────────────────────────────────────────
    //  QUERIES
    // ─────────────────────────────────────────────────────────────

    public bool HasActiveDecree => CurrentDecree != null;

    /// <summary>NPC system prompt에 추가될 칙령 컨텍스트 (NPCPersonaSystem에서 호출)</summary>
    public string GetDecreeContext() =>
        PlayerPrefs.GetString("ActiveDecreeContext", "");
}
