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

    // Quick command templates per profession
    private static readonly Dictionary<NPCPersona.NPCProfession, string[]> QuickCommands = new()
    {
        { NPCPersona.NPCProfession.Farmer, new[] { "Harvest crops", "Plant new fields", "Check food stores", "How is the harvest?" } },
        { NPCPersona.NPCProfession.Soldier, new[] { "Train the troops", "Patrol the walls", "Report on threats", "Are you ready for battle?" } },
        { NPCPersona.NPCProfession.Merchant, new[] { "What goods do you have?", "Trade report", "Buy supplies", "Negotiate a deal" } },
        { NPCPersona.NPCProfession.Vassal, new[] { "Castle status report", "Advise me", "Handle the dispute", "What troubles you?" } },
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
        ClearChat();

        // Welcome message
        AddSystemMessage(LocalizationManager.Instance?.Get("npc_opened_conversation", npc.Name)
            ?? $"You approach {npc.Name}...");

        _commandInput?.ActivateInputField();
    }

    private void PopulateNPCInfo(NPCManager.NPCData npc)
    {
        if (_npcName != null)
            _npcName.text = npc.Name;

        if (_npcProfession != null)
            _npcProfession.text = LocalizationManager.Instance?.Get($"profession_{npc.Profession.ToString().ToLower()}")
                                  ?? npc.Profession.ToString();

        if (_moodSlider != null) _moodSlider.value = npc.MoodScore / 100f;

        if (_moodFill != null)
        {
            _moodFill.color = npc.MoodScore > 60 ? new Color(0.2f, 0.8f, 0.3f)
                : npc.MoodScore > 30 ? new Color(0.9f, 0.7f, 0.1f)
                : new Color(0.9f, 0.2f, 0.1f);
        }

        if (_loyaltyText != null)
            _loyaltyText.text = $"♥ {npc.LoyaltyToLord}/100";

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

        if (!QuickCommands.TryGetValue(profession, out var commands)) return;

        foreach (var cmd in commands)
        {
            var btn = Instantiate(_quickCommandButtonPrefab, _quickCommandsParent);
            var btnText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null) btnText.text = cmd;

            var button = btn.GetComponent<Button>();
            string capturedCmd = cmd;
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
}
