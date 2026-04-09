using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// LLM-powered crisis and event system.
/// Handles Orc raids, NPC conflicts, food shortages, fires, mysterious visitors.
/// </summary>
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    public enum EventType
    {
        OrcRaid,
        NPCConflict,
        FoodShortage,
        Fire,
        MysteriousVisitor,
        TradeOpportunity,
        Disease,
        Rebellion,
        WeatherDisaster,
        CastleSpyDetected
    }

    public enum EventSeverity { Minor, Moderate, Severe, Critical }

    [Serializable]
    public class GameEvent
    {
        public string EventId;
        public EventType Type;
        public EventSeverity Severity;
        public string Title;
        public string Description;
        public string[] InvolvedNPCIds;
        public bool IsResolved;
        public string Resolution;
        public float TimeToResolveSeconds; // 0 = no deadline
        public DateTime OccurredAt;

        public Action<string> OnPlayerResponse;
        public Action<bool, string> OnResolved; // isSuccess, outcome description
    }

    private List<GameEvent> _activeEvents = new();
    private List<GameEvent> _eventHistory = new();
    private GeminiAPIClient _gemini;

    [SerializeField] private float _orcRaidChancePerDay = 0.05f; // 5% per day
    [SerializeField] private float _randomEventChancePerDay = 0.1f; // 10% per day

    public event Action<GameEvent> OnNewEvent;
    public event Action<GameEvent> OnEventResolved;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        _gemini = GeminiAPIClient.Instance;
    }

    public void CheckDailyEvents(int day, int year)
    {
        // Orc raid check
        if (UnityEngine.Random.value < _orcRaidChancePerDay)
            TriggerOrcRaid();

        // Random internal event
        if (UnityEngine.Random.value < _randomEventChancePerDay)
            TriggerRandomInternalEvent(day);

        // Check NPC conflict (based on mood/loyalty)
        CheckNPCConflicts();
    }

    private void TriggerOrcRaid()
    {
        var raidEvent = new GameEvent
        {
            EventId = Guid.NewGuid().ToString("N")[..8],
            Type = EventType.OrcRaid,
            Severity = (EventSeverity)UnityEngine.Random.Range(0, 4),
            Title = LocalizationManager.Instance?.Get("event_orc_raid_title") ?? "Orc Raid!",
            OccurredAt = DateTime.UtcNow,
            TimeToResolveSeconds = 120f // 2 minutes to respond
        };

        string[] raidDescriptions = {
            "A horde of Orcs has been spotted approaching from the east! They will reach the castle walls within the hour!",
            "Orc scouts have been seen near the northern farmlands. A larger raid may follow!",
            "An Orc war chief leads a massive assault on the castle gates!",
            "Orc raiders have set fire to the outer village! Civilians are fleeing!"
        };
        raidEvent.Description = raidDescriptions[UnityEngine.Random.Range(0, raidDescriptions.Length)];

        TriggerEvent(raidEvent);
    }

    private void TriggerRandomInternalEvent(int day)
    {
        var rm = GameManager.Instance?.ResourceManager;
        EventType type;

        // Context-sensitive event selection
        if (rm != null && rm.Food < rm.Population * 3)
            type = EventType.FoodShortage;
        else
        {
            var types = new[] { EventType.NPCConflict, EventType.MysteriousVisitor,
                EventType.TradeOpportunity, EventType.Fire, EventType.Disease };
            type = types[UnityEngine.Random.Range(0, types.Length)];
        }

        var ev = GenerateEvent(type, EventSeverity.Moderate);
        TriggerEvent(ev);
    }

    private GameEvent GenerateEvent(EventType type, EventSeverity severity)
    {
        var nm = NPCManager.Instance;
        var allNPCs = nm?.GetAllNPCs() ?? new System.Collections.Generic.List<NPCManager.NPCData>();

        return type switch
        {
            EventType.NPCConflict => new GameEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..8],
                Type = EventType.NPCConflict,
                Severity = severity,
                Title = LocalizationManager.Instance?.Get("event_conflict_title") ?? "NPC Conflict",
                Description = allNPCs.Count >= 2
                    ? $"{allNPCs[0].Name} and {allNPCs[1].Name} are in a heated dispute over resource allocation."
                    : "Two castle residents are in conflict.",
                InvolvedNPCIds = allNPCs.Count >= 2
                    ? new[] { allNPCs[0].Id, allNPCs[1].Id }
                    : Array.Empty<string>(),
                OccurredAt = DateTime.UtcNow
            },
            EventType.MysteriousVisitor => new GameEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..8],
                Type = EventType.MysteriousVisitor,
                Severity = severity,
                Title = LocalizationManager.Instance?.Get("event_visitor_title") ?? "Mysterious Visitor",
                Description = "A hooded stranger has appeared at the castle gates, seeking an audience.",
                OccurredAt = DateTime.UtcNow
            },
            EventType.FoodShortage => new GameEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..8],
                Type = EventType.FoodShortage,
                Severity = EventSeverity.Severe,
                Title = LocalizationManager.Instance?.Get("event_famine_title") ?? "Food Shortage!",
                Description = "The castle granary is running dangerously low. The people are beginning to starve.",
                OccurredAt = DateTime.UtcNow,
                TimeToResolveSeconds = 180f
            },
            EventType.Fire => new GameEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..8],
                Type = EventType.Fire,
                Severity = EventSeverity.Severe,
                Title = LocalizationManager.Instance?.Get("event_fire_title") ?? "Fire!",
                Description = "Fire has broken out in the castle storage! Quick action is needed to prevent catastrophic loss!",
                OccurredAt = DateTime.UtcNow,
                TimeToResolveSeconds = 60f
            },
            _ => new GameEvent
            {
                EventId = Guid.NewGuid().ToString("N")[..8],
                Type = type,
                Severity = severity,
                Title = type.ToString(),
                Description = $"A {type} event has occurred.",
                OccurredAt = DateTime.UtcNow
            }
        };
    }

    private void TriggerEvent(GameEvent ev)
    {
        _activeEvents.Add(ev);
        OnNewEvent?.Invoke(ev);

        // Use LLM to generate rich description
        EnrichEventWithLLM(ev);

        Debug.Log($"[EventManager] New event: [{ev.Severity}] {ev.Title}");

        GameManager.Instance?.SetGameState(GameManager.GameState.Event);
    }

    private void EnrichEventWithLLM(GameEvent ev)
    {
        if (_gemini == null) return;

        string prompt = $@"You are narrating a medieval castle game event.
Event Type: {ev.Type}
Severity: {ev.Severity}
Base description: {ev.Description}

Write a dramatic 2-sentence narration for this event. Make it feel urgent and immersive.
Keep it concise and impactful. Language: {LocalizationManager.Instance?.CurrentLanguageCode ?? "en"}";

        _gemini.SendMessage(
            prompt,
            "",
            null,
            enrichedDesc =>
            {
                if (!string.IsNullOrEmpty(enrichedDesc))
                    ev.Description = enrichedDesc;
            }
        );
    }

    /// <summary>
    /// Player responds to an active event via text command.
    /// LLM evaluates the response and determines outcome.
    /// </summary>
    public void RespondToEvent(string eventId, string playerResponse, Action<string, bool> onOutcome)
    {
        var ev = _activeEvents.Find(e => e.EventId == eventId);
        if (ev == null) { onOutcome?.Invoke("Event not found.", false); return; }

        var gm = GameManager.Instance;

        string systemPrompt = $@"You are the game narrator for a medieval strategy game.
The Lord ({gm?.LordTitle} {gm?.PlayerName}) is responding to a crisis.

EVENT: {ev.Type} ({ev.Severity} severity)
SITUATION: {ev.Description}
LORD'S RESPONSE: {playerResponse}

Available Resources: Wood={gm?.ResourceManager?.Wood}, Food={gm?.ResourceManager?.Food}, Gold={gm?.ResourceManager?.Gold}
Available Soldiers: {NPCManager.Instance?.GetNPCsByProfession(NPCPersona.NPCProfession.Soldier)?.Count ?? 0}

Evaluate the response and output EXACTLY this JSON format:
{{
  ""success"": true/false,
  ""outcome"": ""Brief outcome description (1-2 sentences)"",
  ""resourceCost"": {{""wood"": 0, ""food"": 0, ""gold"": 0}},
  ""moralEffect"": 0
}}

Is the response clever, resourceful, and appropriate? Judge fairly.";

        _gemini.SendMessage(
            playerResponse,
            systemPrompt,
            null,
            response =>
            {
                ProcessEventOutcome(ev, response, onOutcome);
            },
            error =>
            {
                // Default resolution if LLM fails
                ResolveEvent(ev, true, "The situation was handled adequately.");
                onOutcome?.Invoke("Event resolved.", true);
            }
        );
    }

    private void ProcessEventOutcome(GameEvent ev, string llmResponse, Action<string, bool> onOutcome)
    {
        try
        {
            // Extract JSON from potential markdown wrapper
            string json = llmResponse;
            int jsonStart = llmResponse.IndexOf('{');
            int jsonEnd = llmResponse.LastIndexOf('}');
            if (jsonStart >= 0 && jsonEnd > jsonStart)
                json = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<EventOutcomeData>(json);

            // Apply resource costs — validate affordability first
            int costWood = result.resourceCost?.wood ?? 0;
            int costFood = result.resourceCost?.food ?? 0;
            int costGold = result.resourceCost?.gold ?? 0;
            var rm = GameManager.Instance?.ResourceManager;
            if (rm != null && (costWood > 0 || costFood > 0 || costGold > 0))
            {
                if (!rm.CanAfford(costWood, costFood, costGold))
                {
                    ResolveEvent(ev, false, "Not enough resources to carry out this plan.");
                    onOutcome?.Invoke("Insufficient resources.", false);
                    return;
                }
                rm.TrySpend(costWood, costFood, costGold);
            }

            ResolveEvent(ev, result.success, result.outcome);
            onOutcome?.Invoke(result.outcome, result.success);
        }
        catch
        {
            // Use strict JSON keyword check to avoid false positives (e.g. "no success")
            string lower = llmResponse.ToLower();
            bool success = lower.Contains("\"success\": true") || lower.Contains("\"success\":true")
                           || (lower.Contains("resolved") && !lower.Contains("fail") && !lower.Contains("unsuccessful"));
            ResolveEvent(ev, success, llmResponse);
            onOutcome?.Invoke(llmResponse, success);
        }
    }

    [Serializable]
    private class EventOutcomeData
    {
        public bool success;
        public string outcome;
        public ResourceCostData resourceCost;
        public int moralEffect;
    }

    [Serializable]
    private class ResourceCostData
    {
        public int wood;
        public int food;
        public int gold;
    }

    private void ResolveEvent(GameEvent ev, bool success, string outcome)
    {
        ev.IsResolved = true;
        ev.Resolution = outcome;
        _activeEvents.Remove(ev);
        _eventHistory.Add(ev);
        OnEventResolved?.Invoke(ev);

        if (_activeEvents.Count == 0)
            GameManager.Instance?.SetGameState(GameManager.GameState.Castle);
    }

    private void CheckNPCConflicts()
    {
        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null || npcs.Count < 2) return;

        foreach (var npc in npcs)
        {
            if (npc.MoodScore < 20 && UnityEngine.Random.value < 0.3f)
            {
                TriggerEvent(GenerateEvent(EventType.NPCConflict, EventSeverity.Minor));
                break;
            }
        }
    }

    public List<GameEvent> GetActiveEvents() => new List<GameEvent>(_activeEvents);
    public List<GameEvent> GetEventHistory() => new List<GameEvent>(_eventHistory);
    public bool HasActiveEvents() => _activeEvents.Count > 0;

    /// <summary>
    /// Manually trigger an event from other systems (Research complete, Spy detected, etc.)
    /// </summary>
    public void TriggerManualEvent(string title, string description, EventSeverity severity)
    {
        var ev = new GameEvent
        {
            EventId     = System.Guid.NewGuid().ToString("N")[..8],
            Type        = EventType.CastleSpyDetected,
            Severity    = severity,
            Title       = title,
            Description = description,
            OccurredAt  = System.DateTime.UtcNow
        };
        TriggerEvent(ev);
    }
}
