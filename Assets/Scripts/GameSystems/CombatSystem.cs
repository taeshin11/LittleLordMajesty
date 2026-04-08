using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// Combat system for Orc raid defense and siege battles.
/// Dynamic events - not wave-based tower defense. Player commands determine outcome.
/// </summary>
public class CombatSystem : MonoBehaviour
{
    public static CombatSystem Instance { get; private set; }

    [Serializable]
    public class BattleState
    {
        public string BattleId;
        public BattleType Type;
        public int PlayerForce;
        public int EnemyForce;
        public int PlayerCasualties;
        public int EnemyCasualties;
        public float Duration; // seconds elapsed
        public bool IsActive;
        public string[] PlayerCommandHistory;
    }

    public enum BattleType { OrcRaidDefense, SiegeAttack, SiegeDefense }

    private BattleState _currentBattle;
    private GeminiAPIClient _gemini;

    public event Action<BattleState> OnBattleUpdate;
    public event Action<BattleState, bool> OnBattleEnd; // state, playerWon

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _gemini = GeminiAPIClient.Instance;
    }

    public void StartOrcRaidDefense(int orcCount, Action<bool, string> onEnd)
    {
        int defenders = NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count * 5 ?? 5;
        defenders += GetBuildingDefenseBonus();

        _currentBattle = new BattleState
        {
            BattleId = Guid.NewGuid().ToString("N")[..8],
            Type = BattleType.OrcRaidDefense,
            PlayerForce = defenders,
            EnemyForce = orcCount,
            IsActive = true
        };

        Debug.Log($"[Combat] Orc raid: {orcCount} orcs vs {defenders} defenders");
        OnBattleUpdate?.Invoke(_currentBattle);
        StartCoroutine(ProcessRaidBattle(_currentBattle, onEnd));
    }

    private IEnumerator ProcessRaidBattle(BattleState battle, Action<bool, string> onEnd)
    {
        // Auto-resolve with LLM narrative if no player input
        yield return new WaitForSeconds(1f);

        float playerStrength = battle.PlayerForce * UnityEngine.Random.Range(0.8f, 1.3f);
        float enemyStrength = battle.EnemyForce * UnityEngine.Random.Range(0.8f, 1.2f);
        bool playerWins = playerStrength > enemyStrength * 0.8f;

        // Calculate casualties
        battle.PlayerCasualties = playerWins
            ? Mathf.RoundToInt(battle.PlayerForce * UnityEngine.Random.Range(0.05f, 0.2f))
            : Mathf.RoundToInt(battle.PlayerForce * UnityEngine.Random.Range(0.3f, 0.6f));
        battle.EnemyCasualties = playerWins
            ? battle.EnemyForce
            : Mathf.RoundToInt(battle.EnemyForce * UnityEngine.Random.Range(0.4f, 0.7f));

        // Apply losses
        GameManager.Instance?.ResourceManager?.AddResource(
            ResourceManager.ResourceType.Population, -battle.PlayerCasualties);

        string prompt = $@"Write a 2-sentence battle report for this Orc raid on a medieval castle:
Defenders: {battle.PlayerForce} soldiers. Attackers: {battle.EnemyForce} Orcs.
Outcome: {(playerWins ? "DEFENDERS WIN" : "ORCS BREAK THROUGH")}
Casualties: {battle.PlayerCasualties} defenders lost, {battle.EnemyCasualties} Orcs slain.
Language: {LocalizationManager.Instance?.CurrentLanguageCode ?? "en"}";

        battle.IsActive = false;
        OnBattleEnd?.Invoke(battle, playerWins);

        if (_gemini != null)
        {
            _gemini.SendMessage(prompt, "", null,
                narrative => onEnd?.Invoke(playerWins, narrative),
                _ => onEnd?.Invoke(playerWins, playerWins
                    ? "The defenders repelled the Orc attack!"
                    : "The Orcs breached the walls! Damage was done.")
            );
        }
        else
        {
            onEnd?.Invoke(playerWins, playerWins ? "Victory!" : "The Orcs attacked!");
        }
    }

    public void IssueCommandDuringBattle(string command)
    {
        if (_currentBattle == null || !_currentBattle.IsActive) return;

        // Commands modify battle outcome
        command = command.ToLower();
        if (command.Contains("archer") || command.Contains("rain arrows") || command.Contains("volley"))
            _currentBattle.PlayerForce = Mathf.RoundToInt(_currentBattle.PlayerForce * 1.2f);
        else if (command.Contains("retreat") || command.Contains("fall back"))
            _currentBattle.EnemyForce = Mathf.RoundToInt(_currentBattle.EnemyForce * 0.8f);
        else if (command.Contains("charge") || command.Contains("attack"))
        {
            _currentBattle.PlayerForce = Mathf.RoundToInt(_currentBattle.PlayerForce * 1.3f);
            _currentBattle.PlayerCasualties += 2;
        }

        OnBattleUpdate?.Invoke(_currentBattle);
    }

    private int GetBuildingDefenseBonus()
    {
        int bonus = 0;
        var bm = BuildingManager.Instance;
        if (bm == null) return bonus;

        var walls = bm.GetBuilding(BuildingManager.BuildingType.CastleWalls);
        if (walls?.IsBuilt == true) bonus += walls.Level * 10;

        var tower = bm.GetBuilding(BuildingManager.BuildingType.Watchtower);
        if (tower?.IsBuilt == true) bonus += tower.Level * 5;

        var barracks = bm.GetBuilding(BuildingManager.BuildingType.Barracks);
        if (barracks?.IsBuilt == true) bonus += barracks.Level * 8;

        return bonus;
    }
}
