using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 프롬프트 입력이 귀찮은 유저를 위한 원탭 빠른 실행 템플릿.
///
/// 모든 LLM 입력 UI에 상황에 맞는 템플릿 버튼을 제공.
/// 유저는 버튼 한 번으로 프롬프트 없이 게임 진행 가능.
/// </summary>
public static class QuickActionTemplates
{
    [Serializable]
    public class QuickAction
    {
        public string Label;          // 버튼에 표시되는 짧은 텍스트
        public string Prompt;         // 실제로 LLM에 전달되는 프롬프트
        public string Icon;           // 이모지 아이콘
        public QuickActionCategory Category;
    }

    public enum QuickActionCategory
    {
        NPC,          // NPC 대화
        Battle,       // 전투/방어
        Diplomacy,    // 외교
        Economy,      // 경제/생산
        Espionage,    // 첩보
        Speech        // 연설
    }

    // ─────────────────────────────────────────────────────────────
    //  NPC 대화 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] NPCQuickActions = {
        new() { Label = "임무 보고",   Icon = "📋", Category = QuickActionCategory.NPC,
                Prompt = "지금 네 임무 진행 상황을 간단히 보고해." },
        new() { Label = "사기 올려",   Icon = "💪", Category = QuickActionCategory.NPC,
                Prompt = "기운을 차려. 오늘도 열심히 해줘." },
        new() { Label = "승진 검토",   Icon = "⬆️", Category = QuickActionCategory.NPC,
                Prompt = "네 최근 활약을 보니 승진을 고려해볼 만하다. 본인 생각은?" },
        new() { Label = "문제 있나",   Icon = "❓", Category = QuickActionCategory.NPC,
                Prompt = "요즘 영지에서 신경 쓰이는 문제가 있으면 솔직히 말해봐." },
        new() { Label = "충성 확인",   Icon = "🤝", Category = QuickActionCategory.NPC,
                Prompt = "나는 너를 믿는다. 앞으로도 나와 함께 이 영지를 지켜주겠나?" },
    };

    // ─────────────────────────────────────────────────────────────
    //  직업별 NPC 특화 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly Dictionary<NPCPersona.NPCProfession, QuickAction[]> ProfessionQuickActions = new()
    {
        [NPCPersona.NPCProfession.Farmer] = new QuickAction[] {
            new() { Label = "수확 보고",  Icon = "🌾", Prompt = "이번 수확량이 얼마나 되는지 보고해." },
            new() { Label = "밭 늘려",    Icon = "🚜", Prompt = "식량이 부족하다. 가능한 한 빨리 경작지를 늘려줘." },
            new() { Label = "날씨 어때",  Icon = "☁️", Prompt = "요즘 날씨가 수확에 영향을 미치고 있나?" },
        },
        [NPCPersona.NPCProfession.Soldier] = new QuickAction[] {
            new() { Label = "순찰 나가",  Icon = "⚔️", Prompt = "성벽 주위를 순찰하고 수상한 점이 있으면 즉시 보고해." },
            new() { Label = "훈련 시작",  Icon = "🏋️", Prompt = "부하들을 데리고 훈련 시작해. 다음 전투 대비다." },
            new() { Label = "위협 있나",  Icon = "🔍", Prompt = "주변 영지에서 군사적 위협이 될 만한 움직임이 있나?" },
        },
        [NPCPersona.NPCProfession.Merchant] = new QuickAction[] {
            new() { Label = "수익 보고",  Icon = "💰", Prompt = "이번 달 거래 수익 현황을 간단히 보고해." },
            new() { Label = "싸게 사와",  Icon = "🛒", Prompt = "지금 가장 필요한 건 식량이다. 최대한 싸게 구해와." },
            new() { Label = "무역로",    Icon = "🗺️", Prompt = "새로운 무역 루트나 수익이 좋은 거래처 아는 곳 있어?" },
        },
        [NPCPersona.NPCProfession.Vassal] = new QuickAction[] {
            new() { Label = "조언 구해",  Icon = "🤔", Prompt = "영지 운영에 대해 네 솔직한 조언을 듣고 싶다." },
            new() { Label = "분쟁 해결",  Icon = "⚖️", Prompt = "영지 내 분쟁을 처리해줘. 공평하게 해결하도록." },
            new() { Label = "일정 잡아",  Icon = "📅", Prompt = "이번 주 영지 운영 일정을 정리해줘." },
        },
        [NPCPersona.NPCProfession.Scholar] = new QuickAction[] {
            new() { Label = "역사 조언",  Icon = "📚", Prompt = "과거 왕국들이 이 상황에서 어떻게 했는지 알려줘." },
            new() { Label = "기술 연구",  Icon = "🔬", Prompt = "지금 영지에 가장 필요한 기술이 뭔지 분석해줘." },
            new() { Label = "문서 작성",  Icon = "📜", Prompt = "공식 포고문 초안을 작성해줘." },
        },
        [NPCPersona.NPCProfession.Priest] = new QuickAction[] {
            new() { Label = "축복 요청",  Icon = "✨", Prompt = "오늘 전투를 앞두고 병사들을 축복해줘." },
            new() { Label = "민심 파악",  Icon = "💭", Prompt = "요즘 백성들 사이에서 어떤 이야기가 도는지 알려줘." },
            new() { Label = "조언",       Icon = "🕊️", Prompt = "내가 고민하는 윤리적 문제에 대한 네 생각은?" },
        },
        [NPCPersona.NPCProfession.Spy] = new QuickAction[] {
            new() { Label = "정보 보고",  Icon = "🔍", Prompt = "최근에 수집한 중요한 첩보를 보고해줘." },
            new() { Label = "침투 임무",  Icon = "🕵️", Prompt = "적국 귀족 한 명을 포섭할 계획을 세워봐." },
            new() { Label = "위협 분석",  Icon = "⚠️", Prompt = "현재 영지에 가장 위험한 내부 위협이 뭐라고 생각해?" },
        },
    };

    // ─────────────────────────────────────────────────────────────
    //  전투 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] BattleQuickActions = {
        new() { Label = "전방 방어",   Icon = "🛡️", Category = QuickActionCategory.Battle,
                Prompt = "보병은 성문 앞에 방어진을 펴고, 궁수는 성벽 위에서 지원 사격해라." },
        new() { Label = "역습 준비",   Icon = "⚡", Category = QuickActionCategory.Battle,
                Prompt = "적이 공격을 멈추는 순간 기병대로 역습을 가해라." },
        new() { Label = "함정 설치",   Icon = "🪤", Category = QuickActionCategory.Battle,
                Prompt = "성문 앞 통로에 함정을 최대한 많이 설치해서 적의 진격을 늦춰라." },
        new() { Label = "전군 후퇴",   Icon = "🏃", Category = QuickActionCategory.Battle,
                Prompt = "피해가 너무 크다. 일단 성 안으로 후퇴하고 방어 태세를 갖춰라." },
        new() { Label = "사기 연설",   Icon = "📢", Category = QuickActionCategory.Speech,
                Prompt = "병사들이여! 오늘 우리가 이 성벽을 지키면, 내일 우리 가족이 안전하다! 끝까지 싸워라!" },
    };

    // ─────────────────────────────────────────────────────────────
    //  외교 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] DiplomacyQuickActions = {
        new() { Label = "동맹 제안",   Icon = "🤝", Category = QuickActionCategory.Diplomacy,
                Prompt = "우리 두 영지가 힘을 합치면 이 대륙에서 적이 없을 것이오. 동맹을 제안합니다." },
        new() { Label = "평화 협상",   Icon = "🕊️", Category = QuickActionCategory.Diplomacy,
                Prompt = "더 이상의 전쟁은 서로에게 이롭지 않소. 평화 조약을 맺읍시다." },
        new() { Label = "조공 요구",   Icon = "💎", Category = QuickActionCategory.Diplomacy,
                Prompt = "우리 군대의 위력을 이미 보셨을 것이오. 매달 금화 500냥을 조공으로 바치시오." },
        new() { Label = "통상 제안",   Icon = "⚖️", Category = QuickActionCategory.Diplomacy,
                Prompt = "서로 필요한 자원을 교환하는 통상 협약을 맺는 것이 어떻겠습니까?" },
    };

    // ─────────────────────────────────────────────────────────────
    //  첩보 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] EspionageQuickActions = {
        new() { Label = "스파이 파견",  Icon = "🕵️", Category = QuickActionCategory.Espionage,
                Prompt = "일꾼으로 위장해서 잠입하고, 창고 위치와 병력 현황을 파악해서 돌아와라." },
        new() { Label = "선동 공작",   Icon = "☠️", Category = QuickActionCategory.Espionage,
                Prompt = "농부들에게 '저 영주는 세금을 더 올릴 것'이라는 소문을 퍼뜨려 불만을 높여라." },
        new() { Label = "가짜 뉴스",   Icon = "📰", Category = QuickActionCategory.Espionage,
                Prompt = "저 영주가 오크 두목과 비밀리에 협상을 하고 있다는 소문을 온 대륙에 퍼뜨려라." },
        new() { Label = "물자 파괴",   Icon = "🔥", Category = QuickActionCategory.Espionage,
                Prompt = "창고에 몰래 불을 질러 식량 비축분을 태워버려라. 들키지 않도록 해라." },
    };

    // ─────────────────────────────────────────────────────────────
    //  수비대장 빠른 설정
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] DefenseCommanderPresets = {
        new() { Label = "강경 수비",   Icon = "🗿", Category = QuickActionCategory.Battle,
                Prompt = "어떤 조건에도 성문을 열지 마라. 모든 적을 사살하라. 협상은 없다." },
        new() { Label = "영리한 수비", Icon = "🦊", Category = QuickActionCategory.Battle,
                Prompt = "일단 협상하는 척 시간을 끌면서 지원군을 기다려라. 금화 1000냥 이상 제시하면 받아라." },
        new() { Label = "도발 전술",   Icon = "😤", Category = QuickActionCategory.Battle,
                Prompt = "적을 최대한 조롱하고 화나게 만들어서 경솔한 공격을 유도해라." },
        new() { Label = "유연 대응",   Icon = "🌊", Category = QuickActionCategory.Battle,
                Prompt = "상황을 잘 보고 유리하면 싸우고, 불리하면 협상해라. 성을 지키는 게 목표다." },
    };

    // ─────────────────────────────────────────────────────────────
    //  경제/생산 빠른 명령
    // ─────────────────────────────────────────────────────────────

    public static readonly QuickAction[] EconomyQuickActions = {
        new() { Label = "빵 생산 우선", Icon = "🍞", Category = QuickActionCategory.Economy,
                Prompt = "모든 여분 일꾼을 밀밭과 빵집에 배치해라. 식량 생산이 최우선이다." },
        new() { Label = "무기 생산",    Icon = "⚔️", Category = QuickActionCategory.Economy,
                Prompt = "철광석을 최대한 채굴하고 대장장이를 풀가동해서 무기를 만들어라." },
        new() { Label = "균형 배분",    Icon = "⚖️", Category = QuickActionCategory.Economy,
                Prompt = "모든 생산 체인에 일꾼을 균등하게 배치해서 균형 잡힌 생산을 유지해라." },
        new() { Label = "세금 최대화",  Icon = "💰", Category = QuickActionCategory.Economy,
                Prompt = "시민들의 불만이 폭발하지 않는 선에서 세금을 최대한 올려라." },
    };

    // ─────────────────────────────────────────────────────────────
    //  CONTEXT-AWARE HELPER
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 현재 게임 상태에 맞는 빠른 명령 목록 반환.
    /// UI가 이 목록으로 버튼을 동적 생성.
    /// </summary>
    public static QuickAction[] GetContextualActions(GameManager.GameState state,
        NPCPersona.NPCProfession? npcProfession = null)
    {
        return state switch
        {
            GameManager.GameState.Dialogue =>
                npcProfession.HasValue && ProfessionQuickActions.TryGetValue(npcProfession.Value, out var profActions)
                    ? profActions
                    : NPCQuickActions,
            GameManager.GameState.Battle  => BattleQuickActions,
            GameManager.GameState.WorldMap => DiplomacyQuickActions,
            GameManager.GameState.Castle  => EconomyQuickActions,
            _ => NPCQuickActions
        };
    }
}
