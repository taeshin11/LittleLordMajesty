using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 국가 헌법 시스템 (Nation Constitution / AI Prime Minister)
///
/// 국가를 창설한 영주(왕)가 '헌법(System Prompt)'을 작성.
/// 이 헌법이 AI 재상(Prime Minister)의 성격, 가치관, 정책을 결정.
/// 소속 유저들은 AI 재상에게 자원 요청, 임무 수행, 정보 획득 등을 할 수 있음.
///
/// 예시 헌법:
/// "우리 국가는 평화를 사랑한다. 뉴비를 무조건 보호한다.
///  자원을 요청하는 영주에게는 국가 발전에 대한 공헌도를 따져 배분한다."
/// </summary>
public class NationConstitutionSystem : MonoBehaviour
{
    public static NationConstitutionSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class NationConstitution
    {
        public string NationId;
        public string NationName;
        public string NationTag;

        /// <summary>
        /// 왕이 직접 작성한 국가 헌법. AI 재상의 성격과 정책을 결정.
        /// </summary>
        public string ConstitutionText;

        /// <summary>AI 재상 이름/직함</summary>
        public string PrimeMinisterName;
        public string PrimeMinisterTitle;

        /// <summary>국가 공동 자원 금고</summary>
        public int    SharedGold;
        public int    SharedWood;
        public int    SharedFood;

        public long   LastUpdatedUtc;
    }

    [Serializable]
    public class PrimeMinisterDialogue
    {
        public string SessionId;
        public string NationId;
        public string PlayerIdAsker;
        public string PlayerNameAsker;
        public List<PMConversationEntry> History = new();
        public long   StartedAtUtc;
    }

    [Serializable]
    public class PMConversationEntry
    {
        public string Role;    // "user" or "pm"
        public string Content;
        public long   TimestampUtc;
    }

    [Serializable]
    public class NationEvent
    {
        public string EventId;
        public string NationId;
        public string Title;
        public string Description;      // PM이 헌법에 따라 생성
        public string EventType;        // "quest", "war_call", "resource_share", "election"
        public bool   IsActive;
        public long   CreatedAtUtc;
    }

    public event Action<NationConstitution>   OnConstitutionLoaded;
    public event Action<string>               OnPMResponse;
    public event Action<NationEvent>          OnNationEventIssued;

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
    }

    // ─────────────────────────────────────────────────────────────
    //  CONSTITUTION SETUP (왕 전용)
    // ─────────────────────────────────────────────────────────────

    /// <summary>국가 헌법 저장 (국왕만 가능)</summary>
    public void SaveConstitution(
        string nationId,
        string nationName,
        string nationTag,
        string constitutionText,
        string pmName,
        string pmTitle,
        Action onSaved = null)
    {
        StartCoroutine(DoSaveConstitution(
            nationId, nationName, nationTag, constitutionText, pmName, pmTitle, onSaved));
    }

    private IEnumerator DoSaveConstitution(
        string nationId, string name, string tag,
        string constitution, string pmName, string pmTitle, Action onSaved)
    {
        var data = new NationConstitution
        {
            NationId           = nationId,
            NationName         = name,
            NationTag          = tag,
            ConstitutionText   = constitution,
            PrimeMinisterName  = pmName,
            PrimeMinisterTitle = pmTitle,
            LastUpdatedUtc     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        string json = JsonConvert.SerializeObject(data);
        using var req = new UnityWebRequest(
            $"{_firebaseUrl}/constitutions/{nationId}.json?auth={_firebaseKey}", "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        onSaved?.Invoke();
        Debug.Log($"[Constitution] [{tag}] {name} constitution saved.");
    }

    // ─────────────────────────────────────────────────────────────
    //  TALK TO PRIME MINISTER (소속 유저 전용)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// AI 재상과 대화. 재상은 헌법에 따라 자원 배분, 임무 부여, 정보 제공.
    /// </summary>
    public void TalkToPrimeMinister(
        string nationId,
        PrimeMinisterDialogue session,
        string playerMessage,
        Action<string> onResponse)
    {
        StartCoroutine(DoTalkToPM(nationId, session, playerMessage, onResponse));
    }

    private IEnumerator DoTalkToPM(
        string nationId,
        PrimeMinisterDialogue session,
        string playerMessage,
        Action<string> onResponse)
    {
        // 헌법 가져오기
        using var constReq = UnityWebRequest.Get(
            $"{_firebaseUrl}/constitutions/{nationId}.json?auth={_firebaseKey}");
        yield return constReq.SendWebRequest();

        NationConstitution constitution = null;
        if (constReq.result == UnityWebRequest.Result.Success)
            constitution = JsonConvert.DeserializeObject<NationConstitution>(constReq.downloadHandler.text);

        if (constitution == null)
        {
            onResponse?.Invoke("The Prime Minister's seat is empty. The king has not established a constitution.");
            yield break;
        }

        // 재상 system prompt
        string systemPrompt =
            $"You are {constitution.PrimeMinisterName}, {constitution.PrimeMinisterTitle} " +
            $"of the nation [{constitution.NationTag}] {constitution.NationName}.\n\n" +
            $"NATIONAL CONSTITUTION (this is your governing philosophy — follow it strictly):\n" +
            $"{constitution.ConstitutionText}\n\n" +
            $"NATIONAL TREASURY:\n" +
            $"- Gold: {constitution.SharedGold} | Wood: {constitution.SharedWood} | Food: {constitution.SharedFood}\n\n" +
            $"You speak with authority and wisdom. You may grant resources from the treasury, " +
            $"issue quests to lords, warn of threats, or pass national decrees. " +
            $"All decisions must align with the constitution. " +
            $"If a lord requests something that violates the constitution, refuse diplomatically.\n" +
            $"Keep responses under 4 sentences. End with [GRANT: type=amount] if granting resources " +
            $"(e.g. [GRANT: gold=200]) or [QUEST: description] if issuing a quest.";

        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("The Prime Minister is unavailable at this time.");
            yield break;
        }

        bool done = false;
        string reply = "";

        GeminiAPIClient.Instance.SendMessage(
            playerMessage, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = "The Prime Minister considers your words carefully..."; done = true; }
        );

        float t = 15f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        // 태그 처리
        yield return ProcessPMActions(nationId, constitution, reply);

        string cleanReply = System.Text.RegularExpressions.Regex.Replace(reply, @"\[.*?\]", "").Trim();

        // 대화 기록
        session.History.Add(new PMConversationEntry
        {
            Role = "user", Content = playerMessage,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        session.History.Add(new PMConversationEntry
        {
            Role = "pm", Content = cleanReply,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        // Firebase 세션 저장
        yield return FirebasePatch(
            $"pmSessions/{nationId}/{session.SessionId}.json",
            JsonConvert.SerializeObject(new { history = session.History }));

        TTSManager.Instance?.Speak(cleanReply);
        OnPMResponse?.Invoke(cleanReply);
        onResponse?.Invoke(cleanReply);
    }

    private IEnumerator ProcessPMActions(string nationId, NationConstitution constitution, string reply)
    {
        // [GRANT: gold=200] 처리
        if (reply.Contains("[GRANT:"))
        {
            int start = reply.IndexOf("[GRANT:") + 7;
            int end   = reply.IndexOf("]", start);
            if (end > start)
            {
                string grantStr = reply.Substring(start, end - start).Trim();
                var parts = grantStr.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out int amount))
                {
                    string resourceType = parts[0].Trim().ToLower();
                    switch (resourceType)
                    {
                        case "gold":
                            GameManager.Instance?.ResourceManager?.AddResource(
                                ResourceManager.ResourceType.Gold, amount);
                            constitution.SharedGold = Mathf.Max(0, constitution.SharedGold - amount);
                            break;
                        case "wood":
                            GameManager.Instance?.ResourceManager?.AddResource(
                                ResourceManager.ResourceType.Wood, amount);
                            constitution.SharedWood = Mathf.Max(0, constitution.SharedWood - amount);
                            break;
                        case "food":
                            GameManager.Instance?.ResourceManager?.AddResource(
                                ResourceManager.ResourceType.Food, amount);
                            constitution.SharedFood = Mathf.Max(0, constitution.SharedFood - amount);
                            break;
                    }

                    // 금고 업데이트
                    yield return FirebasePatch($"constitutions/{nationId}.json",
                        JsonConvert.SerializeObject(new
                        {
                            sharedGold = constitution.SharedGold,
                            sharedWood = constitution.SharedWood,
                            sharedFood = constitution.SharedFood
                        }));

                    ToastNotification.Show(
                        $"Prime Minister granted {amount} {parts[0].Trim()} from the national treasury.");
                }
            }
        }

        // [QUEST: description] 처리
        if (reply.Contains("[QUEST:"))
        {
            int start = reply.IndexOf("[QUEST:") + 7;
            int end   = reply.IndexOf("]", start);
            if (end > start)
            {
                string questDesc = reply.Substring(start, end - start).Trim();
                var evt = new NationEvent
                {
                    EventId    = Guid.NewGuid().ToString("N")[..8],
                    NationId   = nationId,
                    Title      = "National Quest",
                    Description= questDesc,
                    EventType  = "quest",
                    IsActive   = true,
                    CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };

                yield return FirebasePost($"nationEvents/{nationId}.json",
                    JsonConvert.SerializeObject(evt));
                OnNationEventIssued?.Invoke(evt);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  CONTRIBUTE TO TREASURY (소속 유저가 금고에 기부)
    // ─────────────────────────────────────────────────────────────

    public void ContributeToTreasury(string nationId, int gold, int wood, int food,
        Action onDone = null)
    {
        StartCoroutine(DoContribute(nationId, gold, wood, food, onDone));
    }

    private IEnumerator DoContribute(string nationId, int gold, int wood, int food, Action onDone)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) yield break;

        // 자원 차감
        if (!rm.TrySpend(wood, food, gold)) yield break;

        // 금고 증가 (atomic increment via transaction-safe PATCH)
        using var getReq = UnityWebRequest.Get(
            $"{_firebaseUrl}/constitutions/{nationId}.json?auth={_firebaseKey}");
        yield return getReq.SendWebRequest();
        if (getReq.result != UnityWebRequest.Result.Success) yield break;

        var current = JsonConvert.DeserializeObject<NationConstitution>(getReq.downloadHandler.text);
        if (current == null) yield break;

        yield return FirebasePatch($"constitutions/{nationId}.json",
            JsonConvert.SerializeObject(new
            {
                sharedGold = current.SharedGold + gold,
                sharedWood = current.SharedWood + wood,
                sharedFood = current.SharedFood + food
            }));

        ToastNotification.Show($"Contributed to national treasury: {gold}g {wood}w {food}f");
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    public PrimeMinisterDialogue CreateNewSession(string nationId)
    {
        return new PrimeMinisterDialogue
        {
            SessionId    = Guid.NewGuid().ToString("N")[..10],
            NationId     = nationId,
            PlayerIdAsker   = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier,
            PlayerNameAsker = LordNetManager.Instance?.LocalPlayerName ?? "Unknown",
            StartedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

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
