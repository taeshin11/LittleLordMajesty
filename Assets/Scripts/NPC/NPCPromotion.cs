using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// NPC progression system. Loyal, high-performing NPCs earn promotions and special abilities.
/// Promotes through tiers: Apprentice → Journeyman → Expert → Master → Champion
/// </summary>
public class NPCPromotion : MonoBehaviour
{
    public static NPCPromotion Instance { get; private set; }

    public enum NPCRank
    {
        Apprentice = 0,
        Journeyman = 1,
        Expert = 2,
        Master = 3,
        Champion = 4
    }

    [Serializable]
    public class NPCRankData
    {
        public string NPCId;
        public NPCRank Rank;
        public int Experience;
        public string[] UnlockedAbilities;
    }

    private Dictionary<string, NPCRankData> _rankData = new();

    public event Action<string, NPCRank> OnNPCPromoted;

    private static readonly int[] XPThresholds = { 0, 100, 300, 600, 1000 };

    private static readonly Dictionary<NPCPersona.NPCProfession, Dictionary<NPCRank, string[]>> RankAbilities =
        new()
        {
            {
                NPCPersona.NPCProfession.Soldier, new()
                {
                    { NPCRank.Journeyman, new[] { "Shield Wall: +5 defense during raids" } },
                    { NPCRank.Expert, new[] { "Veteran Strike: +10% siege attack" } },
                    { NPCRank.Master, new[] { "Battle Commander: grants +3 combat to allies" } },
                    { NPCRank.Champion, new[] { "Hero: can single-handedly repel minor raids" } }
                }
            },
            {
                NPCPersona.NPCProfession.Farmer, new()
                {
                    { NPCRank.Journeyman, new[] { "Green Thumb: +10% food production" } },
                    { NPCRank.Expert, new[] { "Crop Master: unlocks crop rotation tech" } },
                    { NPCRank.Master, new[] { "Harvest Festival: once/year doubles food output" } },
                    { NPCRank.Champion, new[] { "Land Steward: manages 3 farms autonomously" } }
                }
            },
            {
                NPCPersona.NPCProfession.Merchant, new()
                {
                    { NPCRank.Journeyman, new[] { "Haggler: -10% building costs" } },
                    { NPCRank.Expert, new[] { "Trade Network: +5 gold/day" } },
                    { NPCRank.Master, new[] { "Market Monopoly: double market income" } },
                    { NPCRank.Champion, new[] { "Trade Empire: passive gold from all territories" } }
                }
            },
            {
                NPCPersona.NPCProfession.Vassal, new()
                {
                    { NPCRank.Journeyman, new[] { "Advisor: reveals hidden event outcomes" } },
                    { NPCRank.Expert, new[] { "Diplomat: +10 to all AI lord relations" } },
                    { NPCRank.Master, new[] { "Hand of the Lord: can resolve minor events autonomously" } },
                    { NPCRank.Champion, new[] { "Grand Vizier: unlocks advanced political actions" } }
                }
            }
        };

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void GainXP(string npcId, int amount)
    {
        if (!_rankData.ContainsKey(npcId))
            _rankData[npcId] = new NPCRankData { NPCId = npcId, Rank = NPCRank.Apprentice };

        var data = _rankData[npcId];
        data.Experience += amount;

        // Check for promotion
        int nextRankIndex = (int)data.Rank + 1;
        if (nextRankIndex < XPThresholds.Length && data.Experience >= XPThresholds[nextRankIndex])
            PromoteNPC(npcId, data);
    }

    private void PromoteNPC(string npcId, NPCRankData data)
    {
        var newRank = (NPCRank)((int)data.Rank + 1);
        data.Rank = newRank;

        // Apply abilities
        var npc = NPCManager.Instance?.GetNPC(npcId);
        if (npc != null)
        {
            npc.LoyaltyToLord = Mathf.Min(100, npc.LoyaltyToLord + 5);
            ApplyRankBonus(npc, newRank);
        }

        // Get unlocked abilities
        if (npc != null && RankAbilities.TryGetValue(npc.Profession, out var profAbilities))
        {
            if (profAbilities.TryGetValue(newRank, out var abilities))
            {
                data.UnlockedAbilities = abilities;
                string abilityList = string.Join(", ", abilities);
                ToastNotification.Show($"{npc.Name} promoted to {newRank}! Gained: {abilityList}",
                    ToastNotification.ToastType.Success, 5f);
            }
        }

        OnNPCPromoted?.Invoke(npcId, newRank);
        Debug.Log($"[NPCPromotion] {npcId} promoted to {newRank}!");
    }

    private void ApplyRankBonus(NPCManager.NPCData npc, NPCRank rank)
    {
        var rm = GameManager.Instance?.ResourceManager;
        if (rm == null) return;

        switch (npc.Profession)
        {
            case NPCPersona.NPCProfession.Farmer:
                rm.FoodProductionMultiplier += rank == NPCRank.Journeyman ? 0.1f : 0.05f;
                break;
            case NPCPersona.NPCProfession.Merchant:
                rm.GoldProductionMultiplier += rank == NPCRank.Journeyman ? 0.1f : 0.05f;
                break;
        }
    }

    public NPCRank GetRank(string npcId) =>
        _rankData.TryGetValue(npcId, out var d) ? d.Rank : NPCRank.Apprentice;

    public int GetXP(string npcId) =>
        _rankData.TryGetValue(npcId, out var d) ? d.Experience : 0;

    public int GetXPToNextRank(string npcId)
    {
        var rank = GetRank(npcId);
        int nextIdx = (int)rank + 1;
        if (nextIdx >= XPThresholds.Length) return -1; // Max rank
        return XPThresholds[nextIdx] - GetXP(npcId);
    }

    public string[] GetAbilities(string npcId) =>
        _rankData.TryGetValue(npcId, out var d) ? d.UnlockedAbilities ?? Array.Empty<string>() : Array.Empty<string>();
}
