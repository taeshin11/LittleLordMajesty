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
        // Overlay canvas
        var canvasGO = new GameObject("DialogueBoxCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(960, 600);
        scaler.matchWidthOrHeight  = 1f; // match height for landscape WebGL
        canvasGO.AddComponent<GraphicRaycaster>();

        // Box: stretch to bottom, 5% margin each side, 40% screen height
        _root = new GameObject("Box");
        _root.transform.SetParent(canvasGO.transform, false);
        var boxImg = _root.AddComponent<Image>();
        boxImg.color = new Color(0.12f, 0.10f, 0.08f, 0.92f); // Dark parchment
        var boxRT = _root.GetComponent<RectTransform>();
        boxRT.anchorMin = new Vector2(0.03f, 0f);
        boxRT.anchorMax = new Vector2(0.97f, 0.40f);
        boxRT.offsetMin = new Vector2(0f, 8f);
        boxRT.offsetMax = new Vector2(0f, 0f);
        var outline = _root.AddComponent<Outline>();
        outline.effectColor = new Color(0.6f, 0.5f, 0.2f);
        outline.effectDistance = new Vector2(2f, -2f);

        // Portrait: anchored to left, square, with padding
        var portraitGO = new GameObject("Portrait");
        portraitGO.transform.SetParent(_root.transform, false);
        _portrait = portraitGO.AddComponent<Image>();
        _portrait.color = new Color(0.88f, 0.82f, 0.98f);
        _portrait.preserveAspect = true;
        var pRT = _portrait.rectTransform;
        pRT.anchorMin = new Vector2(0f, 0f);
        pRT.anchorMax = new Vector2(0f, 1f);
        pRT.pivot = new Vector2(0f, 0.5f);
        pRT.anchoredPosition = new Vector2(8f, 0f);
        pRT.sizeDelta = new Vector2(140f, -16f); // width fixed, height stretches with padding

        // Right content area starts after portrait (x=156)
        float contentLeft = 156f;

        // Close button (top-right)
        _closeBtn = CreateBtn(_root.transform, "CloseButton",
            LocalizationManager.Instance?.Get("btn_close") ?? "X",
            new Color(0.6f, 0.2f, 0.2f));
        var closeRT = _closeBtn.GetComponent<RectTransform>();
        closeRT.anchorMin = new Vector2(1f, 1f);
        closeRT.anchorMax = new Vector2(1f, 1f);
        closeRT.pivot = new Vector2(1f, 1f);
        closeRT.anchoredPosition = new Vector2(-8f, -8f);
        closeRT.sizeDelta = new Vector2(50f, 30f);
        _closeBtn.onClick.AddListener(Hide);

        // Name label (top of content area)
        _nameLabel = CreateTMP(_root.transform, "NameLabel", "NPC Name",
            20, TextAlignmentOptions.Left, new Color(0.95f, 0.85f, 0.5f), bold: true);
        var nameRT = _nameLabel.rectTransform;
        nameRT.anchorMin = new Vector2(0f, 1f);
        nameRT.anchorMax = new Vector2(1f, 1f);
        nameRT.pivot = new Vector2(0f, 1f);
        nameRT.anchoredPosition = new Vector2(contentLeft, -8f);
        nameRT.sizeDelta = new Vector2(-contentLeft - 70f, 28f);

        // Message text (below name, fills available space)
        _messageLabel = CreateTMP(_root.transform, "MessageLabel", "",
            16, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.80f, 0.70f));
        _messageLabel.overflowMode = TextOverflowModes.Ellipsis;
        var msgRT = _messageLabel.rectTransform;
        msgRT.anchorMin = new Vector2(0f, 0.45f);
        msgRT.anchorMax = new Vector2(1f, 1f);
        msgRT.offsetMin = new Vector2(contentLeft, 0f);
        msgRT.offsetMax = new Vector2(-12f, -38f);

        // Quick command row (bottom-middle area)
        var rowGO = new GameObject("QuickCommands");
        rowGO.transform.SetParent(_root.transform, false);
        var rowRT = rowGO.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0f, 0.2f);
        rowRT.anchorMax = new Vector2(1f, 0.45f);
        rowRT.offsetMin = new Vector2(contentLeft, 2f);
        rowRT.offsetMax = new Vector2(-12f, -2f);
        var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;
        _quickCmdRow = rowGO.transform;

        // Input field + Send button row (bottom)
        var inputBgGO = new GameObject("InputBg");
        inputBgGO.transform.SetParent(_root.transform, false);
        var inputBg = inputBgGO.AddComponent<Image>();
        inputBg.color = new Color(0.25f, 0.22f, 0.18f);
        var inRT = inputBg.rectTransform;
        inRT.anchorMin = new Vector2(0f, 0f);
        inRT.anchorMax = new Vector2(0.82f, 0.2f);
        inRT.offsetMin = new Vector2(contentLeft, 4f);
        inRT.offsetMax = new Vector2(-4f, -2f);

        _input = inputBgGO.AddComponent<TMP_InputField>();
        var inputTxtGO = new GameObject("Text");
        inputTxtGO.transform.SetParent(inputBgGO.transform, false);
        var inputTMP = inputTxtGO.AddComponent<TextMeshProUGUI>();
        inputTMP.color = new Color(0.9f, 0.85f, 0.75f);
        inputTMP.fontSize = 14;
        inputTMP.richText = false;
        inputTMP.enableWordWrapping = false;
        Stretch(inputTxtGO, 10f, 4f, -10f, -4f);

        var phTxtGO = new GameObject("Placeholder");
        phTxtGO.transform.SetParent(inputBgGO.transform, false);
        var phTMP = phTxtGO.AddComponent<TextMeshProUGUI>();
        phTMP.text = LocalizationManager.Instance?.Get("cmd_input_placeholder") ?? "Issue a command...";
        phTMP.color = new Color(0.55f, 0.50f, 0.40f, 0.7f);
        phTMP.fontSize = 14;
        phTMP.richText = false;
        phTMP.enableWordWrapping = false;
        Stretch(phTxtGO, 10f, 4f, -10f, -4f);

        _input.textComponent = inputTMP;
        _input.placeholder   = phTMP;
        _input.onSubmit.AddListener(_ => OnSendClicked());

        // Send button
        _sendBtn = CreateBtn(_root.transform, "SendButton",
            LocalizationManager.Instance?.Get("btn_send") ?? "Send",
            new Color(0.25f, 0.50f, 0.30f));
        var sendRT = _sendBtn.GetComponent<RectTransform>();
        sendRT.anchorMin = new Vector2(0.82f, 0f);
        sendRT.anchorMax = new Vector2(1f, 0.2f);
        sendRT.offsetMin = new Vector2(2f, 4f);
        sendRT.offsetMax = new Vector2(-8f, -2f);
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

    private static Button CreateBtn(Transform parent, string name, string label, Color bg, float fontSize = 13f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bg;
        var btn = go.AddComponent<Button>();
        // Modern look: no outline, subtle color shift on hover
        var colors = btn.colors;
        colors.highlightedColor = new Color(bg.r + 0.1f, bg.g + 0.1f, bg.b + 0.1f);
        colors.pressedColor = new Color(bg.r - 0.1f, bg.g - 0.1f, bg.b - 0.1f);
        btn.colors = colors;
        PinCenter(go.GetComponent<RectTransform>(), Vector2.zero, new Vector2(100f, 40f));

        var txtGO = new GameObject("Label");
        txtGO.transform.SetParent(go.transform, false);
        var tmp = txtGO.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
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

        // Portrait: prefer the generated SDXL portrait, fall back to a
        // procedurally generated placeholder portrait.
        if (_portrait != null)
        {
            var p = Resources.Load<Sprite>($"Art/Generated/portrait_{npcId}");
            if (p == null)
                p = PlaceholderArtGenerator.GetNPCPortrait(npc.Profession, npc.Name);
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
