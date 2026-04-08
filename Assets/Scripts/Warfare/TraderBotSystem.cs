using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// 암시장 AI 상인 시스템 (Trader Bot — Dynamic Negotiation Market)
///
/// 영주가 상인 NPC에게 거래 가이드라인을 주고 월드맵 파견.
/// 다른 플레이어 성에 도착 → 상대 유저와 AI 상인이 직접 흥정.
/// 영주는 잠든 동안에도 상인이 입담으로 장사하고 귀환.
/// </summary>
public class TraderBotSystem : MonoBehaviour
{
    public static TraderBotSystem Instance { get; private set; }

    private string _firebaseUrl;
    private string _firebaseKey;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class TradeOffer
    {
        public string OfferId;
        public string SellerPlayerId;
        public string SellerName;
        public string MerchantName;

        // 판매 물품
        public string GoodsType;     // "wood", "food", "iron", "bread", "cloth", "weapons" 등
        public int    GoodsAmount;

        // 가격 가이드라인 (영주가 설정)
        public int    MinGold;         // 최소 수락 금액
        public int    AskingGold;      // 희망 금액
        public string BarterAccepted;  // 물물교환 수락 품목 (쉼표 구분)
        public int    BarterMinValue;

        public string NegotiationStyle; // 상인 성격 (예: "friendly", "aggressive", "sly")
        public string TraderInstructions; // 영주의 비공개 거래 지침

        // 상태
        public string Status;          // "traveling", "negotiating", "sold", "returned", "failed"
        public string FinalGoodsReceived;
        public int    FinalGoldReceived;
        public List<TradeConversationEntry> NegotiationLog = new();

        public long   CreatedAtUtc;
        public long   ExpiresAtUtc;
    }

    [Serializable]
    public class TradeConversationEntry
    {
        public string Speaker;   // "merchant" or "buyer"
        public string Message;
        public long   TimestampUtc;
    }

    public event Action<TradeOffer>  OnTraderArrived;    // 내 성에 상인 도착
    public event Action<TradeOffer>  OnTradeDone;        // 거래 완료

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
        StartCoroutine(CheckArrivingTraders());
    }

    // ─────────────────────────────────────────────────────────────
    //  DISPATCH TRADER
    // ─────────────────────────────────────────────────────────────

    /// <summary>상인 파견. 타겟 플레이어 성으로 보냄.</summary>
    public void DispatchTrader(
        string targetPlayerId,
        string goodsType, int goodsAmount,
        int minGold, int askingGold,
        string barterAccepted,
        string merchantName,
        string negotiationStyle,
        string traderInstructions,
        Action<TradeOffer> onDispatched)
    {
        StartCoroutine(DoDispatchTrader(targetPlayerId, goodsType, goodsAmount,
            minGold, askingGold, barterAccepted, merchantName,
            negotiationStyle, traderInstructions, onDispatched));
    }

    private IEnumerator DoDispatchTrader(
        string targetId, string goodsType, int amount,
        int minGold, int asking, string barter,
        string merchantName, string style, string instructions,
        Action<TradeOffer> onDispatched)
    {
        string localId   = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        string localName = LordNetManager.Instance?.LocalPlayerName ?? "Unknown";

        var offer = new TradeOffer
        {
            OfferId           = Guid.NewGuid().ToString("N")[..10],
            SellerPlayerId    = localId,
            SellerName        = localName,
            MerchantName      = merchantName,
            GoodsType         = goodsType,
            GoodsAmount       = amount,
            MinGold           = minGold,
            AskingGold        = asking,
            BarterAccepted    = barter,
            NegotiationStyle  = style,
            TraderInstructions= instructions,
            Status            = "traveling",
            CreatedAtUtc      = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpiresAtUtc      = DateTimeOffset.UtcNow.AddDays(2).ToUnixTimeSeconds()
        };

        string json = JsonConvert.SerializeObject(offer);
        yield return FirebasePut($"traders/{targetId}/{offer.OfferId}.json", json);

        onDispatched?.Invoke(offer);
        ToastNotification.Instance?.Show($"{merchantName} dispatched to trade {amount}x {goodsType}.");
    }

    // ─────────────────────────────────────────────────────────────
    //  BUYER SIDE — NEGOTIATE WITH ARRIVING TRADER
    // ─────────────────────────────────────────────────────────────

    public void NegotiateWithTrader(TradeOffer offer, string buyerMessage,
        Action<string, TradeOutcome> onResponse)
    {
        StartCoroutine(DoNegotiate(offer, buyerMessage, onResponse));
    }

    public enum TradeOutcome
    {
        Ongoing,    // 협상 진행 중
        Agreed,     // 거래 성사
        Rejected,   // 상인이 거절
        BuyerLeft   // 구매자가 거절
    }

    private IEnumerator DoNegotiate(TradeOffer offer, string buyerMessage,
        Action<string, TradeOutcome> onResponse)
    {
        if (GeminiAPIClient.Instance == null)
        {
            onResponse?.Invoke("The merchant strokes their beard thoughtfully...", TradeOutcome.Ongoing);
            yield break;
        }

        string systemPrompt =
            $"You are {offer.MerchantName}, a {offer.NegotiationStyle} traveling merchant.\n\n" +
            $"SECRET TRADING INSTRUCTIONS: {offer.TraderInstructions}\n\n" +
            $"FOR SALE: {offer.GoodsAmount}x {offer.GoodsType}\n" +
            $"Asking price: {offer.AskingGold} gold | Minimum: {offer.MinGold} gold\n" +
            $"You'll also accept barter: {offer.BarterAccepted}\n\n" +
            $"Negotiate in character. Be persuasive and true to your personality.\n" +
            $"End with: [DEAL: gold=X] if accepting, [DEAL: barter=goods] if bartering, " +
            $"[REJECT] if refusing, [COUNTER: amount] if countering.";

        bool done = false; string reply = "";
        GeminiAPIClient.Instance.SendMessage(buyerMessage, systemPrompt, null,
            r => { reply = r; done = true; },
            _ => { reply = "The merchant considers your offer silently."; done = true; });

        float t = 12f;
        while (!done && t > 0) { t -= Time.deltaTime; yield return null; }

        var (outcome, goldAmount, barterGoods, cleanReply) = ParseTradeResult(reply, offer);

        if (outcome == TradeOutcome.Agreed)
        {
            yield return ExecuteTrade(offer, goldAmount, barterGoods);
        }

        offer.NegotiationLog.Add(new TradeConversationEntry
        {
            Speaker = "buyer", Message = buyerMessage,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });
        offer.NegotiationLog.Add(new TradeConversationEntry
        {
            Speaker = "merchant", Message = cleanReply,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        });

        string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
        yield return FirebasePatch($"traders/{localId}/{offer.OfferId}.json",
            JsonConvert.SerializeObject(new {
                status = outcome == TradeOutcome.Agreed ? "sold" : "negotiating",
                negotiationLog = offer.NegotiationLog,
                finalGoldReceived = goldAmount
            }));

        TTSManager.Instance?.Speak(cleanReply);
        onResponse?.Invoke(cleanReply, outcome);
    }

    private (TradeOutcome, int, string, string) ParseTradeResult(string raw, TradeOffer offer)
    {
        TradeOutcome outcome = TradeOutcome.Ongoing;
        int gold = 0;
        string barter = null;

        if (raw.Contains("[DEAL: gold="))
        {
            int s = raw.IndexOf("[DEAL: gold=") + 12;
            int e = raw.IndexOf("]", s);
            if (e > s) int.TryParse(raw.Substring(s, e - s).Trim(), out gold);
            outcome = gold >= offer.MinGold ? TradeOutcome.Agreed : TradeOutcome.Rejected;
        }
        else if (raw.Contains("[DEAL: barter="))
        {
            int s = raw.IndexOf("[DEAL: barter=") + 14;
            int e = raw.IndexOf("]", s);
            if (e > s) barter = raw.Substring(s, e - s).Trim();
            outcome = TradeOutcome.Agreed;
        }
        else if (raw.Contains("[REJECT]")) outcome = TradeOutcome.Rejected;

        string clean = System.Text.RegularExpressions.Regex.Replace(raw, @"\[.*?\]", "").Trim();
        return (outcome, gold, barter, clean);
    }

    private IEnumerator ExecuteTrade(TradeOffer offer, int goldPaid, string barterGoods)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) yield break;

        if (goldPaid > 0 && !rm.TrySpend(0, 0, goldPaid)) yield break;

        // 물품 수령
        switch (offer.GoodsType)
        {
            case "wood":    rm.AddResource(ResourceManager.ResourceType.Wood, offer.GoodsAmount); break;
            case "food":    rm.AddResource(ResourceManager.ResourceType.Food, offer.GoodsAmount); break;
            default:
                ProductionChainManager.Instance?.FulfillNeed_External(offer.GoodsType, offer.GoodsAmount);
                break;
        }

        offer.Status = "sold";
        offer.FinalGoldReceived = goldPaid;
        OnTradeDone?.Invoke(offer);
        ToastNotification.Instance?.Show($"Trade complete! Received {offer.GoodsAmount}x {offer.GoodsType}.");

        // 판매자에게 금화 전달 알림
        yield return FirebasePost($"notifications/{offer.SellerPlayerId}.json",
            JsonConvert.SerializeObject(new {
                type = "tradeDone",
                offerId = offer.OfferId,
                merchantName = offer.MerchantName,
                goldEarned = goldPaid,
                buyerName = LordNetManager.Instance?.LocalPlayerName,
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }));
    }

    // ─────────────────────────────────────────────────────────────
    //  CHECK ARRIVING TRADERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator CheckArrivingTraders()
    {
        while (true)
        {
            yield return new WaitForSeconds(40f);
            string localId = LordNetManager.Instance?.LocalPlayerId ?? SystemInfo.deviceUniqueIdentifier;
            using var req = UnityWebRequest.Get($"{_firebaseUrl}/traders/{localId}.json?auth={_firebaseKey}");
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) continue;

            var traders = JsonConvert.DeserializeObject<Dictionary<string, TradeOffer>>(req.downloadHandler.text);
            if (traders == null) continue;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var t in traders.Values)
            {
                if ((t.Status == "traveling" || t.Status == "negotiating")
                    && t.ExpiresAtUtc > now && t.SellerPlayerId != localId)
                {
                    t.Status = "negotiating";
                    OnTraderArrived?.Invoke(t);
                }
            }
        }
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

/// <summary>Extension helper for ProductionChainManager to accept external goods</summary>
public static class ProductionChainExtensions
{
    public static void FulfillNeed_External(this ProductionChainManager pcm, string goodsType, int amount)
    {
        if (pcm == null) return;
        // External goods treated as stored processed goods
        pcm.GetGoodsStock(goodsType); // triggers goods tracking
        Debug.Log($"[Trade] Received {amount}x {goodsType} via trade.");
    }
}
