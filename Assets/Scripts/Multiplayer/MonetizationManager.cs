using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// 부분 유료화(Freemium) 시스템.
///
/// 무과금: 광고 시청으로 대부분 해결 가능
/// 유료: 시간 단축 + 프리미엄 NPC + 스킨 + 월정액
///
/// LLM 특화 수익 모델:
/// - 지혜의 두루마리: AI 대화 횟수 제한 + 충전
/// - 전설의 영웅 NPC: 고급 System Prompt가 적용된 프리미엄 NPC
/// - 수비대장 프리미엄: 더 복잡한 방어 AI 페르소나
/// </summary>
public class MonetizationManager : MonoBehaviour
{
    public static MonetizationManager Instance { get; private set; }

    // ─────────────────────────────────────────────────────────────
    //  FREE TIER LIMITS
    // ─────────────────────────────────────────────────────────────

    private const int FREE_DAILY_SCROLLS     = 30;   // 무료 AI 대화 횟수/일
    private const int PREMIUM_DAILY_SCROLLS  = 999;  // 사실상 무제한
    private const int AD_REWARD_SCROLLS      = 5;    // 광고 1회 보상
    private const int AD_REWARD_GOLD         = 100;
    private const int AD_SCOUT_HINT_COST     = 1;    // 정찰 힌트 광고 횟수

    // ─────────────────────────────────────────────────────────────
    //  PLAYER STATUS
    // ─────────────────────────────────────────────────────────────

    public int   WisdomScrollsToday  { get; private set; }
    public bool  IsPremium           { get; private set; }
    public bool  HasMonthlyPass      => IsPremium;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadStatus();
    }

    private void LoadStatus()
    {
        // 날짜 초기화 체크
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        string lastDay = PlayerPrefs.GetString("LastScrollDay", "");
        if (lastDay != today)
        {
            PlayerPrefs.SetString("LastScrollDay", today);
            PlayerPrefs.SetInt("ScrollsUsedToday", 0);
        }

        WisdomScrollsToday = PlayerPrefs.GetInt("ScrollsUsedToday", 0);
        IsPremium          = PlayerPrefs.GetInt("IsPremium", 0) == 1;
    }

    // ─────────────────────────────────────────────────────────────
    //  WISDOM SCROLLS (AI 대화 토큰)
    // ─────────────────────────────────────────────────────────────

    public int  ScrollsRemaining => DailyLimit - WisdomScrollsToday;
    public int  DailyLimit       => IsPremium ? PREMIUM_DAILY_SCROLLS : FREE_DAILY_SCROLLS;
    public bool CanUseAI         => ScrollsRemaining > 0;

    /// <summary>AI 대화 전에 호출. 두루마리 소모. 반환: 사용 가능 여부.</summary>
    public bool ConsumeScroll(int amount = 1)
    {
        if (!CanUseAI) return false;
        WisdomScrollsToday += amount;
        PlayerPrefs.SetInt("ScrollsUsedToday", WisdomScrollsToday);
        return true;
    }

    public void AddScrolls(int amount)
    {
        WisdomScrollsToday = Mathf.Max(0, WisdomScrollsToday - amount);
        PlayerPrefs.SetInt("ScrollsUsedToday", WisdomScrollsToday);
        ToastNotification.Instance?.Show($"+{amount} Wisdom Scrolls!");
    }

    // ─────────────────────────────────────────────────────────────
    //  REWARDED ADS (광고 보상)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 보상형 광고 시청. 실제 광고 SDK(AdMob 등) 연동은 여기서.
    /// 현재는 Editor에서 즉시 보상.
    /// </summary>
    public void ShowRewardedAd(RewardType type, Action<bool> onComplete)
    {
#if UNITY_EDITOR
        // Editor에서는 즉시 보상
        Debug.Log($"[Monetization] Editor: Rewarded ad simulated ({type})");
        GrantAdReward(type);
        onComplete?.Invoke(true);
#else
        // TODO: AdMob / Unity Ads 연동
        // Ads.ShowRewarded(onComplete);
        Debug.LogWarning("[Monetization] Rewarded ads not configured. Add AdMob package.");
        onComplete?.Invoke(false);
#endif
    }

    public enum RewardType
    {
        /// 이국 상인: 자원 + 두루마리
        WanderingMerchant,
        /// 파랑새 정찰: 적 수비대장 성향 힌트
        ScoutHint,
        /// 신령의 가호: 전투 일시 버프
        BattleBuff,
        /// 건설 가속: 건물 건설 즉시 완료
        BuildAccelerate,
        /// 두루마리 충전
        ScrollRecharge
    }

    private void GrantAdReward(RewardType type)
    {
        var rm = GameManager.Instance?.ResourceManager;

        switch (type)
        {
            case RewardType.WanderingMerchant:
                rm?.AddResource(ResourceManager.ResourceType.Gold, AD_REWARD_GOLD);
                AddScrolls(AD_REWARD_SCROLLS);
                ToastNotification.Instance?.Show(
                    "The merchant thanks you for listening! +100 Gold, +5 Scrolls");
                break;

            case RewardType.ScoutHint:
                // 힌트 내용은 WorldMapUI에서 활용
                PlayerPrefs.SetInt("ScoutHintAvailable", 1);
                ToastNotification.Instance?.Show(
                    "The blue bird whispers enemy secrets...");
                break;

            case RewardType.BattleBuff:
                StartCoroutine(ApplyBattleBuff(30f, 0.2f)); // 30초, 방어력 20% 증가
                ToastNotification.Instance?.Show(
                    "Divine protection granted! Defense +20% for 30 seconds.");
                break;

            case RewardType.BuildAccelerate:
                // BuildingManager에 즉시 완료 신호
                PlayerPrefs.SetInt("BuildAccelerateAvailable", 1);
                ToastNotification.Instance?.Show("Construction accelerated!");
                break;

            case RewardType.ScrollRecharge:
                AddScrolls(AD_REWARD_SCROLLS * 2);
                break;
        }
    }

    private IEnumerator ApplyBattleBuff(float duration, float defenseBoostPercent)
    {
        PlayerPrefs.SetFloat("BattleBuffExpiry",
            Time.realtimeSinceStartup + duration);
        yield return new WaitForSeconds(duration);
        PlayerPrefs.DeleteKey("BattleBuffExpiry");
    }

    public bool HasBattleBuff() =>
        PlayerPrefs.GetFloat("BattleBuffExpiry", 0) > Time.realtimeSinceStartup;

    // ─────────────────────────────────────────────────────────────
    //  PREMIUM NPC (전설의 영웅)
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class LegendaryNPC
    {
        public string Id;
        public string Name;
        public string Title;
        /// <summary>복잡한 System Prompt — 유료 구매 컨텐츠</summary>
        public string PremiumSystemPrompt;
        public int    PriceGems;        // 인앱 결제 보석
        public string Archetype;        // "strategist", "warrior", "merchant", "spy"
    }

    private static readonly LegendaryNPC[] LegendaryNPCs = {
        new() {
            Id = "zhuge_liang",
            Name = "Kongming",
            Title = "The Sleeping Dragon",
            PremiumSystemPrompt =
                "You are Kongming, the greatest strategist who ever lived. " +
                "You speak in riddles and metaphors. You always offer 3 options — " +
                "one aggressive, one diplomatic, one cunning — and explain which " +
                "Sun Tzu principle applies. You never give direct orders; only insights.",
            PriceGems = 300,
            Archetype = "strategist"
        },
        new() {
            Id = "shadow_spy",
            Name = "The Whisperer",
            Title = "Master of Shadows",
            PremiumSystemPrompt =
                "You are a master spy who speaks only in half-truths. " +
                "You always hint that you know more than you reveal. " +
                "Every answer includes one false detail — the lord must figure out which one. " +
                "You trade information for secrets.",
            PriceGems = 250,
            Archetype = "spy"
        },
        new() {
            Id = "iron_general",
            Name = "Ironheart",
            Title = "The Unbroken General",
            PremiumSystemPrompt =
                "You are a battle-hardened general who has never lost a battle. " +
                "You speak bluntly. You always calculate odds precisely. " +
                "You despise cowardice but respect clever tactics. " +
                "In battle situations, give specific troop deployment orders.",
            PriceGems = 280,
            Archetype = "warrior"
        }
    };

    public LegendaryNPC[] GetAllLegendaryNPCs() => LegendaryNPCs;

    public bool OwnsLegendaryNPC(string npcId) =>
        PlayerPrefs.GetInt($"OwnNPC_{npcId}", 0) == 1;

    public void UnlockLegendaryNPC(string npcId)
    {
        PlayerPrefs.SetInt($"OwnNPC_{npcId}", 1);
        var npc = Array.Find(LegendaryNPCs, n => n.Id == npcId);
        if (npc != null)
            ToastNotification.Instance?.Show($"'{npc.Name}' has joined your castle!");
    }

    // ─────────────────────────────────────────────────────────────
    //  MONTHLY PASS (영주의 축복)
    // ─────────────────────────────────────────────────────────────

    public void ActivateMonthlyPass()
    {
        IsPremium = true;
        PlayerPrefs.SetInt("IsPremium", 1);
        PlayerPrefs.SetString("PremiumExpiry",
            DateTime.UtcNow.AddDays(30).ToString("O"));

        ToastNotification.Instance?.Show(
            "Lord's Blessing activated! Enjoy unlimited wisdom and 20% faster construction.");
    }

    public bool CheckPremiumExpiry()
    {
        string expiry = PlayerPrefs.GetString("PremiumExpiry", "");
        if (string.IsNullOrEmpty(expiry)) return false;

        if (DateTime.TryParse(expiry, out DateTime expiryDate))
        {
            if (DateTime.UtcNow > expiryDate)
            {
                IsPremium = false;
                PlayerPrefs.SetInt("IsPremium", 0);
                return false;
            }
            return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    //  UI HELPERS
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 빠른 UI 버튼용 — AI 사용 전에 호출.
    /// 두루마리 없으면 광고 시청 or 결제 팝업 표시.
    /// </summary>
    public void RequestAIUsage(Action onGranted, Action onDenied = null)
    {
        if (CanUseAI)
        {
            ConsumeScroll();
            onGranted?.Invoke();
            return;
        }

        // 두루마리 소진 — 광고 보상 제안
        ShowRewardedAd(RewardType.ScrollRecharge, success =>
        {
            if (success && CanUseAI)
            {
                ConsumeScroll();
                onGranted?.Invoke();
            }
            else
            {
                ToastNotification.Instance?.Show(
                    "No Wisdom Scrolls left. Watch an ad or upgrade to Lord's Blessing.");
                onDenied?.Invoke();
            }
        });
    }
}
