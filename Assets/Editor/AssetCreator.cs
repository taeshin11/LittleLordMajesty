using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Creates required ScriptableObject assets (GameConfig, UITheme, NPCDatabase).
/// Run "LittleLordMajesty > Create Config Assets" after opening the project.
/// Then open each asset in Inspector to fill in your API keys.
/// </summary>
public static class AssetCreator
{
    [MenuItem("LittleLordMajesty/Create Config Assets")]
    public static void CreateAllConfigAssets()
    {
        CreateGameConfig();
        CreateUITheme();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AssetCreator] All config assets created in Resources/Config/");
        EditorUtility.DisplayDialog("Assets Created!",
            "Created:\n• Resources/Config/GameConfig\n• Resources/Config/UITheme\n\n" +
            "Open GameConfig in Inspector to add your API keys.", "OK");
    }

    [MenuItem("LittleLordMajesty/Create Message Prefabs")]
    public static void CreateMessagePrefabs()
    {
        EnsureDir("Assets/Resources/Prefabs");

        // Chat UI prefabs (screen-space overlay)
        CreatePlayerMessagePrefab();
        CreateNPCMessagePrefab();
        CreateThinkingBubblePrefab();
        // NPC world representation is 3D (CastleScene3D) — no 2D sprite prefab needed
        CreateNPCListItemPrefab();
        CreateBuildingMenuItemPrefab();
        CreateQuickCommandButtonPrefab();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AssetCreator] All UI prefabs created in Resources/Prefabs/");
    }

    // ─────────────────────────────────────────────────────────────
    //  CONFIG ASSETS
    // ─────────────────────────────────────────────────────────────

    static void CreateGameConfig()
    {
        EnsureDir("Assets/Resources/Config");
        const string path = "Assets/Resources/Config/GameConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<GameConfig>(path) != null)
        {
            Debug.Log("[AssetCreator] GameConfig already exists, skipping.");
            return;
        }

        var config = ScriptableObject.CreateInstance<GameConfig>();
        config.EnableTTS = false;          // Off by default until key is added
        config.EnableFirebase = false;     // Off by default
        config.EnableDebugLogs = true;     // On for development
        config.GeminiTemperature = 0.85f;
        config.GeminiMaxOutputTokens = 512;
        config.TTSSpeakingRate = 1.0f;
        config.MaxCacheResponseCount = 500;
        config.MaxTTSCacheFileSizeMB = 100;

        AssetDatabase.CreateAsset(config, path);
        Debug.Log($"[AssetCreator] GameConfig created at {path}");

        // Highlight it in Project window
        Selection.activeObject = config;
        EditorGUIUtility.PingObject(config);
    }

    static void CreateUITheme()
    {
        EnsureDir("Assets/Resources/Config");
        const string path = "Assets/Resources/Config/UITheme.asset";
        if (AssetDatabase.LoadAssetAtPath<UITheme>(path) != null) return;

        var theme = ScriptableObject.CreateInstance<UITheme>();
        AssetDatabase.CreateAsset(theme, path);
        Debug.Log($"[AssetCreator] UITheme created at {path}");
    }

    // ─────────────────────────────────────────────────────────────
    //  UI PREFABS
    // ─────────────────────────────────────────────────────────────

    static void CreatePlayerMessagePrefab()
    {
        const string path = "Assets/Resources/Prefabs/PlayerMessage.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("PlayerMessage");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.2f, 0.35f, 0.5f);

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 60);

        go.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 60;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TMPro.TextAlignmentOptions.Right;
        tmp.enableWordWrapping = true;
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(12, 6); tRT.offsetMax = new Vector2(-12, -6);

        var csf = go.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        SavePrefab(go, path);
    }

    static void CreateNPCMessagePrefab()
    {
        const string path = "Assets/Resources/Prefabs/NPCMessage.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("NPCMessage");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.12f, 0.1f, 0.18f);

        go.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 60;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.fontSize = 24;
        tmp.color = new Color(0.9f, 0.87f, 0.8f);
        tmp.alignment = TMPro.TextAlignmentOptions.Left;
        tmp.enableWordWrapping = true;
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(12, 6); tRT.offsetMax = new Vector2(-12, -6);

        var csf = go.AddComponent<UnityEngine.UI.ContentSizeFitter>();
        csf.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

        SavePrefab(go, path);
    }

    static void CreateThinkingBubblePrefab()
    {
        const string path = "Assets/Resources/Prefabs/ThinkingBubble.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("ThinkingBubble");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.15f, 0.15f, 0.25f);
        go.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 50;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(go.transform, false);
        var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "● ● ●";
        tmp.fontSize = 28;
        tmp.color = new Color(0.6f, 0.6f, 0.9f);
        tmp.alignment = TMPro.TextAlignmentOptions.Left;
        var tRT = textGO.GetComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero; tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(16, 0); tRT.offsetMax = new Vector2(-16, 0);

        SavePrefab(go, path);
    }

    static void CreateNPCSpritePrefab()
    {
        const string path = "Assets/Resources/Prefabs/NPCSprite.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("NPCSprite");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(80, 120);

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.5f, 0.5f, 0.7f);

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;

        // Speech bubble (initially hidden)
        var bubbleGO = new GameObject("SpeechBubble");
        bubbleGO.transform.SetParent(go.transform, false);
        var bubbleImg = bubbleGO.AddComponent<UnityEngine.UI.Image>();
        bubbleImg.color = new Color(0.95f, 0.95f, 0.9f, 0.95f);
        var bubbleRT = bubbleGO.GetComponent<RectTransform>();
        bubbleRT.anchoredPosition = new Vector2(60, 80);
        bubbleRT.sizeDelta = new Vector2(200, 80);

        var speechText = new GameObject("SpeechText");
        speechText.transform.SetParent(bubbleGO.transform, false);
        var st = speechText.AddComponent<TMPro.TextMeshProUGUI>();
        st.fontSize = 18;
        st.color = Color.black;
        st.alignment = TMPro.TextAlignmentOptions.Center;
        st.enableWordWrapping = true;
        var stRT = speechText.GetComponent<RectTransform>();
        stRT.anchorMin = Vector2.zero; stRT.anchorMax = Vector2.one;
        stRT.offsetMin = new Vector2(6, 4); stRT.offsetMax = new Vector2(-6, -4);
        bubbleGO.SetActive(false);

        // NPCSpriteController removed — 3D NPCs handled by NPC3DClickHandler in CastleScene3D
        SavePrefab(go, path);
    }

    static void CreateBuildingSlotPrefab()
    {
        const string path = "Assets/Resources/Prefabs/BuildingSlot.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("BuildingSlot");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(100, 100);

        var img = go.AddComponent<UnityEngine.UI.Image>();
        img.color = new Color(0.2f, 0.2f, 0.3f);

        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = img;

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        var label = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
        label.text = "Build";
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TMPro.TextAlignmentOptions.Center;
        var lRT = labelGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = Vector2.zero; lRT.offsetMax = Vector2.zero;

        SavePrefab(go, path);
    }

    static void CreateNPCListItemPrefab()
    {
        const string path = "Assets/Resources/Prefabs/NPCListItem.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("NPCListItem");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.1f, 0.1f, 0.18f);
        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredHeight = 80;
        le.flexibleWidth = 1;

        var nameGO = new GameObject("Name");
        nameGO.transform.SetParent(go.transform, false);
        var nameTxt = nameGO.AddComponent<TMPro.TextMeshProUGUI>();
        nameTxt.fontSize = 24; nameTxt.color = Color.white;
        nameTxt.alignment = TMPro.TextAlignmentOptions.Left;
        var nameRT = nameGO.GetComponent<RectTransform>();
        nameRT.anchorMin = new Vector2(0, 0.5f); nameRT.anchorMax = new Vector2(0.5f, 1);
        nameRT.offsetMin = new Vector2(12, 0); nameRT.offsetMax = Vector2.zero;

        var taskGO = new GameObject("Task");
        taskGO.transform.SetParent(go.transform, false);
        var taskTxt = taskGO.AddComponent<TMPro.TextMeshProUGUI>();
        taskTxt.fontSize = 18; taskTxt.color = new Color(0.6f, 0.6f, 0.8f);
        taskTxt.alignment = TMPro.TextAlignmentOptions.Left;
        var taskRT = taskGO.GetComponent<RectTransform>();
        taskRT.anchorMin = new Vector2(0, 0); taskRT.anchorMax = new Vector2(0.5f, 0.5f);
        taskRT.offsetMin = new Vector2(12, 0); taskRT.offsetMax = Vector2.zero;

        var talkBtnGO = new GameObject("TalkButton");
        talkBtnGO.transform.SetParent(go.transform, false);
        var talkBtnImg = talkBtnGO.AddComponent<UnityEngine.UI.Image>();
        talkBtnImg.color = new Color(0.3f, 0.2f, 0.5f);
        var talkBtn = talkBtnGO.AddComponent<UnityEngine.UI.Button>();
        talkBtn.targetGraphic = talkBtnImg;
        var talkRT = talkBtnGO.GetComponent<RectTransform>();
        talkRT.anchorMin = new Vector2(0.75f, 0.1f); talkRT.anchorMax = new Vector2(0.98f, 0.9f);
        talkRT.offsetMin = Vector2.zero; talkRT.offsetMax = Vector2.zero;
        var talkLblGO = new GameObject("Label");
        talkLblGO.transform.SetParent(talkBtnGO.transform, false);
        var talkLbl = talkLblGO.AddComponent<TMPro.TextMeshProUGUI>();
        talkLbl.text = "Talk"; talkLbl.fontSize = 22;
        talkLbl.color = Color.white; talkLbl.alignment = TMPro.TextAlignmentOptions.Center;
        var tlRT = talkLblGO.GetComponent<RectTransform>();
        tlRT.anchorMin = Vector2.zero; tlRT.anchorMax = Vector2.one;
        tlRT.offsetMin = Vector2.zero; tlRT.offsetMax = Vector2.zero;

        SavePrefab(go, path);
    }

    static void CreateBuildingMenuItemPrefab()
    {
        const string path = "Assets/Resources/Prefabs/BuildingMenuItem.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("BuildingMenuItem");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.1f, 0.1f, 0.18f);
        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.preferredHeight = 100; le.flexibleWidth = 1;

        string[] names = { "Name", "Cost", "Description" };
        float[] yFrac = { 0.65f, 0.35f, 0.1f };
        float[] heights = { 0.35f, 0.3f, 0.3f };

        for (int i = 0; i < names.Length; i++)
        {
            var tGO = new GameObject(names[i]);
            tGO.transform.SetParent(go.transform, false);
            var tmp = tGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.fontSize = i == 0 ? 26 : 20;
            tmp.color = i == 0 ? Color.white : new Color(0.7f, 0.7f, 0.7f);
            tmp.alignment = TMPro.TextAlignmentOptions.Left;
            var tRT = tGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0.02f, yFrac[i] - heights[i]);
            tRT.anchorMax = new Vector2(0.65f, yFrac[i]);
            tRT.offsetMin = Vector2.zero; tRT.offsetMax = Vector2.zero;
        }

        var buildBtnGO = new GameObject("BuildButton");
        buildBtnGO.transform.SetParent(go.transform, false);
        var bbImg = buildBtnGO.AddComponent<UnityEngine.UI.Image>();
        bbImg.color = new Color(0.2f, 0.45f, 0.2f);
        var bbBtn = buildBtnGO.AddComponent<UnityEngine.UI.Button>();
        bbBtn.targetGraphic = bbImg;
        var bbRT = buildBtnGO.GetComponent<RectTransform>();
        bbRT.anchorMin = new Vector2(0.7f, 0.1f); bbRT.anchorMax = new Vector2(0.98f, 0.9f);
        bbRT.offsetMin = Vector2.zero; bbRT.offsetMax = Vector2.zero;
        var bbLblGO = new GameObject("Label");
        bbLblGO.transform.SetParent(buildBtnGO.transform, false);
        var bbLbl = bbLblGO.AddComponent<TMPro.TextMeshProUGUI>();
        bbLbl.text = "Build"; bbLbl.fontSize = 22;
        bbLbl.color = Color.white; bbLbl.alignment = TMPro.TextAlignmentOptions.Center;
        var bblRT = bbLblGO.GetComponent<RectTransform>();
        bblRT.anchorMin = Vector2.zero; bblRT.anchorMax = Vector2.one;
        bblRT.offsetMin = Vector2.zero; bblRT.offsetMax = Vector2.zero;

        SavePrefab(go, path);
    }

    static void CreateQuickCommandButtonPrefab()
    {
        const string path = "Assets/Resources/Prefabs/QuickCommandButton.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;

        var go = new GameObject("QuickCommandButton");
        var bg = go.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.2f, 0.2f, 0.35f);
        var btn = go.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = bg;
        var le = go.AddComponent<UnityEngine.UI.LayoutElement>();
        le.minWidth = 150; le.minHeight = 50; le.flexibleWidth = 0;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(150, 50);

        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(go.transform, false);
        var lbl = lblGO.AddComponent<TMPro.TextMeshProUGUI>();
        lbl.fontSize = 20; lbl.color = new Color(0.8f, 0.8f, 1f);
        lbl.alignment = TMPro.TextAlignmentOptions.Center;
        lbl.enableWordWrapping = false;
        var lRT = lblGO.GetComponent<RectTransform>();
        lRT.anchorMin = Vector2.zero; lRT.anchorMax = Vector2.one;
        lRT.offsetMin = new Vector2(6, 0); lRT.offsetMax = new Vector2(-6, 0);

        SavePrefab(go, path);
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    static void SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log($"[AssetCreator] Prefab saved: {path}");
    }
}
