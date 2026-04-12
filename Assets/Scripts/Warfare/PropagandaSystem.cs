using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 정보전 시스템 — 가짜 뉴스 & 음유시인 (Fake News / Bard Propaganda)
///
/// 음유시인에게 돈을 주고 적의 평판을 날조.
/// 월드맵 플레이어들을 랜덤 순회하며 가짜 뉴스 전파.
/// 소문을 믿은 AI 영주들이 외교 관계 파기.
/// 유저는 해명 공문으로 사태 수습.
/// </summary>
public class PropagandaSystem : MonoBehaviour
{
    public static PropagandaSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    [Serializable]
    public class RumorCampaign
    {
        public string CampaignId;
        public string InstigatorId;
        public string InstigatorName;
        public string TargetPlayerId;

        public string BardInstruction;   // 영주의 지령
        public string GeneratedRumor;    // Gemini가 생성한 가짜 뉴스 텍스트
        public string BardName;          // 음유시인 이름 (랜덤 생성)

        public List<string> SpreadTo = new(); // 퍼진 플레이어 ID 목록
        public int    CredibilityScore;  // 0-100 (높을수록 믿는 플레이어 많음)
        public bool   Debunked;          // 해명됐는지
        public string DebunkMessage;

        public long   StartedAtUtc;
        public long   ExpiresAtUtc;      // 3일 후 소문 수명 만료
    }

    public event Action<RumorCampaign> OnRumorReceived;    // 내 성에 소문 도착
    public event Action<string>        OnRumorDebunked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        var config = Resources.Load<GameConfig>("Config/GameConfig");
        if (config != null) { _firebaseUrl = config.FirebaseDatabaseURL; _firebaseKey = config.FirebaseAPIKey; }
        StartCoroutine(CheckIncomingRumors());
    }

    // ─────────────────────────────────────────────────────────────
    //  LAUNCH PROPAGANDA CAMPAIGN
    // ─────────────────────────────────────────────────────────────

    public void HireBardsForPropaganda(
        string targetPlayerId,
        string bardInstruction,   // "말해줘: 저 왕이 오크와 내통하고 있다고"
        int goldCost,
        Action<RumorCampaign> onLaunched)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm?.Gold < goldCost) {
            ToastNotification.Show("Not enough gold to hire bards.");
            return;
        }
        rm?.TrySpend(0, 0, goldCost);
        StartCoroutine(DoLaunchCampaign(targetPlayerId, bardInstruction, onLaunched));
    }

    private IEnumerator DoLaunchCampaign(string targetId, string instruction, Action<RumorCampaign> onLaunched)
    {
        string localId   = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string localName = LordNetManager.Instance?.LocalPlayerName ?? "Unknown Lord";

        // Gemini가 그럴듯한 가짜 뉴스 작성
        string rumorText = "";
        if (GeminiAPIClient.Instance != null)
        {
            bool done = false;
            GeminiAPIClient.Instance.SendMessage(
                $"Create a believable but false medieval rumor based on this instruction: \"{instruction}\"\n" +
                $"Make it sound like a real bard's song verse or gossip. 2-3 sentences max.",
                "You are a master propagandist writing fake medieval gossip.",
                null,
                r => { rumorText = r; done = true; },
                _ => { rumorText = instruction; done = true; });
            float t = 10f;
            while (!done && t > 0) { t -= Time.deltaTime; yield return null; }
        }
        else rumorText = instruction;

        var campaign = new RumorCampaign
        {
            CampaignId      = Guid.NewGuid().ToString("N")[..10],
            InstigatorId    = localId,
            InstigatorName  = localName,
            TargetPlayerId  = targetId,
            BardInstruction = instruction,
            GeneratedRumor  = rumorText,
            BardName        = GenerateBardName(),
            CredibilityScore= UnityEngine.Random.Range(40, 90),
            StartedAtUtc    = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUtc    = DateTimeOffset.UtcNow.AddDays(3).ToUnixTimeSeconds()
        };

        // Firebase에 저장 (타겟 포함 다른 플레이어들에게도 전파)
        yield return FirebasePut($"rumors/{targetId}/{campaign.CampaignId}.json",
            JsonConvert.SerializeObject(campaign));

        // 월드맵의 다른 플레이어들에게도 전파 (랜덤 2~5명)
        var worldPlayers = LordNetManager.Instance?.GetAllWorldPlayers();
        if (worldPlayers != null)
        {
            var shuffled = worldPlayers.FindAll(p =>
                p.PlayerId != localId && p.PlayerId != targetId);
            shuffled.Sort((a, b) => UnityEngine.Random.Range(-1, 2));
            int spreadCount = Mathf.Min(UnityEngine.Random.Range(2, 6), shuffled.Count);

            for (int i = 0; i < spreadCount; i++)
            {
                campaign.SpreadTo.Add(shuffled[i].PlayerId);
                yield return FirebasePut($"rumors/{shuffled[i].PlayerId}/{campaign.CampaignId}.json",
                    JsonConvert.SerializeObject(campaign));
            }
        }

        onLaunched?.Invoke(campaign);
        Debug.Log($"[Propaganda] Campaign launched. Rumor: {rumorText}");
    }

    // ─────────────────────────────────────────────────────────────
    //  DEBUNK (해명)
    // ─────────────────────────────────────────────────────────────

    public void IssueDebunkStatement(string campaignId, string debunkMessage, Action onDone = null)
    {
        StartCoroutine(DoDebunk(campaignId, debunkMessage, onDone));
    }

    private IEnumerator DoDebunk(string campaignId, string message, Action onDone)
    {
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        yield return FirebasePatch($"rumors/{localId}/{campaignId}.json",
            JsonConvert.SerializeObject(new { debunked = true, debunkMessage = message }));

        // 같은 소문을 받은 다른 플레이어들에게도 해명 전파
        yield return FirebasePost($"debunkBroadcasts/{campaignId}.json",
            JsonConvert.SerializeObject(new {
                campaignId = campaignId,
                debunkerId = localId,
                debunkerName = LordNetManager.Instance?.LocalPlayerName,
                message = message,
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }));

        OnRumorDebunked?.Invoke(campaignId);
        ToastNotification.Show("Debunk statement issued across the realm.");
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  RECEIVE RUMORS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator CheckIncomingRumors()
    {
        while (true)
        {
            yield return new WaitForSeconds(45f);
            string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
            using var req = UnityWebRequest.Get($"{_firebaseUrl}/rumors/{localId}.json?auth={_firebaseKey}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) continue;

            var campaigns = JsonConvert.DeserializeObject<Dictionary<string, RumorCampaign>>(req.downloadHandler.text);
            if (campaigns == null) continue;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var c in campaigns.Values)
            {
                if (c.ExpiresAtUtc > now && !c.Debunked && c.InstigatorId != localId)
                    OnRumorReceived?.Invoke(c);
            }
        }
    }

    private string GenerateBardName()
    {
        string[] prefixes = { "Wandering", "Silver-Tongued", "Merry", "Crooked", "Whispering" };
        string[] names    = { "Finn", "Mira", "Oswald", "Cara", "Jofrey", "Sylvana", "Toben" };
        return $"{prefixes[UnityEngine.Random.Range(0, prefixes.Length)]} {names[UnityEngine.Random.Range(0, names.Length)]}";
    }

    private IEnumerator FirebasePut(string path, string json)
    {
        string url = $"{_firebaseUrl}/{path}?auth={_firebaseKey}";
        using var req = new UnityWebRequest(url, "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
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
