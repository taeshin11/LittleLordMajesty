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

        // Toast layer (on top of everything)
        var toastLayer = CreateFullscreenPanel(canvas.transform, "ToastLayer", Color.clear);
        var toastNotif = toastLayer.AddComponent<ToastNotification>();

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

        // Wire MainMenuUI
        var mmu = panel.GetComponent<MainMenuUI>();
        var soMM = new SerializedObject(mmu);
        soMM.FindProperty("_startButton").objectReferenceValue    = startBtn.GetComponent<Button>();
        soMM.FindProperty("_continueButton").objectReferenceValue = contBtn.GetComponent<Button>();
        soMM.FindProperty("_settingsButton").objectReferenceValue = settBtn.GetComponent<Button>();
        soMM.FindProperty("_quitButton").objectReferenceValue     = quitBtn.GetComponent<Button>();
        soMM.FindProperty("_playerNameInput").objectReferenceValue = nameInput.GetComponent<TMP_InputField>();
        soMM.FindProperty("_titleText").objectReferenceValue      = title.GetComponent<TextMeshProUGUI>();
        soMM.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildCastleViewPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "CastleViewPanel", new Color(0.08f, 0.06f, 0.12f));
        var castleUI = panel.AddComponent<CastleViewUI>();

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

        var woodTxt = CreateTMPText(resStrip.transform, "WoodText",  "🪵 500", 24, TextAlignmentOptions.Center, new Color(0.7f, 0.5f, 0.3f));
        var foodTxt = CreateTMPText(resStrip.transform, "FoodText",  "🌾 500", 24, TextAlignmentOptions.Center, new Color(0.4f, 0.8f, 0.3f));
        var goldTxt = CreateTMPText(resStrip.transform, "GoldText",  "💰 200", 24, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.1f));
        var popTxt  = CreateTMPText(resStrip.transform, "PopulationText", "👥 20/50", 24, TextAlignmentOptions.Center, Color.white);

        SetAnchored(woodTxt, new Vector2(-380, 0), new Vector2(200, 50));
        SetAnchored(foodTxt, new Vector2(-130, 0), new Vector2(200, 50));
        SetAnchored(goldTxt, new Vector2( 120, 0), new Vector2(200, 50));
        SetAnchored(popTxt,  new Vector2( 370, 0), new Vector2(200, 50));

        // NPC container (castle interior area)
        var npcContainer = new GameObject("NPCContainer");
        npcContainer.transform.SetParent(panel.transform, false);
        var npcContRT = npcContainer.AddComponent<RectTransform>();
        npcContRT.anchorMin = new Vector2(0, 0.15f); npcContRT.anchorMax = new Vector2(1, 0.75f);
        npcContRT.offsetMin = Vector2.zero; npcContRT.offsetMax = Vector2.zero;

        // Building container
        var buildingContainer = new GameObject("BuildingContainer");
        buildingContainer.transform.SetParent(panel.transform, false);
        var buildContRT = buildingContainer.AddComponent<RectTransform>();
        buildContRT.anchorMin = new Vector2(0, 0); buildContRT.anchorMax = new Vector2(1, 0.15f);
        buildContRT.offsetMin = Vector2.zero; buildContRT.offsetMax = Vector2.zero;

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
        var menuBtn     = CreateButton(actionBar.transform, "MenuButton",    "☰",        new Color(0.25f, 0.25f, 0.25f));

        SetAnchored(buildBtn,    new Vector2(-430, 0), new Vector2(160, 70));
        SetAnchored(saveBtn,     new Vector2(-215, 0), new Vector2(160, 70));
        SetAnchored(npcListBtn,  new Vector2(   0, 0), new Vector2(160, 70));
        SetAnchored(worldMapBtn, new Vector2( 215, 0), new Vector2(160, 70));
        SetAnchored(menuBtn,     new Vector2( 430, 0), new Vector2(100, 70));

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

        // NPC List drawer
        var npcListPanel = CreatePanel(panel.transform, "NPCListPanel",
            new Color(0.08f, 0.08f, 0.15f, 0.98f), new Vector2(600, 800));
        var nlpRT = npcListPanel.GetComponent<RectTransform>();
        nlpRT.anchorMin = new Vector2(0, 0); nlpRT.anchorMax = new Vector2(0, 1);
        nlpRT.pivot = new Vector2(0, 0.5f);
        nlpRT.offsetMin = Vector2.zero; nlpRT.offsetMax = new Vector2(600, 0);
        npcListPanel.SetActive(false);

        var (npcScrollRect, npcListContent) = CreateScrollView(npcListPanel.transform, "NPCListScroll");

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
        soCastle.FindProperty("_npcContainer").objectReferenceValue         = npcContainer.transform;
        soCastle.FindProperty("_buildingContainer").objectReferenceValue    = buildingContainer.transform;
        soCastle.FindProperty("_lordTitleText").objectReferenceValue        = lordTitle.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_dateText").objectReferenceValue             = dateText.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_woodText").objectReferenceValue             = woodTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_foodText").objectReferenceValue             = foodTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_goldText").objectReferenceValue             = goldTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_populationText").objectReferenceValue       = popTxt.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_menuButton").objectReferenceValue           = menuBtn.GetComponent<Button>();
        soCastle.FindProperty("_worldMapButton").objectReferenceValue       = worldMapBtn.GetComponent<Button>();
        soCastle.FindProperty("_buildButton").objectReferenceValue          = buildBtn.GetComponent<Button>();
        soCastle.FindProperty("_saveButton").objectReferenceValue           = saveBtn.GetComponent<Button>();
        soCastle.FindProperty("_npcListButton").objectReferenceValue        = npcListBtn.GetComponent<Button>();
        soCastle.FindProperty("_notificationBanner").objectReferenceValue   = notifBanner;
        soCastle.FindProperty("_notificationText").objectReferenceValue     = notifText.GetComponent<TextMeshProUGUI>();
        soCastle.FindProperty("_npcListPanel").objectReferenceValue         = npcListPanel;
        soCastle.FindProperty("_npcListContent").objectReferenceValue       = npcListContent;
        soCastle.FindProperty("_buildingMenuPanel").objectReferenceValue    = buildMenuPanel;
        soCastle.FindProperty("_buildingMenuContent").objectReferenceValue  = buildMenuContent;
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

        var loyaltyTxt = CreateTMPText(infoBar.transform, "LoyaltyText", "♥ 75/100",
            20, TextAlignmentOptions.Right, new Color(0.9f, 0.4f, 0.4f));
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

        // Close button
        var closeBtn = CreateButton(infoBar.transform, "CloseButton", "✕", new Color(0.5f, 0.2f, 0.2f));
        SetAnchored(closeBtn, new Vector2(480, 0), new Vector2(70, 70));

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
        var quickCmdsGO = new GameObject("QuickCommandsStrip"); quickCmdsGO.transform.SetParent(panel.transform, false);
        var qcRT = quickCmdsGO.GetComponent<RectTransform>() ?? quickCmdsGO.AddComponent<RectTransform>();
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

        var sendBtn = CreateButton(inputBar.transform, "SendButton", "➤", new Color(0.3f, 0.6f, 0.2f));
        SetAnchored(sendBtn, new Vector2(440, 0), new Vector2(85, 75));

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
        soNPC.FindProperty("_thinkingBubblePrefab").objectReferenceValue = null; // Will use PlaceholderPrefabs
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
        var panel = CreateFullscreenPanel(parent, "SettingsPanel", new Color(0.06f, 0.05f, 0.1f));
        panel.AddComponent<SettingsUI>();
        var header = CreateTMPText(panel.transform, "SettingsTitle", "SETTINGS",
            48, TextAlignmentOptions.Center, Color.white);
        SetAnchored(header, new Vector2(0, 700), new Vector2(600, 80));

        var closeBtn = CreateButton(panel.transform, "CloseButton", "✕ Back", new Color(0.3f, 0.3f, 0.3f));
        SetAnchored(closeBtn, new Vector2(-420, 700), new Vector2(150, 70));

        var soSett = new SerializedObject(panel.GetComponent<SettingsUI>());
        soSett.FindProperty("_closeButton").objectReferenceValue  = closeBtn.GetComponent<Button>();
        soSett.FindProperty("_titleText").objectReferenceValue    = header.GetComponent<TextMeshProUGUI>();
        soSett.ApplyModifiedProperties();

        return panel;
    }

    static GameObject BuildWorldMapPanel(Transform parent)
    {
        var panel = CreateFullscreenPanel(parent, "WorldMapPanel", new Color(0.05f, 0.08f, 0.05f));
        panel.AddComponent<WorldMapUI>();

        var header = CreateTMPText(panel.transform, "WorldMapTitle", "WORLD MAP",
            40, TextAlignmentOptions.Center, new Color(0.7f, 0.9f, 0.7f));
        SetAnchored(header, new Vector2(0, 850), new Vector2(600, 70));

        var closeBtn = CreateButton(panel.transform, "CloseButton", "← Castle", new Color(0.2f, 0.3f, 0.2f));
        SetAnchored(closeBtn, new Vector2(-400, 850), new Vector2(220, 65));

        var mapContainer = new GameObject("MapContainer"); mapContainer.transform.SetParent(panel.transform, false);
        var mapContRT = mapContainer.AddComponent<RectTransform>();
        mapContRT.anchorMin = new Vector2(0, 0.1f); mapContRT.anchorMax = new Vector2(1, 0.9f);
        mapContRT.offsetMin = new Vector2(20, 0); mapContRT.offsetMax = new Vector2(-20, 0);

        var soMap = new SerializedObject(panel.GetComponent<WorldMapUI>());
        soMap.FindProperty("_mapContainer").objectReferenceValue  = mapContainer.transform;
        soMap.FindProperty("_titleText").objectReferenceValue     = header.GetComponent<TextMeshProUGUI>();
        soMap.FindProperty("_closeButton").objectReferenceValue   = closeBtn.GetComponent<Button>();
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
        soLB.FindProperty("_titleText").objectReferenceValue  = header.GetComponent<TextMeshProUGUI>();
        soLB.FindProperty("_scrollRect").objectReferenceValue = scrollRect;
        soLB.FindProperty("_entryContainer").objectReferenceValue = content;
        soLB.ApplyModifiedProperties();

        return panel;
    }

    static void WireNPCInteractionUI(GameObject dialoguePanel) { /* wired in BuildDialoguePanel */ }
    static void WireCastleViewUI(GameObject castlePanel) { /* wired in BuildCastleViewPanel */ }
    static void WireWorldMapUI(GameObject worldMapPanel) { /* wired in BuildWorldMapPanel */ }
    static void WireSettingsUI(GameObject settingsPanel) { /* wired in BuildSettingsPanel */ }

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
        var go = new GameObject(name);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        scaler = go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
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
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject CreateButton(Transform parent, string name, string label, Color bgColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = bgColor;
        var btn = go.AddComponent<Button>();

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lbl = lblGO.AddComponent<TextMeshProUGUI>();
        lbl.text = label;
        lbl.fontSize = 28;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.color = Color.white;
        var lblRT = lblGO.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;

        btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(bgColor.r * 1.3f, bgColor.g * 1.3f, bgColor.b * 1.3f);
        colors.pressedColor = new Color(bgColor.r * 0.7f, bgColor.g * 0.7f, bgColor.b * 0.7f);
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
        var t = root.Find(name);
        return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
    }

    static Button FindButton(Transform root, string name)
    {
        var t = root.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }
}
