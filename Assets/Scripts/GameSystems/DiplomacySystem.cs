using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Diplomacy system - negotiate with AI lords via text prompts.
/// Send messengers, propose alliances, demand tribute, or declare war.
/// Gemini plays the AI lord with their personality.
/// </summary>
public class DiplomacySystem : MonoBehaviour
{
    public static DiplomacySystem Instance { get; private set; }

    public enum DiplomaticRelation
    {
        War = -2,
        Hostile = -1,
        Neutral = 0,
        Friendly = 1,
        Allied = 2
    }

    public enum DiplomaticAction
    {
        ProposeAlliance,
        SendTribute,
        DemandTribute,
        DeclareWar,
        ProposePeace,
        TradeAgreement,
        IntelligenceExchange
    }

    [Serializable]
    public class DiplomaticState
    {
        public string LordId;
        public DiplomaticRelation Relation;
        public int RelationScore; // -100 to 100
        public List<string> DialogueHistory;
        public bool AllianceActive;
        public bool TradeAgreementActive;
    }

    private Dictionary<string, DiplomaticState> _relations = new();
    private GeminiAPIClient _gemini;

    public event Action<string, DiplomaticRelation> OnRelationChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _gemini = GeminiAPIClient.Instance;
        // InitializeDiplomacy must run in Start (after WorldMapManager.Start generates the map)
        InitializeDiplomacy();
    }

    private void InitializeDiplomacy()
    {
        var aiLords = WorldMapManager.Instance?.GetAILords();
        if (aiLords == null) return;

        foreach (var lord in aiLords)
        {
            _relations[lord.LordId] = new DiplomaticState
            {
                LordId = lord.LordId,
                Relation = DiplomaticRelation.Neutral,
                RelationScore = lord.DiplomaticScore - 50, // Convert to -50 to +50
                DialogueHistory = new List<string>()
            };
        }
    }

    /// <summary>
    /// Sends a diplomatic message to an AI lord. Gemini plays the lord.
    /// </summary>
    public void SendMessage(string lordId, string playerMessage, Action<string> onResponse)
    {
        var aiLord = WorldMapManager.Instance?.GetAILords().Find(l => l.LordId == lordId);
        if (aiLord == null || _gemini == null) return;

        if (!_relations.ContainsKey(lordId))
            InitializeDiplomacy();

        var state = _relations[lordId];
        var gm = GameManager.Instance;
        string lang = LocalizationManager.Instance?.CurrentLanguageCode ?? "en";

        string systemPrompt = $@"You are {aiLord.Title} {aiLord.Name}, ruler of your own castle.
PERSONALITY: {aiLord.Personality}
MILITARY STRENGTH: {aiLord.MilitaryStrength}/100
CURRENT RELATION with {gm?.LordTitle} {gm?.PlayerName}: {state.Relation} (score: {state.RelationScore}/100)

You are responding to a diplomatic message. React according to your personality:
- {WorldMapManager.AILordPersonality.Aggressive}: Arrogant, threatening, sees weakness
- {WorldMapManager.AILordPersonality.Defensive}: Cautious, non-committal, avoids conflict
- {WorldMapManager.AILordPersonality.Diplomatic}: Open to negotiation, calculated
- {WorldMapManager.AILordPersonality.Expansionist}: Ambitious, always angling for advantage
- {WorldMapManager.AILordPersonality.Isolationist}: Wary of outsiders, prefers independence

Keep responses to 2-3 sentences. Be in character. Language: {lang}";

        var history = new List<(string role, string text)>();
        for (int i = 0; i < state.DialogueHistory.Count; i++)
            history.Add((i % 2 == 0 ? "user" : "model", state.DialogueHistory[i]));

        state.DialogueHistory.Add(playerMessage);

        _gemini.SendMessage(playerMessage, systemPrompt, history, response =>
        {
            state.DialogueHistory.Add(response);
            UpdateRelationFromDialogue(state, playerMessage, response, aiLord);
            onResponse?.Invoke(response);
        });
    }

    private void UpdateRelationFromDialogue(DiplomaticState state, string message, string response,
        WorldMapManager.AILord lord)
    {
        string msg = message.ToLower();
        string resp = response.ToLower();

        // Positive signals
        if (msg.Contains("tribute") && msg.Contains("offer")) state.RelationScore += 15;
        else if (msg.Contains("alliance") || msg.Contains("cooperate")) state.RelationScore += 5;
        else if (msg.Contains("trade")) state.RelationScore += 3;

        // Negative signals
        else if (msg.Contains("demand") || msg.Contains("surrender")) state.RelationScore -= 15;
        else if (msg.Contains("war") || msg.Contains("attack")) state.RelationScore -= 20;

        // Response signals
        if (resp.Contains("agree") || resp.Contains("accept") || resp.Contains("deal"))
            state.RelationScore += 5;
        else if (resp.Contains("refuse") || resp.Contains("never") || resp.Contains("outrage"))
            state.RelationScore -= 5;

        state.RelationScore = Mathf.Clamp(state.RelationScore, -100, 100);
        UpdateRelationLevel(state, lord);
    }

    private void UpdateRelationLevel(DiplomaticState state, WorldMapManager.AILord lord)
    {
        var oldRelation = state.Relation;
        state.Relation = state.RelationScore switch
        {
            >= 60 => DiplomaticRelation.Allied,
            >= 20 => DiplomaticRelation.Friendly,
            >= -20 => DiplomaticRelation.Neutral,
            >= -60 => DiplomaticRelation.Hostile,
            _ => DiplomaticRelation.War
        };

        if (state.Relation != oldRelation)
        {
            OnRelationChanged?.Invoke(state.LordId, state.Relation);
            Debug.Log($"[Diplomacy] {lord.Name}: {oldRelation} -> {state.Relation}");
        }
    }

    public void PerformAction(string lordId, DiplomaticAction action, Action<string, bool> onResult)
    {
        var aiLord = WorldMapManager.Instance?.GetAILords().Find(l => l.LordId == lordId);
        if (aiLord == null) return;

        var state = _relations.TryGetValue(lordId, out var s) ? s : null;
        if (state == null) return;

        string message = action switch
        {
            DiplomaticAction.ProposeAlliance => "I propose a formal alliance between our realms.",
            DiplomaticAction.SendTribute => "As a gesture of goodwill, I offer tribute to your coffers.",
            DiplomaticAction.DemandTribute => "Acknowledge my superiority and send tribute.",
            DiplomaticAction.DeclareWar => "I hereby declare war upon your domain!",
            DiplomaticAction.ProposePeace => "Let us end this conflict and find peace.",
            DiplomaticAction.TradeAgreement => "I propose a mutual trade agreement for our benefit.",
            _ => "I seek to discuss matters of mutual interest."
        };

        SendMessage(lordId, message, response =>
        {
            bool accepted = response.ToLower().Contains("agree") || response.ToLower().Contains("accept")
                            || response.ToLower().Contains("so be it");
            ApplyActionResult(state, action, accepted);
            onResult?.Invoke(response, accepted);
        });
    }

    private void ApplyActionResult(DiplomaticState state, DiplomaticAction action, bool accepted)
    {
        if (!accepted) return;

        switch (action)
        {
            case DiplomaticAction.ProposeAlliance:
                state.AllianceActive = true;
                state.RelationScore = Mathf.Max(state.RelationScore, 60);
                break;
            case DiplomaticAction.SendTribute:
                GameManager.Instance?.ResourceManager?.TrySpend(gold: 50);
                state.RelationScore += 20;
                break;
            case DiplomaticAction.TradeAgreement:
                state.TradeAgreementActive = true;
                GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Gold, 20);
                break;
            case DiplomaticAction.DeclareWar:
                state.Relation = DiplomaticRelation.War;
                state.RelationScore = -100;
                break;
            case DiplomaticAction.ProposePeace:
                if (state.Relation == DiplomaticRelation.War)
                    state.RelationScore = -20;
                break;
        }
    }

    public DiplomaticState GetRelation(string lordId) =>
        _relations.TryGetValue(lordId, out var s) ? s : null;

    public List<DiplomaticState> GetAllRelations() => new List<DiplomaticState>(_relations.Values);
}

