using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// M16-06 — Bottom-of-screen classic RPG dialogue box. Replaces the
/// full-screen card-grid NPCInteractionUI that the M16 pivot retires.
///
/// Layout (all programmatic — no scene YAML edit needed, so this lands
/// in the WebGL deploy as soon as CI builds a commit that references
/// DialogueBoxUI.Instance):
///
///   ┌────────────────────────────────────────────────────────┐
///   │ [portrait] [name]                                [X]  │
///   │            [message text, typewriter, word-wrapped]   │
///   │            [Q1][Q2][Q3][Q4]                           │
///   │            [________ input ________] [Send]           │
///   └────────────────────────────────────────────────────────┘
///
/// Anchored to the bottom of the screen, 90% width, 320 px tall. Text
/// uses the pastel palette + PinCenterRect anchor discipline from the
/// M16 UI overflow fix. Korean-safe: richText off on the message TMP so
/// the IL2CPP WebGL TMP parser bug can't fire.
///
/// Opens on E-press via InteractionFinder → DialogueBoxUI.Instance.Open(id).
/// </summary>
public class DialogueBoxUI : MonoBehaviour
{
    public static DialogueBoxUI Instance { get; private set; }

    // Cached UI references populated in BuildLayout.
    private GameObject       _root;
    private Image            _portrait;
    private TextMeshProUGUI  _nameLabel;
    private TextMeshProUGUI  _messageLabel;
    private Transform        _quickCmdRow;
    private TMP_InputField   _input;
    private Button           _sendBtn;
    private Button           _closeBtn;

    private string _currentNPCId;
    private bool   _isWaitingForResponse;
    private Coroutine _typewriter;

    // Quick command keys per profession — same table as the legacy UI so
    // LocalDialogueBank + Gemini keep seeing identical phrasing on both
    // sides during the pivot transition.
    private static readonly Dictionary<NPCPersona.NPCProfession, string[]> QuickCommandKeys = new()
    {
        { NPCPersona.NPCProfession.Farmer,   new[] { "quick_cmd_farmer_1",   "quick_cmd_farmer_2",   "quick_cmd_farmer_3",   "quick_cmd_farmer_4"   } },
        { NPCPersona.NPCProfession.Soldier,  new[] { "quick_cmd_soldier_1",  "quick_cmd_soldier_2",  "quick_cmd_soldier_3",  "quick_cmd_soldier_4"  } },
        { NPCPersona.NPCProfession.Merchant, new[] { "quick_cmd_merchant_1", "quick_cmd_merchant_2", "quick_cmd_merchant_3", "quick_cmd_merchant_4" } },
        { NPCPersona.NPCProfession.Vassal,   new[] { "quick_cmd_vassal_1",   "quick_cmd_vassal_2",   "quick_cmd_vassal_3",   "quick_cmd_vassal_4"   } },
    };

    /// <summary>
    /// Auto-install a singleton on scene load. The dialogue box lives on a
    /// dedicated overlay canvas so it always draws above the gameplay UI,
    /// regardless of what CastleView/TopHUD/etc. are doing.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (Instance != null) return;
        var host = new GameObject("DialogueBoxUI");
        host.AddComponent<DialogueBoxUI>();
        Object.DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildLayout();
        Hide();
    }

    // ─────────────────────────────────────────────────────────────
    //  LAYOUT CONSTRUCTION
    // ─────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var canvasGO = new GameObject("DialogueBoxCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // ── Main panel: frosted glass style ──
        _root = new GameObject("Box");
        _root.transform.SetParent(canvasGO.transform, false);
        var boxImg = _root.AddComponent<Image>();
        boxImg.color = new Color(0.95f, 0.95f, 0.97f, 0.95f); // Light frosted white
        var boxRT = _root.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.02f, 0.01f);
        boxRT.anchorMax = new Vector2(0.98f, 0.38f);
        boxRT.offsetMin = Vector2.zero;
        boxRT.offsetMax = Vector2.zero;

        // Subtle shadow
        var shadow = _root.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.3f);
        shadow.effectDistance = new Vector2(0f, -3f);

        // ── Header bar (name + close) ──
        var header = new GameObject("Header");
        header.transform.SetParent(_root.transform, false);
        var headerImg = header.AddComponent<Image>();
        headerImg.color = new Color(0.28f, 0.47f, 0.75f, 1f); // Modern blue
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.anchorMin = new Vector2(0f, 1f);
        headerRT.anchorMax = new Vector2(1f, 1f);
        headerRT.pivot = new Vector2(0.5f, 1f);
        headerRT.anchoredPosition = Vector2.zero;
        headerRT.sizeDelta = new Vector2(0f, 36f);

        // Name in header
        _nameLabel = CreateTMP(header.transform, "NameLabel", "",
            16, TextAlignmentOptions.MidlineLeft, Color.white, bold: true);
        var nameRT = _nameLabel.rectTransform;
        nameRT.anchorMin = Vector2.zero; nameRT.anchorMax = Vector2.one;
        nameRT.offsetMin = new Vector2(16f, 0f); nameRT.offsetMax = new Vector2(-50f, 0f);

        // Close button in header
        _closeBtn = CreateBtn(header.transform, "Close", "X",
            new Color(1f, 1f, 1f, 0.2f), 14f);
        var closeRT = _closeBtn.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 0f); closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 0.5f);
        closeRT.anchoredPosition = new Vector2(-4f, 0f);
        closeRT.sizeDelta = new Vector2(36f, 0f);
        _closeBtn.onClick.AddListener(Hide);

        // ── Content area (below header) ──
        float headerH = 36f;

        // Message bubble — left-aligned chat bubble style
        var bubbleGO = new GameObject("MessageBubble");
        bubbleGO.transform.SetParent(_root.transform, false);
        var bubbleImg = bubbleGO.AddComponent<Image>();
        bubbleImg.color = new Color(0.92f, 0.95f, 1f, 1f); // Light blue tint
        var bubbleRT = bubbleGO.GetComponent<RectTransform>();
        bubbleRT.anchorMin = new Vector2(0.01f, 0.42f);
        bubbleRT.anchorMax = new Vector2(0.99f, 1f);
        bubbleRT.offsetMin = new Vector2(6f, 4f);
        bubbleRT.offsetMax = new Vector2(-6f, -headerH - 4f);

        _messageLabel = CreateTMP(bubbleGO.transform, "Message", "",
            14, TextAlignmentOptions.TopLeft, new Color(0.15f, 0.15f, 0.20f));
        _messageLabel.overflowMode = TextOverflowModes.Ellipsis;
        Stretch(_messageLabel.gameObject, 12f, 6f, -12f, -6f);

        // ── Quick command pills ──
        var rowGO = new GameObject("QuickCommands");
        rowGO.transform.SetParent(_root.transform, false);
        var rowRT = rowGO.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0.01f, 0.22f);
        rowRT.anchorMax = new Vector2(0.99f, 0.42f);
        rowRT.offsetMin = new Vector2(6f, 2f);
        rowRT.offsetMax = new Vector2(-6f, -2f);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        hlg.padding = new RectOffset(2, 2, 2, 2);
        _quickCmdRow = rowGO.transform;

        // ── Input row ──
        var inputBgGO = new GameObject("InputBg");
        inputBgGO.transform.SetParent(_root.transform, false);
        var inputBg = inputBgGO.AddComponent<Image>();
        inputBg.color = new Color(0.93f, 0.93f, 0.95f, 1f); // Light gray
        var inRT = inputBg.rectTransform;
        inRT.anchorMin = new Vector2(0.01f, 0.01f);
        inRT.anchorMax = new Vector2(0.84f, 0.22f);
        inRT.offsetMin = new Vector2(6f, 4f);
        inRT.offsetMax = new Vector2(-4f, -2f);

        _input = inputBgGO.AddComponent<TMP_InputField>();
        var inputTxtGO = new GameObject("Text");
        inputTxtGO.transform.SetParent(inputBgGO.transform, false);
        var inputTMP = inputTxtGO.AddComponent<TextMeshProUGUI>();
        inputTMP.color = new Color(0.15f, 0.15f, 0.20f);
        inputTMP.fontSize = 13;
        inputTMP.richText = false;
        inputTMP.enableWordWrapping = false;
        Stretch(inputTxtGO, 10f, 4f, -10f, -4f);

        var phTxtGO = new GameObject("Placeholder");
        phTxtGO.transform.SetParent(inputBgGO.transform, false);
        var phTMP = phTxtGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = LocalizationManager.Instance?.Get("cmd_input_placeholder") ?? "명령을 입력하세요...";
        phTMP.color = new Color(0.55f, 0.55f, 0.60f, 0.7f);
        phTMP.fontSize = 13;
        phTMP.richText = false;
        phTMP.enableWordWrapping = false;
        Stretch(phTxtGO, 10f, 4f, -10f, -4f);

        _input.textComponent = inputTMP;
        _input.placeholder = phTMP;
        _input.onSubmit.AddListener(_ => OnSendClicked());

        // Send button — accent blue, force white text
        _sendBtn = CreateBtn(_root.transform, "Send",
            LocalizationManager.Instance?.Get("btn_send") ?? "보내기",
            new Color(0.28f, 0.47f, 0.75f), 13f);
        // Force white text on the send button label
        var sendLabel = _sendBtn.GetComponentInChildren<TextMeshProUGUI>();
        if (sendLabel != null) sendLabel.color = Color.white;
        var sendRT = _sendBtn.GetComponent<RectTransform>();
        sendRT.anchorMin = new Vector2(0.84f, 0.01f);
        sendRT.anchorMax = new Vector2(0.99f, 0.22f);
        sendRT.offsetMin = new Vector2(2f, 4f);
        sendRT.offsetMax = new Vector2(-6f, -2f);
        _sendBtn.onClick.AddListener(OnSendClicked);
    }

    private static void Stretch(GameObject go, float left, float bottom, float right, float top)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(right, top);
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS — anchor-safe widget creation
    // ─────────────────────────────────────────────────────────────

    private static void PinCenter(RectTransform rt, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI CreateTMP(Transform parent, string name, string text,
        int size, TextAlignmentOptions align, Color color, bool bold = false)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = true;
        tmp.richText = false;
        if (bold) tmp.fontStyle = FontStyles.Bold;
        PinCenter(tmp.rectTransform, Vector2.zero, new Vector2(100f, 30f));
        return tmp;
    }

    private static Button CreateBtn(Transform parent, string name, string label, Color bg, float fontSize = 12f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.95f, 1f);
        colors.pressedColor = new Color(0.8f, 0.85f, 0.9f);
        btn.colors = colors;
        PinCenter(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(100f, 32f));

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = bg.grayscale > 0.6f ? new Color(0.15f, 0.15f, 0.2f) : Color.white;
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        var txtRT = tmp.rectTransform;
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = new Vector2(4f, 0f); txtRT.offsetMax = new Vector2(-4f, 0f);
        return btn;
    }

    // ─────────────────────────────────────────────────────────────
    //  PUBLIC API
    // ─────────────────────────────────────────────────────────────

    public void Open(string npcId)
    {
        var npc = NPCManager.Instance?.GetNPC(npcId);
        if (npc == null) return;
        _currentNPCId = npcId;
        if (_root != null) _root.SetActive(true);

        if (_nameLabel != null)
            _nameLabel.text = $"{npc.Name} ({npc.Profession})";

        // Portrait: use Tiny Dungeon character tile
        if (_portrait != null)
        {
            // Map NPC to character tile (same mapping as RoamingBootstrap)
            string spritePath = npcId switch {
                "vassal_01"  => TinyTileset.TD_Knight,
                "soldier_01" => TinyTileset.TD_RedHair,
                "farmer_01"  => TinyTileset.TD_BrownHair,
                "merchant_01" => TinyTileset.TD_Archer,
                _ => TinyTileset.TD_Bard
            };
            var p = Resources.Load<Sprite>(spritePath);
            if (p != null) _portrait.sprite = p;
        }

        // Greeting line from LocalDialogueBank — same pre-generated EXAONE
        // bank the legacy UI used, so Korean-safe fallback still works.
        string greeting = LocalDialogueBank.GetRandom(
            npc.Profession, LocalDialogueBank.Context.Greeting);
        if (string.IsNullOrEmpty(greeting))
            greeting = LocalizationManager.Instance?.Get("npc_opened_conversation", npc.Name)
                    ?? $"You approach {npc.Name}.";
        PlayText(greeting);

        PopulateQuickCommands(npc.Profession);

        // Switch game state so the player controller locks input.
        GameManager.Instance?.SetGameState(GameManager.GameState.Dialogue);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
        _currentNPCId = null;
        if (_typewriter != null) { StopCoroutine(_typewriter); _typewriter = null; }
        // Restore gameplay state so the player controller unlocks input.
        if (GameManager.Instance != null
            && GameManager.Instance.CurrentState == GameManager.GameState.Dialogue)
            GameManager.Instance.SetGameState(GameManager.GameState.Castle);
    }

    // ─────────────────────────────────────────────────────────────
    //  COMMAND FLOW
    // ─────────────────────────────────────────────────────────────

    private void PopulateQuickCommands(NPCPersona.NPCProfession profession)
    {
        if (_quickCmdRow == null) return;
        foreach (Transform child in _quickCmdRow) Destroy(child.gameObject);
        if (!QuickCommandKeys.TryGetValue(profession, out var keys)) return;

        var loc = LocalizationManager.Instance;
        foreach (var key in keys)
        {
            string text = loc?.Get(key) ?? key;
            var btn = CreateBtn(_quickCmdRow, $"Q_{key}", text, new Color(0.88f, 0.91f, 0.96f), 11f);
            string captured = text;
            btn.onClick.AddListener(() => SendCommand(captured));
        }
    }

    private void OnSendClicked()
    {
        if (_input == null || string.IsNullOrWhiteSpace(_input.text)) return;
        string cmd = _input.text.Trim();
        _input.text = "";
        SendCommand(cmd);
        _input.ActivateInputField();
    }

    private void SendCommand(string command)
    {
        if (_isWaitingForResponse || string.IsNullOrEmpty(_currentNPCId)) return;
        _isWaitingForResponse = true;
        PlayText("...");

        // Try Gemini first, but if no API key, use LocalDialogueBank
        var npc = NPCManager.Instance?.GetNPC(_currentNPCId);
        NPCManager.Instance?.IssueCommandToNPC(_currentNPCId, command, response =>
        {
            _isWaitingForResponse = false;
            // If got the generic fallback, try local dialogue instead
            if (npc != null && (response.Contains("cannot answer") || response.Contains("대답할 수 없")))
            {
                string local = LocalDialogueBank.GetRandom(
                    npc.Profession, LocalDialogueBank.Context.Accept)
                    ?? LocalDialogueBank.GetRandom(npc.Profession, LocalDialogueBank.Context.Idle);
                if (!string.IsNullOrEmpty(local)) response = local;
            }
            PlayText(response);
        });
    }

    private void PlayText(string text)
    {
        if (_messageLabel == null) return;
        if (_typewriter != null) StopCoroutine(_typewriter);
        _typewriter = StartCoroutine(TypewriterEffect(text, 0.025f));
    }

    private IEnumerator TypewriterEffect(string full, float delay)
    {
        _messageLabel.text = "";
        foreach (char c in full)
        {
            _messageLabel.text += c;
            if (c == '.' || c == '!' || c == '?') yield return new WaitForSeconds(delay * 5);
            else yield return new WaitForSeconds(delay);
        }
    }
}
