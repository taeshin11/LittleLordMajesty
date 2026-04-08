using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 첩보전: 프롬프트 바이러스 (Prompt Injection Attack)
///
/// 스파이 NPC를 적 성에 잠입시켜 적의 NPC들을 사상적으로 오염.
/// 물리적 파괴 없이 상대 생산성/충성도를 무너뜨리는 고도의 심리전.
///
/// "일꾼으로 위장해서 밤마다 농부들에게 파업을 선동해라"
/// → 상대방 NPC들의 만족도 감소, 생산량 저하
/// → 상대방은 이상 행동을 보이는 NPC를 심문해서 스파이 색출해야 함
/// </summary>
public class SpyInfiltrationSystem : MonoBehaviour
{
    public static SpyInfiltrationSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class Spy
    {
        public string SpyId;
        public string SenderId;
        public string SenderName;
        public string TargetPlayerId;

        public string DisguiseAs;      // "Farmer", "Soldier", "Merchant" (위장 직업)
        public string InfiltrationOrder; // 영주의 비밀 지령 (Firebase에는 암호화해 저장)
        public string PublicPersona;   // 대외용 인물 설명 (심문 시 Gemini가 이 페르소나로 행동)

        public string Status;          // "active", "exposed", "captured", "returned"
        public int    DaysActive;
        public int    DamageDealt;     // 누적 피해 수치 (만족도 감소량)

        public long   InfiltratedAtUtc;
        public long   ExpiresAtUtc;
    }

    [Serializable]
    public class PromptVirusEffect
    {
        public string TargetPlayerId;
        public string SourceSpyId;
        public string AffectedNPCId;    // 영향받은 NPC
        public string ContaminatedIdea; // 심어진 생각 (공개적으로는 알 수 없음)
        public int    SatisfactionDelta;
        public int    ProductivityDelta;
        public bool   IsDetected;
        public long   AppliedAtUtc;
    }

    public event Action<Spy>              OnSpyDetected;  // 내 성에서 스파이 발견
    public event Action<PromptVirusEffect> OnNPCContaminated; // 적 NPC 오염 성공

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

        StartCoroutine(CheckForInfiltrations());
        StartCoroutine(TickActiveSpies());
    }

    // ─────────────────────────────────────────────────────────────
    //  SEND SPY
    // ─────────────────────────────────────────────────────────────

    /// <summary>스파이 파견</summary>
    public void SendSpy(
        string targetPlayerId,
        string disguiseAs,
        string infiltrationOrder,  // 비공개 지령
        string publicPersona,      // 들켰을 때의 위장 신분
        Action<Spy> onSent)
    {
        StartCoroutine(DoSendSpy(targetPlayerId, disguiseAs, infiltrationOrder, publicPersona, onSent));
    }

    private IEnumerator DoSendSpy(string targetId, string disguise, string order,
        string persona, Action<Spy> onSent)
    {
        var spy = new Spy
        {
            SpyId            = Guid.NewGuid().ToString("N")[..10],
            SenderId         = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier,
            SenderName       = LordNetManager.Instance?.LocalPlayerName ?? "Unknown",
            TargetPlayerId   = targetId,
            DisguiseAs       = disguise,
            InfiltrationOrder= SimpleObfuscate(order), // 약한 암호화 (서버측 보호 불가, 게임적 요소)
            PublicPersona    = persona,
            Status           = "active",
            DaysActive       = 0,
            InfiltratedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUtc     = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()
        };

        string json = JsonConvert.SerializeObject(spy);
        yield return FirebasePut($"spies/{targetId}/{spy.SpyId}.json", json);

        onSent?.Invoke(spy);
        Debug.Log($"[Spy] Agent dispatched to {targetId} as {disguise}");
    }

    // ─────────────────────────────────────────────────────────────
    //  ACTIVE SPY TICK (매일)
    // ─────────────────────────────────────────────────────────────

    private IEnumerator TickActiveSpies()
    {
        // GameManager.OnDayChanged에 연결하는 것이 이상적이나, 코루틴으로 대체
        while (true)
        {
            yield return new WaitForSeconds(300f); // 5분 = 1인게임 일 (실제 배포시 조정)
            yield return ApplyActiveSpyEffects();
        }
    }

    private IEnumerator ApplyActiveSpyEffects()
    {
        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string url = $"{_firebaseUrl}/spies/{localId}.json?auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;

        var spies = JsonConvert.DeserializeObject<Dictionary<string, Spy>>(req.downloadHandler.text);
        if (spies == null) yield break;

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        foreach (var spy in spies.Values)
        {
            if (spy.Status != "active") continue;
            if (spy.ExpiresAtUtc < now) continue;

            yield return ApplySpyEffect(spy);
        }
    }

    private IEnumerator ApplySpyEffect(Spy spy)
    {
        // Gemini에게 이 스파이의 활동 결과를 생성시킴
        if (GeminiAPIClient.Instance == null) yield break;

        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null || npcs.Count == 0) yield break;

        // 랜덤 NPC 선택
        var target = npcs[UnityEngine.Random.Range(0, npcs.Count)];
        string decodedOrder = SimpleDeobfuscate(spy.InfiltrationOrder);

        string prompt =
            $"A spy disguised as a {spy.DisguiseAs} has been spreading sedition in the castle. " +
            $"Their secret order: \"{decodedOrder}\"\n" +
            $"Target NPC: {target.Name} ({target.Profession}), loyalty={target.LoyaltyToLord}\n\n" +
            $"Rate the effect on this NPC:\n" +
            $"{{\"satisfactionDelta\": int (-20 to 0), \"loyaltyDelta\": int (-15 to 0), " +
            $"\"detectionChance\": float (0.0-0.4), \"whisperText\": string}}";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(prompt, "", null,
            r => { reply = r; done = true; }, _ => { done = true; });

        float t = 10f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        yield return ProcessSpyEffectResult(spy, target, reply);
    }

    private IEnumerator ProcessSpyEffectResult(Spy spy, NPCManager.NPCData targetNPC, string llmReply)
    {
        try
        {
            var effect = JsonConvert.DeserializeObject<SpyEffectResult>(llmReply);
            if (effect == null) yield break;

            // NPC 상태 변경
            targetNPC.MoodScore     = Mathf.Clamp(targetNPC.MoodScore + effect.satisfactionDelta, 0, 100);
            targetNPC.LoyaltyToLord = Mathf.Clamp(targetNPC.LoyaltyToLord + effect.loyaltyDelta, 0, 100);

            // 발견 판정
            bool detected = UnityEngine.Random.value < effect.detectionChance;
            if (detected)
            {
                spy.Status = "exposed";
                yield return FirebasePatch($"spies/{LordNetManager.Instance?.LocalPlayerId}/{spy.SpyId}.json",
                    JsonConvert.SerializeObject(new { status = "exposed" }));

                OnSpyDetected?.Invoke(spy);
                GameManager.Instance?.EventManager?.TriggerManualEvent(
                    "Spy Detected!",
                    $"A suspicious {spy.DisguiseAs} has been caught spreading rumors. " +
                    $"'{effect.whisperText}'\nInterrogate or execute the agent?",
                    EventManager.EventSeverity.Moderate);
            }
            else
            {
                // 생산성 피해 기록
                var virusEffect = new PromptVirusEffect
                {
                    TargetPlayerId   = LordNetManager.Instance?.LocalPlayerId,
                    SourceSpyId      = spy.SpyId,
                    AffectedNPCId    = targetNPC.Id,
                    ContaminatedIdea = effect.whisperText,
                    SatisfactionDelta= effect.satisfactionDelta,
                    ProductivityDelta= effect.satisfactionDelta / 2,
                    IsDetected       = false,
                    AppliedAtUtc     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                };
                OnNPCContaminated?.Invoke(virusEffect);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Spy] Effect parse failed: {e.Message}");
        }
    }

    [Serializable] private class SpyEffectResult
    {
        public int    satisfactionDelta;
        public int    loyaltyDelta;
        public float  detectionChance;
        public string whisperText;
    }

    // ─────────────────────────────────────────────────────────────
    //  INTERROGATE SUSPECTED SPY
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 의심 NPC 심문. Gemini가 위장 신분을 유지하거나 자백.
    /// </summary>
    public void InterrogateSuspect(Spy spy, string interrogationMessage, Action<string, bool> onResult)
    {
        StartCoroutine(DoInterrogate(spy, interrogationMessage, onResult));
    }

    private IEnumerator DoInterrogate(Spy spy, string message, Action<string, bool> onResult)
    {
        if (GeminiAPIClient.Instance == null) { onResult?.Invoke("...", false); yield break; }

        string systemPrompt =
            $"You are {spy.PublicPersona} — a {spy.DisguiseAs} in this castle. " +
            $"You are actually a spy. Your cover: {spy.PublicPersona}\n\n" +
            $"Under this interrogation, you must decide: maintain cover or crack? " +
            $"If the accusation is very specific and backed by evidence in the message, " +
            $"you may crack (20% base chance, higher if message is precise).\n" +
            $"Respond in character. End with [CRACKED] if you confess, [HOLDING] if you maintain cover.";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(message, systemPrompt, null,
            r => { reply = r; done = true; }, _ => { reply = "I don't know what you mean, my lord."; done = true; });

        float t = 12f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        bool confessed = reply.Contains("[CRACKED]");
        string cleanReply = reply.Replace("[CRACKED]", "").Replace("[HOLDING]", "").Trim();

        if (confessed)
        {
            spy.Status = "captured";
            yield return FirebasePatch($"spies/{LordNetManager.Instance?.LocalPlayerId}/{spy.SpyId}.json",
                JsonConvert.SerializeObject(new { status = "captured" }));
        }

        TTSManager.Instance?.Speak(cleanReply);
        onResult?.Invoke(cleanReply, confessed);
    }

    // ─────────────────────────────────────────────────────────────
    //  CHECK INCOMING INFILTRATIONS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator CheckForInfiltrations()
    {
        while (true)
        {
            yield return new WaitForSeconds(60f);
            string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
            string url = $"{_firebaseUrl}/spies/{localId}.json?orderBy=\"status\"&equalTo=\"active\"&auth={_firebaseKey}";
            using var req = UnityWebRequest.Get(url);
            yield return req.SendWebRequest();
            // Handled in ApplyActiveSpyEffects
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private string SimpleObfuscate(string text) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private string SimpleDeobfuscate(string encoded)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(encoded)); }
        catch { return encoded; }
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
}
