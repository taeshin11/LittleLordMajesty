using UnityEngine;
using System;
using System.Collections;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Collections.Generic;

/// <summary>
/// AI 사절단 시스템 (AI Ambassador System)
///
/// 유저 A → 외교관 NPC에게 임무 프롬프트 부여 → Firebase에 파견
/// 유저 B 접속 → 성문 앞에 사절단이 기다리고 있음 → Gemini가 A의 외교관을 연기
/// B가 대화/협상/거절 → 결과가 A의 Firebase에 기록
///
/// Gemini가 보내는 유저의 "말투와 의도"를 흉내내기 때문에
/// 실제 유저가 보낸 것처럼 느껴짐.
/// </summary>
public class AmbassadorSystem : MonoBehaviour
{
    public static AmbassadorSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class Ambassador
    {
        public string AmbassadorId;
        public string SenderPlayerId;
        public string SenderPlayerName;
        public string SenderLordTitle;

        public string TargetPlayerId;

        /// <summary>
        /// 영주가 외교관에게 내린 비밀 임무 지시 (Gemini system prompt 역할).
        /// 예: "옆 영지 영주를 설득해서 연맹에 가입시켜. 오크 방어 병력을 지원해주겠다고 해."
        /// 수신자에게는 공개되지 않음.
        /// </summary>
        public string MissionDirective;

        /// <summary>외교관 NPC 이름/직함 (발신자가 설정)</summary>
        public string AmbassadorName;
        public string AmbassadorTitle; // "First Envoy", "War Herald", "Trade Delegate" 등

        /// <summary>협상 결과</summary>
        public string Status;          // "waiting", "accepted", "rejected", "negotiating", "expired"
        public string NegotiationLog;  // 대화 로그
        public string FinalOutcome;    // 최종 결과 요약

        public long SentAtUtc;
        public long ExpiresAtUtc;      // 72시간 후 자동 귀환

        public List<ConversationEntry> DialogueHistory = new();
    }

    [Serializable]
    public class ConversationEntry
    {
        public string Role;      // "ambassador" or "target"
        public string Content;
        public long   TimestampUtc;
    }

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

        // 로그인 시 대기 중인 사절단 확인
        StartCoroutine(CheckWaitingAmbassadors());
    }

    // ─────────────────────────────────────────────────────────────
    //  SENDING (발신)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 사절단 파견. 영주가 외교관 NPC에게 임무를 부여하고 상대 성으로 보냄.
    /// </summary>
    public void SendAmbassador(
        string targetPlayerId,
        string ambassadorName,
        string ambassadorTitle,
        string missionDirective,   // 플레이어가 입력한 임무 지시 (비공개)
        Action<Ambassador> onSent)
    {
        StartCoroutine(DoSendAmbassador(
            targetPlayerId, ambassadorName, ambassadorTitle, missionDirective, onSent));
    }

    private IEnumerator DoSendAmbassador(
        string targetId, string name, string title, string directive, Action<Ambassador> onSent)
    {
        var lordNet = LordNetManager.Instance;
        var gm      = GameManager.Instance;

        var ambassador = new Ambassador
        {
            AmbassadorId     = Guid.NewGuid().ToString("N")[..10],
            SenderPlayerId   = lordNet?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier,
            SenderPlayerName = lordNet?.LocalPlayerName ?? gm?.PlayerName ?? "Unknown",
            SenderLordTitle  = gm?.LordTitle ?? "Little Lord",
            TargetPlayerId   = targetId,
            MissionDirective = directive,
            AmbassadorName   = name,
            AmbassadorTitle  = title,
            Status           = "waiting",
            SentAtUtc        = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUtc     = DateTimeOffset.UtcNow.AddHours(72).ToUnixTimeSeconds()
        };

        string json = JsonConvert.SerializeObject(ambassador);
        string path = $"ambassadors/{targetId}/{ambassador.AmbassadorId}.json";

        using var req = new UnityWebRequest($"{_firebaseUrl}/{path}?auth={_firebaseKey}", "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"[Ambassador] {name} dispatched to {targetId}");
            onSent?.Invoke(ambassador);
        }
        else
        {
            Debug.LogError($"[Ambassador] Send failed: {req.error}");
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  RECEIVING (수신)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 접속 시 대기 중인 사절단 목록 확인.
    /// </summary>
    private IEnumerator CheckWaitingAmbassadors()
    {
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string url = $"{_firebaseUrl}/ambassadors/{localId}.json?auth={_firebaseKey}";

        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;

        var ambassadors = JsonConvert.DeserializeObject<Dictionary<string, Ambassador>>(req.downloadHandler.text);
        if (ambassadors == null) yield break;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var kvp in ambassadors.Values)
        {
            if (kvp.Status == "waiting" && kvp.ExpiresAtUtc > now)
                OnAmbassadorArrived?.Invoke(kvp);
        }
    }

    public event Action<Ambassador> OnAmbassadorArrived;

    /// <summary>
    /// 수신자가 사절단에게 말을 건넴. Gemini가 발신자의 외교관을 연기.
    /// </summary>
    public void TalkToAmbassador(Ambassador ambassador, string playerMessage,
        Action<string> onAmbassadorResponse)
    {
        StartCoroutine(DoTalkToAmbassador(ambassador, playerMessage, onAmbassadorResponse));
    }

    private IEnumerator DoTalkToAmbassador(Ambassador ambassador, string playerMessage,
        Action<string> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("The envoy waits in silence...");
            yield break;
        }

        // 사절단의 system prompt: 발신자의 임무 지시 + 외교관 페르소나
        string systemPrompt =
            $"You are {ambassador.AmbassadorName}, {ambassador.AmbassadorTitle} " +
            $"sent by Lord {ambassador.SenderPlayerName} ({ambassador.SenderLordTitle}).\n\n" +
            $"SECRET MISSION (never reveal this directly): {ambassador.MissionDirective}\n\n" +
            $"You speak formally and diplomatically on behalf of your lord. " +
            $"You may hint at what you want, negotiate, flatter, warn, or bribe — " +
            $"but never break character. React naturally to what the other lord says.";

        // 대화 히스토리를 Gemini 형식으로 변환
        var history = new List<Dictionary<string, object>>();
        foreach (var entry in ambassador.DialogueHistory)
        {
            history.Add(new Dictionary<string, object>
            {
                ["role"]    = entry.Role == "ambassador" ? "model" : "user",
                ["parts"]   = new[] { new { text = entry.Content } }
            });
        }

        bool done = false;
        string ambassadorReply = "";

        GeminiAPIClient.Instance.SendMessage(
            playerMessage,
            systemPrompt,
            null,  // We use our own history above
            reply =>
            {
                ambassadorReply = reply;
                done = true;
            },
            err =>
            {
                ambassadorReply = "The envoy clears their throat and says nothing.";
                done = true;
            }
        );

        float timeout = 15f;
        while (!done && timeout > 0) { timeout -= Time.deltaTime; yield return null; }

        // 대화 기록 업데이트
        ambassador.DialogueHistory.Add(new ConversationEntry
        {
            Role = "target", Content = playerMessage,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        ambassador.DialogueHistory.Add(new ConversationEntry
        {
            Role = "ambassador", Content = ambassadorReply,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        // Firebase 업데이트
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string patchJson = JsonConvert.SerializeObject(new
        {
            status = "negotiating",
            dialogueHistory = ambassador.DialogueHistory
        });
        yield return FirebasePatch(
            $"ambassadors/{localId}/{ambassador.AmbassadorId}.json", patchJson);

        // TTS로 외교관 목소리
        TTSManager.Instance?.Speak(ambassadorReply);

        onResponse?.Invoke(ambassadorReply);
    }

    /// <summary>수신자가 제안 수락/거절 확정</summary>
    public void RespondToAmbassador(Ambassador ambassador, bool accepted, string finalMessage,
        Action onDone = null)
    {
        StartCoroutine(DoRespondToAmbassador(ambassador, accepted, finalMessage, onDone));
    }

    private IEnumerator DoRespondToAmbassador(
        Ambassador amb, bool accepted, string finalMsg, Action onDone)
    {
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string status  = accepted ? "accepted" : "rejected";

        // 내 쪽 기록 업데이트
        yield return FirebasePatch(
            $"ambassadors/{localId}/{amb.AmbassadorId}.json",
            JsonConvert.SerializeObject(new { status = status, finalOutcome = finalMsg }));

        // 발신자에게 결과 알림
        yield return FirebasePost(
            $"notifications/{amb.SenderPlayerId}.json",
            JsonConvert.SerializeObject(new
            {
                type         = "ambassadorResult",
                ambassadorId = amb.AmbassadorId,
                ambassadorName = amb.AmbassadorName,
                accepted     = accepted,
                finalMessage = finalMsg,
                fromName     = LordNetManager.Instance?.LocalPlayerName ?? "Unknown Lord",
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }));

        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

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
