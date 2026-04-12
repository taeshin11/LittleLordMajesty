using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Nation (Guild) panel UI.
/// Create/join nations, view members, chat, declare war or peace.
/// </summary>
public class NationUI : MonoBehaviour
{
    [Header("Top Info")]
    [SerializeField] private TextMeshProUGUI _nationNameText;
    [SerializeField] private TextMeshProUGUI _nationTagText;
    [SerializeField] private TextMeshProUGUI _memberCountText;
    [SerializeField] private TextMeshProUGUI _territoryCountText;

    [Header("No Nation State")]
    [SerializeField] private GameObject  _noNationPanel;
    [SerializeField] private TMP_InputField _createNameInput;
    [SerializeField] private TMP_InputField _createTagInput;
    [SerializeField] private Button      _createButton;
    [SerializeField] private TMP_InputField _joinIdInput;
    [SerializeField] private Button      _joinButton;

    [Header("Nation State")]
    [SerializeField] private GameObject  _nationPanel;
    [SerializeField] private Transform   _memberListContent;
    [SerializeField] private GameObject  _memberItemPrefab;

    [Header("Diplomacy")]
    [SerializeField] private Transform   _diplomacyListContent;
    [SerializeField] private GameObject  _diplomacyItemPrefab;
    [SerializeField] private Button      _declareWarButton;
    [SerializeField] private Button      _proposePeaceButton;
    [SerializeField] private Button      _proposeAllianceButton;
    [SerializeField] private TMP_Dropdown _targetNationDropdown;

    [Header("Chat")]
    [SerializeField] private ScrollRect  _chatScrollRect;
    [SerializeField] private Transform   _chatContent;
    [SerializeField] private GameObject  _chatMessagePrefab;
    [SerializeField] private TMP_InputField _chatInput;
    [SerializeField] private Button      _chatSendButton;

    [Header("Close")]
    [SerializeField] private Button _closeButton;

    private List<LordNetManager.NationData> _knownNations = new();

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        if (_createButton != null)  _createButton.onClick.AddListener(OnCreateNation);
        if (_joinButton != null)    _joinButton.onClick.AddListener(OnJoinNation);
        if (_chatSendButton != null)_chatSendButton.onClick.AddListener(OnSendChat);
        if (_closeButton != null)   _closeButton.onClick.AddListener(() => gameObject.SetActive(false));

        if (_chatInput != null)
            _chatInput.onSubmit.AddListener(_ => OnSendChat());

        if (_declareWarButton != null)   _declareWarButton.onClick.AddListener(() => SendDiplomacy("war"));
        if (_proposePeaceButton != null) _proposePeaceButton.onClick.AddListener(() => SendDiplomacy("peace"));
        if (_proposeAllianceButton != null)_proposeAllianceButton.onClick.AddListener(() => SendDiplomacy("alliance"));

        if (LordNetManager.Instance != null)
            LordNetManager.Instance.OnNationUpdated += RefreshNationPanel;
    }

    private void OnEnable()
    {
        RefreshUI();
        StartCoroutine(RefreshChatLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // ─────────────────────────────────────────────────────────────
    //  UI REFRESH
    // ─────────────────────────────────────────────────────────────

    private void RefreshUI()
    {
        bool inNation = !string.IsNullOrEmpty(LordNetManager.Instance?.LocalNationId);
        if (_noNationPanel != null) _noNationPanel.SetActive(!inNation);
        if (_nationPanel != null)   _nationPanel.SetActive(inNation);
    }

    private void RefreshNationPanel(LordNetManager.NationData nation)
    {
        RefreshUI();
        if (_nationNameText != null)  _nationNameText.text  = nation.Name;
        if (_nationTagText != null)   _nationTagText.text   = $"[{nation.Tag}]";
        if (_memberCountText != null) _memberCountText.text = $"{nation.MemberIds?.Count ?? 0} Lords";

        RefreshMemberList(nation);
    }

    private void RefreshMemberList(LordNetManager.NationData nation)
    {
        if (_memberListContent == null || _memberItemPrefab == null) return;

        foreach (Transform child in _memberListContent) Destroy(child.gameObject);

        var worldPlayers = LordNetManager.Instance?.GetAllWorldPlayers();
        if (worldPlayers == null || nation.MemberIds == null) return;

        foreach (var memberId in nation.MemberIds)
        {
            var player = worldPlayers.Find(p => p.PlayerId == memberId);
            string name = player?.PlayerName ?? memberId;
            bool online = player?.IsOnline ?? false;

            var item = Instantiate(_memberItemPrefab, _memberListContent);
            var nameTxt = item.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var statusDot = item.transform.Find("Status")?.GetComponent<Image>();
            var territoriesTxt = item.transform.Find("Territories")?.GetComponent<TextMeshProUGUI>();

            if (nameTxt != null)       nameTxt.text = name;
            if (statusDot != null)     statusDot.color = online ? new Color(0.2f, 0.9f, 0.3f) : Color.gray;
            if (territoriesTxt != null) territoriesTxt.text = $"{player?.TerritoryCount ?? 0} territories";
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  ACTIONS
    // ─────────────────────────────────────────────────────────────

    private void OnCreateNation()
    {
        string name = _createNameInput?.text?.Trim() ?? "";
        string tag  = _createTagInput?.text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(tag)) return;

        LordNetManager.Instance?.CreateNation(name, tag, nation =>
        {
            RefreshNationPanel(nation);
            ToastNotification.Show($"Nation [{nation.Tag}] {nation.Name} founded!");
        });
    }

    private void OnJoinNation()
    {
        string nationId = _joinIdInput?.text?.Trim() ?? "";
        if (string.IsNullOrEmpty(nationId)) return;

        LordNetManager.Instance?.JoinNation(nationId, nation =>
        {
            RefreshNationPanel(nation);
            ToastNotification.Show($"Joined [{nation.Tag}] {nation.Name}!");
        });
    }

    private void OnSendChat()
    {
        if (_chatInput == null || string.IsNullOrWhiteSpace(_chatInput.text)) return;
        string msg = _chatInput.text.Trim();
        _chatInput.text = "";
        LordNetManager.Instance?.SendNationChat(msg);
        _chatInput.ActivateInputField();
    }

    private IEnumerator RefreshChatLoop()
    {
        while (true)
        {
            string nationId = LordNetManager.Instance?.LocalNationId;
            if (!string.IsNullOrEmpty(nationId))
            {
                yield return LordNetManager.Instance.FetchNationChat(nationId, messages =>
                {
                    if (messages != null) PopulateChat(messages);
                });
            }
            yield return new WaitForSeconds(5f);
        }
    }

    private void PopulateChat(List<LordNetManager.ChatMessage> messages)
    {
        if (_chatContent == null || _chatMessagePrefab == null) return;
        foreach (Transform child in _chatContent) Destroy(child.gameObject);

        messages.Sort((a, b) => a.TimestampUtc.CompareTo(b.TimestampUtc));
        foreach (var msg in messages)
        {
            var item = Instantiate(_chatMessagePrefab, _chatContent);
            var tmp = item.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null)
            {
                bool isLocal = msg.PlayerId == LordNetManager.Instance?.LocalPlayerId;
                tmp.text = isLocal
                    ? $"<color=#88aaff><b>You:</b></color> {msg.Message}"
                    : $"<color=#ffaa44><b>{msg.PlayerName}:</b></color> {msg.Message}";
            }
        }

        // Scroll to bottom
        Canvas.ForceUpdateCanvases();
        if (_chatScrollRect != null)
            _chatScrollRect.normalizedPosition = Vector2.zero;
    }

    private void SendDiplomacy(string proposal)
    {
        // Target nation selected from dropdown
        if (_targetNationDropdown == null || _knownNations.Count == 0) return;
        int idx = _targetNationDropdown.value;
        if (idx >= _knownNations.Count) return;

        var targetNation = _knownNations[idx];
        string message = proposal switch
        {
            "war"       => $"Lord {LordNetManager.Instance?.LocalPlayerName} declares war upon your nation!",
            "peace"     => $"Lord {LordNetManager.Instance?.LocalPlayerName} proposes a peace treaty.",
            "alliance"  => $"Lord {LordNetManager.Instance?.LocalPlayerName} proposes a formal alliance.",
            _           => proposal
        };

        // Send to nation leader
        LordNetManager.Instance?.SendDiplomaticMessage(
            targetNation.LeaderId, message, proposal,
            () => ToastNotification.Show($"Diplomatic message sent to [{targetNation.Tag}]"));
    }

    private void OnDestroy()
    {
        if (LordNetManager.Instance != null)
            LordNetManager.Instance.OnNationUpdated -= RefreshNationPanel;
    }
}
