using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages the World Map - territory conquest, AI lords, scouting, and siege battles.
/// </summary>
public class WorldMapManager : MonoBehaviour
{
    public static WorldMapManager Instance { get; private set; }

    public enum TerritoryType { Castle, Village, Forest, Mountain, Plains, River }
    public enum ConquestState { Allied, Neutral, Hostile, Besieged, Owned }

    [Serializable]
    public class Territory
    {
        public string TerritoryId;
        public string Name;
        public TerritoryType Type;
        public ConquestState State;
        public string OwnerLordId; // null = player owned
        public Vector2 MapPosition;
        public int DefenseStrength; // 0-100
        public int Garrison; // Defending soldiers
        public int ResourceBonus; // Gold/wood/food bonus per day
        public bool IsScouted;
        public string[] AdjacentTerritoryIds;
    }

    [Serializable]
    public class AILord
    {
        public string LordId;
        public string Name;
        public string Title;
        public int MilitaryStrength;
        public int DiplomaticScore; // How friendly
        public string[] OwnedTerritoryIds;
        public AILordPersonality Personality;
    }

    public enum AILordPersonality { Aggressive, Defensive, Diplomatic, Expansionist, Isolationist }

    [Header("Map Configuration")]
    [SerializeField] private int _mapWidth = 7;
    [SerializeField] private int _mapHeight = 5;

    private List<Territory> _territories = new();
    private List<AILord> _aiLords = new();
    private string _playerTerritoryId = "home_castle";
    private GeminiAPIClient _gemini;

    public int OwnedTerritoryCount => _territories.FindAll(t => t.OwnerLordId == null || t.State == ConquestState.Owned).Count;

    public event Action<Territory> OnTerritoryConquered;
    public event Action<Territory> OnBattleStarted;
    public event Action<string, bool> OnBattleResolved; // territoryId, playerWon

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _gemini = GeminiAPIClient.Instance;
        GenerateWorldMap();
    }

    private void GenerateWorldMap()
    {
        _territories.Clear();
        _aiLords.Clear();

        // Player's starting castle
        _territories.Add(new Territory
        {
            TerritoryId = "home_castle",
            Name = "Ironhold",
            Type = TerritoryType.Castle,
            State = ConquestState.Owned,
            OwnerLordId = null,
            MapPosition = new Vector2(3, 2),
            DefenseStrength = 50,
            Garrison = 10,
            ResourceBonus = 5,
            IsScouted = true,
            AdjacentTerritoryIds = new[] { "plains_01", "forest_01", "village_01" }
        });

        // Neutral territories
        AddTerritory("plains_01", "Golden Plains", TerritoryType.Plains, new Vector2(4, 2));
        AddTerritory("forest_01", "Darkwood", TerritoryType.Forest, new Vector2(2, 3));
        AddTerritory("village_01", "Millhaven", TerritoryType.Village, new Vector2(3, 1));
        AddTerritory("mountain_01", "Stonepeak", TerritoryType.Mountain, new Vector2(5, 3));
        AddTerritory("river_01", "Coldwater Crossing", TerritoryType.River, new Vector2(2, 1));

        // AI Lord territories
        AddAILordWithCastle("lord_eastkeep", "Lord Ravenmoor", "Baron", AILordPersonality.Aggressive,
            "castle_east", "Ravenmoor Keep", new Vector2(6, 2));
        AddAILordWithCastle("lord_northhold", "Lady Thornwick", "Countess", AILordPersonality.Diplomatic,
            "castle_north", "Thornwick Hall", new Vector2(3, 4));
        AddAILordWithCastle("lord_westgate", "Lord Grimwald", "Baron", AILordPersonality.Expansionist,
            "castle_west", "Westgate", new Vector2(0, 2));

        Debug.Log($"[WorldMap] Generated {_territories.Count} territories, {_aiLords.Count} AI lords");
    }

    private void AddTerritory(string id, string name, TerritoryType type, Vector2 pos)
    {
        _territories.Add(new Territory
        {
            TerritoryId = id,
            Name = name,
            Type = type,
            State = ConquestState.Neutral,
            MapPosition = pos,
            DefenseStrength = UnityEngine.Random.Range(10, 40),
            Garrison = UnityEngine.Random.Range(0, 5),
            ResourceBonus = UnityEngine.Random.Range(1, 10),
            IsScouted = false
        });
    }

    private void AddAILordWithCastle(string lordId, string lordName, string title,
        AILordPersonality personality, string castleId, string castleName, Vector2 pos)
    {
        var castle = new Territory
        {
            TerritoryId = castleId,
            Name = castleName,
            Type = TerritoryType.Castle,
            State = ConquestState.Hostile,
            OwnerLordId = lordId,
            MapPosition = pos,
            DefenseStrength = UnityEngine.Random.Range(40, 80),
            Garrison = UnityEngine.Random.Range(10, 30),
            ResourceBonus = UnityEngine.Random.Range(5, 15),
            IsScouted = false
        };
        _territories.Add(castle);

        var aiLord = new AILord
        {
            LordId = lordId,
            Name = lordName,
            Title = title,
            MilitaryStrength = UnityEngine.Random.Range(20, 60),
            DiplomaticScore = UnityEngine.Random.Range(20, 80),
            OwnedTerritoryIds = new[] { castleId },
            Personality = personality
        };
        _aiLords.Add(aiLord);
    }

    public void ScoutTerritory(string territoryId, Action<Territory> onComplete)
    {
        var territory = GetTerritory(territoryId);
        if (territory == null) return;

        // Scout costs resources
        bool canAfford = GameManager.Instance?.ResourceManager?.TrySpend(gold: 10) ?? false;
        if (!canAfford)
        {
            Debug.LogWarning("[WorldMap] Not enough gold to scout.");
            return;
        }

        StartCoroutine(ScoutCoroutine(territory, onComplete));
    }

    private IEnumerator ScoutCoroutine(Territory territory, Action<Territory> onComplete)
    {
        yield return new WaitForSeconds(2f); // Scouting takes time
        territory.IsScouted = true;
        onComplete?.Invoke(territory);
        Debug.Log($"[WorldMap] Scouted: {territory.Name} - Defense: {territory.DefenseStrength}, Garrison: {territory.Garrison}");
    }

    public void LaunchSiege(string territoryId, int attackingForce, Action<bool, string> onBattleEnd)
    {
        var territory = GetTerritory(territoryId);
        if (territory == null) return;

        OnBattleStarted?.Invoke(territory);
        GameManager.Instance?.SetGameState(GameManager.GameState.Battle);

        // Use LLM to generate battle narrative
        GenerateBattleNarrative(territory, attackingForce, onBattleEnd);
    }

    private void GenerateBattleNarrative(Territory territory, int attackingForce, Action<bool, string> onBattleEnd)
    {
        float attackStrength = attackingForce * UnityEngine.Random.Range(0.8f, 1.2f);
        float defenseStrength = territory.DefenseStrength + territory.Garrison * 2;
        bool playerWins = attackStrength > defenseStrength * 0.7f;

        string prompt = $@"Generate a short battle report (3-4 sentences) for this medieval siege:

Attacker: {GameManager.Instance?.LordTitle} {GameManager.Instance?.PlayerName}
Target: {territory.Name}
Attacking Force: {attackingForce} soldiers
Defending Force: {territory.Garrison} soldiers, Defense Rating: {territory.DefenseStrength}
Battle Outcome: {(playerWins ? "ATTACKER WINS" : "DEFENDER WINS")}

Make it dramatic and cinematic. Language: {LocalizationManager.Instance?.CurrentLanguageCode ?? "en"}";

        if (_gemini != null)
        {
            _gemini.SendMessage(prompt, "", null,
                narrative =>
                {
                    ProcessSiegeResult(territory, playerWins, narrative, onBattleEnd);
                },
                _ =>
                {
                    string fallback = playerWins
                        ? $"Your forces stormed {territory.Name} and emerged victorious!"
                        : $"The assault on {territory.Name} was repelled with heavy losses.";
                    ProcessSiegeResult(territory, playerWins, fallback, onBattleEnd);
                }
            );
        }
        else
        {
            string fallback = playerWins ? $"Victory at {territory.Name}!" : $"Defeat at {territory.Name}.";
            ProcessSiegeResult(territory, playerWins, fallback, onBattleEnd);
        }
    }

    private void ProcessSiegeResult(Territory territory, bool playerWon, string narrative, Action<bool, string> onBattleEnd)
    {
        if (playerWon)
        {
            territory.State = ConquestState.Owned;
            territory.OwnerLordId = null;
            GameManager.Instance?.UpdateLordTitle(OwnedTerritoryCount);
            OnTerritoryConquered?.Invoke(territory);
        }

        OnBattleResolved?.Invoke(territory.TerritoryId, playerWon);
        GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
        onBattleEnd?.Invoke(playerWon, narrative);

        Debug.Log($"[WorldMap] Siege of {territory.Name}: {(playerWon ? "Victory" : "Defeat")}");
    }

    public Territory GetTerritory(string id) => _territories.Find(t => t.TerritoryId == id);
    public List<Territory> GetAllTerritories() => new List<Territory>(_territories);
    public List<Territory> GetOwnedTerritories() => _territories.FindAll(t => t.State == ConquestState.Owned);
    public List<Territory> GetHostileTerritories() => _territories.FindAll(t => t.State == ConquestState.Hostile);
    public List<AILord> GetAILords() => new List<AILord>(_aiLords);

    public void ProcessAITurns()
    {
        foreach (var lord in _aiLords)
        {
            if (lord.Personality == AILordPersonality.Aggressive && UnityEngine.Random.value < 0.1f)
                AIAttackPlayer(lord);
        }
    }

    private void AIAttackPlayer(AILord lord)
    {
        // AI initiates an orc-raid-style attack on player
        Debug.Log($"[WorldMap] {lord.Name} launches an attack!");
        EventManager.Instance?.CheckDailyEvents(0, 0); // Triggers raid event
    }
}
