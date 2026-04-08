using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Defines NPC personas with unique personalities, backgrounds, and speech styles.
/// Each persona generates a system prompt for Gemini to maintain character consistency.
/// </summary>
[CreateAssetMenu(fileName = "NPCPersona", menuName = "LLM/NPC Persona")]
public class NPCPersona : ScriptableObject
{
    [Header("Identity")]
    public string PersonaName;
    public NPCProfession Profession;
    public NPCPersonality Personality;
    public string BackgroundStory;
    public string[] SpeechQuirks; // e.g., "says 'hmmm' often", "formal speech", "uses old language"

    [Header("Relationships")]
    [Range(-100, 100)] public int LoyaltyToLord = 50;
    [Range(0, 100)] public int WorkEthic = 70;
    [Range(0, 100)] public int Courage = 50;

    [Header("Voice Profile")]
    public string TTSVoiceName = "en-US-Neural2-D"; // Google Cloud TTS voice
    public float SpeakingRate = 1.0f;
    public float Pitch = 0f;

    [Header("Localization")]
    public string LocalizationKey; // Key for localized name/description

    public enum NPCProfession
    {
        Vassal,
        Soldier,
        Merchant,
        Farmer,
        Blacksmith,
        Healer,
        Scout,
        Guard,
        Builder,
        Mage,
        OrcRaider,  // Enemy type
        MysteriousVisitor
    }

    public enum NPCPersonality
    {
        Loyal,
        Greedy,
        Cowardly,
        Brave,
        Lazy,
        Hardworking,
        Suspicious,
        Cheerful,
        Grumpy,
        Wise,
        Naive,
        Cunning
    }

    /// <summary>
    /// Generates the Gemini system prompt for this NPC character.
    /// </summary>
    public string GenerateSystemPrompt(string lordName, string lordTitle, string language = "en")
    {
        string professionDesc = GetProfessionDescription(Profession);
        string personalityDesc = GetPersonalityDescription(Personality);
        string loyaltyDesc = LoyaltyToLord > 70 ? "deeply loyal" : LoyaltyToLord > 30 ? "moderately loyal" : "suspicious and resentful";
        string quirksText = SpeechQuirks?.Length > 0 ? string.Join(", ", SpeechQuirks) : "no particular quirks";

        string languageInstruction = GetLanguageInstruction(language);

        return $@"You are {PersonaName}, a {professionDesc} in the medieval castle of {lordTitle} {lordName}.

PERSONALITY: You are {personalityDesc}. Your loyalty to the Lord is {loyaltyDesc} (score: {LoyaltyToLord}/100).
WORK ETHIC: {WorkEthic}/100. COURAGE: {Courage}/100.

BACKGROUND: {BackgroundStory}

SPEECH STYLE: {quirksText}. Keep responses short (1-3 sentences) and in character.
NEVER break character. NEVER use modern slang unless your character would.
React emotionally based on your personality. Express joy, fear, greed, or loyalty as fits your character.

{languageInstruction}

Current context: You are speaking directly to {lordTitle} {lordName} who has issued you a command or question. Respond naturally as your character would.";
    }

    private string GetLanguageInstruction(string language)
    {
        return language switch
        {
            "ko" => "IMPORTANT: Respond entirely in Korean (한국어). Use appropriate speech levels based on your character's relationship with the Lord.",
            "ja" => "IMPORTANT: Respond entirely in Japanese (日本語). Use appropriate honorific speech levels.",
            "zh" => "IMPORTANT: Respond entirely in Chinese (中文).",
            "fr" => "IMPORTANT: Respond entirely in French.",
            "de" => "IMPORTANT: Respond entirely in German.",
            "es" => "IMPORTANT: Respond entirely in Spanish.",
            _ => "Respond in English."
        };
    }

    private string GetProfessionDescription(NPCProfession profession) => profession switch
    {
        NPCProfession.Vassal => "trusted vassal and advisor",
        NPCProfession.Soldier => "battle-hardened soldier",
        NPCProfession.Merchant => "shrewd merchant and trader",
        NPCProfession.Farmer => "hardworking farmer who tends the castle lands",
        NPCProfession.Blacksmith => "skilled blacksmith who forges weapons and tools",
        NPCProfession.Healer => "wise healer and herbalist",
        NPCProfession.Scout => "swift scout and spy",
        NPCProfession.Guard => "castle gate guard",
        NPCProfession.Builder => "master builder and architect",
        NPCProfession.Mage => "mysterious court mage",
        NPCProfession.OrcRaider => "fearsome Orc raider (enemy)",
        NPCProfession.MysteriousVisitor => "mysterious stranger of unknown origins",
        _ => "castle inhabitant"
    };

    private string GetPersonalityDescription(NPCPersonality personality) => personality switch
    {
        NPCPersonality.Loyal => "fiercely loyal and devoted to your lord above all else",
        NPCPersonality.Greedy => "driven by greed and always seeking personal gain",
        NPCPersonality.Cowardly => "timid and easily frightened, avoiding danger at all costs",
        NPCPersonality.Brave => "courageous and eager for battle and glory",
        NPCPersonality.Lazy => "lazy and always looking for shortcuts and rest",
        NPCPersonality.Hardworking => "diligent and proud of honest work",
        NPCPersonality.Suspicious => "paranoid and suspicious of everyone's motives",
        NPCPersonality.Cheerful => "cheerful and optimistic even in dark times",
        NPCPersonality.Grumpy => "grumpy and perpetually dissatisfied",
        NPCPersonality.Wise => "calm, wise, and measured in all things",
        NPCPersonality.Naive => "innocent and naive, easily deceived",
        NPCPersonality.Cunning => "cunning and calculating, always planning ahead",
        _ => "ordinary"
    };
}

/// <summary>
/// Runtime NPC persona manager - handles conversation state per NPC.
/// </summary>
public class NPCConversationState
{
    public NPCPersona Persona;
    public List<(string role, string text)> ConversationHistory = new();
    public int TotalInteractions = 0;
    public float RelationshipChange = 0f; // Delta from interactions

    private const int MAX_HISTORY = 10; // Keep last 10 exchanges

    public void AddToHistory(string role, string text)
    {
        ConversationHistory.Add((role, text));
        TotalInteractions++;

        // Trim to prevent context overflow
        while (ConversationHistory.Count > MAX_HISTORY * 2)
            ConversationHistory.RemoveAt(0);
    }

    public void ClearHistory() => ConversationHistory.Clear();
}
