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
        // Overlay canvas at a very high sorting order so we're always on top.
        var canvasGO = new GameObject("DialogueBoxCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Box anchored to bottom-center, 90% width, 320 px tall.
        _root = new GameObject("Box");
        _root.transform.SetParent(canvasGO.transform, false);
        var boxImg = _root.AddComponent<Image>();
        boxImg.color = new Color(1.00f, 0.96f, 0.88f, 0.98f); // Pastel cream
        var boxRT = _root.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.5f, 0f);
        boxRT.anchorMax = new Vector2(0.5f, 0f);
        boxRT.pivot     = new Vector2(0.5f, 0f);
        boxRT.anchoredPosition = new Vector2(0f, 24f);
        boxRT.sizeDelta = new Vector2(980f, 320f);
        var outline = _root.AddComponent<Outline>();
        outline.effectColor = new Color(0.8f, 0.65f, 0.2f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Portrait frame (left).
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(_root.transform, false);
        _portrait = portraitGO.AddComponent<Image>();
        _portrait.color = new Color(0.88f, 0.82f, 0.98f); // Lavender placeholder
        PinCenter(_portrait.rectTransform, new Vector2(-380f, 0f), new Vector2(220f, 280f));

        // Name label (top-right of portrait).
        _nameLabel = CreateTMP(_root.transform, "NameLabel", "NPC Name",
            36, TextAlignmentOptions.Left, new Color(0.25f, 0.15f, 0.08f), bold: true);
        PinCenter(_nameLabel.rectTransform, new Vector2(50f, 115f), new Vector2(600f, 50f));

        // Message text (large, word-wrapped, typewriter).
        _messageLabel = CreateTMP(_root.transform, "MessageLabel", "",
            26, TextAlignmentOptions.TopLeft, new Color(0.40f, 0.28f, 0.18f));
        _messageLabel.overflowMode = TextOverflowModes.Ellipsis;
        PinCenter(_messageLabel.rectTransform, new Vector2(50f, 30f), new Vector2(700f, 130f));

        // Quick command row (4 buttons).
        var rowGO = new GameObject("QuickCommands");
        rowGO.transform.SetParent(_root.transform, false);
        var rowRT = rowGO.AddComponent<RectTransform>();
        rowRT.anchorMin = rowRT.anchorMax = new Vector2(0.5f, 0.5f);
        rowRT.pivot     = new Vector2(0.5f, 0.5f);
        rowRT.anchoredPosition = new Vector2(50f, -70f);
        rowRT.sizeDelta = new Vector2(720f, 60f);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        _quickCmdRow = rowGO.transform;

        // Input field (bottom, next to Send).
        var inputBgGO = new GameObject("InputBg");
        inputBgGO.transform.SetParent(_root.transform, false);
        var inputBg = inputBgGO.AddComponent<Image>();
        inputBg.color = new Color(0.94f, 0.88f, 0.78f); // Pale cream field
        PinCenter(inputBg.rectTransform, new Vector2(0f, -130f), new Vector2(620f, 58f));

        _input = inputBgGO.AddComponent<TMP_InputField>();
        // InputField needs child "Text Area" + "Text" children. Simplest:
        // create the Text and PlaceholderText children, leave Text Area null
        // (TMP_InputField supports a flat layout at the cost of clipping,
        // which is fine for a single-line input).
        var inputTxtGO = new GameObject("Text");
        inputTxtGO.transform.SetParent(inputBgGO.transform, false);
        var inputTMP = inputTxtGO.AddComponent<TextMeshProUGUI>();
        inputTMP.color = new Color(0.25f, 0.15f, 0.08f);
        inputTMP.fontSize = 24;
        inputTMP.richText = false;
        inputTMP.enableWordWrapping = false;
        var inputTxtRT = inputTxtGO.GetComponent<RectTransform>();
        inputTxtRT.anchorMin = new Vector2(0, 0); inputTxtRT.anchorMax = new Vector2(1, 1);
        inputTxtRT.offsetMin = new Vector2(16, 6); inputTxtRT.offsetMax = new Vector2(-16, -6);

        var phTxtGO = new GameObject("Placeholder");
        phTxtGO.transform.SetParent(inputBgGO.transform, false);
        var phTMP = phTxtGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = LocalizationManager.Instance?.Get("cmd_input_placeholder") ?? "Issue a command...";
        phTMP.color = new Color(0.55f, 0.45f, 0.35f, 0.7f);
        phTMP.fontSize = 24;
        phTMP.richText = false;
        phTMP.enableWordWrapping = false;
        var phRT = phTxtGO.GetComponent<RectTransform>();
        phRT.anchorMin = new Vector2(0, 0); phRT.anchorMax = new Vector2(1, 1);
        phRT.offsetMin = new Vector2(16, 6); phRT.offsetMax = new Vector2(-16, -6);

        _input.textComponent = inputTMP;
        _input.placeholder   = phTMP;
        _input.onSubmit.AddListener(_ => OnSendClicked());

        // Send button.
        _sendBtn = CreateBtn(_root.transform, "SendButton",
            LocalizationManager.Instance?.Get("btn_send") ?? "Send",
            new Color(0.72f, 0.92f, 0.78f));
        PinCenter(_sendBtn.GetComponent<RectTransform>(), new Vector2(410f, -130f), new Vector2(140f, 58f));
        _sendBtn.onClick.AddListener(OnSendClicked);

        // Close button (top-right).
        _closeBtn = CreateBtn(_root.transform, "CloseButton",
            LocalizationManager.Instance?.Get("btn_close") ?? "Close",
            new Color(0.98f, 0.75f, 0.75f));
        PinCenter(_closeBtn.GetComponent<RectTransform>(), new Vector2(430f, 115f), new Vector2(100f, 50f));
        _closeBtn.onClick.AddListener(Hide);
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

    private static Button CreateBtn(Transform parent, string name, string label, Color bg)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.45f, 0.28f, 0.18f);
        outline.effectDistance = new Vector2(2f, -2f);
        PinCenter(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(100f, 40f));

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 22;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.25f, 0.15f, 0.08f);
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        var txtRT = tmp.rectTransform;
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;
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

        // Portrait: prefer the generated SDXL portrait, fall back to the
        // front-facing sprite, then a colored placeholder.
        if (_portrait != null)
        {
            var p = Resources.Load<Sprite>($"Art/Generated/portrait_{npcId}")
                 ?? Resources.Load<Sprite>($"Art/Sprites/{npcId}_s_0");
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
            var btn = CreateBtn(_quickCmdRow, $"Q_{key}", text, new Color(0.80f, 0.85f, 0.98f));
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
        NPCManager.Instance?.IssueCommandToNPC(_currentNPCId, command, response =>
        {
            _isWaitingForResponse = false;
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
