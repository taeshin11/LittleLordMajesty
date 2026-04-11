using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages all NPCs in the castle - creation, state, tasks, and AI interactions.
/// </summary>
public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Serializable]
    public class NPCData
    {
        public string Id;
        public string Name;
        public NPCPersona.NPCProfession Profession;
        public NPCPersona.NPCPersonality Personality;
        public int LoyaltyToLord;
        public string CurrentTask;
        public NPCTaskState TaskState;
        public float MoodScore; // 0-100
        public bool IsAvailable;
        public string BackgroundStory;
        [System.Obsolete("Use WorldPosition for 3D placement")]
        public Vector2 Position; // Legacy 2D position (kept for save compatibility)
        public Vector3 WorldPosition; // 3D world position in castle scene
    }

    [Serializable]
    public class NPCSaveData
    {
        public string Id;
        public string CurrentTask;
        public float MoodScore;
        public int LoyaltyToLord;
        public Vector3 WorldPosition;
    }

    public enum NPCTaskState { Idle, Working, Combat, Resting, Fleeing, InDialogue }

    private List<NPCData> _npcs = new();
    private Dictionary<string, NPCData> _npcLookup = new();
    private Dictionary<string, NPCConversationState> _conversationStates = new();
    private Dictionary<string, NPCDailyRoutine> _routineCache = new();
    private GeminiAPIClient _gemini;

    public event Action<NPCData> OnNPCAdded;
    public event Action<NPCData> OnNPCTaskChanged;
    public event Action<string, string> OnNPCDialogue; // npcId, dialogueText

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _gemini = GeminiAPIClient.Instance;
        if (_gemini == null)
            Debug.LogWarning("[NPCManager] GeminiAPIClient not found — NPC dialogue will use fallback responses.");
    }

    public void InitializeStartingNPCs()
    {
        _npcs.Clear();
        _npcLookup.Clear();
        _conversationStates.Clear();
        _routineCache.Clear();

        // Starting NPC roster
        AddNPC(new NPCData
        {
            Id = "vassal_01",
            Name = "Aldric",
            Profession = NPCPersona.NPCProfession.Vassal,
            Personality = NPCPersona.NPCPersonality.Loyal,
            LoyaltyToLord = 80,
            MoodScore = 75,
            IsAvailable = true,
            BackgroundStory = "A veteran steward who has served the castle for 20 years. Pragmatic and wise.",
            WorldPosition = new Vector3(0, 0, 2f)
        });

        AddNPC(new NPCData
        {
            Id = "soldier_01",
            Name = "Bram",
            Profession = NPCPersona.NPCProfession.Soldier,
            Personality = NPCPersona.NPCPersonality.Brave,
            LoyaltyToLord = 65,
            MoodScore = 60,
            IsAvailable = true,
            BackgroundStory = "A young soldier eager for battle and glory. Reckless but fearless.",
            WorldPosition = new Vector3(3f, 0, -1f)
        });

        AddNPC(new NPCData
        {
            Id = "farmer_01",
            Name = "Marta",
            Profession = NPCPersona.NPCProfession.Farmer,
            Personality = NPCPersona.NPCPersonality.Hardworking,
            LoyaltyToLord = 70,
            MoodScore = 65,
            IsAvailable = true,
            BackgroundStory = "A sturdy farmer who has worked these lands her whole life. Honest and reliable.",
            WorldPosition = new Vector3(-3f, 0, -1f)
        });

        AddNPC(new NPCData
        {
            Id = "merchant_01",
            Name = "Sivaro",
            Profession = NPCPersona.NPCProfession.Merchant,
            Personality = NPCPersona.NPCPersonality.Greedy,
            LoyaltyToLord = 40,
            MoodScore = 70,
            IsAvailable = true,
            BackgroundStory = "A traveling merchant who settled in the castle. Shrewd and always seeking profit.",
            WorldPosition = new Vector3(2f, 0, -3f)
        });

        Debug.Log($"[NPCManager] Initialized {_npcs.Count} starting NPCs");
    }

    public void AddNPC(NPCData npc)
    {
        if (GetNPC(npc.Id) != null)
        {
            Debug.LogWarning($"[NPCManager] NPC '{npc.Id}' already exists, skipping.");
            return;
        }
        _npcs.Add(npc);
        _npcLookup[npc.Id] = npc;
        _conversationStates[npc.Id] = new NPCConversationState
        {
            Persona = CreatePersonaForNPC(npc)
        };
        OnNPCAdded?.Invoke(npc);
    }

    private NPCPersona CreatePersonaForNPC(NPCData npc)
    {
        var persona = ScriptableObject.CreateInstance<NPCPersona>();
        persona.PersonaName = npc.Name;
        persona.Profession = npc.Profession;
        persona.Personality = npc.Personality;
        persona.LoyaltyToLord = npc.LoyaltyToLord;
        persona.BackgroundStory = npc.BackgroundStory;
        return persona;
    }

    /// <summary>
    /// Player issues a command to an NPC via text prompt.
    /// Gemini interprets the command and generates NPC response.
    /// </summary>
    public void IssueCommandToNPC(string npcId, string playerCommand, Action<string> onResponse)
    {
        var npc = GetNPC(npcId);
        if (npc == null) { onResponse?.Invoke("NPC not found."); return; }

        if (_gemini == null)
        {
            string fallback = LocalizationManager.Instance?.Get("npc_no_response")
                              ?? "I... cannot answer right now, my lord.";
            onResponse?.Invoke(fallback);
            return;
        }

        if (!_conversationStates.TryGetValue(npcId, out var convState))
        {
            Debug.LogError($"[NPCManager] No conversation state for NPC {npcId}");
            onResponse?.Invoke("...");
            return;
        }

        var gm = GameManager.Instance;

        string systemPrompt = convState.Persona.GenerateSystemPrompt(
            gm?.PlayerName ?? "My Lord",
            gm?.LordTitle ?? "Lord",
            LocalizationManager.Instance?.CurrentLanguageCode ?? "en"
        );

        // Add task + location context from daily routine
        if (!string.IsNullOrEmpty(npc.CurrentTask))
            systemPrompt += $"\n\nCurrent task: {npc.CurrentTask}. Mood: {npc.MoodScore:F0}/100.";

        // Inject location-aware context (where the NPC is right now)
        var routine = FindNPCRoutine(npcId);
        if (routine != null)
        {
            string locationCtx = routine.GetCurrentActivityContext();
            if (!string.IsNullOrEmpty(locationCtx))
                systemPrompt += "\n\n" + locationCtx;
        }

        convState.AddToHistory("user", playerCommand);

        _gemini.SendMessage(
            playerCommand,
            systemPrompt,
            convState.GetContextualHistory(),
            response =>
            {
                convState.AddToHistory("model", response);
                UpdateNPCFromResponse(npc, playerCommand, response);
                OnNPCDialogue?.Invoke(npcId, response);

                // Speak the response via TTS
                if (TTSManager.Instance != null)
                {
                    string lang = LocalizationManager.Instance?.GetTTSLanguageCode(
                        LocalizationManager.Instance.CurrentLanguage) ?? "en-US";
                    TTSManager.Instance.Speak(response, languageCode: lang);
                }

                onResponse?.Invoke(response);
            },
            error =>
            {
                Debug.LogError($"[NPCManager] Gemini error for {npcId}: {error}");
                string fallback = LocalizationManager.Instance?.Get("npc_no_response") ?? "I... cannot answer right now, my lord.";
                onResponse?.Invoke(fallback);
            }
        );
    }

    private static bool ContainsCI(string haystack, string needle) =>
        haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

    private void UpdateNPCFromResponse(NPCData npc, string command, string response)
    {
        // Simple heuristic: positive commands boost mood/loyalty.
        // Uses OrdinalIgnoreCase IndexOf — zero allocations vs. ToLower() + Contains.
        if (ContainsCI(command, "reward") || ContainsCI(command, "praise") || ContainsCI(command, "thank"))
        {
            npc.MoodScore = Mathf.Min(100, npc.MoodScore + 5);
            npc.LoyaltyToLord = Mathf.Min(100, npc.LoyaltyToLord + 2);
        }
        else if (ContainsCI(command, "punish") || ContainsCI(command, "threaten") || ContainsCI(command, "execute"))
        {
            npc.MoodScore = Mathf.Max(0, npc.MoodScore - 10);
            npc.LoyaltyToLord = Mathf.Max(0, npc.LoyaltyToLord - 5);
        }

        if (ContainsCI(command, "farm") || ContainsCI(command, "harvest"))
            AssignTask(npc.Id, "Farming");
        else if (ContainsCI(command, "build") || ContainsCI(command, "construct"))
            AssignTask(npc.Id, "Construction");
        else if (ContainsCI(command, "patrol") || ContainsCI(command, "guard"))
            AssignTask(npc.Id, "Patrol");
        else if (ContainsCI(command, "scout") || ContainsCI(command, "spy"))
            AssignTask(npc.Id, "Scouting");
    }

    public void AssignTask(string npcId, string task)
    {
        var npc = GetNPC(npcId);
        if (npc == null) return;
        npc.CurrentTask = task;
        npc.TaskState = NPCTaskState.Working;
        OnNPCTaskChanged?.Invoke(npc);
        Debug.Log($"[NPCManager] {npc.Name} assigned to: {task}");
    }

    public NPCData GetNPC(string id) =>
        _npcLookup.TryGetValue(id, out var npc) ? npc : null;

    public void RegisterRoutine(NPCDailyRoutine routine)
    {
        if (routine == null || string.IsNullOrEmpty(routine.NpcId)) return;
        _routineCache[routine.NpcId] = routine;
    }

    public void UnregisterRoutine(NPCDailyRoutine routine)
    {
        if (routine == null || string.IsNullOrEmpty(routine.NpcId)) return;
        if (_routineCache.TryGetValue(routine.NpcId, out var cached) && cached == routine)
            _routineCache.Remove(routine.NpcId);
    }

    private NPCDailyRoutine FindNPCRoutine(string npcId)
    {
        return _routineCache.TryGetValue(npcId, out var cached) && cached != null ? cached : null;
    }

    public IReadOnlyList<NPCData> GetAllNPCs() => _npcs;

    // Reusable buffers to avoid per-call List allocations.
    private readonly List<NPCData> _availableBuffer = new();
    private readonly List<NPCData> _professionBuffer = new();

    public IReadOnlyList<NPCData> GetAvailableNPCs()
    {
        _availableBuffer.Clear();
        for (int i = 0; i < _npcs.Count; i++)
        {
            var n = _npcs[i];
            if (n.IsAvailable && n.TaskState == NPCTaskState.Idle) _availableBuffer.Add(n);
        }
        return _availableBuffer;
    }

    public IReadOnlyList<NPCData> GetNPCsByProfession(NPCPersona.NPCProfession profession)
    {
        _professionBuffer.Clear();
        for (int i = 0; i < _npcs.Count; i++)
        {
            if (_npcs[i].Profession == profession) _professionBuffer.Add(_npcs[i]);
        }
        return _professionBuffer;
    }

    public NPCSaveData[] GetSaveData()
    {
        var saves = new NPCSaveData[_npcs.Count];
        for (int i = 0; i < _npcs.Count; i++)
        {
            saves[i] = new NPCSaveData
            {
                Id = _npcs[i].Id,
                CurrentTask = _npcs[i].CurrentTask,
                MoodScore = _npcs[i].MoodScore,
                LoyaltyToLord = _npcs[i].LoyaltyToLord,
                WorldPosition = _npcs[i].WorldPosition
            };
        }
        return saves;
    }

    public void LoadSaveData(NPCSaveData[] saveData)
    {
        if (saveData == null) return;
        foreach (var save in saveData)
        {
            var npc = GetNPC(save.Id);
            if (npc != null)
            {
                npc.CurrentTask = save.CurrentTask;
                npc.MoodScore = save.MoodScore;
                npc.LoyaltyToLord = save.LoyaltyToLord;
                npc.WorldPosition = save.WorldPosition;
            }
        }
    }
}
