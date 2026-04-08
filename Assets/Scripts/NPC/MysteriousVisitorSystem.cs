using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Handles the "Mysterious Visitor" event - a key LLM interaction feature.
/// Unknown visitors appear with hidden agendas. Player must converse and decide:
/// Hire, Trade, Banish, or Trust. Their true nature is revealed via dialogue.
/// </summary>
public class MysteriousVisitorSystem : MonoBehaviour
{
    public static MysteriousVisitorSystem Instance { get; private set; }

    public enum VisitorType
    {
        DisguisedSpy,      // Enemy lord's spy gathering intel
        WanderingMerchant, // Rare goods trader
        FugitiveMage,      // Powerful but dangerous
        RebellionAgitator, // Tries to stir unrest
        LostKnight,        // Loyal if hired, lethal if banished
        OrcInterpreter,    // Can negotiate with Orcs
        ExiledNoble,       // Claims a territory by rights
        ForeignEmissary    // Alliance opportunity
    }

    [Serializable]
    public class VisitorData
    {
        public string VisitorId;
        public string Name;
        public VisitorType TrueType; // Hidden from player initially
        public string FalseIdentity; // What they claim to be
        public string Motivation;
        public int ClueRevealedCount;
        public bool TrueIdentityRevealed;
        public List<string> DialogueHistory;
        public int SuspicionLevel; // 0-100, rises as player presses
    }

    private VisitorData _currentVisitor;
    private NPCConversationState _conversationState;
    private GeminiAPIClient _gemini;

    public event Action<VisitorData> OnVisitorArrived;
    public event Action<VisitorData, string> OnVisitorDecision; // visitor, decision

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start() => _gemini = GeminiAPIClient.Instance;

    /// <summary>
    /// Spawns a new mysterious visitor event.
    /// </summary>
    public void SpawnVisitor()
    {
        var type = (VisitorType)UnityEngine.Random.Range(0, Enum.GetValues(typeof(VisitorType)).Length);
        var visitor = GenerateVisitor(type);
        _currentVisitor = visitor;

        // Create a persona for the visitor
        var persona = ScriptableObject.CreateInstance<NPCPersona>();
        persona.PersonaName = visitor.Name;
        persona.Profession = NPCPersona.NPCProfession.MysteriousVisitor;
        persona.Personality = GetPersonalityForType(type);
        persona.BackgroundStory = GetBackgroundForType(type, visitor.Name);
        persona.LoyaltyToLord = UnityEngine.Random.Range(10, 60);

        _conversationState = new NPCConversationState { Persona = persona };

        OnVisitorArrived?.Invoke(visitor);
        Debug.Log($"[Visitor] {visitor.Name} arrives claiming to be a {visitor.FalseIdentity}");
    }

    private VisitorData GenerateVisitor(VisitorType type)
    {
        string[] names = { "Mira Duskfall", "Theron Ashveil", "Lysa Coldwater",
                           "Doran Grayveil", "Sera Nightwhisper", "Cael Ironmask" };
        string name = names[UnityEngine.Random.Range(0, names.Length)];

        return new VisitorData
        {
            VisitorId = Guid.NewGuid().ToString("N")[..8],
            Name = name,
            TrueType = type,
            FalseIdentity = GetFalseIdentity(type),
            Motivation = GetMotivation(type),
            DialogueHistory = new List<string>(),
            SuspicionLevel = 0,
            TrueIdentityRevealed = false
        };
    }

    /// <summary>
    /// Player interrogates/converses with the visitor.
    /// LLM plays the visitor maintaining their false identity with hidden clues.
    /// </summary>
    public void ConverseWithVisitor(string playerMessage, Action<string, bool> onResponse)
    {
        if (_currentVisitor == null || _gemini == null)
        {
            onResponse?.Invoke("The visitor has already left.", false);
            return;
        }

        string lang = LocalizationManager.Instance?.CurrentLanguageCode ?? "en";
        string gm_name = GameManager.Instance?.PlayerName ?? "Lord";
        string gm_title = GameManager.Instance?.LordTitle ?? "My Lord";

        // Update suspicion based on questions
        UpdateSuspicion(playerMessage);

        string systemPrompt = BuildVisitorSystemPrompt(lang, gm_name, gm_title);

        _conversationState.AddToHistory("user", playerMessage);

        _gemini.SendMessage(
            playerMessage,
            systemPrompt,
            _conversationState.ConversationHistory,
            response =>
            {
                _conversationState.AddToHistory("model", response);
                _currentVisitor.DialogueHistory.Add($"[Lord]: {playerMessage}");
                _currentVisitor.DialogueHistory.Add($"[Visitor]: {response}");

                bool identityRevealed = CheckIfIdentityRevealed(response);
                if (identityRevealed)
                {
                    _currentVisitor.TrueIdentityRevealed = true;
                    _currentVisitor.ClueRevealedCount++;
                }

                // TTS for visitor
                if (TTSManager.Instance != null)
                {
                    string ttsLang = LocalizationManager.Instance?.GetTTSLanguageCode(
                        LocalizationManager.Instance.CurrentLanguage) ?? "en-US";
                    TTSManager.Instance.Speak(response, languageCode: ttsLang);
                }

                onResponse?.Invoke(response, identityRevealed);
            },
            _ => onResponse?.Invoke("...", false)
        );
    }

    private string BuildVisitorSystemPrompt(string lang, string lordName, string lordTitle)
    {
        var v = _currentVisitor;
        string suspicionNote = v.SuspicionLevel > 60
            ? "You are getting nervous and starting to slip up. Give small hints of your true nature without fully revealing it."
            : v.SuspicionLevel > 30
            ? "You are somewhat suspicious. Maintain your cover but be slightly evasive."
            : "You are confident and playing your role perfectly.";

        return $@"You are {v.Name}, a MYSTERIOUS VISITOR to {lordTitle} {lordName}'s castle.

YOUR COVER IDENTITY: You claim to be {v.FalseIdentity}.
YOUR TRUE IDENTITY: {v.TrueType} - {v.Motivation}
YOUR CURRENT STATE: {suspicionNote}

RULES:
- NEVER directly reveal your true identity unless pressured to breaking point (SuspicionLevel = 100)
- Drop 1 subtle clue per 3 exchanges if the lord seems perceptive
- React with appropriate emotion: fear if threatened, charm if flattered, frustration if accused
- Keep responses to 2-3 sentences
- If SuspicionLevel > 80: you may panic and partially reveal the truth

SPEAK IN: {(lang == "en" ? "English" : lang == "ko" ? "Korean (한국어)" : lang == "ja" ? "Japanese (日本語)" : "English")}";
    }

    private void UpdateSuspicion(string message)
    {
        message = message.ToLower();
        int increase = 0;
        if (message.Contains("spy") || message.Contains("traitor") || message.Contains("liar")) increase += 20;
        else if (message.Contains("who are you") || message.Contains("why are you here")) increase += 5;
        else if (message.Contains("where are you from") || message.Contains("prove it")) increase += 10;
        else if (message.Contains("trust") || message.Contains("tell me more")) increase -= 5;

        _currentVisitor.SuspicionLevel = Mathf.Clamp(_currentVisitor.SuspicionLevel + increase, 0, 100);
    }

    private bool CheckIfIdentityRevealed(string response)
    {
        if (_currentVisitor.SuspicionLevel >= 90) return true;

        string lower = response.ToLower();
        return lower.Contains("i must confess") || lower.Contains("you've seen through me")
               || lower.Contains("you're right") && _currentVisitor.SuspicionLevel > 60;
    }

    /// <summary>
    /// Player makes their decision about the visitor.
    /// </summary>
    public void MakeDecision(string decision, Action<string> onOutcome)
    {
        if (_currentVisitor == null) return;

        decision = decision.ToLower();
        string outcome;

        if (decision.Contains("hire") || decision.Contains("recruit") || decision.Contains("welcome"))
            outcome = ProcessHireDecision();
        else if (decision.Contains("trade") || decision.Contains("deal") || decision.Contains("buy"))
            outcome = ProcessTradeDecision();
        else if (decision.Contains("banish") || decision.Contains("leave") || decision.Contains("go"))
            outcome = ProcessBanishDecision();
        else if (decision.Contains("imprison") || decision.Contains("arrest") || decision.Contains("dungeon"))
            outcome = ProcessImprisonDecision();
        else
            outcome = "The visitor waits for a clear decision.";

        OnVisitorDecision?.Invoke(_currentVisitor, decision);
        onOutcome?.Invoke(outcome);

        if (!outcome.Contains("waits"))
            _currentVisitor = null;
    }

    private string ProcessHireDecision()
    {
        var v = _currentVisitor;
        switch (v.TrueType)
        {
            case VisitorType.LostKnight:
                NPCManager.Instance?.AddNPC(new NPCManager.NPCData
                {
                    Id = $"knight_{v.VisitorId}",
                    Name = v.Name,
                    Profession = NPCPersona.NPCProfession.Soldier,
                    Personality = NPCPersona.NPCPersonality.Brave,
                    LoyaltyToLord = 75,
                    MoodScore = 70,
                    IsAvailable = true,
                    BackgroundStory = $"A wandering knight who found purpose serving you."
                });
                return $"{v.Name} joins your garrison as a loyal knight! (+1 Elite Soldier)";

            case VisitorType.FugitiveMage:
                return $"{v.Name} joins as court mage. Their power will serve you well... for now.";

            case VisitorType.DisguisedSpy:
                GameManager.Instance?.ResourceManager?.AddResource(
                    ResourceManager.ResourceType.Gold, -50);
                return $"You hired {v.Name}, not knowing they are an enemy spy. Intel is being passed to your rivals. (-50 Gold, security risk)";

            default:
                return $"{v.Name} is hired and joins the castle staff.";
        }
    }

    private string ProcessTradeDecision()
    {
        var v = _currentVisitor;
        if (v.TrueType == VisitorType.WanderingMerchant)
        {
            GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Gold, -30);
            GameManager.Instance?.ResourceManager?.AddResource(ResourceManager.ResourceType.Food, 80);
            return $"Trade complete. {v.Name} sold you rare goods for 30 gold. (+80 Food, -30 Gold)";
        }
        return $"{v.Name} has nothing of value to trade.";
    }

    private string ProcessBanishDecision()
    {
        var v = _currentVisitor;
        return v.TrueType switch
        {
            VisitorType.DisguisedSpy => $"{v.Name} is banished. The spy returns to their master empty-handed. (Crisis averted)",
            VisitorType.LostKnight => $"{v.Name} leaves with bitter resentment. They may return as an enemy.",
            VisitorType.RebellionAgitator => $"{v.Name} is driven out. The agitator's work is undone. (Morale +5)",
            _ => $"{v.Name} departs without incident."
        };
    }

    private string ProcessImprisonDecision()
    {
        var v = _currentVisitor;
        if (v.TrueType == VisitorType.DisguisedSpy || v.TrueType == VisitorType.RebellionAgitator)
            return $"{v.Name} is imprisoned! Interrogation may reveal your enemy's plans. (Intelligence +)";
        else
            return $"You imprisoned {v.Name} unjustly. Reputation suffers. Other visitors will be wary.";
    }

    private NPCPersona.NPCPersonality GetPersonalityForType(VisitorType type) => type switch
    {
        VisitorType.DisguisedSpy => NPCPersona.NPCPersonality.Cunning,
        VisitorType.WanderingMerchant => NPCPersona.NPCPersonality.Greedy,
        VisitorType.FugitiveMage => NPCPersona.NPCPersonality.Wise,
        VisitorType.RebellionAgitator => NPCPersona.NPCPersonality.Cunning,
        VisitorType.LostKnight => NPCPersona.NPCPersonality.Loyal,
        VisitorType.ForeignEmissary => NPCPersona.NPCPersonality.Wise,
        _ => NPCPersona.NPCPersonality.Suspicious
    };

    private string GetFalseIdentity(VisitorType type) => type switch
    {
        VisitorType.DisguisedSpy => "traveling herbalist",
        VisitorType.WanderingMerchant => "wandering merchant",
        VisitorType.FugitiveMage => "simple scholar",
        VisitorType.RebellionAgitator => "concerned citizen seeking protection",
        VisitorType.LostKnight => "retired soldier seeking work",
        VisitorType.OrcInterpreter => "former prisoner who learned Orc tongue",
        VisitorType.ExiledNoble => "noble seeking asylum",
        VisitorType.ForeignEmissary => "diplomatic envoy",
        _ => "traveler"
    };

    private string GetMotivation(VisitorType type) => type switch
    {
        VisitorType.DisguisedSpy => "Gathering military intelligence for a rival lord",
        VisitorType.WanderingMerchant => "Selling rare goods at inflated prices",
        VisitorType.FugitiveMage => "Hiding from a mage guild who wants them dead",
        VisitorType.RebellionAgitator => "Stirring unrest to weaken the castle from within",
        VisitorType.LostKnight => "Seeking purpose after losing their lord in battle",
        VisitorType.OrcInterpreter => "Can broker peace with the Orcs - for a price",
        VisitorType.ExiledNoble => "Claims ownership of an adjacent territory by bloodright",
        VisitorType.ForeignEmissary => "Seeking military alliance against a common enemy",
        _ => "Unknown purpose"
    };

    private string GetBackgroundForType(VisitorType type, string name) =>
        $"{name} arrived at the castle gates claiming to be a {GetFalseIdentity(type)}. Their true motives remain unclear.";

    public VisitorData GetCurrentVisitor() => _currentVisitor;
    public bool HasActiveVisitor() => _currentVisitor != null;
}
