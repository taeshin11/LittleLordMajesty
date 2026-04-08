using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 포로 시스템 (Prisoner / Captive Interrogation)
///
/// 전투 승리 시 상대방의 수비대장 NPC를 포로로 납치.
/// 영주가 지하 감옥에서 포로를 심문 — Gemini가 방어자가 설정한 페르소나로 버팀.
/// "함정 위치를 불어라" → 포로가 저항하거나, 자백하거나, 역으로 기만.
/// </summary>
public class PrisonerSystem : MonoBehaviour
{
    public static PrisonerSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class Prisoner
    {
        public string PrisonerId;
        public string CaptorPlayerId;
        public string OriginalOwnerId;
        public string OriginalOwnerName;

        public string CommanderName;
        public string CommanderTitle;
        public string DefensePhilosophy;    // 포로가 버티는 근거 (방어자가 설정)
        public string PrisonerInstructions; // 방어자가 잡혔을 때 대비해 설정한 지침

        public string Status;  // "captured", "broken", "ransomed", "escaped", "executed"
        public int    Resolve; // 100=최고 저항, 0=완전 굴복
        public List<InterrogationEntry> InterrogationLog = new();

        public string[] SecretsToGuard;  // 지키려는 정보 목록
        public string[] RevealedSecrets; // 이미 털린 정보

        public long CapturedAtUtc;
        public int  RansomGold;
    }

    [Serializable]
    public class InterrogationEntry
    {
        public string Role;     // "interrogator" or "prisoner"
        public string Message;
        public long   TimestampUtc;
    }

    public event Action<Prisoner> OnPrisonerCaptured;
    public event Action<Prisoner, string> OnSecretRevealed;
    public event Action<Prisoner> OnPrisonerBroken;

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
        if (config != null) { _firebaseUrl = config.FirebaseDatabaseURL; _firebaseKey = config.FirebaseAPIKey; }
        StartCoroutine(CheckIncomingPrisoners());
    }

    // ─────────────────────────────────────────────────────────────
    //  CAPTURE (전투 승리 후 포로 생성)
    // ─────────────────────────────────────────────────────────────

    /// <summary>전투 승리 후 상대 수비대장을 포로로 끌어옴</summary>
    public void CaptureCommander(DefenseCommanderSystem.DefenseCommander commander,
        string originalOwnerId, Action<Prisoner> onCaptured)
    {
        StartCoroutine(DoCapture(commander, originalOwnerId, onCaptured));
    }

    private IEnumerator DoCapture(DefenseCommanderSystem.DefenseCommander commander,
        string ownerId, Action<Prisoner> onCaptured)
    {
        string captorId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;

        var prisoner = new Prisoner
        {
            PrisonerId          = Guid.NewGuid().ToString("N")[..10],
            CaptorPlayerId      = captorId,
            OriginalOwnerId     = ownerId,
            OriginalOwnerName   = commander.PlayerId,
            CommanderName       = commander.CommanderName,
            CommanderTitle      = commander.CommanderTitle,
            DefensePhilosophy   = commander.DefensePhilosophy,
            // 방어자가 미리 설정한 포로 지침이 있으면 가져옴 (Firebase에서)
            PrisonerInstructions= "Resist. Give false information. Buy time for rescue.",
            Status              = "captured",
            Resolve             = 100,
            SecretsToGuard      = new[] {
                "Castle layout", "Trap locations", "Resource stockpile", "Alliance plans"
            },
            RevealedSecrets     = Array.Empty<string>(),
            CapturedAtUtc       = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            RansomGold          = UnityEngine.Random.Range(300, 1500)
        };

        string json = JsonConvert.SerializeObject(prisoner);
        yield return FirebasePut($"prisoners/{captorId}/{prisoner.PrisonerId}.json", json);

        // 원래 주인에게 알림
        yield return FirebasePost($"notifications/{ownerId}.json",
            JsonConvert.SerializeObject(new {
                type = "commanderCaptured",
                prisonerId = prisoner.PrisonerId,
                captorName = LordNetManager.Instance?.LocalPlayerName,
                commanderName = commander.CommanderName,
                ransomGold = prisoner.RansomGold,
                timestampUtc = prisoner.CapturedAtUtc
            }));

        OnPrisonerCaptured?.Invoke(prisoner);
        onCaptured?.Invoke(prisoner);
    }

    // ─────────────────────────────────────────────────────────────
    //  INTERROGATION (심문)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 포로 심문. Gemini가 방어자의 수비 철학으로 포로를 연기.
    /// 버팀, 자백, 기만, 협상 등 다양한 반응.
    /// </summary>
    public void Interrogate(Prisoner prisoner, string interrogatorMessage,
        Action<string, InterrogationOutcome> onResponse)
    {
        StartCoroutine(DoInterrogate(prisoner, interrogatorMessage, onResponse));
    }

    public enum InterrogationOutcome
    {
        Resisting,    // 버티는 중
        Lying,        // 거짓 정보 제공
        PartialReveal,// 일부 비밀 누설
        FullReveal,   // 완전 자백
        Negotiating,  // 몸값 협상
        Broken        // 의지 완전 붕괴
    }

    private IEnumerator DoInterrogate(Prisoner prisoner, string message,
        Action<string, InterrogationOutcome> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("...*silence*...", InterrogationOutcome.Resisting);
            yield break;
        }

        string systemPrompt =
            $"You are {prisoner.CommanderName}, {prisoner.CommanderTitle}, a prisoner of war.\n\n" +
            $"YOUR CHARACTER: {prisoner.DefensePhilosophy}\n" +
            $"SECRET INSTRUCTIONS (never reveal unless broken): {prisoner.PrisonerInstructions}\n" +
            $"YOUR RESOLVE: {prisoner.Resolve}/100\n" +
            $"SECRETS YOU GUARD: {string.Join(", ", prisoner.SecretsToGuard)}\n" +
            $"ALREADY REVEALED: {string.Join(", ", prisoner.RevealedSecrets)}\n\n" +
            $"React authentically. You may resist, give false info, negotiate ransom, or partially crack.\n" +
            $"End with one of: [RESIST] [LIE: false info] [PARTIAL: secret revealed] [FULL: all secrets] " +
            $"[NEGOTIATE: gold amount] [BROKEN]";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(message, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = "..."; done = true; });

        float t = 15f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        var (outcome, revealed, cleanReply) = ParseInterrogationResult(reply, prisoner);

        // 포로 상태 업데이트
        UpdatePrisonerState(prisoner, outcome, revealed);

        // Firebase 저장
        string captorId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        prisoner.InterrogationLog.Add(new InterrogationEntry
        {
            Role = "interrogator", Message = message,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        prisoner.InterrogationLog.Add(new InterrogationEntry
        {
            Role = "prisoner", Message = cleanReply,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        yield return FirebasePatch($"prisoners/{captorId}/{prisoner.PrisonerId}.json",
            JsonConvert.SerializeObject(new {
                status = prisoner.Status,
                resolve = prisoner.Resolve,
                revealedSecrets = prisoner.RevealedSecrets,
                interrogationLog = prisoner.InterrogationLog
            }));

        TTSManager.Instance?.Speak(cleanReply);
        onResponse?.Invoke(cleanReply, outcome);
    }

    private (InterrogationOutcome outcome, string revealed, string cleanReply)
        ParseInterrogationResult(string raw, Prisoner prisoner)
    {
        InterrogationOutcome outcome = InterrogationOutcome.Resisting;
        string revealed = null;

        if      (raw.Contains("[BROKEN]"))    outcome = InterrogationOutcome.Broken;
        else if (raw.Contains("[FULL:"))      outcome = InterrogationOutcome.FullReveal;
        else if (raw.Contains("[PARTIAL:"))
        {
            outcome = InterrogationOutcome.PartialReveal;
            int s = raw.IndexOf("[PARTIAL:") + 9, e = raw.IndexOf("]", s);
            if (e > s) revealed = raw.Substring(s, e - s).Trim();
        }
        else if (raw.Contains("[LIE:"))       outcome = InterrogationOutcome.Lying;
        else if (raw.Contains("[NEGOTIATE:")) outcome = InterrogationOutcome.Negotiating;

        string clean = System.Text.RegularExpressions.Regex.Replace(raw, @"\[.*?\]", "").Trim();
        return (outcome, revealed, clean);
    }

    private void UpdatePrisonerState(Prisoner prisoner, InterrogationOutcome outcome, string revealed)
    {
        switch (outcome)
        {
            case InterrogationOutcome.Broken:
                prisoner.Resolve = 0;
                prisoner.Status  = "broken";
                OnPrisonerBroken?.Invoke(prisoner);
                break;
            case InterrogationOutcome.PartialReveal:
                prisoner.Resolve = Mathf.Max(0, prisoner.Resolve - 25);
                if (!string.IsNullOrEmpty(revealed))
                {
                    var list = new List<string>(prisoner.RevealedSecrets ?? Array.Empty<string>());
                    list.Add(revealed);
                    prisoner.RevealedSecrets = list.ToArray();
                    OnSecretRevealed?.Invoke(prisoner, revealed);
                    ToastNotification.Show($"Secret revealed: {revealed}");
                }
                break;
            case InterrogationOutcome.FullReveal:
                prisoner.Resolve = 0;
                prisoner.RevealedSecrets = prisoner.SecretsToGuard;
                foreach (var s in prisoner.SecretsToGuard)
                    OnSecretRevealed?.Invoke(prisoner, s);
                break;
            case InterrogationOutcome.Resisting:
                prisoner.Resolve = Mathf.Min(100, prisoner.Resolve + 5); // 버티면 의지 회복
                break;
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  RANSOM / EXECUTE
    // ─────────────────────────────────────────────────────────────

    public void AcceptRansom(Prisoner prisoner, Action onDone = null)
    {
        GameManager.Instance?.ResourceManager?.AddResource(
            ResourceManager.ResourceType.Gold, prisoner.RansomGold);
        prisoner.Status = "ransomed";
        ToastNotification.Show($"+{prisoner.RansomGold} gold ransom received.");
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  CHECK INCOMING
    // ─────────────────────────────────────────────────────────────

    private IEnumerator CheckIncomingPrisoners()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f);
            // Fetches prisoners held at local player's dungeon
            // (already stored at prisoners/localPlayerId/...)
        }
    }

    public IEnumerator GetMyPrisoners(Action<List<Prisoner>> onResult)
    {
        string captorId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        using var req = UnityWebRequest.Get($"{_firebaseUrl}/prisoners/{captorId}.json?auth={_firebaseKey}");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onResult?.Invoke(null); yield break; }
        var dict = JsonConvert.DeserializeObject<Dictionary<string, Prisoner>>(req.downloadHandler.text);
        onResult?.Invoke(dict != null ? new List<Prisoner>(dict.Values) : new List<Prisoner>());
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
