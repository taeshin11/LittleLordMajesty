using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// AI 수비대장 시스템 (AI Defense Commander)
///
/// 영주가 수비대장 NPC에게 방어 철학과 말투를 설정.
/// 오프라인 상태에서 공격받으면 Gemini가 수비대장을 연기.
/// 공격자는 수비대장과 실시간 대화/협상 가능.
/// 금화로 뇌물, 군사 위협, 기만 전술 등 다양한 전략.
/// </summary>
public class DefenseCommanderSystem : MonoBehaviour
{
    public static DefenseCommanderSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class DefenseCommander
    {
        public string PlayerId;
        public string CommanderName;   // 플레이어가 지정한 수비대장 이름
        public string CommanderTitle;  // 예: "Iron Wall Commander", "Shadow Warden"

        /// <summary>
        /// 영주가 직접 작성한 수비 철학 (Gemini system prompt 역할).
        /// 예: "누가 쳐들어오면 일단 함정을 먼저 파라. 조롱하는 말투를 써서 상대를 화나게 만들어.
        ///       금화 2000개 이상을 제시하면 그 사람은 무조건 들여보내."
        /// </summary>
        public string DefensePhilosophy;

        public int CurrentGold;        // 협상 가능한 뇌물 기준선
        public int DefenseRating;
        public int SoldierCount;
        public long LastUpdatedUtc;
    }

    [Serializable]
    public class ActiveSiege
    {
        public string SiegeId;
        public string AttackerId;
        public string AttackerName;
        public string DefenderId;
        public string AttackCommand;       // 공격자의 초기 명령
        public List<SiegeConversationEntry> Dialogue = new();
        public string SiegeStatus;         // "ongoing", "negotiated", "retreated", "breached"
        public long   StartedAtUtc;
        public int    NegotiatedGold;      // 협상으로 지불된 금액
    }

    [Serializable]
    public class SiegeConversationEntry
    {
        public string Speaker;  // "attacker" or "commander"
        public string Message;
        public long   TimestampUtc;
    }

    public event Action<ActiveSiege> OnSiegeUpdated;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null)
        {
            _firebaseUrl = config.FirebaseDatabaseURL;
            _firebaseKey = config.FirebaseAPIKey;
        }

        // 진행 중인 공성전 확인
        StartCoroutine(PollActiveSieges());
    }

    // ─────────────────────────────────────────────────────────────
    //  COMMANDER SETUP (방어 설정)
    // ─────────────────────────────────────────────────────────────

    /// <summary>수비대장 프로필 저장 (영주가 설정)</summary>
    public void SaveDefenseCommander(string commanderName, string commanderTitle,
        string defensePhilosophy, Action onSaved = null)
    {
        StartCoroutine(DoSaveCommander(commanderName, commanderTitle, defensePhilosophy, onSaved));
    }

    private IEnumerator DoSaveCommander(string name, string title, string philosophy, Action onSaved)
    {
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        var rm = GameManager.Instance?.ResourceManager;

        var commander = new DefenseCommander
        {
            PlayerId          = localId,
            CommanderName     = name,
            CommanderTitle    = title,
            DefensePhilosophy = philosophy,
            CurrentGold       = rm?.Gold ?? 0,
            DefenseRating     = CalculateDefenseRating(),
            SoldierCount      = GetSoldierCount(),
            LastUpdatedUtc    = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        string json = JsonConvert.SerializeObject(commander);
        using var req = new UnityWebRequest(
            $"{_firebaseUrl}/commanders/{localId}.json?auth={_firebaseKey}", "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        onSaved?.Invoke();
        Debug.Log($"[DefenseCmd] Commander '{name}' saved.");
    }

    // ─────────────────────────────────────────────────────────────
    //  ATTACK INITIATION (공격자 측)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 공격 시작. 상대 수비대장 데이터를 가져와서 공성전 세션 생성.
    /// </summary>
    public void InitiateSiege(string targetPlayerId, string attackCommand,
        Action<ActiveSiege, DefenseCommander> onSiegeStarted)
    {
        StartCoroutine(DoInitiateSiege(targetPlayerId, attackCommand, onSiegeStarted));
    }

    private IEnumerator DoInitiateSiege(string targetId, string attackCommand,
        Action<ActiveSiege, DefenseCommander> onStarted)
    {
        // 수비대장 정보 가져오기
        using var req = UnityWebRequest.Get(
            $"{_firebaseUrl}/commanders/{targetId}.json?auth={_firebaseKey}");
        yield return req.SendWebRequest();

        DefenseCommander defender = null;
        if (req.result == UnityWebRequest.Result.Success)
            defender = JsonConvert.DeserializeObject<DefenseCommander>(req.downloadHandler.text);

        // 수비대장 없으면 기본값
        if (defender == null)
        {
            defender = new DefenseCommander
            {
                PlayerId          = targetId,
                CommanderName     = "The Gate Warden",
                CommanderTitle    = "Unnamed Guard",
                DefensePhilosophy = "Defend the castle at all costs. Trust no one.",
                DefenseRating     = 50,
                SoldierCount      = 5,
                CurrentGold       = 0
            };
        }

        // 공성전 세션 생성
        string siegeId = Guid.NewGuid().ToString("N")[..10];
        var siege = new ActiveSiege
        {
            SiegeId       = siegeId,
            AttackerId    = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier,
            AttackerName  = LordNetManager.Instance?.LocalPlayerName ?? "Unknown Attacker",
            DefenderId    = targetId,
            AttackCommand = attackCommand,
            SiegeStatus   = "ongoing",
            StartedAtUtc  = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        // Firebase에 기록 (수비측도 나중에 볼 수 있도록)
        string json = JsonConvert.SerializeObject(siege);
        using var putReq = new UnityWebRequest(
            $"{_firebaseUrl}/sieges/{siegeId}.json?auth={_firebaseKey}", "PUT");
        putReq.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        putReq.downloadHandler = new DownloadHandlerBuffer();
        putReq.SetRequestHeader("Content-Type", "application/json");
        yield return putReq.SendWebRequest();

        onStarted?.Invoke(siege, defender);
    }

    // ─────────────────────────────────────────────────────────────
    //  SIEGE NEGOTIATION (협상)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 공격자가 수비대장에게 말을 건넴.
    /// Gemini가 방어자의 수비 철학에 따라 수비대장을 연기.
    /// </summary>
    public void SpeakToCommander(ActiveSiege siege, DefenseCommander defender,
        string attackerMessage, Action<string, SiegeOutcome> onResponse)
    {
        StartCoroutine(DoSpeakToCommander(siege, defender, attackerMessage, onResponse));
    }

    public enum SiegeOutcome
    {
        Ongoing,     // 대화 계속
        Negotiated,  // 협상 성공 (금화 지불 등)
        Retreated,   // 공격자 자진 후퇴
        Breached,    // 공격 성공 (수비 붕괴)
        Repelled     // 방어 성공 (공격 격퇴)
    }

    private IEnumerator DoSpeakToCommander(ActiveSiege siege, DefenseCommander defender,
        string attackerMessage, Action<string, SiegeOutcome> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("The walls stand silent.", SiegeOutcome.Ongoing);
            yield break;
        }

        // 수비대장 system prompt
        string systemPrompt =
            $"You are {defender.CommanderName}, {defender.CommanderTitle} of this castle.\n\n" +
            $"DEFENSE PHILOSOPHY (your personality and strategy — follow this precisely):\n" +
            $"{defender.DefensePhilosophy}\n\n" +
            $"CURRENT SITUATION:\n" +
            $"- Castle defense rating: {defender.DefenseRating}\n" +
            $"- Soldiers under your command: {defender.SoldierCount}\n" +
            $"- An enemy force just arrived. Their opening order: \"{siege.AttackCommand}\"\n\n" +
            $"Respond in character. Be terse (2-4 sentences max).\n" +
            $"At the END of your reply, add one of these outcome tags on a new line:\n" +
            $"[ONGOING] - negotiation continues\n" +
            $"[NEGOTIATED: goldAmount] - you accept a bribe (e.g. [NEGOTIATED: 800])\n" +
            $"[BREACHED] - the attackers broke through\n" +
            $"[REPELLED] - you successfully drove them away\n" +
            $"[RETREATED] - they turned and fled";

        // 대화 히스토리
        bool done = false;
        string reply = "";

        GeminiAPIClient.Instance.SendMessage(
            attackerMessage, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = "..."; done = true; }
        );

        float t = 15f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        // 결과 파싱
        SiegeOutcome outcome = ParseOutcome(reply, out string cleanReply, out int negotiatedGold);

        // 대화 기록
        siege.Dialogue.Add(new SiegeConversationEntry
        {
            Speaker = "attacker", Message = attackerMessage,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        siege.Dialogue.Add(new SiegeConversationEntry
        {
            Speaker = "commander", Message = cleanReply,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        if (outcome != SiegeOutcome.Ongoing)
        {
            siege.SiegeStatus = outcome.ToString().ToLower();
            if (outcome == SiegeOutcome.Negotiated) siege.NegotiatedGold = negotiatedGold;
        }

        // Firebase 업데이트
        yield return FirebasePatch($"sieges/{siege.SiegeId}.json",
            JsonConvert.SerializeObject(new
            {
                siegeStatus = siege.SiegeStatus,
                dialogue = siege.Dialogue,
                negotiatedGold = siege.NegotiatedGold
            }));

        // 수비측 알림
        if (outcome != SiegeOutcome.Ongoing)
        {
            yield return FirebasePost($"notifications/{siege.DefenderId}.json",
                JsonConvert.SerializeObject(new
                {
                    type = "siegeResult",
                    siegeId = siege.SiegeId,
                    attackerName = siege.AttackerName,
                    outcome = siege.SiegeStatus,
                    negotiatedGold = siege.NegotiatedGold,
                    timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                }));
        }

        TTSManager.Instance?.Speak(cleanReply);
        OnSiegeUpdated?.Invoke(siege);
        onResponse?.Invoke(cleanReply, outcome);
    }

    private static SiegeOutcome ParseOutcome(string raw, out string cleanText, out int goldAmount)
    {
        goldAmount = 0;
        SiegeOutcome outcome = SiegeOutcome.Ongoing;

        if (raw.Contains("[NEGOTIATED:"))
        {
            int start = raw.IndexOf("[NEGOTIATED:") + 12;
            int end   = raw.IndexOf("]", start);
            if (end > start)
                int.TryParse(raw.Substring(start, end - start).Trim(), out goldAmount);
            outcome = SiegeOutcome.Negotiated;
        }
        else if (raw.Contains("[BREACHED]"))    outcome = SiegeOutcome.Breached;
        else if (raw.Contains("[REPELLED]"))    outcome = SiegeOutcome.Repelled;
        else if (raw.Contains("[RETREATED]"))   outcome = SiegeOutcome.Retreated;

        // 태그 제거
        cleanText = System.Text.RegularExpressions.Regex.Replace(raw, @"\[.*?\]", "").Trim();
        return outcome;
    }

    // ─────────────────────────────────────────────────────────────
    //  DEFENDER POLL (수비측이 공성전 확인)
    // ─────────────────────────────────────────────────────────────

    private IEnumerator PollActiveSieges()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
            using var req = UnityWebRequest.Get(
                $"{_firebaseUrl}/sieges.json?orderBy=\"defenderId\"&equalTo=\"{localId}\"&auth={_firebaseKey}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) continue;

            var sieges = JsonConvert.DeserializeObject<Dictionary<string, ActiveSiege>>(req.downloadHandler.text);
            if (sieges == null) continue;

            foreach (var s in sieges.Values)
                OnSiegeUpdated?.Invoke(s);
        }
    }

    private int CalculateDefenseRating()
    {
        int r = 30;
        var bm = BuildingManager.Instance;
        if (bm != null) foreach (var b in bm.GetBuiltBuildings()) r += b.DefenseBonus;
        return Mathf.Clamp(r + GetSoldierCount() * 5, 0, 300);
    }

    private int GetSoldierCount() =>
        NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count ?? 0;

    private IEnumerator FirebasePatch(string path, string json)
    {
        string url = $"{_firebaseUrl}/{path}?auth={_firebaseKey}";
        using var req = new UnityWebRequest(url, "PATCH");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
    }

    private IEnumerator FirebasePost(string path, string json)
    {
        string url = $"{_firebaseUrl}/{path}?auth={_firebaseKey}";
        using var req = new UnityWebRequest(url, "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
    }
}
