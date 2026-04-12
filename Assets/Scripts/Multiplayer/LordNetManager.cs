using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// LordNet — Async multiplayer world manager.
/// All players share one persistent world map on Firebase.
///
/// Key concept: When you attack an offline player, Gemini AI defends
/// using that player's playstyle data. Players and AI lords are
/// indistinguishable from the outside — that ambiguity IS the strategy.
/// </summary>
public class LordNetManager : MonoBehaviour
{
    public static LordNetManager Instance { get; private set; }

    [Header("Config (from GameConfig)")]
    private string _firebaseUrl;
    private string _firebaseKey;

    // Local player identity
    public string LocalPlayerId   { get; private set; }
    public string LocalPlayerName { get; private set; }
    public string LocalNationId   { get; private set; }

    // Cached world data
    private readonly Dictionary<string, WorldPlayer> _worldPlayers = new();
    private readonly Dictionary<string, NationData>  _nations      = new();

    public event Action<List<WorldPlayer>>         OnWorldPlayersUpdated;
    public event Action<BattleResult>              OnBattleResultReceived;
    public event Action<DiplomaticMessage>         OnDiplomaticMessageReceived;
    public event Action<NationData>                OnNationUpdated;
    public event Action<string>                    OnChatMessageReceived;

    // ─────────────────────────────────────────────────────────────
    //  DATA MODELS
    // ─────────────────────────────────────────────────────────────

    [Serializable]
    public class WorldPlayer
    {
        public string PlayerId;
        public string PlayerName;
        public string LordTitle;
        public string NationId;
        public int    TerritoryX;    // World map grid position
        public int    TerritoryZ;
        public int    TerritoryCount;
        public int    DefenseRating;
        public long   LastActiveUtc;
        public bool   IsOnline => (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - LastActiveUtc) < 300;
        public string PersonaStyle;  // Gemini uses this to impersonate for defense
    }

    [Serializable]
    public class NationData
    {
        public string NationId;
        public string Name;
        public string Tag;           // 3-letter tag e.g. "KNG"
        public string LeaderId;
        public List<string> MemberIds = new();
        public Dictionary<string, string> Diplomacy = new(); // nationId → "allied"/"war"/"peace"
        public long   FoundedUtc;
        public int    TotalTerritories;
    }

    [Serializable]
    public class BattleRecord
    {
        public string BattleId;
        public string AttackerId;
        public string DefenderId;
        public long   TimestampUtc;
        public bool   AttackerWon;
        public int    TerritoryGained;
        public string AiDefenseNarrative; // Gemini-generated defense story
        public string AttackerCommand;    // What the attacker ordered
    }

    [Serializable]
    public class BattleResult
    {
        public BattleRecord Record;
        public bool         IsLocalPlayerAttacker;
        public bool         IsLocalPlayerDefender;
    }

    [Serializable]
    public class DiplomaticMessage
    {
        public string MessageId;
        public string FromPlayerId;
        public string FromPlayerName;
        public string ToPlayerId;
        public string Content;
        public string Proposal;      // "alliance", "war", "tribute", "trade"
        public long   TimestampUtc;
        public bool   Responded;
    }

    [Serializable]
    public class CastleSnapshot
    {
        public string PlayerId;
        public int    WoodProduction;
        public int    FoodProduction;
        public int    GoldProduction;
        public int    PopulationMax;
        public int    DefenseRating;
        public Dictionary<string, int> BuildingLevels = new(); // BuildingType → level
        public int    SoldierCount;
        public int    ArcherCount;
        public long   SnapshotUtc;
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
        if (config == null) { Debug.LogWarning("[LordNet] GameConfig not found."); return; }

        _firebaseUrl = config.FirebaseDatabaseURL;
        _firebaseKey = config.FirebaseAPIKey;

        LocalPlayerId   = SystemInfo.deviceUniqueIdentifier;
        LocalPlayerName = GameManager.Instance?.PlayerName ?? "Lord Unknown";

        // Register this player in the world and start polling
        StartCoroutine(RegisterLocalPlayer());
        StartCoroutine(PollWorldState());
        StartCoroutine(PollNotifications());
        InvokeRepeating(nameof(HeartbeatPresence), 0f, 60f);
    }

    private void OnDestroy()
    {
        CancelInvoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  PLAYER REGISTRATION & PRESENCE
    // ─────────────────────────────────────────────────────────────

    private IEnumerator RegisterLocalPlayer()
    {
        var gm = GameManager.Instance;
        var snapshot = BuildCastleSnapshot();

        var player = new WorldPlayer
        {
            PlayerId      = LocalPlayerId,
            PlayerName    = LocalPlayerName,
            LordTitle     = gm?.LordTitle ?? "Little Lord",
            TerritoryX    = UnityEngine.Random.Range(0, 100),
            TerritoryZ    = UnityEngine.Random.Range(0, 100),
            TerritoryCount= gm?.WorldMapManager?.OwnedTerritoryCount ?? 1,
            DefenseRating = snapshot.DefenseRating,
            LastActiveUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            PersonaStyle  = BuildPersonaStyle()
        };

        string json = JsonConvert.SerializeObject(player);
        yield return FirebasePut($"players/{LocalPlayerId}.json", json);
        yield return FirebasePut($"castleSnapshots/{LocalPlayerId}.json",
            JsonConvert.SerializeObject(snapshot));

        Debug.Log($"[LordNet] Registered as {LocalPlayerName} ({LocalPlayerId})");
    }

    private void HeartbeatPresence()
    {
        StartCoroutine(FirebasePatch($"players/{LocalPlayerId}.json",
            $"{{\"lastActiveUtc\":{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}," +
            $"\"lordTitle\":\"{GameManager.Instance?.LordTitle ?? "Little Lord"}\"," +
            $"\"territoryCount\":{GameManager.Instance?.WorldMapManager?.OwnedTerritoryCount ?? 1}}}"));
    }

    // ─────────────────────────────────────────────────────────────
    //  WORLD STATE POLLING
    // ─────────────────────────────────────────────────────────────

    private IEnumerator PollWorldState()
    {
        while (true)
        {
            yield return new WaitForSeconds(30f);
            yield return FetchWorldPlayers();
        }
    }

    private IEnumerator FetchWorldPlayers()
    {
        string url = $"{_firebaseUrl}/players.json?auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success) yield break;

        var raw = JsonConvert.DeserializeObject<Dictionary<string, WorldPlayer>>(req.downloadHandler.text);
        if (raw == null) yield break;

        _worldPlayers.Clear();
        foreach (var kvp in raw)
            if (kvp.Key != LocalPlayerId)
                _worldPlayers[kvp.Key] = kvp.Value;

        OnWorldPlayersUpdated?.Invoke(new List<WorldPlayer>(_worldPlayers.Values));
    }

    private IEnumerator PollNotifications()
    {
        while (true)
        {
            yield return new WaitForSeconds(20f);
            yield return FetchPendingBattles();
            yield return FetchDiplomaticMessages();
        }
    }

    private IEnumerator FetchPendingBattles()
    {
        string url = $"{_firebaseUrl}/battles.json?orderBy=\"defenderId\"" +
                     $"&equalTo=\"{LocalPlayerId}\"&auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;

        var battles = JsonConvert.DeserializeObject<Dictionary<string, BattleRecord>>(req.downloadHandler.text);
        if (battles == null) yield break;

        foreach (var kvp in battles)
        {
            OnBattleResultReceived?.Invoke(new BattleResult
            {
                Record = kvp.Value,
                IsLocalPlayerDefender = true,
                IsLocalPlayerAttacker = false
            });
        }
    }

    private IEnumerator FetchDiplomaticMessages()
    {
        string url = $"{_firebaseUrl}/diplomacy/{LocalPlayerId}.json?auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) yield break;

        var messages = JsonConvert.DeserializeObject<Dictionary<string, DiplomaticMessage>>(req.downloadHandler.text);
        if (messages == null) yield break;

        foreach (var kvp in messages.Values)
            if (!kvp.Responded) OnDiplomaticMessageReceived?.Invoke(kvp);
    }

    // ─────────────────────────────────────────────────────────────
    //  BATTLE SYSTEM (ASYNC)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attack another player's castle.
    /// If defender is offline, Gemini simulates their defense using their persona.
    /// </summary>
    public void AttackPlayer(string targetPlayerId, string attackCommand, Action<BattleResult> onResult)
    {
        StartCoroutine(ExecuteAttack(targetPlayerId, attackCommand, onResult));
    }

    private IEnumerator ExecuteAttack(string targetId, string attackCommand, Action<BattleResult> onResult)
    {
        // 1. Fetch target's castle snapshot
        string snapUrl = $"{_firebaseUrl}/castleSnapshots/{targetId}.json?auth={_firebaseKey}";
        using var snapReq = UnityWebRequest.Get(snapUrl);
        yield return snapReq.SendWebRequest();

        CastleSnapshot defenderSnapshot = null;
        if (snapReq.result == UnityWebRequest.Result.Success)
            defenderSnapshot = JsonConvert.DeserializeObject<CastleSnapshot>(snapReq.downloadHandler.text);

        // 2. Fetch target's persona for Gemini impersonation
        string playerUrl = $"{_firebaseUrl}/players/{targetId}.json?auth={_firebaseKey}";
        using var playerReq = UnityWebRequest.Get(playerUrl);
        yield return playerReq.SendWebRequest();

        WorldPlayer defender = null;
        if (playerReq.result == UnityWebRequest.Result.Success)
            defender = JsonConvert.DeserializeObject<WorldPlayer>(playerReq.downloadHandler.text);

        // 3. Ask Gemini to simulate the battle
        string defenseNarrative = "";
        bool attackerWon = false;

        if (GeminiAPIClient.Instance != null)
        {
            var gm = GameManager.Instance;
            int attackerStrength = CalculateAttackStrength();
            int defenderStrength = defenderSnapshot?.DefenseRating ?? 50;

            string battlePrompt = BuildBattlePrompt(
                attacker: LocalPlayerName,
                defender: defender?.PlayerName ?? "Unknown Lord",
                defenderPersona: defender?.PersonaStyle ?? "",
                attackCommand: attackCommand,
                attackerStrength: attackerStrength,
                defenderStrength: defenderStrength
            );

            bool geminiDone = false;
            GeminiAPIClient.Instance.SendMessage(
                battlePrompt,
                "You are a battle narrator for a medieval strategy game. " +
                "Respond with JSON: {\"attackerWon\": bool, \"narrative\": string, \"territoriesLost\": int}",
                null,
                response =>
                {
                    try
                    {
                        var result = JsonConvert.DeserializeObject<BattleNarrativeResult>(response);
                        attackerWon = result?.attackerWon ?? (attackerStrength > defenderStrength);
                        defenseNarrative = result?.narrative ?? response;
                    }
                    catch
                    {
                        attackerWon = attackerStrength > defenderStrength;
                        defenseNarrative = response;
                    }
                    geminiDone = true;
                },
                _ => { attackerWon = attackerStrength > (defenderSnapshot?.DefenseRating ?? 50); geminiDone = true; }
            );

            float timeout = 10f;
            while (!geminiDone && timeout > 0)
            {
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            attackerWon = CalculateAttackStrength() > (defenderSnapshot?.DefenseRating ?? 50);
            defenseNarrative = attackerWon
                ? $"The enemy lord {defender?.PlayerName} was caught off guard and could not mount a proper defense."
                : $"Lord {defender?.PlayerName}'s defenses held firm. Your forces retreated with heavy losses.";
        }

        // 4. Record battle result to Firebase
        string battleId = Guid.NewGuid().ToString("N")[..12];
        var record = new BattleRecord
        {
            BattleId          = battleId,
            AttackerId        = LocalPlayerId,
            DefenderId        = targetId,
            TimestampUtc      = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            AttackerWon       = attackerWon,
            TerritoryGained   = attackerWon ? 1 : 0,
            AiDefenseNarrative= defenseNarrative,
            AttackerCommand   = attackCommand
        };

        yield return FirebasePut($"battles/{battleId}.json",
            JsonConvert.SerializeObject(record));

        // 5. Notify defender
        yield return FirebasePost($"notifications/{targetId}.json",
            JsonConvert.SerializeObject(new
            {
                type = "battle",
                battleId = battleId,
                attackerName = LocalPlayerName,
                attackerWon = attackerWon,
                timestampUtc = record.TimestampUtc
            }));

        // 6. Update attacker's territory if won
        if (attackerWon)
        {
            yield return FirebasePatch($"players/{LocalPlayerId}.json",
                $"{{\"territoryCount\":{(GameManager.Instance?.WorldMapManager?.OwnedTerritoryCount ?? 1) + 1}}}");
        }

        onResult?.Invoke(new BattleResult
        {
            Record = record,
            IsLocalPlayerAttacker = true,
            IsLocalPlayerDefender = false
        });
    }

    // ─────────────────────────────────────────────────────────────
    //  NATION SYSTEM
    // ─────────────────────────────────────────────────────────────

    public void CreateNation(string name, string tag, Action<NationData> onCreated)
    {
        StartCoroutine(DoCreateNation(name, tag, onCreated));
    }

    private IEnumerator DoCreateNation(string name, string tag, Action<NationData> onCreated)
    {
        string nationId = Guid.NewGuid().ToString("N")[..8];
        var nation = new NationData
        {
            NationId   = nationId,
            Name       = name,
            Tag        = tag.ToUpper()[..Mathf.Min(3, tag.Length)],
            LeaderId   = LocalPlayerId,
            MemberIds  = new List<string> { LocalPlayerId },
            FoundedUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        yield return FirebasePut($"nations/{nationId}.json", JsonConvert.SerializeObject(nation));
        yield return FirebasePatch($"players/{LocalPlayerId}.json", $"{{\"nationId\":\"{nationId}\"}}");

        LocalNationId = nationId;
        _nations[nationId] = nation;
        OnNationUpdated?.Invoke(nation);
        onCreated?.Invoke(nation);
        Debug.Log($"[LordNet] Nation '{name} [{tag}]' created.");
    }

    public void JoinNation(string nationId, Action<NationData> onJoined)
    {
        StartCoroutine(DoJoinNation(nationId, onJoined));
    }

    private IEnumerator DoJoinNation(string nationId, Action<NationData> onJoined)
    {
        // Add this player to nation's member list
        yield return FirebasePatch($"nations/{nationId}/members/{LocalPlayerId}.json", "true");
        yield return FirebasePatch($"players/{LocalPlayerId}.json", $"{{\"nationId\":\"{nationId}\"}}");

        LocalNationId = nationId;

        // Fetch updated nation
        string url = $"{_firebaseUrl}/nations/{nationId}.json?auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result == UnityWebRequest.Result.Success)
        {
            var nation = JsonConvert.DeserializeObject<NationData>(req.downloadHandler.text);
            if (nation != null)
            {
                _nations[nationId] = nation;
                OnNationUpdated?.Invoke(nation);
                onJoined?.Invoke(nation);
            }
        }
    }

    /// <summary>Send a diplomatic message/proposal to another player.</summary>
    public void SendDiplomaticMessage(string targetPlayerId, string content, string proposal,
        Action onSent = null)
    {
        StartCoroutine(DoSendDiplomacy(targetPlayerId, content, proposal, onSent));
    }

    private IEnumerator DoSendDiplomacy(string targetId, string content, string proposal, Action onSent)
    {
        var msg = new DiplomaticMessage
        {
            MessageId    = Guid.NewGuid().ToString("N")[..10],
            FromPlayerId = LocalPlayerId,
            FromPlayerName = LocalPlayerName,
            ToPlayerId   = targetId,
            Content      = content,
            Proposal     = proposal,
            TimestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Responded    = false
        };

        yield return FirebasePost($"diplomacy/{targetId}.json", JsonConvert.SerializeObject(msg));
        onSent?.Invoke();
    }

    // ─────────────────────────────────────────────────────────────
    //  NATION CHAT
    // ─────────────────────────────────────────────────────────────

    public void SendNationChat(string message)
    {
        if (string.IsNullOrEmpty(LocalNationId)) return;
        StartCoroutine(FirebasePost($"nations/{LocalNationId}/chat.json",
            JsonConvert.SerializeObject(new
            {
                playerId = LocalPlayerId,
                playerName = LocalPlayerName,
                message = message,
                timestampUtc = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            })));
    }

    public IEnumerator FetchNationChat(string nationId, Action<List<ChatMessage>> onResult)
    {
        string url = $"{_firebaseUrl}/nations/{nationId}/chat.json?" +
                     $"orderBy=\"timestampUtc\"&limitToLast=50&auth={_firebaseKey}";
        using var req = UnityWebRequest.Get(url);
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success) { onResult?.Invoke(null); yield break; }

        var raw = JsonConvert.DeserializeObject<Dictionary<string, ChatMessage>>(req.downloadHandler.text);
        onResult?.Invoke(raw != null ? new List<ChatMessage>(raw.Values) : new List<ChatMessage>());
    }

    [Serializable]
    public class ChatMessage
    {
        public string PlayerId;
        public string PlayerName;
        public string Message;
        public long   TimestampUtc;
    }

    // ─────────────────────────────────────────────────────────────
    //  WORLD MAP DATA
    // ─────────────────────────────────────────────────────────────

    public List<WorldPlayer> GetAllWorldPlayers() => new(_worldPlayers.Values);

    public List<WorldPlayer> GetNearbyPlayers(int centerX, int centerZ, int radius)
    {
        var result = new List<WorldPlayer>();
        foreach (var p in _worldPlayers.Values)
            if (Mathf.Abs(p.TerritoryX - centerX) <= radius &&
                Mathf.Abs(p.TerritoryZ - centerZ) <= radius)
                result.Add(p);
        return result;
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private CastleSnapshot BuildCastleSnapshot()
    {
        var rm = GameManager.Instance?.ResourceManager;
        var bm = BuildingManager.Instance;

        var snap = new CastleSnapshot
        {
            PlayerId        = LocalPlayerId,
            DefenseRating   = CalculateDefenseRating(),
            SoldierCount    = NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count ?? 0,
            SnapshotUtc     = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        if (bm != null)
            foreach (var b in bm.GetBuiltBuildings())
                snap.BuildingLevels[b.Type.ToString()] = b.Level;

        return snap;
    }

    private int CalculateDefenseRating()
    {
        int rating = 30; // Base
        var bm = BuildingManager.Instance;
        if (bm != null)
            foreach (var b in bm.GetBuiltBuildings())
                rating += b.DefenseBonus;
        int soldiers = NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count ?? 0;
        rating += soldiers * 5;
        return Mathf.Clamp(rating, 0, 300);
    }

    private int CalculateAttackStrength()
    {
        int str = 20;
        var bm = BuildingManager.Instance;
        if (bm != null)
        {
            var barracks = bm.GetBuilding(BuildingManager.BuildingType.Barracks);
            if (barracks?.IsBuilt == true) str += barracks.Level * 15;
        }
        int soldiers = NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count ?? 0;
        return str + soldiers * 8;
    }

    private string BuildPersonaStyle()
    {
        var gm = GameManager.Instance;
        int territories = gm?.WorldMapManager?.OwnedTerritoryCount ?? 1;
        int day = gm?.Day ?? 1;
        return $"Aggressive={territories > 5}|Expansive={territories > 3}|Experienced={day > 100}";
    }

    private string BuildBattlePrompt(string attacker, string defender, string defenderPersona,
        string attackCommand, int attackerStrength, int defenderStrength)
    {
        return $"Medieval battle: '{attacker}' attacks '{defender}'.\n" +
               $"Attacker orders: \"{attackCommand}\"\n" +
               $"Attacker strength: {attackerStrength} | Defender strength: {defenderStrength}\n" +
               $"Defender personality hints: {defenderPersona}\n" +
               $"Write a 2-sentence dramatic battle narrative, then output JSON result.";
    }

    [Serializable]
    private class BattleNarrativeResult
    {
        public bool   attackerWon;
        public string narrative;
        public int    territoriesLost;
    }

    // ─────────────────────────────────────────────────────────────
    //  FIREBASE REST HELPERS
    // ─────────────────────────────────────────────────────────────

    private IEnumerator FirebasePut(string path, string json)
    {
        string url = $"{_firebaseUrl}/{path}?auth={_firebaseKey}";
        using var req = new UnityWebRequest(url, "PUT");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();
        if (req.result != UnityWebRequest.Result.Success)
            Debug.LogWarning($"[LordNet] PUT {path} failed: {req.error}");
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

    /// <summary>Sync current castle snapshot to Firebase (call after major changes).</summary>
    public void UploadCastleSnapshot() =>
        StartCoroutine(FirebasePut($"castleSnapshots/{LocalPlayerId}.json",
            JsonConvert.SerializeObject(BuildCastleSnapshot())));
}
