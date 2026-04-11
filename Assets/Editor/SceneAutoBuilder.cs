using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;

/// <summary>
/// One-click scene builder. Run "LittleLordMajesty > Build All Scenes" after opening the project.
/// Creates Bootstrap.unity and Game.unity with full hierarchy and wired references.
/// </summary>
public static class SceneAutoBuilder
{
    private const string SCENES_PATH = "Assets/Scenes";

    // ─────────────────────────────────────────────────────────────
    //  MENU ENTRY
    // ─────────────────────────────────────────────────────────────

    [MenuItem("LittleLordMajesty/Build All Scenes")]
    public static void BuildAllScenes()
    {
        if (!Directory.Exists(SCENES_PATH))
            Directory.CreateDirectory(SCENES_PATH);

        // Save current scene first
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildBootstrapScene();
        BuildGameScene();

        // Configure Build Settings
        AddScenesToBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SceneBuilder] Bootstrap.unity + Game.unity created. Check Build Settings.");
        EditorUtility.DisplayDialog("Done!", "Bootstrap.unity + Game.unity built.\nOpen Bootstrap.unity to start testing.", "OK");
    }

    [MenuItem("LittleLordMajesty/Open Bootstrap Scene")]
    public static void OpenBootstrap() =>
        EditorSceneManager.OpenScene($"{SCENES_PATH}/Bootstrap.unity");

    [MenuItem("LittleLordMajesty/Open Game Scene")]
    public static void OpenGame() =>
        EditorSceneManager.OpenScene($"{SCENES_PATH}/Game.unity");

    // ─────────────────────────────────────────────────────────────
    //  BOOTSTRAP SCENE
    // ─────────────────────────────────────────────────────────────

    static void BuildBootstrapScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
        cam.orthographic = false;
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 100f;
        camGO.tag = "MainCamera";

        // EventSystem
        CreateEventSystem();

        // Canvas root
        var canvas = CreateCanvas("BootstrapCanvas", out var canvasScaler);
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1080, 1920);
        canvasScaler.matchWidthOrHeight = 0.5f;

        // Background image
        var bg = CreatePanel(canvas.transform, "Background",
            new Color(0.05f, 0.05f, 0.08f), new Vector2(1080, 1920));
        bg.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        bg.GetComponent<RectTransform>().anchorMax = Vector2.one;
        bg.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        bg.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // Title
        var title = CreateTMPText(bg.transform, "Title", "Little Lord\nMajesty",
            72, TextAlignmentOptions.Center, Color.white);
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchoredPosition = new Vector2(0, 250);
        titleRT.sizeDelta = new Vector2(900, 200);

        // Loading bar
        var barGO = new GameObject("LoadingBar");
        barGO.transform.SetParent(bg.transform, false);
        var barRT = barGO.AddComponent<RectTransform>();
        barRT.anchoredPosition = new Vector2(0, -400);
        barRT.sizeDelta = new Vector2(800, 24);

        var bar = barGO.AddComponent<Slider>();
        bar.minValue = 0; bar.maxValue = 1; bar.value = 0;

        // Slider needs Background + Fill Area/Fill
        var barBg = new GameObject("Background"); barBg.transform.SetParent(barGO.transform, false);
        var barBgImg = barBg.AddComponent<Image>();
        barBgImg.color = new Color(0.2f, 0.2f, 0.2f);
        var barBgRT = barBg.GetComponent<RectTransform>();
        barBgRT.anchorMin = Vector2.zero; barBgRT.anchorMax = Vector2.one;
        barBgRT.offsetMin = Vector2.zero; barBgRT.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area"); fillArea.transform.SetParent(barGO.transform, false);
        var fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero; fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(5, 0); fillAreaRT.offsetMax = new Vector2(-5, 0);

        var fill = new GameObject("Fill"); fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.7f, 0.55f, 0.2f); // Gold
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0, 1);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        fillRT.sizeDelta = new Vector2(10, 0);

        bar.fillRect = fillRT;
        bar.targetGraphic = fillImg;

        // Loading text
        var loadingText = CreateTMPText(bg.transform, "LoadingText", "Initializing...",
            28, TextAlignmentOptions.Center, new Color(0.7f, 0.7f, 0.7f));
        var ltRT = loadingText.GetComponent<RectTransform>();
        ltRT.anchoredPosition = new Vector2(0, -450);
        ltRT.sizeDelta = new Vector2(700, 50);

        // Bootstrap object
        var bootstrapGO = new GameObject("Bootstrap");
        var bootstrap = bootstrapGO.AddComponent<GameBootstrap>();

        // Wire fields via SerializedObject
        var so = new SerializedObject(bootstrap);
        so.FindProperty("_splashScreen").objectReferenceValue = bg;
        so.FindProperty("_loadingBar").objectReferenceValue = bar;
        so.FindProperty("_loadingText").objectReferenceValue = loadingText.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedProperties();

        EditorSceneManager.SaveScene(scene, $"{SCENES_PATH}/Bootstrap.unity");
        Debug.Log("[SceneBuilder] Bootstrap.unity saved.");
    }

    // ─────────────────────────────────────────────────────────────
    //  GAME SCENE
    // ─────────────────────────────────────────────────────────────

    static void BuildGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera — 3D isometric perspective
        var camGO = new GameObject("Main Camera");
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.35f, 0.55f, 0.70f); // Sky blue
        cam.orthographic = false;
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 100f;
        camGO.transform.position = new UnityEngine.Vector3(0, 12, -10);
        camGO.transform.eulerAngles = new UnityEngine.Vector3(55, 0, 0);
        camGO.tag = "MainCamera";

        // EventSystem
        CreateEventSystem();

        // Lighting — Directional Light (sun)
        var sunGO = new GameObject("Directional Light");
        var sun = sunGO.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1.0f, 0.95f, 0.85f); // Warm sunlight
        sun.intensity = 1.2f;
        sun.shadows = LightShadows.Soft;
        sunGO.transform.eulerAngles = new Vector3(50f, -30f, 0f);

        // Ambient + Fog
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.5f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.55f, 0.65f, 0.75f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 35f;
        RenderSettings.fogEndDistance = 90f;

        // Main Canvas
        var canvas = CreateCanvas("MainCanvas", out var canvasScaler);
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1080, 1920);
        canvasScaler.matchWidthOrHeight = 0.5f;

        // UIManager on canvas root
        var uiManager = canvas.AddComponent<UIManager>();

        // ── Panels ──────────────────────────────────────────────
        var mainMenuPanel   = BuildMainMenuPanel(canvas.transform);
        var castlePanel     = BuildCastleViewPanel(canvas.transform);
        var worldMapPanel   = BuildWorldMapPanel(canvas.transform);
        var dialoguePanel   = BuildDialoguePanel(canvas.transform);
        var eventPanel      = BuildEventPanel(canvas.transform);
        var pausePanel      = BuildPausePanel(canvas.transform);
        var loadingPanel    = BuildLoadingPanel(canvas.transform);
        var settingsPanel   = BuildSettingsPanel(canvas.transform);

        // Toast layer (on top of everything). Must NOT block raycasts — it covers the
        // whole screen as an invisible Image, so with raycastTarget=true (the default)
        // it would eat every click meant for the MainMenu buttons beneath it.
        var toastLayer = CreateFullscreenPanel(canvas.transform, "ToastLayer", Color.clear);
        var toastLayerImg = toastLayer.GetComponent<Image>();
        if (toastLayerImg != null) toastLayerImg.raycastTarget = false;
        var toastNotif = toastLayer.AddComponent<ToastNotification>();

        // Tutorial overlay (above toast, below nothing)
        var tutorialPanel = BuildTutorialPanel(canvas.transform);

        // Leaderboard panel
        var leaderboardPanel = BuildLeaderboardPanel(canvas.transform);
        leaderboardPanel.SetActive(false);

        // ── Wire UIManager ───────────────────────────────────────
        var soUI = new SerializedObject(uiManager);
        soUI.FindProperty("_mainMenuPanel").objectReferenceValue   = mainMenuPanel;
        soUI.FindProperty("_castleViewPanel").objectReferenceValue = castlePanel;
        soUI.FindProperty("_worldMapPanel").objectReferenceValue   = worldMapPanel;
        soUI.FindProperty("_dialoguePanel").objectReferenceValue   = dialoguePanel;
        soUI.FindProperty("_eventPanel").objectReferenceValue      = eventPanel;
        soUI.FindProperty("_pausePanel").objectReferenceValue      = pausePanel;
        soUI.FindProperty("_loadingPanel").objectReferenceValue    = loadingPanel;
        soUI.FindProperty("_settingsPanel").objectReferenceValue   = settingsPanel;
        soUI.FindProperty("_canvasScaler").objectReferenceValue    = canvasScaler;

        // HUD elements (live inside CastlePanel HUD bar)
        var hud = castlePanel.transform.Find("TopHUD");
        if (hud != null)
        {
            soUI.FindProperty("_woodText").objectReferenceValue  = FindTMP(hud, "WoodText");
            soUI.FindProperty("_foodText").objectReferenceValue  = FindTMP(hud, "FoodText");
            soUI.FindProperty("_goldText").objectReferenceValue  = FindTMP(hud, "GoldText");
            soUI.FindProperty("_dateText").objectReferenceValue  = FindTMP(hud, "DateText");
            soUI.FindProperty("_lordTitleText").objectReferenceValue = FindTMP(hud, "LordTitleText");
        }

        // Dialogue panel refs
        var npcNameText      = FindTMP(dialoguePanel.transform, "NPCName");
        var npcDialogueText  = FindTMP(dialoguePanel.transform, "DialogueText");
        var playerInput      = dialoguePanel.GetComponentInChildren<TMP_InputField>();
        var sendBtn          = FindButton(dialoguePanel.transform, "SendButton");
        var thinkingIndicator = dialoguePanel.transform.Find("ThinkingIndicator")?.gameObject;
        var npcPortrait      = dialoguePanel.transform.Find("NPCPortrait")?.GetComponent<Image>();

        soUI.FindProperty("_npcNameText").objectReferenceValue      = npcNameText;
        soUI.FindProperty("_npcDialogueText").objectReferenceValue  = npcDialogueText;
        soUI.FindProperty("_playerInputField").objectReferenceValue = playerInput;
        soUI.FindProperty("_sendCommandButton").objectReferenceValue = sendBtn;
        if (thinkingIndicator) soUI.FindProperty("_thinkingIndicator").objectReferenceValue = thinkingIndicator;
        if (npcPortrait) soUI.FindProperty("_npcPortrait").objectReferenceValue = npcPortrait;

        // Event panel refs
        soUI.FindProperty("_eventTitleText").objectReferenceValue    = FindTMP(eventPanel.transform, "EventTitle");
        soUI.FindProperty("_eventDescText").objectReferenceValue     = FindTMP(eventPanel.transform, "EventDesc");
        soUI.FindProperty("_eventResponseField").objectReferenceValue = eventPanel.GetComponentInChildren<TMP_InputField>();
        soUI.FindProperty("_eventSubmitButton").objectReferenceValue  = FindButton(eventPanel.transform, "SubmitButton");

        soUI.ApplyModifiedProperties();

        // ── Wire NPCInteractionUI ────────────────────────────────
        WireNPCInteractionUI(dialoguePanel);

        // ── Wire CastleViewUI ───────────────────────────────────
        WireCastleViewUI(castlePanel);

        // ── Wire WorldMapUI ─────────────────────────────────────
        WireWorldMapUI(worldMapPanel);

        // ── Wire SettingsUI ─────────────────────────────────────
        WireSettingsUI(settingsPanel);

        // ── 3D World Scene ───────────────────────────────────────
        var castleScene3DGO = new GameObject("CastleScene3D");
        castleScene3DGO.AddComponent<CastleScene3D>();

        // Set camera reference in CastleScene3D
        var soCastle3D = new SerializedObject(castleScene3DGO.GetComponent<CastleScene3D>());
        soCastle3D.FindProperty("_mainCamera").objectReferenceValue = camGO.GetComponent<Camera>();
        soCastle3D.ApplyModifiedProperties();

        // ── Input & Debug ─────────────────────────────────────────
        var inputHandlerGO = new GameObject("InputHandler");
        inputHandlerGO.AddComponent<InputHandler>();

        var debugConsoleGO = new GameObject("DebugConsole");
        debugConsoleGO.AddComponent<DebugConsole>();

        // All panels start hidden except MainMenu
        mainMenuPanel.SetActive(true);
        castlePanel.SetActive(false);
        worldMapPanel.SetActive(false);
        dialoguePanel.SetActive(false);
        eventPanel.SetActive(false);
        pausePanel.SetActive(false);
        loadingPanel.SetActive(false);
        settingsPanel.SetActive(false);

        EditorSceneManager.SaveScene(scene, $"{SCENES_PATH}/Game.unity");
        Debug.Log("[SceneBuilder] Game.unity saved.");
    }

    // ─────────────────────────────────────────────────────────────
    //  PANEL BUILDERS
    // ─────────────────────────────────────────────────────────────

    static GameObject BuildMainMenuPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "MainMenuPanel", new Color(0.05f, 0.04f, 0.08f));
        panel.AddComponent<MainMenuUI>();

        // Full-screen background art layer (Gemini-generated at runtime).
        var bgArt = CreateFullscreenPanel(panel.transform, "BackgroundArt",
            new Color(0.08f, 0.07f, 0.12f, 1f));
        var bgArtImg = bgArt.GetComponent<Image>();
        bgArtImg.raycastTarget = false;

        // Title
        var title = CreateTMPText(panel.transform, "TitleText", "Little Lord\nMajesty",
            80, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.2f));
        SetAnchored(title, new Vector2(0, 500), new Vector2(900, 220));

        // Subtitle
        var sub = CreateTMPText(panel.transform, "SubtitleText", "AI-Powered Kingdom Sim",
            28, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.7f));
        SetAnchored(sub, new Vector2(0, 360), new Vector2(700, 50));

        // Name input
        var nameInput = CreateInputField(panel.transform, "PlayerNameInput", "Enter your name...");
        SetAnchored(nameInput, new Vector2(0, 150), new Vector2(600, 70));

        // Buttons
        var startBtn  = CreateButton(panel.transform, "StartButton",  "New Game",  new Color(0.3f, 0.6f, 0.2f));
        var contBtn   = CreateButton(panel.transform, "ContinueButton","Continue", new Color(0.2f, 0.3f, 0.6f));
        var settBtn   = CreateButton(panel.transform, "SettingsButton","Settings", new Color(0.3f, 0.3f, 0.3f));
        var quitBtn   = CreateButton(panel.transform, "QuitButton",   "Quit",     new Color(0.5f, 0.2f, 0.2f));

        SetAnchored(startBtn, new Vector2(0,  20),   new Vector2(500, 80));
        SetAnchored(contBtn,  new Vector2(0, -80),   new Vector2(500, 80));
        SetAnchored(settBtn,  new Vector2(0, -180),  new Vector2(500, 80));
        SetAnchored(quitBtn,  new Vector2(0, -280),  new Vector2(500, 80));

        // Version label (baked into panel, updated by MainMenuUI at runtime)
        var ver = CreateTMPText(panel.transform, "VersionText", "v0.1.0 Alpha",
            18, TextAlignmentOptions.Center, new Color(0.4f, 0.4f, 0.5f));
        SetAnchored(ver, new Vector2(0, -400), new Vector2(400, 30));

        // Wire MainMenuUI
        var mmu = panel.GetComponent<MainMenuUI>();
        var soMM = new SerializedObject(mmu);
        // The visible green button is the player's "start a new game" click — wire it to
        // _newGameButton so MainMenuUI.OnNewGameClicked() fires (not the modal confirm path).
        soMM.FindProperty("_newGameButton").objectReferenceValue   = startBtn.GetComponent<Button>();
        soMM.FindProperty("_continueButton").objectReferenceValue  = contBtn.GetComponent<Button>();
        soMM.FindProperty("_settingsButton").objectReferenceValue  = settBtn.GetComponent<Button>();
        soMM.FindProperty("_quitButton").objectReferenceValue      = quitBtn.GetComponent<Button>();
        soMM.FindProperty("_playerNameInput").objectReferenceValue = nameInput.GetComponent<TMP_InputField>();
        soMM.FindProperty("_titleText").objectReferenceValue       = title.GetComponent<TextMeshProUGUI>();
        soMM.FindProperty("_subtitleText").objectReferenceValue    = sub.GetComponent<TextMeshProUGUI>();
        soMM.FindProperty("_versionText").objectReferenceValue     = ver.GetComponent<TextMeshProUGUI>();
        soMM.FindProperty("_backgroundArt").objectReferenceValue   = bgArtImg;
        soMM.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildCastleViewPanel(Transform parent)
    {
        // Fully transparent root panel so the 3D castle scene (CastleScene3D) is
        // visible behind the UI — without this the scene is hidden under a solid
        // dark fill and players complain they "can't see the characters".
        var panel = CreateFullscreenPanel(parent, "CastleViewPanel", new Color(0, 0, 0, 0));
        var panelImg = panel.GetComponent<Image>();
        panelImg.raycastTarget = false;
        var castleUI = panel.AddComponent<CastleViewUI>();

        // Full-screen background art layer — sits behind all HUD elements. Gemini
        // fills this with a generated castle courtyard at runtime. Semi-transparent
        // so the 3D scene shows through even before Gemini art arrives.
        var bgArt = CreateFullscreenPanel(panel.transform, "BackgroundArt",
            new Color(0.10f, 0.09f, 0.15f, 0.55f));
        var bgArtImg = bgArt.GetComponent<Image>();
        bgArtImg.raycastTarget = false; // don't eat clicks

        // Top HUD bar
        var topHUD = CreatePanel(panel.transform, "TopHUD", new Color(0, 0, 0, 0.7f), new Vector2(1080, 120));
        var topRT = topHUD.GetComponent<RectTransform>();
        topRT.anchorMin = new Vector2(0, 1); topRT.anchorMax = new Vector2(1, 1);
        topRT.pivot = new Vector2(0.5f, 1);
        topRT.offsetMin = new Vector2(0, -120); topRT.offsetMax = Vector2.zero;

        var lordTitle = CreateTMPText(topHUD.transform, "LordTitleText", "Little Lord Player",
            28, TextAlignmentOptions.Left, Color.white);
        SetAnchored(lordTitle, new Vector2(-350, 0), new Vector2(400, 50));

        var dateText = CreateTMPText(topHUD.transform, "DateText", "Year 1, Day 1",
            22, TextAlignmentOptions.Right, new Color(0.8f, 0.8f, 0.6f));
        SetAnchored(dateText, new Vector2(350, 0), new Vector2(350, 50));

        // Resource strip (below top HUD)
        var resStrip = CreatePanel(panel.transform, "ResourceStrip", new Color(0, 0, 0, 0.5f), new Vector2(1080, 60));
        var rsRT = resStrip.GetComponent<RectTransform>();
        rsRT.anchorMin = new Vector2(0, 1); rsRT.anchorMax = new Vector2(1, 1);
        rsRT.pivot = new Vector2(0.5f, 1);
        rsRT.offsetMin = new Vector2(0, -180); rsRT.offsetMax = new Vector2(0, -120);

        // NOTE: All resource labels use PLAIN ASCII only. Emoji glyphs (wood/food/gold/people)
        // are NOT in LiberationSans SDF and hit the TMP dynamic-fallback null-function crash
        // on WebGL IL2CPP. The colored label acts as the visual cue instead of an icon.
        var woodTxt = CreateTMPText(resStrip.transform, "WoodText",  "Wood 500", 24, TextAlignmentOptions.Center, new Color(0.85f, 0.62f, 0.35f));
        var foodTxt = CreateTMPText(resStrip.transform, "FoodText",  "Food 500", 24, TextAlignmentOptions.Center, new Color(0.55f, 0.9f,  0.35f));
        var goldTxt = CreateTMPText(resStrip.transform, "GoldText",  "Gold 200", 24, TextAlignmentOptions.Center, new Color(1.00f, 0.85f, 0.20f));
        var popTxt  = CreateTMPText(resStrip.transform, "PopulationText", "Pop 20/50", 24, TextAlignmentOptions.Center, Color.white);

        SetAnchored(woodTxt, new Vector2(-380, 0), new Vector2(200, 50));
        SetAnchored(foodTxt, new Vector2(-130, 0), new Vector2(200, 50));
        SetAnchored(goldTxt, new Vector2( 120, 0), new Vector2(200, 50));
        SetAnchored(popTxt,  new Vector2( 370, 0), new Vector2(200, 50));

        // Objective banner — single-line hint at the top telling the player what to do.
        // Plain ASCII default; CastleViewUI swaps in the localized string at runtime.
        var objective = CreateTMPText(panel.transform, "ObjectiveText",
            "Welcome to your castle. Tap a vassal card below to begin.",
            26, TextAlignmentOptions.Center, new Color(1f, 0.95f, 0.65f));
        SetAnchored(objective, new Vector2(0, 680), new Vector2(1000, 60));
        objective.GetComponent<TextMeshProUGUI>().enableWordWrapping = false;

        // Central NPC grid — big, ALWAYS visible. No more hidden drawer.
        // Fills most of the middle of the screen so NPCs are immediately obvious.
        var npcGrid = new GameObject("NPCGrid");
        npcGrid.transform.SetParent(panel.transform, false);
        var npcGridRT = npcGrid.AddComponent<RectTransform>();
        npcGridRT.anchorMin = new Vector2(0.5f, 0.5f);
        npcGridRT.anchorMax = new Vector2(0.5f, 0.5f);
        npcGridRT.pivot     = new Vector2(0.5f, 0.5f);
        npcGridRT.sizeDelta = new Vector2(1020, 780);
        npcGridRT.anchoredPosition = new Vector2(0, 20);
        // Fully transparent background so Gemini art shows through
        var npcGridImg = npcGrid.AddComponent<Image>();
        npcGridImg.color = new Color(0, 0, 0, 0);
        npcGridImg.raycastTarget = false;

        // Bottom action bar
        var actionBar = CreatePanel(panel.transform, "ActionBar", new Color(0, 0, 0, 0.8f), new Vector2(1080, 100));
        var abRT = actionBar.GetComponent<RectTransform>();
        abRT.anchorMin = new Vector2(0, 0); abRT.anchorMax = new Vector2(1, 0);
        abRT.pivot = new Vector2(0.5f, 0);
        abRT.offsetMin = Vector2.zero; abRT.offsetMax = new Vector2(0, 100);

        var buildBtn    = CreateButton(actionBar.transform, "BuildButton",   "Build",    new Color(0.2f, 0.4f, 0.6f));
        var saveBtn     = CreateButton(actionBar.transform, "SaveButton",    "Save",     new Color(0.2f, 0.5f, 0.3f));
        var npcListBtn  = CreateButton(actionBar.transform, "NPCListButton", "NPCs",     new Color(0.4f, 0.2f, 0.6f));
        var worldMapBtn = CreateButton(actionBar.transform, "WorldMapButton","Map",      new Color(0.5f, 0.3f, 0.1f));
        // NOTE: Do NOT use ⚙ (U+2699). LiberationSans SDF has no glyph for it, and the
        // TMP dynamic font-fallback lookup path hits a null function pointer on WebGL IL2CPP,
        // crashing the main loop with "RuntimeError: null function or function signature mismatch".
        // Plain-ASCII word labels instead of * and = placeholders — far more legible
        // and avoids any ambiguity about what the icon-like button does.
        var settingsBtn = CreateButton(actionBar.transform, "SettingsButton","Options",  new Color(0.30f, 0.30f, 0.38f));
        var menuBtn     = CreateButton(actionBar.transform, "MenuButton",    "Pause",    new Color(0.30f, 0.30f, 0.30f));

        // Uniform button widths so the action bar reads as a single row of equals.
        // Previously settings/menu were crammed into 90-wide slots that truncated their
        // labels; now they match the others at 150.
        SetAnchored(buildBtn,    new Vector2(-480, 0), new Vector2(150, 72));
        SetAnchored(saveBtn,     new Vector2(-320, 0), new Vector2(150, 72));
        SetAnchored(npcListBtn,  new Vector2(-160, 0), new Vector2(150, 72));
        SetAnchored(worldMapBtn, new Vector2(   0, 0), new Vector2(150, 72));
        SetAnchored(settingsBtn, new Vector2( 160, 0), new Vector2(150, 72));
        SetAnchored(menuBtn,     new Vector2( 320, 0), new Vector2(150, 72));

        // Notification banner
        var notifBanner = CreatePanel(panel.transform, "NotificationBanner",
            new Color(0.1f, 0.5f, 0.1f, 0.95f), new Vector2(900, 60));
        SetAnchored(notifBanner, new Vector2(0, -220), new Vector2(900, 60));
        notifBanner.SetActive(false);
        var notifText = CreateTMPText(notifBanner.transform, "NotificationText", "",
            26, TextAlignmentOptions.Center, Color.white);
        var ntRT = notifText.GetComponent<RectTransform>();
        ntRT.anchorMin = Vector2.zero; ntRT.anchorMax = Vector2.one;
        ntRT.offsetMin = new Vector2(10, 0); ntRT.offsetMax = new Vector2(-10, 0);

        // NPC list panel is now an alias for the central grid — the old drawer concept
        // is gone. The grid's RectTransform is both the panel reference (for the
        // active-toggle logic in CastleViewUI) and the content parent.
        var npcListPanel = npcGrid;
        var npcListContent = npcGrid.transform;

        // Building menu panel
        var buildMenuPanel = CreatePanel(panel.transform, "BuildingMenuPanel",
            new Color(0.08f, 0.08f, 0.15f, 0.98f), new Vector2(1080, 500));
        var bmpRT = buildMenuPanel.GetComponent<RectTransform>();
        bmpRT.anchorMin = new Vector2(0, 0); bmpRT.anchorMax = new Vector2(1, 0);
        bmpRT.pivot = new Vector2(0.5f, 0);
        bmpRT.offsetMin = new Vector2(0, 100); bmpRT.offsetMax = new Vector2(0, 600);
        buildMenuPanel.SetActive(false);

        var (buildScrollRect, buildMenuContent) = CreateScrollView(buildMenuPanel.transform, "BuildMenuScroll");

        // Wire CastleViewUI
        var soCastle = new SerializedObject(castleUI);
        soCastle.FindProperty("_lordTitleText").objectReferenceValue        = lordTitle.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_dateText").objectReferenceValue             = dateText.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_woodText").objectReferenceValue             = woodTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_foodText").objectReferenceValue             = foodTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_goldText").objectReferenceValue             = goldTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_populationText").objectReferenceValue       = popTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_menuButton").objectReferenceValue           = menuBtn.GetComponent<Button>();
        soCastle.FindProperty("_worldMapButton").objectReferenceValue       = worldMapBtn.GetComponent<Button>();
        soCastle.FindProperty("_settingsButton").objectReferenceValue       = settingsBtn.GetComponent<Button>();
        soCastle.FindProperty("_buildButton").objectReferenceValue          = buildBtn.GetComponent<Button>();
        soCastle.FindProperty("_saveButton").objectReferenceValue           = saveBtn.GetComponent<Button>();
        soCastle.FindProperty("_npcListButton").objectReferenceValue        = npcListBtn.GetComponent<Button>();
        soCastle.FindProperty("_backgroundArt").objectReferenceValue        = bgArtImg;
        soCastle.FindProperty("_notificationBanner").objectReferenceValue   = notifBanner;
        soCastle.FindProperty("_notificationText").objectReferenceValue     = notifText.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_npcListPanel").objectReferenceValue         = npcListPanel;
        soCastle.FindProperty("_npcListContent").objectReferenceValue       = npcListContent;
        soCastle.FindProperty("_npcListItemPrefab").objectReferenceValue    =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/NPCListItem.prefab");
        soCastle.FindProperty("_buildingMenuPanel").objectReferenceValue    = buildMenuPanel;
        soCastle.FindProperty("_buildingMenuContent").objectReferenceValue  = buildMenuContent;
        soCastle.FindProperty("_buildingMenuItemPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/BuildingMenuItem.prefab");
        soCastle.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildDialoguePanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "DialoguePanel", new Color(0, 0, 0, 0.85f));
        var interactionUI = panel.AddComponent<NPCInteractionUI>();

        // NPC info bar (top)
        var infoBar = CreatePanel(panel.transform, "NPCInfoBar",
            new Color(0.08f, 0.06f, 0.12f), new Vector2(1080, 160));
        var ibRT = infoBar.GetComponent<RectTransform>();
        ibRT.anchorMin = new Vector2(0, 1); ibRT.anchorMax = new Vector2(1, 1);
        ibRT.pivot = new Vector2(0.5f, 1);
        ibRT.offsetMin = new Vector2(0, -160); ibRT.offsetMax = Vector2.zero;

        // NPC avatar placeholder
        var avatarGO = new GameObject("NPCPortrait"); avatarGO.transform.SetParent(infoBar.transform, false);
        var avatar = avatarGO.AddComponent<Image>();
        avatar.color = new Color(0.3f, 0.3f, 0.4f);
        var avatarRT = avatarGO.GetComponent<RectTransform>();
        avatarRT.anchoredPosition = new Vector2(-440, 0);
        avatarRT.sizeDelta = new Vector2(120, 120);

        var npcName = CreateTMPText(infoBar.transform, "NPCName", "NPC Name",
            32, TextAlignmentOptions.Left, Color.white);
        SetAnchored(npcName, new Vector2(-250, 35), new Vector2(400, 45));

        var npcProf = CreateTMPText(infoBar.transform, "NPCProfession", "Farmer",
            22, TextAlignmentOptions.Left, new Color(0.6f, 0.6f, 0.8f));
        SetAnchored(npcProf, new Vector2(-250, -5), new Vector2(300, 35));

        // Plain-ASCII "Loyalty" prefix — the heart glyph has no LiberationSans SDF entry
        // and would trip the TMP fallback null-function crash on WebGL IL2CPP.
        var loyaltyTxt = CreateTMPText(infoBar.transform, "LoyaltyText", "Loyalty 75/100",
            20, TextAlignmentOptions.Right, new Color(0.95f, 0.55f, 0.55f));
        SetAnchored(loyaltyTxt, new Vector2(400, 35), new Vector2(200, 35));

        var taskTxt = CreateTMPText(infoBar.transform, "CurrentTask", "Idle",
            20, TextAlignmentOptions.Right, new Color(0.7f, 0.7f, 0.5f));
        SetAnchored(taskTxt, new Vector2(400, -5), new Vector2(200, 35));

        // Mood slider
        var moodBarGO = new GameObject("MoodBar"); moodBarGO.transform.SetParent(infoBar.transform, false);
        var moodSlider = moodBarGO.AddComponent<Slider>();
        moodSlider.minValue = 0; moodSlider.maxValue = 1; moodSlider.value = 0.75f;
        var moodRT = moodBarGO.GetComponent<RectTransform>();
        moodRT.anchoredPosition = new Vector2(300, -55);
        moodRT.sizeDelta = new Vector2(200, 16);
        SetupSliderVisuals(moodBarGO, moodSlider, new Color(0.2f, 0.8f, 0.3f));

        // Close button — word label instead of a lone X. Wider slot so the text fits.
        var closeBtn = CreateButton(infoBar.transform, "CloseButton", "Close", new Color(0.55f, 0.25f, 0.25f));
        SetAnchored(closeBtn, new Vector2(460, 0), new Vector2(130, 60));

        // Chat scroll view
        var chatScroll = new GameObject("ChatScrollRect"); chatScroll.transform.SetParent(panel.transform, false);
        var chatScrollComp = chatScroll.AddComponent<ScrollRect>();
        var chatScrollRT = chatScroll.GetComponent<RectTransform>();
        chatScrollRT.anchorMin = new Vector2(0, 0.15f); chatScrollRT.anchorMax = new Vector2(1, 1);
        chatScrollRT.offsetMin = new Vector2(0, 0); chatScrollRT.offsetMax = new Vector2(0, -160);

        var chatViewport = new GameObject("Viewport"); chatViewport.transform.SetParent(chatScroll.transform, false);
        chatViewport.AddComponent<RectMask2D>();
        var chatVpRT = chatViewport.GetComponent<RectTransform>();
        chatVpRT.anchorMin = Vector2.zero; chatVpRT.anchorMax = Vector2.one;
        chatVpRT.offsetMin = Vector2.zero; chatVpRT.offsetMax = Vector2.zero;

        var chatContent = new GameObject("ChatContent"); chatContent.transform.SetParent(chatViewport.transform, false);
        var chatContentRT = chatContent.AddComponent<RectTransform>();
        chatContentRT.anchorMin = new Vector2(0, 1); chatContentRT.anchorMax = new Vector2(1, 1);
        chatContentRT.pivot = new Vector2(0.5f, 1);
        chatContentRT.offsetMin = Vector2.zero; chatContentRT.offsetMax = Vector2.zero;
        chatContentRT.sizeDelta = new Vector2(0, 0);

        var vlg = chatContent.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 8;
        vlg.padding = new RectOffset(16, 16, 8, 8);

        var csf = chatContent.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        chatScrollComp.content = chatContentRT;
        chatScrollComp.viewport = chatVpRT;
        chatScrollComp.horizontal = false;
        chatScrollComp.vertical = true;
        chatScrollComp.scrollSensitivity = 30;

        // Thinking indicator
        var thinkingGO = new GameObject("ThinkingIndicator"); thinkingGO.transform.SetParent(panel.transform, false);
        var thinkingImg = thinkingGO.AddComponent<Image>();
        thinkingImg.color = new Color(0.15f, 0.15f, 0.25f, 0.9f);
        var tRT = thinkingGO.GetComponent<RectTransform>();
        tRT.anchorMin = new Vector2(0, 0.15f); tRT.anchorMax = new Vector2(0.5f, 0.2f);
        tRT.offsetMin = new Vector2(16, 0); tRT.offsetMax = new Vector2(-4, 0);
        var thinkText = CreateTMPText(thinkingGO.transform, "ThinkText", "Thinking...",
            20, TextAlignmentOptions.Left, new Color(0.7f, 0.7f, 0.9f));
        var ttRT = thinkText.GetComponent<RectTransform>();
        ttRT.anchorMin = Vector2.zero; ttRT.anchorMax = Vector2.one;
        ttRT.offsetMin = new Vector2(12, 0); ttRT.offsetMax = new Vector2(-12, 0);
        thinkingGO.SetActive(false);

        // Quick commands strip
        var quickCmdsGO = new GameObject("QuickCommandsStrip");
        var qcRT = quickCmdsGO.AddComponent<RectTransform>();
        quickCmdsGO.transform.SetParent(panel.transform, false);
        qcRT.anchorMin = new Vector2(0, 0.1f); qcRT.anchorMax = new Vector2(1, 0.16f);
        qcRT.offsetMin = new Vector2(8, 0); qcRT.offsetMax = new Vector2(-8, 0);
        var qcHLG = quickCmdsGO.AddComponent<HorizontalLayoutGroup>();
        qcHLG.spacing = 8;
        qcHLG.childForceExpandWidth = false;
        qcHLG.childForceExpandHeight = true;

        // Input bar (bottom)
        var inputBar = CreatePanel(panel.transform, "InputBar",
            new Color(0.08f, 0.08f, 0.15f), new Vector2(1080, 110));
        var inputBarRT = inputBar.GetComponent<RectTransform>();
        inputBarRT.anchorMin = new Vector2(0, 0); inputBarRT.anchorMax = new Vector2(1, 0);
        inputBarRT.pivot = new Vector2(0.5f, 0);
        inputBarRT.offsetMin = Vector2.zero; inputBarRT.offsetMax = new Vector2(0, 110);

        var cmdInput = CreateInputField(inputBar.transform, "CommandInput", "Issue a command...");
        SetAnchored(cmdInput, new Vector2(-90, 0), new Vector2(850, 75));

        var sendBtn = CreateButton(inputBar.transform, "SendButton", "Send", new Color(0.32f, 0.62f, 0.25f));
        SetAnchored(sendBtn, new Vector2(435, 0), new Vector2(120, 75));

        // TTS toggle
        var ttsToggleGO = new GameObject("TTSToggle"); ttsToggleGO.transform.SetParent(inputBar.transform, false);
        var ttsToggle = ttsToggleGO.AddComponent<Toggle>();
        var ttsToggleRT = ttsToggleGO.GetComponent<RectTransform>();
        ttsToggleRT.anchoredPosition = new Vector2(-490, 0);
        ttsToggleRT.sizeDelta = new Vector2(60, 40);

        var ttsLabel = CreateTMPText(inputBar.transform, "TTSLabel", "Voice",
            18, TextAlignmentOptions.Center, new Color(0.6f, 0.6f, 0.8f));
        SetAnchored(ttsLabel, new Vector2(-490, -35), new Vector2(80, 25));

        // Wire NPCInteractionUI
        var soNPC = new SerializedObject(interactionUI);
        soNPC.FindProperty("_npcAvatar").objectReferenceValue       = avatar;
        soNPC.FindProperty("_npcName").objectReferenceValue         = npcName.GetComponent<TextMeshProUGUI>();
        soNPC.FindProperty("_npcProfession").objectReferenceValue   = npcProf.GetComponent<TextMeshProUGUI>();
        soNPC.FindProperty("_moodSlider").objectReferenceValue      = moodSlider;
        soNPC.FindProperty("_moodFill").objectReferenceValue        = moodBarGO.GetComponentInChildren<Image>();
        soNPC.FindProperty("_loyaltyText").objectReferenceValue     = loyaltyTxt.GetComponent<TextMeshProUGUI>();
        soNPC.FindProperty("_currentTaskText").objectReferenceValue = taskTxt.GetComponent<TextMeshProUGUI>();
        soNPC.FindProperty("_chatScrollRect").objectReferenceValue  = chatScrollComp;
        soNPC.FindProperty("_chatContentParent").objectReferenceValue = chatContentRT;
        soNPC.FindProperty("_commandInput").objectReferenceValue    = cmdInput.GetComponent<TMP_InputField>();
        soNPC.FindProperty("_sendButton").objectReferenceValue      = sendBtn.GetComponent<Button>();
        soNPC.FindProperty("_closeButton").objectReferenceValue     = closeBtn.GetComponent<Button>();
        soNPC.FindProperty("_quickCommandsParent").objectReferenceValue = quickCmdsGO.transform;
        soNPC.FindProperty("_ttsToggle").objectReferenceValue       = ttsToggle;
        soNPC.FindProperty("_ttsLabel").objectReferenceValue        = ttsLabel.GetComponent<TextMeshProUGUI>();
        soNPC.FindProperty("_playerMessagePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/PlayerMessage.prefab");
        soNPC.FindProperty("_npcMessagePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/NPCMessage.prefab");
        soNPC.FindProperty("_thinkingBubblePrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/ThinkingBubble.prefab");
        soNPC.FindProperty("_quickCommandButtonPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Prefabs/QuickCommandButton.prefab");
        soNPC.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildEventPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "EventPanel", new Color(0, 0, 0, 0.92f));

        var bg = CreatePanel(panel.transform, "EventCard",
            new Color(0.1f, 0.07f, 0.05f), new Vector2(900, 700));
        SetAnchored(bg, new Vector2(0, 50), new Vector2(900, 700));

        var title = CreateTMPText(bg.transform, "EventTitle", "EVENT TITLE",
            42, TextAlignmentOptions.Center, new Color(1f, 0.7f, 0.2f));
        SetAnchored(title, new Vector2(0, 250), new Vector2(820, 80));

        var desc = CreateTMPText(bg.transform, "EventDesc",
            "Event description appears here...",
            26, TextAlignmentOptions.Center, new Color(0.85f, 0.85f, 0.85f));
        SetAnchored(desc, new Vector2(0, 50), new Vector2(820, 250));
        desc.GetComponent<TextMeshProUGUI>().enableWordWrapping = true;

        var responseField = CreateInputField(bg.transform, "ResponseField", "How do you respond?");
        SetAnchored(responseField, new Vector2(0, -200), new Vector2(820, 80));

        var submitBtn = CreateButton(bg.transform, "SubmitButton", "Respond", new Color(0.3f, 0.5f, 0.2f));
        SetAnchored(submitBtn, new Vector2(0, -320), new Vector2(400, 70));

        return panel;
    }

    static GameObject BuildPausePanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "PausePanel", new Color(0, 0, 0, 0.7f));
        var pauseUI = panel.AddComponent<PauseUI>();

        var card = CreatePanel(panel.transform, "PauseCard",
            new Color(0.1f, 0.08f, 0.15f), new Vector2(500, 500));
        SetAnchored(card, Vector2.zero, new Vector2(500, 500));

        var header = CreateTMPText(card.transform, "PauseTitle", "PAUSED",
            54, TextAlignmentOptions.Center, Color.white);
        SetAnchored(header, new Vector2(0, 170), new Vector2(420, 80));

        var resumeBtn = CreateButton(card.transform, "ResumeButton",  "Resume",  new Color(0.2f, 0.5f, 0.2f));
        var saveBtn   = CreateButton(card.transform, "SaveButton",    "Save",    new Color(0.2f, 0.3f, 0.6f));
        var quitBtn   = CreateButton(card.transform, "QuitButton",    "Main Menu", new Color(0.5f, 0.2f, 0.2f));

        SetAnchored(resumeBtn, new Vector2(0, 60),  new Vector2(400, 75));
        SetAnchored(saveBtn,   new Vector2(0, -40), new Vector2(400, 75));
        SetAnchored(quitBtn,   new Vector2(0,-140), new Vector2(400, 75));

        // Wire PauseUI serialized fields — without these the buttons have no click
        // handlers and the pause menu becomes a deadlock.
        var soPause = new SerializedObject(pauseUI);
        soPause.FindProperty("_resumeButton").objectReferenceValue   = resumeBtn.GetComponent<Button>();
        soPause.FindProperty("_saveButton").objectReferenceValue     = saveBtn.GetComponent<Button>();
        soPause.FindProperty("_mainMenuButton").objectReferenceValue = quitBtn.GetComponent<Button>();
        soPause.FindProperty("_titleText").objectReferenceValue      = header.GetComponent<TextMeshProUGUI>();
        soPause.FindProperty("_resumeLabel").objectReferenceValue    = resumeBtn.GetComponentInChildren<TextMeshProUGUI>();
        soPause.FindProperty("_saveLabel").objectReferenceValue      = saveBtn.GetComponentInChildren<TextMeshProUGUI>();
        soPause.FindProperty("_mainMenuLabel").objectReferenceValue  = quitBtn.GetComponentInChildren<TextMeshProUGUI>();
        soPause.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildLoadingPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "LoadingPanel", new Color(0.05f, 0.04f, 0.08f));
        var loadTxt = CreateTMPText(panel.transform, "LoadingText", "Loading...",
            36, TextAlignmentOptions.Center, Color.white);
        SetAnchored(loadTxt, new Vector2(0, 100), new Vector2(600, 60));
        return panel;
    }

    static GameObject BuildSettingsPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "SettingsPanel", new Color(0.06f, 0.05f, 0.1f, 0.95f));
        var settingsUI = panel.AddComponent<SettingsUI>();

        var header = CreateTMPText(panel.transform, "SettingsTitle", "SETTINGS",
            48, TextAlignmentOptions.Center, Color.white);
        SetAnchored(header, new Vector2(0, 800), new Vector2(600, 80));

        var closeBtn = CreateButton(panel.transform, "CloseButton", "Close", new Color(0.32f, 0.32f, 0.36f));
        SetAnchored(closeBtn, new Vector2(-430, 800), new Vector2(150, 70));

        // Language row
        var langLabel = CreateTMPText(panel.transform, "LangLabel", "Language",
            28, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.9f));
        SetAnchored(langLabel, new Vector2(-200, 550), new Vector2(400, 50));

        var langDropdown = CreateDropdown(panel.transform, "LanguageDropdown");
        SetAnchored(langDropdown, new Vector2(150, 550), new Vector2(400, 60));

        // Music volume row
        var musicLabel = CreateTMPText(panel.transform, "MusicLabel", "Music Volume",
            28, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.9f));
        SetAnchored(musicLabel, new Vector2(-200, 400), new Vector2(400, 50));
        var musicSlider = CreateSlider(panel.transform, "MusicSlider", 0f, 1f, 0.8f);
        SetAnchored(musicSlider, new Vector2(150, 400), new Vector2(400, 40));

        // SFX volume row
        var sfxLabel = CreateTMPText(panel.transform, "SFXLabel", "SFX Volume",
            28, TextAlignmentOptions.Left, new Color(0.85f, 0.85f, 0.9f));
        SetAnchored(sfxLabel, new Vector2(-200, 270), new Vector2(400, 50));
        var sfxSlider = CreateSlider(panel.transform, "SFXSlider", 0f, 1f, 1.0f);
        SetAnchored(sfxSlider, new Vector2(150, 270), new Vector2(400, 40));

        // Save button
        var saveBtn = CreateButton(panel.transform, "SaveSettingsButton", "Save", new Color(0.3f, 0.55f, 0.25f));
        SetAnchored(saveBtn, new Vector2(0, -600), new Vector2(400, 80));

        var soSett = new SerializedObject(settingsUI);
        soSett.FindProperty("_closeButton").objectReferenceValue       = closeBtn.GetComponent<Button>();
        soSett.FindProperty("_saveButton").objectReferenceValue        = saveBtn.GetComponent<Button>();
        soSett.FindProperty("_languageDropdown").objectReferenceValue  = langDropdown.GetComponent<TMP_Dropdown>();
        soSett.FindProperty("_musicSlider").objectReferenceValue       = musicSlider.GetComponent<Slider>();
        soSett.FindProperty("_sfxSlider").objectReferenceValue         = sfxSlider.GetComponent<Slider>();
        soSett.ApplyModifiedProperties();

        return panel;
    }

    /// <summary>Creates a TMP Dropdown with a dark background.</summary>
    static GameObject CreateDropdown(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.22f);

        var dropdown = go.AddComponent<TMP_Dropdown>();

        // Label
        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = "Option";
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Left;
        label.color = Color.white;
        var labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(15, 4); labelRT.offsetMax = new Vector2(-30, -4);

        // Template (required by TMP_Dropdown — minimal working template)
        var template = new GameObject("Template");
        template.transform.SetParent(go.transform, false);
        var templateImg = template.AddComponent<Image>();
        templateImg.color = new Color(0.1f, 0.1f, 0.15f);
        var templateRT = template.GetComponent<RectTransform>();
        templateRT.anchorMin = new Vector2(0, 0); templateRT.anchorMax = new Vector2(1, 0);
        templateRT.pivot = new Vector2(0.5f, 1);
        templateRT.anchoredPosition = new Vector2(0, 2);
        templateRT.sizeDelta = new Vector2(0, 200);
        var templateScrollRect = template.AddComponent<ScrollRect>();

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(template.transform, false);
        var vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(0.1f, 0.1f, 0.15f);
        viewport.AddComponent<Mask>().showMaskGraphic = true;
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);

        var item = new GameObject("Item");
        item.transform.SetParent(content.transform, false);
        var itemToggle = item.AddComponent<Toggle>();
        var itemBg = item.AddComponent<Image>();
        itemBg.color = new Color(0.2f, 0.2f, 0.3f);
        itemToggle.targetGraphic = itemBg;
        var itemRT = item.GetComponent<RectTransform>();
        itemRT.anchorMin = new Vector2(0, 0.5f); itemRT.anchorMax = new Vector2(1, 0.5f);
        itemRT.sizeDelta = new Vector2(0, 40);

        var itemLabelGO = new GameObject("Item Label");
        itemLabelGO.transform.SetParent(item.transform, false);
        var itemLabel = itemLabelGO.AddComponent<TextMeshProUGUI>();
        itemLabel.text = "Option";
        itemLabel.fontSize = 22;
        itemLabel.alignment = TextAlignmentOptions.Left;
        itemLabel.color = Color.white;
        var itemLabelRT = itemLabelGO.GetComponent<RectTransform>();
        itemLabelRT.anchorMin = Vector2.zero; itemLabelRT.anchorMax = Vector2.one;
        itemLabelRT.offsetMin = new Vector2(20, 0); itemLabelRT.offsetMax = new Vector2(-10, 0);

        // Wire ScrollRect references — without these, TMP_Dropdown's template
        // validation hits null references deep in UI runtime code and can cause
        // wasm signature-mismatch crashes in WebGL IL2CPP builds.
        templateScrollRect.content    = contentRT;
        templateScrollRect.viewport   = vpRT;
        templateScrollRect.horizontal = false;
        templateScrollRect.vertical   = true;
        templateScrollRect.movementType = ScrollRect.MovementType.Clamped;

        template.SetActive(false);

        dropdown.template    = templateRT;
        dropdown.captionText = label;
        dropdown.itemText    = itemLabel;
        dropdown.targetGraphic = img;

        return go;
    }

    /// <summary>Creates a horizontal Slider.</summary>
    static GameObject CreateSlider(Transform parent, string name, float min, float max, float value)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        var slider = go.AddComponent<Slider>();

        var bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.2f, 0.2f, 0.25f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        var fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        var faRT = fillArea.AddComponent<RectTransform>();
        faRT.anchorMin = new Vector2(0, 0.25f); faRT.anchorMax = new Vector2(1, 0.75f);
        faRT.offsetMin = new Vector2(5, 0); faRT.offsetMax = new Vector2(-15, 0);

        var fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.7f, 0.55f, 0.2f);
        var fillRT = fill.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(1, 1);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;

        var handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(go.transform, false);
        var haRT = handleArea.AddComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.offsetMin = new Vector2(10, 0); haRT.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;
        var handleRT = handle.GetComponent<RectTransform>();
        handleRT.anchorMin = new Vector2(0, 0); handleRT.anchorMax = new Vector2(0, 1);
        handleRT.sizeDelta = new Vector2(20, 0);

        slider.fillRect      = fillRT;
        slider.handleRect    = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue      = min;
        slider.maxValue      = max;
        slider.value         = value;
        slider.direction     = Slider.Direction.LeftToRight;

        return go;
    }

    static GameObject BuildWorldMapPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "WorldMapPanel", new Color(0.05f, 0.08f, 0.05f));
        panel.AddComponent<WorldMapUI>();

        // Full-screen background art (parchment-style, Gemini-generated)
        var bgArt = CreateFullscreenPanel(panel.transform, "BackgroundArt",
            new Color(0.08f, 0.09f, 0.07f, 1f));
        var bgArtImg = bgArt.GetComponent<Image>();
        bgArtImg.raycastTarget = false;

        var header = CreateTMPText(panel.transform, "WorldMapTitle", "WORLD MAP",
            40, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 0.7f));
        SetAnchored(header, new Vector2(0, 850), new Vector2(600, 70));

        // Back-to-castle button — plain ASCII word, no angle-bracket glyph.
        var closeBtn = CreateButton(panel.transform, "CloseButton", "Back to Castle", new Color(0.22f, 0.32f, 0.22f));
        SetAnchored(closeBtn, new Vector2(-400, 850), new Vector2(260, 70));

        var mapContainer = new GameObject("MapContainer"); mapContainer.transform.SetParent(panel.transform, false);
        var mapContRT = mapContainer.AddComponent<RectTransform>();
        mapContRT.anchorMin = new Vector2(0, 0.1f); mapContRT.anchorMax = new Vector2(1, 0.9f);
        mapContRT.offsetMin = new Vector2(20, 0); mapContRT.offsetMax = new Vector2(-20, 0);

        var soMap = new SerializedObject(panel.GetComponent<WorldMapUI>());
        soMap.FindProperty("_mapGridParent").objectReferenceValue = mapContainer.transform;
        soMap.FindProperty("_closeButton").objectReferenceValue = closeBtn.GetComponent<Button>();
        soMap.FindProperty("_backgroundArt").objectReferenceValue = bgArtImg;
        soMap.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildLeaderboardPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "LeaderboardPanel", new Color(0.06f, 0.05f, 0.1f, 0.98f));
        panel.AddComponent<LeaderboardUI>();

        var header = CreateTMPText(panel.transform, "Title", "LEADERBOARD",
            44, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.2f));
        SetAnchored(header, new Vector2(0, 800), new Vector2(700, 80));

        var (scrollRect, content) = CreateScrollView(panel.transform, "LeaderScroll");

        var soLB = new SerializedObject(panel.GetComponent<LeaderboardUI>());
        soLB.FindProperty("_entriesContainer").objectReferenceValue = content;
        soLB.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildTutorialPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "TutorialOverlay", Color.clear);
        var tutUI = panel.AddComponent<TutorialUI>();

        // Dim background
        var dim = panel.GetComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.5f);
        dim.raycastTarget = true;

        // Highlight frame (repositioned at runtime)
        var highlight = new GameObject("HighlightFrame", typeof(RectTransform), typeof(Image));
        highlight.transform.SetParent(panel.transform, false);
        var hlImg = highlight.GetComponent<Image>();
        hlImg.color = new Color(1f, 0.85f, 0.2f, 0.6f);
        hlImg.raycastTarget = false;
        highlight.SetActive(false);

        // Arrow indicator
        var arrow = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrow.transform.SetParent(panel.transform, false);
        var arrowImg = arrow.GetComponent<Image>();
        arrowImg.color = new Color(1f, 0.85f, 0.2f);
        arrowImg.raycastTarget = false;
        SetAnchored(arrow, new Vector2(0, 100), new Vector2(40, 60));

        // Dialogue box at bottom
        var box = CreatePanel(panel.transform, "DialogueBox",
            new Color(0.08f, 0.06f, 0.14f, 0.95f), new Vector2(900, 300));
        SetAnchored(box, new Vector2(0, -650), new Vector2(900, 300));
        var boxOutline = box.AddComponent<Outline>();
        boxOutline.effectColor = new Color(0.8f, 0.65f, 0.2f);
        boxOutline.effectDistance = new Vector2(2, -2);

        // Title
        var title = CreateTMPText(box.transform, "TutorialTitle", "Welcome!",
            32, TextAlignmentOptions.Center, new Color(0.95f, 0.85f, 0.4f));
        SetAnchored(title, new Vector2(0, 100), new Vector2(800, 50));

        // Description
        var desc = CreateTMPText(box.transform, "TutorialDesc", "",
            22, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.82f, 0.75f));
        SetAnchored(desc, new Vector2(0, 20), new Vector2(800, 120));

        // Next button
        var nextBtn = CreateButton(box.transform, "NextButton", "Next",
            new Color(0.25f, 0.55f, 0.3f));
        SetAnchored(nextBtn, new Vector2(200, -110), new Vector2(180, 50));

        // Next button text
        var nextBtnText = nextBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Skip button
        var skipBtn = CreateButton(box.transform, "SkipButton", "Skip Tutorial",
            new Color(0.3f, 0.2f, 0.2f));
        SetAnchored(skipBtn, new Vector2(-200, -110), new Vector2(180, 50));

        // Wire TutorialUI serialized fields
        var soTut = new SerializedObject(tutUI);
        soTut.FindProperty("_overlayRoot").objectReferenceValue    = panel;
        soTut.FindProperty("_dimBackground").objectReferenceValue  = dim;
        soTut.FindProperty("_dialogueBox").objectReferenceValue    = box;
        soTut.FindProperty("_titleText").objectReferenceValue      = title.GetComponent<TextMeshProUGUI>();
        soTut.FindProperty("_descriptionText").objectReferenceValue = desc.GetComponent<TextMeshProUGUI>();
        soTut.FindProperty("_nextButton").objectReferenceValue     = nextBtn.GetComponent<Button>();
        soTut.FindProperty("_nextButtonText").objectReferenceValue = nextBtnText;
        soTut.FindProperty("_skipButton").objectReferenceValue     = skipBtn.GetComponent<Button>();
        soTut.FindProperty("_highlightFrame").objectReferenceValue = highlight;
        soTut.FindProperty("_arrowImage").objectReferenceValue     = arrowImg;
        soTut.ApplyModifiedProperties();

        panel.SetActive(false); // Hidden until tutorial starts
        return panel;
    }

    static void WireNPCInteractionUI(GameObject dialoguePanel) { /* wired in BuildDialoguePanel */ }
    static void WireCastleViewUI(GameObject castlePanel) { /* wired in BuildCastleViewPanel */ }
    static void WireWorldMapUI(GameObject worldMapPanel) { /* wired in BuildWorldMapPanel */ }
    static void WireSettingsUI(GameObject settingsPanel) { /* wired in BuildSettingsPanel */ }

    // ─────────────────────────────────────────────────────────────
    //  CI BUILD ENTRY POINTS  (called via -executeMethod)
    // ─────────────────────────────────────────────────────────────

    /// <summary>Called by GitHub Actions: Unity.exe -executeMethod SceneAutoBuilder.BuildWebGL</summary>
    public static void BuildWebGL()
    {
        string outPath = "Builds/WebGL";
        Directory.CreateDirectory(outPath);
        Debug.Log($"[SceneBuilder] Building WebGL → {outPath}");

        // Disable compression for GitHub Pages compatibility (no Content-Encoding header support)
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = true;

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes             = GetBuildScenePaths(),
            locationPathName   = outPath,
            target             = BuildTarget.WebGL,
            options            = BuildOptions.None
        });

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[SceneBuilder] WebGL build succeeded ({report.summary.totalSize / 1024} KB)");
        else
        {
            Debug.LogError($"[SceneBuilder] WebGL build FAILED: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Called by GitHub Actions: Unity.exe -executeMethod SceneAutoBuilder.BuildWindows</summary>
    public static void BuildWindows()
    {
        string outPath = "Builds/Windows/LittleLordMajesty.exe";
        Directory.CreateDirectory("Builds/Windows");
        Debug.Log($"[SceneBuilder] Building Windows → {outPath}");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes             = GetBuildScenePaths(),
            locationPathName   = outPath,
            target             = BuildTarget.StandaloneWindows64,
            options            = BuildOptions.None
        });

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            Debug.Log($"[SceneBuilder] Windows build succeeded ({report.summary.totalSize / 1024} KB)");
        else
        {
            Debug.LogError($"[SceneBuilder] Windows build FAILED: {report.summary.result}");
            EditorApplication.Exit(1);
        }
    }

    static string[] GetBuildScenePaths()
    {
        // Use Build Settings if populated; otherwise fall back to known scene paths
        var configured = EditorBuildSettings.scenes;
        if (configured != null && configured.Length > 0)
        {
            var paths = new System.Collections.Generic.List<string>();
            foreach (var s in configured)
                if (s.enabled) paths.Add(s.path);
            if (paths.Count > 0) return paths.ToArray();
        }
        return new[] { $"{SCENES_PATH}/Bootstrap.unity", $"{SCENES_PATH}/Game.unity" };
    }

    // ─────────────────────────────────────────────────────────────
    //  BUILD SETTINGS
    // ─────────────────────────────────────────────────────────────

    static void AddScenesToBuildSettings()
    {
        var scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene($"{SCENES_PATH}/Bootstrap.unity", true),
            new EditorBuildSettingsScene($"{SCENES_PATH}/Game.unity",      true),
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("[SceneBuilder] Build Settings updated with Bootstrap (0) + Game (1).");
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPER METHODS
    // ─────────────────────────────────────────────────────────────

    static GameObject CreateCanvas(string name, out CanvasScaler scaler)
    {
        // CRITICAL: Direct property assignment on RectTransform fails to persist in batch
        // mode (SaveScene writes the struct back as all-zero). The reliable path is
        // SerializedObject.ApplyModifiedProperties(), which writes the field values
        // through Unity's serialization pipeline so SaveScene picks them up.
        //
        // Symptom of a broken canvas: ScreenSpaceOverlay still renders UI correctly
        // (screen-space projection ignores localScale), but GraphicRaycaster computes
        // hit-tests in world coords — a zero-scale canvas collapses every child to
        // the origin and silently drops every click. Cost hours to diagnose.
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        scaler = go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        // Normalise via SerializedObject — direct property assignment doesn't stick
        // through SaveScene in batch mode.
        var so = new SerializedObject(rt);
        so.FindProperty("m_LocalScale").vector3Value = Vector3.one;
        so.FindProperty("m_LocalPosition").vector3Value = Vector3.zero;
        so.FindProperty("m_LocalRotation").quaternionValue = Quaternion.identity;
        so.FindProperty("m_AnchorMin").vector2Value = Vector2.zero;
        so.FindProperty("m_AnchorMax").vector2Value = Vector2.one;
        so.FindProperty("m_AnchoredPosition").vector2Value = Vector2.zero;
        so.FindProperty("m_SizeDelta").vector2Value = Vector2.zero;
        so.FindProperty("m_Pivot").vector2Value = new Vector2(0.5f, 0.5f);
        so.ApplyModifiedPropertiesWithoutUndo();

        Debug.Log($"[SceneBuilder] CreateCanvas({name}): localScale = {rt.localScale}");
        return go;
    }

    static void CreateEventSystem()
    {
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
    }

    static GameObject CreateFullscreenPanel(Transform parent, string name, Color color)
    {
        var go = CreatePanel(parent, name, color, Vector2.zero);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        // Defensive: a fully-transparent Image is almost never meant to be a click target.
        // Without this, invisible overlays silently eat clicks from everything beneath.
        if (color.a <= 0.001f) img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        return go;
    }

    static GameObject CreateTMPText(Transform parent, string name, string text,
        int fontSize, TextAlignmentOptions align, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = false;
        // Note: TextMeshProUGUI already adds RectTransform implicitly
        return go;
    }

    /// <summary>
    /// Creates a button with a themed image background, a centered TMP label,
    /// a 1px dark outline for contrast against bright backgrounds, and proper
    /// hover/pressed/selected/disabled color states for keyboard and mouse
    /// focus feedback. Label uses TMP auto-size so long words (e.g. Back to
    /// Castle, Neues Spiel, Launch Siege) don't overflow the button rect.
    /// </summary>
    static GameObject CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        // Outline on the button frame gives high-contrast separation from the
        // dark HUD panel underneath and helps every button feel "clickable".
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text = label;
        // Auto-size: TMP picks the largest size in [14, 26] that fits the
        // button rect. Prevents clipping on short buttons and long labels.
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = 14;
        lbl.fontSizeMax = 26;
        lbl.fontSize    = 24;
        lbl.fontStyle   = FontStyles.Bold;
        lbl.alignment   = TextAlignmentOptions.Center;
        lbl.color       = Color.white;
        lbl.enableWordWrapping = false;
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        lbl.raycastTarget = false;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        // Small horizontal inset so the label never touches the button edge.
        lblRT.offsetMin = new Vector2(10, 4);
        lblRT.offsetMax = new Vector2(-10, -4);

        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.normalColor      = bgColor;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(bgColor.r * 1.35f + 0.05f),
            Mathf.Clamp01(bgColor.g * 1.35f + 0.05f),
            Mathf.Clamp01(bgColor.b * 1.35f + 0.05f),
            bgColor.a);
        colors.pressedColor     = new Color(bgColor.r * 0.65f, bgColor.g * 0.65f, bgColor.b * 0.65f, bgColor.a);
        colors.selectedColor    = new Color(
            Mathf.Clamp01(bgColor.r * 1.20f),
            Mathf.Clamp01(bgColor.g * 1.20f),
            Mathf.Clamp01(bgColor.b * 1.20f),
            bgColor.a);
        colors.disabledColor    = new Color(bgColor.r * 0.45f, bgColor.g * 0.45f, bgColor.b * 0.45f, 0.6f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.1f;
        btn.colors = colors;

        return go;
    }

    static GameObject CreateInputField(Transform parent, string name, string placeholder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.12f, 0.12f, 0.2f);

        var inputField = go.AddComponent<TMP_InputField>();

        var textAreaGO = new GameObject("Text Area");
        textAreaGO.transform.SetParent(go.transform, false);
        textAreaGO.AddComponent<RectMask2D>();
        var taRT = textAreaGO.GetComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(10, 4); taRT.offsetMax = new Vector2(-10, -4);

        var phGO = new GameObject("Placeholder");
        phGO.transform.SetParent(textAreaGO.transform, false);
        var ph = phGO.AddComponent<TextMeshProUGUI>();
        ph.text = placeholder;
        ph.fontSize = 26;
        ph.color = new Color(0.5f, 0.5f, 0.6f);
        var phRT = phGO.GetComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;

        var txtGO = new GameObject("Text");
        txtGO.transform.SetParent(textAreaGO.transform, false);
        var txt = txtGO.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 26;
        txt.color = Color.white;
        var txtRT = txtGO.GetComponent<RectTransform>();
        txtRT.anchorMin = Vector2.zero; txtRT.anchorMax = Vector2.one;
        txtRT.offsetMin = Vector2.zero; txtRT.offsetMax = Vector2.zero;

        inputField.textComponent = txt;
        inputField.placeholder = ph;
        inputField.targetGraphic = bg;

        return go;
    }

    static (ScrollRect, RectTransform) CreateScrollView(Transform parent, string name)
    {
        var scrollGO = new GameObject(name);
        scrollGO.transform.SetParent(parent, false);
        var scrollRT = scrollGO.AddComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0.05f); scrollRT.anchorMax = new Vector2(1, 0.85f);
        scrollRT.offsetMin = new Vector2(10, 0); scrollRT.offsetMax = new Vector2(-10, 0);

        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.3f);

        var vpGO = new GameObject("Viewport");
        vpGO.transform.SetParent(scrollGO.transform, false);
        vpGO.AddComponent<RectMask2D>();
        var vpRT = vpGO.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;

        var contentGO = new GameObject("Content");
        contentGO.transform.SetParent(vpGO.transform, false);
        var contentRT = contentGO.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1);
        contentRT.offsetMin = Vector2.zero; contentRT.offsetMax = Vector2.zero;

        var vlg = contentGO.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 6;
        vlg.padding = new RectOffset(8, 8, 8, 8);

        var csf = contentGO.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.content = contentRT;
        scrollRect.viewport = vpRT;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 30;

        return (scrollRect, contentRT);
    }

    static void SetAnchored(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
    }

    static void SetupSliderVisuals(GameObject sliderGO, Slider slider, Color fillColor)
    {
        var bgGO = new GameObject("Background"); bgGO.transform.SetParent(sliderGO.transform, false);
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = new Color(0.2f, 0.2f, 0.2f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        var fillAreaGO = new GameObject("Fill Area"); fillAreaGO.transform.SetParent(sliderGO.transform, false);
        var fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = Vector2.zero; fillAreaRT.anchorMax = Vector2.one;
        fillAreaRT.offsetMin = new Vector2(3, 0); fillAreaRT.offsetMax = new Vector2(-3, 0);

        var fillGO = new GameObject("Fill"); fillGO.transform.SetParent(fillAreaGO.transform, false);
        var fillImg = fillGO.AddComponent<Image>(); fillImg.color = fillColor;
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero; fillRT.anchorMax = new Vector2(0, 1);
        fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
        fillRT.sizeDelta = new Vector2(10, 0);

        slider.fillRect = fillRT;
        slider.targetGraphic = fillImg;
    }

    static TextMeshProUGUI FindTMP(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    static Button FindButton(Transform root, string name)
    {
        var t = FindDeep(root, name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    /// <summary>
    /// Depth-first child search by name. Transform.Find() only checks direct children,
    /// which breaks for panels with nested hierarchies (EventPanel → EventCard → EventTitle).
    /// </summary>
    static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }
}
