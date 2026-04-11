using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Rich NPC interaction panel with conversation history, mood display, and command shortcuts.
/// Modern chat-style UI with medieval theme.
/// </summary>
public class NPCInteractionUI : MonoBehaviour
{
    [Header("NPC Info Bar")]
    [SerializeField] private Image _npcAvatar;
    [SerializeField] private TextMeshProUGUI _npcName;
    [SerializeField] private TextMeshProUGUI _npcProfession;
    [SerializeField] private Slider _moodSlider;
    [SerializeField] private Image _moodFill;
    [SerializeField] private TextMeshProUGUI _loyaltyText;
    [SerializeField] private TextMeshProUGUI _currentTaskText;

    [Header("Chat Window")]
    [SerializeField] private ScrollRect _chatScrollRect;
    [SerializeField] private Transform _chatContentParent;
    [SerializeField] private GameObject _playerMessagePrefab;
    [SerializeField] private GameObject _npcMessagePrefab;
    [SerializeField] private GameObject _thinkingBubblePrefab;

    [Header("Input Bar")]
    [SerializeField] private TMP_InputField _commandInput;
    [SerializeField] private Button _sendButton;
    [SerializeField] private Button _closeButton;

    [Header("Quick Commands")]
    [SerializeField] private Transform _quickCommandsParent;
    [SerializeField] private GameObject _quickCommandButtonPrefab;

    [Header("TTS Toggle")]
    [SerializeField] private Toggle _ttsToggle;
    [SerializeField] private TextMeshProUGUI _ttsLabel;

    private string _currentNPCId;
    private bool _isWaitingForResponse;
    private GameObject _thinkingBubble;

    // Quick command template KEYS per profession — resolved via LocalizationManager at display time.
    // Gemini handles any language, so the localized text is what we send as the command.
    private static readonly Dictionary<NPCPersona.NPCProfession, string[]> QuickCommandKeys = new()
    {
        { NPCPersona.NPCProfession.Farmer,   new[] { "quick_cmd_farmer_1",   "quick_cmd_farmer_2",   "quick_cmd_farmer_3",   "quick_cmd_farmer_4"   } },
        { NPCPersona.NPCProfession.Soldier,  new[] { "quick_cmd_soldier_1",  "quick_cmd_soldier_2",  "quick_cmd_soldier_3",  "quick_cmd_soldier_4"  } },
        { NPCPersona.NPCProfession.Merchant, new[] { "quick_cmd_merchant_1", "quick_cmd_merchant_2", "quick_cmd_merchant_3", "quick_cmd_merchant_4" } },
        { NPCPersona.NPCProfession.Vassal,   new[] { "quick_cmd_vassal_1",   "quick_cmd_vassal_2",   "quick_cmd_vassal_3",   "quick_cmd_vassal_4"   } },
    };

    private void Start()
    {
        if (_sendButton != null) _sendButton.onClick.AddListener(OnSendClicked);
        if (_closeButton != null) _closeButton.onClick.AddListener(OnCloseClicked);
        if (_commandInput != null)
            _commandInput.onSubmit.AddListener(_ => OnSendClicked());
        if (_ttsToggle != null)
        {
            bool ttsEnabled = PlayerPrefs.GetInt("TTS_Enabled", 1) == 1;
            _ttsToggle.isOn = ttsEnabled;
            _ttsToggle.onValueChanged.AddListener(OnTTSToggled);
        }
    }

    public void OpenForNPC(string npcId)
    {
        _currentNPCId = npcId;
        var npc = NPCManager.Instance?.GetNPC(npcId);
        if (npc == null) return;

        gameObject.SetActive(true);
        PopulateNPCInfo(npc);
        PopulateQuickCommands(npc.Profession);
        RequestPortrait(npc);
        ClearChat();

        // Welcome message
        AddSystemMessage(LocalizationManager.Instance?.Get("npc_opened_conversation", npc.Name)
            ?? $"You approach {npc.Name}...");

        // Pre-generated greeting from EXAONE-built dialogue bank — zero API
        // cost, instant feedback. Falls through to silence if the bank
        // doesn't have a line for this profession (e.g. Blacksmith).
        var greeting = LocalDialogueBank.GetRandom(npc.Profession, LocalDialogueBank.Context.Greeting);
        if (!string.IsNullOrEmpty(greeting))
            AddNPCMessage(greeting);

        _commandInput?.ActivateInputField();
    }

    /// <summary>
    /// Fire-and-forget portrait generation via Gemini 2.5 Flash Image. Served from disk
    /// cache on repeat calls (zero API cost), generated once otherwise. The Image.sprite
    /// swaps in when ready; the placeholder avatar stays visible until then.
    /// </summary>
    private void RequestPortrait(NPCManager.NPCData npc)
    {
        if (_npcAvatar == null) return;

        // First try the offline-baked SDXL Turbo portrait (zero API cost).
        // tools/image_gen/generate.py builds portrait_<npc_id>.png into
        // Resources/Art/Generated/.
        var local = LocalArtBank.GetNPCPortrait(npc.Id);
        if (local != null)
        {
            _npcAvatar.sprite = local;
            _npcAvatar.preserveAspect = true;
            return;
        }

        if (GeminiImageClient.Instance == null) return;

        // Build a deterministic prompt per NPC so the hash stays stable across sessions
        // and the same NPC always resolves from cache after first generation.
        string professionKey = NPCManager.GetProfessionLocKey(npc.Profession);
        string professionLoc = LocalizationManager.Instance?.Get(professionKey) ?? npc.Profession.ToString();
        string prompt =
            $"Single solo cute chibi character portrait of {npc.Name}, one figure only, a {npc.Profession} " +
            $"in a tiny medieval kingdom. Big head small body, oversized round eyes, pastel colors, " +
            $"thick clean outlines, soft cel-shaded toon shading, Zelda Echoes of Wisdom inspired, " +
            $"Nintendo soft palette, plain pastel mint background, centered front view. Character ID: {npc.Id}.";

        // Cache the target Image so we don't swap the wrong portrait if the user closes
        // the panel and opens a different NPC while generation is in flight.
        var targetImage = _npcAvatar;
        string requestedNpcId = npc.Id;

        GeminiImageClient.Instance.GenerateImage(prompt,
            onSuccess: tex =>
            {
                // Guard: only apply if the user is still viewing the same NPC.
                if (_currentNPCId != requestedNpcId || targetImage == null) return;
                var sprite = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f));
                targetImage.sprite = sprite;
                targetImage.color = Color.white; // clear any placeholder tint
            },
            onError: err => Debug.LogWarning($"[NPCPortrait] Generation failed for {npc.Name}: {err}"));
    }

    private void PopulateNPCInfo(NPCManager.NPCData npc)
    {
        if (_npcName != null)
            _npcName.text = npc.Name;

        if (_npcProfession != null)
            _npcProfession.text = LocalizationManager.Instance?.Get(NPCManager.GetProfessionLocKey(npc.Profession))
                                  ?? npc.Profession.ToString();

        if (_moodSlider != null) _moodSlider.value = npc.MoodScore / 100f;

        if (_moodFill != null)
        {
            _moodFill.color = npc.MoodScore > 60 ? new Color(0.2f, 0.8f, 0.3f)
                : npc.MoodScore > 30 ? new Color(0.9f, 0.7f, 0.1f)
                : new Color(0.9f, 0.2f, 0.1f);
        }

        if (_loyaltyText != null)
            _loyaltyText.text = $"Loyalty {npc.LoyaltyToLord}/100";

        if (_currentTaskText != null)
            _currentTaskText.text = string.IsNullOrEmpty(npc.CurrentTask)
                ? LocalizationManager.Instance?.Get("npc_idle") ?? "Idle"
                : npc.CurrentTask;
    }

    private void PopulateQuickCommands(NPCPersona.NPCProfession profession)
    {
        if (_quickCommandsParent == null || _quickCommandButtonPrefab == null) return;

        // Clear existing
        foreach (Transform child in _quickCommandsParent)
            Destroy(child.gameObject);

        if (!QuickCommandKeys.TryGetValue(profession, out var keys)) return;

        var loc = LocalizationManager.Instance;
        foreach (var key in keys)
        {
            string localizedCmd = loc?.Get(key) ?? key;
            var btn = Instantiate(_quickCommandButtonPrefab, _quickCommandsParent);
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = localizedCmd;

            var button = btn.GetComponent<Button>();
            string capturedCmd = localizedCmd;
            button?.onClick.AddListener(() => SendCommand(capturedCmd));
        }
    }

    private void OnSendClicked()
    {
        if (_commandInput == null || string.IsNullOrWhiteSpace(_commandInput.text)) return;
        string cmd = _commandInput.text.Trim();
        _commandInput.text = "";
        SendCommand(cmd);
        _commandInput.ActivateInputField();
    }

    private void SendCommand(string command)
    {
        if (_isWaitingForResponse) return;

        AddPlayerMessage(command);
        SetInputEnabled(false);
        ShowThinking(true);

        NPCManager.Instance?.IssueCommandToNPC(_currentNPCId, command, response =>
        {
            ShowThinking(false);
            SetInputEnabled(true);
            AddNPCMessage(response);
            RefreshNPCInfo();
        });
    }

    private void AddPlayerMessage(string text)
    {
        if (_playerMessagePrefab == null) return;
        var msg = Instantiate(_playerMessagePrefab, _chatContentParent);
        var tmp = msg.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) tmp.text = text;
        ScrollToBottom();
    }

    private void AddNPCMessage(string text)
    {
        if (_npcMessagePrefab == null) return;
        var npc = NPCManager.Instance?.GetNPC(_currentNPCId);
        var msg = Instantiate(_npcMessagePrefab, _chatContentParent);
        var tmp = msg.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null) StartCoroutine(TypewriterEffect(tmp, text, 0.025f));
        ScrollToBottom();
    }

    private void AddSystemMessage(string text)
    {
        // Use NPC bubble with italic style for system messages
        AddNPCMessage($"<i><color=#aaaaaa>{text}</color></i>");
    }

    private IEnumerator TypewriterEffect(TextMeshProUGUI tmp, string fullText, float delay)
    {
        tmp.text = "";
        foreach (char c in fullText)
        {
            tmp.text += c;
            if (c == '.' || c == '!' || c == '?') yield return new WaitForSeconds(delay * 5);
            else yield return new WaitForSeconds(delay);
        }
        ScrollToBottom();
    }

    private void ShowThinking(bool show)
    {
        _isWaitingForResponse = show;
        if (show && _thinkingBubblePrefab != null)
        {
            _thinkingBubble = Instantiate(_thinkingBubblePrefab, _chatContentParent);
            ScrollToBottom();
        }
        else if (!show && _thinkingBubble != null)
        {
            Destroy(_thinkingBubble);
            _thinkingBubble = null;
        }
    }

    private void SetInputEnabled(bool enabled)
    {
        if (_commandInput != null) _commandInput.interactable = enabled;
        if (_sendButton != null) _sendButton.interactable = enabled;
    }

    private void RefreshNPCInfo()
    {
        var npc = NPCManager.Instance?.GetNPC(_currentNPCId);
        if (npc != null) PopulateNPCInfo(npc);
    }

    private void ClearChat()
    {
        if (_chatContentParent == null) return;
        foreach (Transform child in _chatContentParent)
            Destroy(child.gameObject);
    }

    private void ScrollToBottom()
    {
        if (_chatScrollRect == null) return;
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null; // Wait for layout rebuild
        _chatScrollRect.normalizedPosition = new Vector2(0, 0);
    }

    private void OnTTSToggled(bool enabled)
    {
        PlayerPrefs.SetInt("TTS_Enabled", enabled ? 1 : 0);
        TTSManager.Instance?.SetEnabled(enabled);
        if (_ttsLabel != null)
            _ttsLabel.text = LocalizationManager.Instance?.Get(enabled ? "tts_on" : "tts_off") ?? (enabled ? "Voice ON" : "Voice OFF");
    }

    private void OnCloseClicked()
    {
        TTSManager.Instance?.StopSpeaking();
        gameObject.SetActive(false);
        UIManager.Instance?.CloseDialogue();
    }

    private void OnDestroy()
    {
        if (_sendButton != null) _sendButton.onClick.RemoveAllListeners();
        if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
        if (_commandInput != null)
        {
            _commandInput.onSubmit.RemoveAllListeners();
            _commandInput.onValueChanged.RemoveAllListeners();
        }
        if (_ttsToggle != null) _ttsToggle.onValueChanged.RemoveAllListeners();
    }
}
