using UnityEngine;
using TMPro;

/// <summary>
/// 3D roaming pivot — runtime bootstrap that spawns the 3D world using
/// Kenney FBX models from Resources/Models/, with procedural fallbacks.
///
/// 3D top-down perspective: camera angled ~45° looking down at XZ ground plane.
/// Buildings use Castle Kit + Fantasy Town Kit FBX models.
/// Trees/nature use Nature Kit FBX models.
/// Characters use blocky cube humanoids (Minecraft/Crossy Road style) via CharacterBuilder.
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector3 _playerSpawn = new Vector3(0f, 0f, -1f);
    [SerializeField] private float _playerWalkSpeed = 4f;

    private bool _spawned;
    private bool _subscribed;
    private GameObject _player;
    private readonly System.Collections.Generic.Dictionary<string, GameObject> _npcObjects = new();
    private Transform _npcRoot;
    private Camera _roamingCam;

    // Cached primitive meshes — avoids CreatePrimitive which adds Colliders
    // and crashes on WebGL where Physics module is stripped.
    private static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> _primitiveMeshes = new();

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        if (_primitiveMeshes.TryGetValue(type, out var cached) && cached != null)
            return cached;
#if UNITY_EDITOR || !UNITY_WEBGL
        var temp = GameObject.CreatePrimitive(type);
        var autoCollider = temp.GetComponent<Collider>();
        if (autoCollider != null) DestroyImmediate(autoCollider);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        _primitiveMeshes[type] = mesh;
        DestroyImmediate(temp);
        return mesh;
#else
        string meshName = type switch
        {
            PrimitiveType.Cube     => "Cube.fbx",
            PrimitiveType.Sphere   => "Sphere.fbx",
            PrimitiveType.Cylinder => "Cylinder.fbx",
            PrimitiveType.Capsule  => "Capsule.fbx",
            PrimitiveType.Plane    => "Plane.fbx",
            PrimitiveType.Quad     => "Quad.fbx",
            _ => "Cube.fbx"
        };
        var builtInMesh = Resources.GetBuiltinResource<Mesh>(meshName);
        _primitiveMeshes[type] = builtInMesh;
        return builtInMesh;
#endif
    }

    private static GameObject CreateVisualPrimitive(PrimitiveType type, string name = "Primitive")
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetPrimitiveMesh(type);
        go.AddComponent<MeshRenderer>();
        return go;
    }

    /// <summary>Add a BoxCollider to a building so the player can't walk through it.</summary>
    private static void AddBuildingCollider(GameObject building, Vector3 size, Vector3 center = default)
    {
        var col = building.AddComponent<BoxCollider>();
        col.size = size;
        col.center = center == default ? new Vector3(0f, size.y / 2f, 0f) : center;
    }

    // Shared material — tries several shaders, falls through to Unlit/Color
    private static Material _sharedMaterial;
    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;
        string[] shaderNames = {
            "Universal Render Pipeline/Lit",
            "Standard",
            "Mobile/Diffuse",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",
        };
        foreach (var n in shaderNames)
        {
            var shader = Shader.Find(n);
            if (shader != null) { _sharedMaterial = new Material(shader); return _sharedMaterial; }
        }
        _sharedMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
        return _sharedMaterial;
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        renderer.material = new Material(GetSharedMaterial()) { color = color };
    }

    /// <summary>
    /// Load a Kenney FBX model from Resources/Models/{subPath}.
    /// Returns instantiated GameObject or null if not found.
    /// </summary>
    private static GameObject LoadModel(string subPath, Transform parent = null, string name = null)
    {
        var prefab = Resources.Load<GameObject>($"Models/{subPath}");
        if (prefab == null)
        {
            Debug.LogWarning($"[RoamingBootstrap] Model not found: Models/{subPath}");
            return null;
        }
        var instance = Instantiate(prefab);
        if (name != null) instance.name = name;
        if (parent != null) instance.transform.SetParent(parent, false);
        return instance;
    }

    /// <summary>
    /// Load a Kenney FBX model, falling back to a colored primitive if the FBX is missing.
    /// </summary>
    private static GameObject LoadModelOrPrimitive(string subPath, PrimitiveType fallbackType,
        Color fallbackColor, Transform parent = null, string name = null)
    {
        var model = LoadModel(subPath, parent, name);
        if (model != null) return model;

        // Fallback to primitive
        var go = CreateVisualPrimitive(fallbackType, name ?? "Fallback");
        if (parent != null) go.transform.SetParent(parent, false);
        ApplyColor(go, fallbackColor);
        return go;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        if (FindFirstObjectByType<RoamingBootstrap>() != null) return;
        var host = new GameObject("RoamingBootstrap");
        host.AddComponent<RoamingBootstrap>();
        Object.DontDestroyOnLoad(host);
    }

    private void Start() => TrySubscribeAndSpawn();

    private void Update()
    {
        if (!_subscribed) TrySubscribeAndSpawn();
        else if (!_spawned) TrySpawn();
    }

    private void TrySubscribeAndSpawn()
    {
        if (_subscribed) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnGameStateChanged += OnStateChanged;
        _subscribed = true;
        TrySpawn();
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameManager.GameState _old, GameManager.GameState _new)
    {
        if (_new == GameManager.GameState.Castle) TrySpawn();
    }

    private void TrySpawn()
    {
        if (_spawned) return;
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.CurrentState != GameManager.GameState.Castle) return;

        RetireLegacyUI();
        SuppressLegacyTutorial();
        BuildRoamingCamera();
        BuildGround();
        BuildPlayer();
        WireCamera();
        SpawnNPCs();
        SpawnBuildings();
        SpawnEnvironment();
        _spawned = true;
        Debug.Log("[RoamingBootstrap] 3D roaming world built (Kenney FBX models)");
    }

    // ---------------------------------------------------------------
    //  LEGACY UI REMOVAL
    // ---------------------------------------------------------------

    private void RetireLegacyUI()
    {
        string[] retireNames = {
            "CastleViewPanel", "DialoguePanel", "EventPanel",
            "TutorialOverlay", "TopHUD", "ActionBar",
        };
        foreach (var name in retireNames)
        {
            var go = FindIncludingInactive(name);
            if (go != null) Destroy(go);
        }
    }

    private static GameObject FindIncludingInactive(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in all)
        {
            if (go == null || go.name != name) continue;
            if (go.scene.name == null || !go.scene.IsValid()) continue;
            return go;
        }
        return null;
    }

    private void SuppressLegacyTutorial()
    {
        var tut = TutorialSystem.Instance;
        if (tut != null)
        {
            try { tut.enabled = false; } catch { }
        }
    }

    // ---------------------------------------------------------------
    //  3D CAMERA
    // ---------------------------------------------------------------

    private void BuildRoamingCamera()
    {
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.orthographic = true;  // Isometric orthographic
        _roamingCam.orthographicSize = 12f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.53f, 0.81f, 0.92f); // Sky blue
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 200f;
        _roamingCam.depth = 10;
        // Isometric angle: 35° pitch, 45° yaw
        camGO.transform.position = new Vector3(20f, 20f, -20f);
        camGO.transform.rotation = Quaternion.Euler(35f, 45f, 0f);
        try { camGO.tag = "MainCamera"; } catch { }

        var follow = camGO.AddComponent<FollowCamera>();
        follow.SetHeight(10f);
        follow.SetDistance(8f);
    }

    // ---------------------------------------------------------------
    //  3D GROUND
    // ---------------------------------------------------------------

    private void BuildGround()
    {
        // Main ground plane — large and GREEN
        var ground = CreateVisualPrimitive(PrimitiveType.Plane, "Ground");
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(30f, 1f, 30f); // 300x300 units — always fills camera
        ApplyColor(ground, new Color(0.30f, 0.55f, 0.20f)); // Grass green
        // Floor collider so CharacterController doesn't fall through
        ground.AddComponent<MeshCollider>();

        // Try to add Kenney ground_grass tiles for detail in the courtyard
        var grassTile = LoadModel("Nature/ground_grass", null, "CourtyardGrass");
        if (grassTile != null)
        {
            grassTile.transform.position = new Vector3(0f, 0.005f, 0f);
            grassTile.transform.localScale = Vector3.one * 2f;
        }

        // Courtyard area — slightly lighter stone ground
        var courtyard = CreateVisualPrimitive(PrimitiveType.Plane, "Courtyard");
        courtyard.transform.position = new Vector3(0f, 0.01f, 0f); // Slightly above to avoid z-fight
        courtyard.transform.localScale = new Vector3(1.2f, 1f, 1.2f); // 12x12 units
        ApplyColor(courtyard, new Color(0.55f, 0.50f, 0.40f)); // Sandy stone

        // Dirt path from gate to keep
        var path = CreateVisualPrimitive(PrimitiveType.Cube, "Path");
        path.transform.position = new Vector3(0f, 0.005f, -3.5f);
        path.transform.localScale = new Vector3(1.5f, 0.01f, 7f);
        ApplyColor(path, new Color(0.50f, 0.42f, 0.30f)); // Dirt brown
    }

    // ---------------------------------------------------------------
    //  3D PLAYER
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;

        // Blocky humanoid via CharacterBuilder
        var playerConfig = new CharacterBuilder.CharacterConfig
        {
            bodyColor  = new Color(0.45f, 0.25f, 0.65f),  // royal purple
            pantsColor = new Color(0.3f, 0.15f, 0.45f),   // dark purple
            skinColor  = new Color(0.92f, 0.78f, 0.62f),  // warm skin
            hairColor  = new Color(0.4f, 0.25f, 0.12f),   // brown
            hasHair    = true,
            accessory  = CharacterBuilder.AccessoryType.Crown,
        };
        var visualRoot = CharacterBuilder.BuildCharacter(_player.transform, playerConfig);

        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.ConfigureAtRuntime(_playerWalkSpeed, visualRoot.transform);

        _player.AddComponent<InteractionFinder>();
    }

    private void WireCamera()
    {
        if (_roamingCam == null) return;
        var follow = _roamingCam.GetComponent<FollowCamera>()
                  ?? _roamingCam.gameObject.AddComponent<FollowCamera>();
        follow.SetTarget(_player.transform);
    }

    // ---------------------------------------------------------------
    //  NPC SPAWNING
    // ---------------------------------------------------------------

    private void SpawnNPCs()
    {
        var npcm = GameManager.Instance?.NPCManager;
        if (npcm == null) return;

        _npcRoot = new GameObject("RoamingNPCs").transform;

        var existing = npcm.GetAllNPCs();
        if (existing != null)
            foreach (var npc in existing) SpawnOneNPC(npc);
        npcm.OnNPCAdded += SpawnOneNPC;
    }

    private void SpawnOneNPC(NPCManager.NPCData npc)
    {
        if (npc == null || string.IsNullOrEmpty(npc.Id)) return;
        if (_npcObjects.ContainsKey(npc.Id)) return;

        var root = new GameObject($"NPC_{npc.Id}");
        root.transform.SetParent(_npcRoot, false);
        // Use 3D world positions directly (XZ ground plane)
        root.transform.position = new Vector3(npc.WorldPosition.x, 0f, npc.WorldPosition.z);

        // Build blocky humanoid via CharacterBuilder
        var config = GetNPCCharacterConfig(npc);
        var visualRoot = CharacterBuilder.BuildCharacter(root.transform, config);

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignVisualRoot(visualRoot.transform);

        var routine = root.AddComponent<NPCDailyRoutine>();
        routine.NpcId = npc.Id;

        BuildInteractPrompt(root, npc.Name);

        _npcObjects[npc.Id] = root;
    }

    /// <summary>
    /// Maps NPC profession to a blocky CharacterBuilder config with distinct colors and accessories.
    /// </summary>
    private static CharacterBuilder.CharacterConfig GetNPCCharacterConfig(NPCManager.NPCData npc)
    {
        // Vary skin tones slightly per NPC for visual differentiation
        int nameHash = npc.Name?.GetHashCode() ?? 0;
        float skinVar = (Mathf.Abs(nameHash) % 20) * 0.01f;
        Color defaultSkin = new Color(0.85f + skinVar * 0.3f, 0.70f + skinVar, 0.55f + skinVar * 0.5f);

        return npc.Profession switch
        {
            // Vassal (Aldric): blue tunic, dark pants, dark hair, no accessory
            NPCPersona.NPCProfession.Vassal => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.25f, 0.40f, 0.75f),   // blue tunic
                pantsColor = new Color(0.2f, 0.2f, 0.25f),     // dark pants
                skinColor  = defaultSkin,
                hairColor  = new Color(0.18f, 0.12f, 0.08f),   // dark hair
                hasHair    = true,
                accessory  = CharacterBuilder.AccessoryType.None,
            },
            // Soldier (Bram): gray metal body, brown pants, light skin, Helmet
            NPCPersona.NPCProfession.Soldier => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.55f, 0.55f, 0.60f),   // gray metal
                pantsColor = new Color(0.45f, 0.30f, 0.18f),   // brown pants
                skinColor  = new Color(0.92f, 0.82f, 0.72f),   // light skin
                hairColor  = Color.clear,
                hasHair    = false,
                accessory  = CharacterBuilder.AccessoryType.Helmet,
            },
            // Farmer (Marta): green tunic, brown pants, red hair, StrawHat
            NPCPersona.NPCProfession.Farmer => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.30f, 0.65f, 0.25f),   // green tunic
                pantsColor = new Color(0.45f, 0.30f, 0.18f),   // brown pants
                skinColor  = defaultSkin,
                hairColor  = new Color(0.72f, 0.25f, 0.12f),   // red hair
                hasHair    = true,
                accessory  = CharacterBuilder.AccessoryType.StrawHat,
            },
            // Merchant (Sivaro): dark red tunic, black pants, dark hair, WizardHat
            NPCPersona.NPCProfession.Merchant => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.60f, 0.15f, 0.15f),   // dark red tunic
                pantsColor = new Color(0.12f, 0.12f, 0.12f),   // black pants
                skinColor  = defaultSkin,
                hairColor  = new Color(0.18f, 0.12f, 0.08f),   // dark hair
                hasHair    = true,
                accessory  = CharacterBuilder.AccessoryType.WizardHat,
            },
            // Scholar: blue robes, dark pants, no hair (bald scholar), no accessory
            NPCPersona.NPCProfession.Scholar => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.25f, 0.50f, 0.85f),
                pantsColor = new Color(0.18f, 0.18f, 0.25f),
                skinColor  = defaultSkin,
                hairColor  = Color.clear,
                hasHair    = false,
                accessory  = CharacterBuilder.AccessoryType.None,
            },
            // Priest: white robes, light pants, no hair
            NPCPersona.NPCProfession.Priest => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.95f, 0.93f, 0.88f),
                pantsColor = new Color(0.80f, 0.78f, 0.72f),
                skinColor  = defaultSkin,
                hairColor  = Color.clear,
                hasHair    = false,
                accessory  = CharacterBuilder.AccessoryType.Crown,
            },
            // Spy: dark cloak, dark pants, Hood
            NPCPersona.NPCProfession.Spy => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.18f, 0.18f, 0.25f),
                pantsColor = new Color(0.12f, 0.12f, 0.15f),
                skinColor  = defaultSkin,
                hairColor  = Color.clear,
                hasHair    = false,
                accessory  = CharacterBuilder.AccessoryType.Hood,
            },
            // Default fallback
            _ => new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.5f, 0.5f, 0.5f),
                pantsColor = new Color(0.3f, 0.3f, 0.3f),
                skinColor  = defaultSkin,
                hairColor  = Color.clear,
                hasHair    = false,
                accessory  = CharacterBuilder.AccessoryType.None,
            },
        };
    }

    private void BuildInteractPrompt(GameObject npcRoot, string npcName)
    {
        var promptGO = new GameObject("InteractPrompt");
        promptGO.transform.SetParent(npcRoot.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);

        var canvas = promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 50f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // Semi-transparent background panel for readability
        var bgGO = new GameObject("PromptBG");
        bgGO.transform.SetParent(promptGO.transform, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.12f, 0.75f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(-10f, -4f);
        bgRT.offsetMax = new Vector2(10f, 4f);

        // Subtle border accent at bottom
        var borderGO = new GameObject("PromptBorder");
        borderGO.transform.SetParent(bgGO.transform, false);
        var borderImg = borderGO.AddComponent<UnityEngine.UI.Image>();
        borderImg.color = new Color(0.35f, 0.65f, 0.95f, 0.8f);
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.anchorMin = new Vector2(0f, 0f);
        borderRT.anchorMax = new Vector2(1f, 0f);
        borderRT.pivot = new Vector2(0.5f, 0f);
        borderRT.anchoredPosition = Vector2.zero;
        borderRT.sizeDelta = new Vector2(0f, 2f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(promptGO.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = $"E   Talk to {npcName}";
        tmp.fontSize = 28;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.95f, 0.95f, 1f);
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        var lblRT = tmp.rectTransform;
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        var outline = labelGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        var prompt = promptGO.AddComponent<InteractPromptUI>();
        prompt.SetupRuntime(canvas, tmp);

        // Make prompt always face camera
        promptGO.AddComponent<BillboardFaceCamera>();

        canvas.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  BUILDINGS — Kenney FBX models with procedural fallbacks
    // ---------------------------------------------------------------

    private void SpawnBuildings()
    {
        var buildRoot = new GameObject("Buildings");

        // Central Keep — stacked tower segments from Castle Kit
        SpawnKeep(buildRoot.transform);

        // Barracks (east side) — Fantasy Town wall+roof combo
        SpawnBarracks(buildRoot.transform);

        // Farm (west side) — Kenney fence pieces + ground
        SpawnFarm(buildRoot.transform);

        // Market stall (northeast) — Fantasy Town stall model
        SpawnMarketStall(buildRoot.transform);

        // Castle gate (south) — Castle Kit gate.fbx
        SpawnCastleGate(buildRoot.transform);

        // Watchtower (northwest corner)
        SpawnWatchtower(buildRoot.transform);

        // Castle walls — Castle Kit wall.fbx repeated + wall-corner at corners
        SpawnCastleWalls(buildRoot.transform);
    }

    private void SpawnKeep(Transform parent)
    {
        var keep = new GameObject("CentralKeep");
        keep.transform.SetParent(parent, false);
        keep.transform.localPosition = new Vector3(0f, 0f, 2f);

        // Try Kenney Castle Kit tower stack: base + mid + roof
        var towerBase = LoadModel("Castle/tower-square-base", keep.transform, "TowerBase");
        if (towerBase != null)
        {
            towerBase.transform.localPosition = Vector3.zero;
            towerBase.transform.localScale = Vector3.one * 1.5f;

            var towerMid = LoadModel("Castle/tower-square-mid", keep.transform, "TowerMid");
            if (towerMid != null)
            {
                towerMid.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                towerMid.transform.localScale = Vector3.one * 1.5f;
            }

            var towerRoof = LoadModel("Castle/tower-square-roof", keep.transform, "TowerRoof");
            if (towerRoof != null)
            {
                towerRoof.transform.localPosition = new Vector3(0f, 3.0f, 0f);
                towerRoof.transform.localScale = Vector3.one * 1.5f;
            }

            // Flag on top
            var flag = LoadModel("Castle/flag", keep.transform, "KeepFlag");
            if (flag != null)
            {
                flag.transform.localPosition = new Vector3(0f, 4.5f, 0f);
                flag.transform.localScale = Vector3.one * 1.5f;
            }
        }
        else
        {
            // Fallback to procedural
            var body = CreateVisualPrimitive(PrimitiveType.Cube, "KeepBody");
            body.transform.SetParent(keep.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            body.transform.localScale = new Vector3(3f, 3f, 3f);
            ApplyColor(body, new Color(0.50f, 0.45f, 0.40f));

            var roof = CreateVisualPrimitive(PrimitiveType.Cube, "KeepRoof");
            roof.transform.SetParent(keep.transform, false);
            roof.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            roof.transform.localScale = new Vector3(3.2f, 0.4f, 3.2f);
            ApplyColor(roof, new Color(0.25f, 0.20f, 0.40f));

            var pole = CreateVisualPrimitive(PrimitiveType.Cylinder, "FlagPole");
            pole.transform.SetParent(keep.transform, false);
            pole.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            pole.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
            ApplyColor(pole, new Color(0.6f, 0.5f, 0.3f));

            var flagFallback = CreateVisualPrimitive(PrimitiveType.Cube, "Flag");
            flagFallback.transform.SetParent(pole.transform, false);
            flagFallback.transform.localPosition = new Vector3(0.4f, 0.6f, 0f);
            flagFallback.transform.localScale = new Vector3(8f, 4f, 0.5f);
            ApplyColor(flagFallback, new Color(0.70f, 0.15f, 0.15f));
        }

        // Keep collider
        AddBuildingCollider(keep, new Vector3(3f, 3f, 3f), new Vector3(0f, 1.5f, 0f));
    }

    private void SpawnBarracks(Transform parent)
    {
        var barracks = new GameObject("Barracks");
        barracks.transform.SetParent(parent, false);
        barracks.transform.localPosition = new Vector3(5f, 0f, -3f);

        // Use windmill.fbx as the barracks — it's a complete building model
        var windmill = LoadModel("Town/windmill", barracks.transform, "BarracksBuilding");
        if (windmill != null)
        {
            windmill.transform.localPosition = Vector3.zero;
            windmill.transform.localScale = Vector3.one * 1.2f;
        }
        else
        {
            // Fallback: compose 4 walls + roof into a proper house
            float s = 1.5f; // scale for wall pieces
            float hw = 0.75f; // half-width offset (wall is ~1 unit wide at scale 1, so 1.5*0.5)

            // North wall (window)
            var wallN = LoadModel("Town/wall-window-shutters", barracks.transform, "BarracksWallN");
            if (wallN != null)
            {
                wallN.transform.localPosition = new Vector3(0f, 0f, hw);
                wallN.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                wallN.transform.localScale = Vector3.one * s;
            }
            // South wall (door)
            var wallS = LoadModel("Town/wall-door", barracks.transform, "BarracksWallS");
            if (wallS != null)
            {
                wallS.transform.localPosition = new Vector3(0f, 0f, -hw);
                wallS.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                wallS.transform.localScale = Vector3.one * s;
            }
            // East wall
            var wallE = LoadModel("Town/wall", barracks.transform, "BarracksWallE");
            if (wallE != null)
            {
                wallE.transform.localPosition = new Vector3(hw, 0f, 0f);
                wallE.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                wallE.transform.localScale = Vector3.one * s;
            }
            // West wall
            var wallW = LoadModel("Town/wall", barracks.transform, "BarracksWallW");
            if (wallW != null)
            {
                wallW.transform.localPosition = new Vector3(-hw, 0f, 0f);
                wallW.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                wallW.transform.localScale = Vector3.one * s;
            }
            // Roof on top
            var roofModel = LoadModel("Town/roof-gable", barracks.transform, "BarracksRoof");
            if (roofModel != null)
            {
                roofModel.transform.localPosition = new Vector3(0f, s, 0f);
                roofModel.transform.localScale = Vector3.one * s;
            }
            else
            {
                // Ultimate fallback: procedural box building
                var body = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksBody");
                body.transform.SetParent(barracks.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
                body.transform.localScale = new Vector3(3f, 1.2f, 2f);
                ApplyColor(body, new Color(0.50f, 0.30f, 0.20f));

                var roofFb = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksRoof");
                roofFb.transform.SetParent(barracks.transform, false);
                roofFb.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                roofFb.transform.localScale = new Vector3(3.2f, 0.15f, 2.2f);
                ApplyColor(roofFb, new Color(0.35f, 0.25f, 0.15f));
            }
        }

        AddBuildingCollider(barracks, new Vector3(2.5f, 2.5f, 2.5f), new Vector3(0f, 1.25f, 0f));
    }

    private void SpawnFarm(Transform parent)
    {
        var farm = new GameObject("Farm");
        farm.transform.SetParent(parent, false);
        farm.transform.localPosition = new Vector3(-5f, 0f, 3f);

        // Use watermill.fbx as the farm barn — it's a complete building model
        var barnModel = LoadModel("Town/watermill", farm.transform, "FarmBarn");
        if (barnModel != null)
        {
            barnModel.transform.localPosition = new Vector3(0f, 0f, 1f);
            barnModel.transform.localScale = Vector3.one * 1.0f;
            AddBuildingCollider(farm, new Vector3(2f, 2f, 2f), new Vector3(0f, 1f, 1f));
        }
        else
        {
            // Fallback: procedural barn
            var barn = CreateVisualPrimitive(PrimitiveType.Cube, "Barn");
            barn.transform.SetParent(farm.transform, false);
            barn.transform.localPosition = new Vector3(0f, 0.5f, 1f);
            barn.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
            ApplyColor(barn, new Color(0.50f, 0.30f, 0.15f));

            var barnRoof = CreateVisualPrimitive(PrimitiveType.Cube, "BarnRoof");
            barnRoof.transform.SetParent(farm.transform, false);
            barnRoof.transform.localPosition = new Vector3(0f, 1.15f, 1f);
            barnRoof.transform.localScale = new Vector3(1.7f, 0.15f, 1.7f);
            ApplyColor(barnRoof, new Color(0.60f, 0.25f, 0.10f));

            AddBuildingCollider(farm, new Vector3(1.5f, 1f, 1.5f), new Vector3(0f, 0.5f, 1f));
        }

        // Fence enclosure around the crop area
        bool hasFence = false;
        for (int i = -2; i <= 2; i++)
        {
            var fencePiece = LoadModel("Town/fence", farm.transform, $"Fence_{i}");
            if (fencePiece != null)
            {
                hasFence = true;
                fencePiece.transform.localPosition = new Vector3(i * 1.0f, 0f, -2f);
                fencePiece.transform.localScale = Vector3.one * 1.0f;
            }
        }
        // Side fences
        for (int i = -1; i <= 0; i++)
        {
            var fenceL = LoadModel("Town/fence", farm.transform, $"FenceL_{i}");
            if (fenceL != null)
            {
                fenceL.transform.localPosition = new Vector3(-2f, 0f, i * 1.0f);
                fenceL.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                fenceL.transform.localScale = Vector3.one * 1.0f;
            }
            var fenceR = LoadModel("Town/fence", farm.transform, $"FenceR_{i}");
            if (fenceR != null)
            {
                fenceR.transform.localPosition = new Vector3(2f, 0f, i * 1.0f);
                fenceR.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                fenceR.transform.localScale = Vector3.one * 1.0f;
            }
        }

        // Fence gate
        var fenceGate = LoadModel("Town/fence-gate", farm.transform, "FenceGate");
        if (fenceGate != null)
        {
            fenceGate.transform.localPosition = new Vector3(0f, 0f, -2f);
            fenceGate.transform.localScale = Vector3.one * 1.0f;
        }

        if (!hasFence)
        {
            // Fallback fence posts
            float[] fenceX = { -2f, -1f, 0f, 1f, 2f, 2f, 2f, -2f, -2f };
            float[] fenceZ = { -2f, -2f, -2f, -2f, -2f, -1f, 0f, -1f, 0f };
            for (int i = 0; i < fenceX.Length; i++)
            {
                var post = CreateVisualPrimitive(PrimitiveType.Cylinder, "FencePost");
                post.transform.SetParent(farm.transform, false);
                post.transform.localPosition = new Vector3(fenceX[i], 0.2f, fenceZ[i]);
                post.transform.localScale = new Vector3(0.06f, 0.2f, 0.06f);
                ApplyColor(post, new Color(0.45f, 0.35f, 0.20f));
            }
        }

        // Crop rows (flat green cubes)
        for (int row = 0; row < 3; row++)
        {
            var crop = CreateVisualPrimitive(PrimitiveType.Cube, "CropRow");
            crop.transform.SetParent(farm.transform, false);
            crop.transform.localPosition = new Vector3(-0.5f + row, 0.08f, -1f);
            crop.transform.localScale = new Vector3(0.6f, 0.15f, 2.5f);
            ApplyColor(crop, new Color(0.25f + row * 0.05f, 0.55f, 0.15f));
        }
    }

    private void SpawnMarketStall(Transform parent)
    {
        var market = new GameObject("MarketStall");
        market.transform.SetParent(parent, false);
        market.transform.localPosition = new Vector3(2f, 0f, 5f);

        // Try Kenney Fantasy Town stall model
        var stallModel = LoadModel("Town/stall-red", market.transform, "Stall");
        if (stallModel == null)
            stallModel = LoadModel("Town/stall", market.transform, "Stall");

        if (stallModel != null)
        {
            stallModel.transform.localPosition = Vector3.zero;
            stallModel.transform.localScale = Vector3.one * 1.2f;

            // Add a cart beside the stall for flavor
            var cartModel = LoadModel("Town/cart", market.transform, "Cart");
            if (cartModel != null)
            {
                cartModel.transform.localPosition = new Vector3(2f, 0f, 0f);
                cartModel.transform.localScale = Vector3.one * 1.0f;
            }
        }
        else
        {
            // Fallback
            var counter = CreateVisualPrimitive(PrimitiveType.Cube, "Counter");
            counter.transform.SetParent(market.transform, false);
            counter.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            counter.transform.localScale = new Vector3(2f, 0.8f, 0.8f);
            ApplyColor(counter, new Color(0.55f, 0.40f, 0.20f));

            var canopy = CreateVisualPrimitive(PrimitiveType.Cube, "Canopy");
            canopy.transform.SetParent(market.transform, false);
            canopy.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            canopy.transform.localScale = new Vector3(2.5f, 0.08f, 1.5f);
            ApplyColor(canopy, new Color(0.80f, 0.30f, 0.10f));

            for (int i = -1; i <= 1; i += 2)
            {
                var pole = CreateVisualPrimitive(PrimitiveType.Cylinder, "CanopyPole");
                pole.transform.SetParent(market.transform, false);
                pole.transform.localPosition = new Vector3(i * 0.9f, 0.75f, -0.5f);
                pole.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
                ApplyColor(pole, new Color(0.45f, 0.35f, 0.20f));
            }
        }
    }

    private void SpawnCastleGate(Transform parent)
    {
        var gate = new GameObject("CastleGate");
        gate.transform.SetParent(parent, false);
        gate.transform.localPosition = new Vector3(0f, 0f, -8f);

        // Try Kenney Castle Kit gate model
        var gateModel = LoadModel("Castle/gate", gate.transform, "GateModel");
        if (gateModel != null)
        {
            gateModel.transform.localPosition = Vector3.zero;
            gateModel.transform.localScale = Vector3.one * 1.5f;

            // Add door inside the gate
            var doorModel = LoadModel("Castle/door", gate.transform, "GateDoor");
            if (doorModel != null)
            {
                doorModel.transform.localPosition = new Vector3(0f, 0f, 0.1f);
                doorModel.transform.localScale = Vector3.one * 1.5f;
            }
        }
        else
        {
            // Fallback: two gate towers
            for (int side = -1; side <= 1; side += 2)
            {
                var tower = CreateVisualPrimitive(PrimitiveType.Cylinder, "GateTower");
                tower.transform.SetParent(gate.transform, false);
                tower.transform.localPosition = new Vector3(side * 1.5f, 1.2f, 0f);
                tower.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
                ApplyColor(tower, new Color(0.50f, 0.48f, 0.45f));

                var top = CreateVisualPrimitive(PrimitiveType.Cube, "GateTowerTop");
                top.transform.SetParent(tower.transform, false);
                top.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                top.transform.localScale = new Vector3(1.3f, 0.15f, 1.3f);
                ApplyColor(top, new Color(0.45f, 0.43f, 0.40f));
            }

            var arch = CreateVisualPrimitive(PrimitiveType.Cube, "GateArch");
            arch.transform.SetParent(gate.transform, false);
            arch.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            arch.transform.localScale = new Vector3(2f, 0.4f, 0.6f);
            ApplyColor(arch, new Color(0.50f, 0.48f, 0.45f));
        }
    }

    private void SpawnWatchtower(Transform parent)
    {
        var tower = new GameObject("Watchtower");
        tower.transform.SetParent(parent, false);
        tower.transform.localPosition = new Vector3(-7f, 0f, -7f);

        // Try Castle Kit tower stack
        var towerBase = LoadModel("Castle/tower-square-base", tower.transform, "WatchBase");
        if (towerBase != null)
        {
            towerBase.transform.localPosition = Vector3.zero;
            towerBase.transform.localScale = Vector3.one * 1.0f;

            var towerMid = LoadModel("Castle/tower-square-mid", tower.transform, "WatchMid");
            if (towerMid != null)
            {
                towerMid.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                towerMid.transform.localScale = Vector3.one * 1.0f;
            }

            var towerRoof = LoadModel("Castle/tower-square-roof", tower.transform, "WatchRoof");
            if (towerRoof != null)
            {
                towerRoof.transform.localPosition = new Vector3(0f, 2.0f, 0f);
                towerRoof.transform.localScale = Vector3.one * 1.0f;
            }

            var flagModel = LoadModel("Castle/flag", tower.transform, "WatchFlag");
            if (flagModel != null)
            {
                flagModel.transform.localPosition = new Vector3(0f, 3.0f, 0f);
                flagModel.transform.localScale = Vector3.one * 1.0f;
            }
        }
        else
        {
            // Fallback
            var body = CreateVisualPrimitive(PrimitiveType.Cylinder, "TowerBody");
            body.transform.SetParent(tower.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            body.transform.localScale = new Vector3(1f, 1.5f, 1f);
            ApplyColor(body, new Color(0.50f, 0.48f, 0.45f));

            var top = CreateVisualPrimitive(PrimitiveType.Cube, "TowerTop");
            top.transform.SetParent(tower.transform, false);
            top.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            top.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
            ApplyColor(top, new Color(0.45f, 0.43f, 0.40f));
        }
    }

    private void SpawnCastleWalls(Transform parent)
    {
        float wallSize = 9.5f;

        // Try Kenney Castle Kit wall.fbx segments
        var testWall = Resources.Load<GameObject>("Models/Castle/wall");
        if (testWall != null)
        {
            // Use real wall models — repeat along each edge
            float spacing = 1.5f; // Kenney wall segments are ~1 unit, scale 1.5x
            Color wallColor = new Color(0.50f, 0.48f, 0.45f);

            // North wall
            SpawnWallRow(parent, new Vector3(-wallSize, 0f, wallSize),
                new Vector3(wallSize, 0f, wallSize), spacing, 0f, "WallN");
            // South wall (gap for gate in middle)
            SpawnWallRow(parent, new Vector3(-wallSize, 0f, -wallSize),
                new Vector3(-2f, 0f, -wallSize), spacing, 0f, "WallSL");
            SpawnWallRow(parent, new Vector3(2f, 0f, -wallSize),
                new Vector3(wallSize, 0f, -wallSize), spacing, 0f, "WallSR");
            // West wall
            SpawnWallRow(parent, new Vector3(-wallSize, 0f, -wallSize),
                new Vector3(-wallSize, 0f, wallSize), spacing, 90f, "WallW");
            // East wall
            SpawnWallRow(parent, new Vector3(wallSize, 0f, -wallSize),
                new Vector3(wallSize, 0f, wallSize), spacing, 90f, "WallE");

            // Corner towers using wall-corner
            float[] cx = { -wallSize, wallSize, -wallSize, wallSize };
            float[] cz = { -wallSize, -wallSize, wallSize, wallSize };
            float[] cRot = { 0f, 90f, 270f, 180f };
            for (int i = 0; i < 4; i++)
            {
                var corner = LoadModel("Castle/wall-corner", parent, $"Corner_{i}");
                if (corner != null)
                {
                    corner.transform.localPosition = new Vector3(cx[i], 0f, cz[i]);
                    corner.transform.localRotation = Quaternion.Euler(0f, cRot[i], 0f);
                    corner.transform.localScale = Vector3.one * 1.5f;
                }
                else
                {
                    // Fallback corner tower
                    var ct = CreateVisualPrimitive(PrimitiveType.Cylinder, "CornerTower");
                    ct.transform.SetParent(parent, false);
                    ct.transform.localPosition = new Vector3(cx[i], 1f, cz[i]);
                    ct.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                    ApplyColor(ct, wallColor * 0.9f);
                }
            }
        }
        else
        {
            // Fallback: procedural walls
            float wallH = 1.2f;
            float wallW = 0.4f;
            Color wallColor = new Color(0.50f, 0.48f, 0.45f);

            CreateWallSegment(parent, new Vector3(0f, wallH / 2, wallSize),
                new Vector3(wallSize * 2, wallH, wallW), wallColor, "WallNorth");
            CreateWallSegment(parent, new Vector3(-5.5f, wallH / 2, -wallSize),
                new Vector3(8f, wallH, wallW), wallColor, "WallSouthL");
            CreateWallSegment(parent, new Vector3(5.5f, wallH / 2, -wallSize),
                new Vector3(8f, wallH, wallW), wallColor, "WallSouthR");
            CreateWallSegment(parent, new Vector3(-wallSize, wallH / 2, 0f),
                new Vector3(wallW, wallH, wallSize * 2), wallColor, "WallWest");
            CreateWallSegment(parent, new Vector3(wallSize, wallH / 2, 0f),
                new Vector3(wallW, wallH, wallSize * 2), wallColor, "WallEast");

            float[] cx = { -wallSize, wallSize, -wallSize, wallSize };
            float[] cz = { -wallSize, -wallSize, wallSize, wallSize };
            for (int i = 0; i < 4; i++)
            {
                var ct = CreateVisualPrimitive(PrimitiveType.Cylinder, "CornerTower");
                ct.transform.SetParent(parent, false);
                ct.transform.localPosition = new Vector3(cx[i], 1f, cz[i]);
                ct.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
                ApplyColor(ct, wallColor * 0.9f);
            }
        }
    }

    /// <summary>
    /// Spawn a row of Kenney wall.fbx segments between two points.
    /// </summary>
    private void SpawnWallRow(Transform parent, Vector3 from, Vector3 to, float spacing, float yRot, string prefix)
    {
        Vector3 dir = (to - from);
        float length = dir.magnitude;
        if (length < 0.1f) return;
        dir.Normalize();

        int count = Mathf.Max(1, Mathf.RoundToInt(length / spacing));
        float actualSpacing = length / count;

        for (int i = 0; i <= count; i++)
        {
            Vector3 pos = from + dir * (i * actualSpacing);
            var seg = LoadModel("Castle/wall", parent, $"{prefix}_{i}");
            if (seg != null)
            {
                seg.transform.localPosition = pos;
                seg.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
                seg.transform.localScale = Vector3.one * 1.5f;
            }
        }

        // Also add a physics collider spanning the full wall
        var colliderGO = new GameObject($"{prefix}_Collider");
        colliderGO.transform.SetParent(parent, false);
        Vector3 center = (from + to) * 0.5f + Vector3.up * 0.6f;
        colliderGO.transform.localPosition = center;
        var box = colliderGO.AddComponent<BoxCollider>();
        // Determine oriented size
        float dx = Mathf.Abs(to.x - from.x);
        float dz = Mathf.Abs(to.z - from.z);
        box.size = new Vector3(
            Mathf.Max(dx, 0.4f),
            1.2f,
            Mathf.Max(dz, 0.4f)
        );
    }

    private void CreateWallSegment(Transform parent, Vector3 pos, Vector3 scale, Color color, string name)
    {
        var wall = CreateVisualPrimitive(PrimitiveType.Cube, name);
        wall.transform.SetParent(parent, false);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        ApplyColor(wall, color);
        // Walls block the player
        var col = wall.AddComponent<BoxCollider>();
    }

    // ---------------------------------------------------------------
    //  ENVIRONMENT — Kenney Nature Kit FBX models
    // ---------------------------------------------------------------

    private void SpawnEnvironment()
    {
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;

        System.Random rng = new System.Random(42);

        // Stone paths connecting buildings
        SpawnStonePaths(envRoot.transform);

        // Courtyard border — raised stone rim around the central area
        SpawnCourtyardBorder(envRoot.transform);

        // Trees around the outer ring (between walls and edge of ground)
        float treeRingSize = 12f;
        for (float x = -treeRingSize; x <= treeRingSize; x += 3f)
        {
            SpawnTree(envRoot.transform, new Vector3(
                x + (float)(rng.NextDouble() * 1.5), 0f,
                treeRingSize + (float)(rng.NextDouble() * 3f)), rng);
            SpawnTree(envRoot.transform, new Vector3(
                x + (float)(rng.NextDouble() * 1.5), 0f,
                -treeRingSize - (float)(rng.NextDouble() * 3f)), rng);
        }
        for (float z = -treeRingSize; z <= treeRingSize; z += 3f)
        {
            SpawnTree(envRoot.transform, new Vector3(
                -treeRingSize - (float)(rng.NextDouble() * 3f), 0f,
                z + (float)(rng.NextDouble() * 1.5)), rng);
            SpawnTree(envRoot.transform, new Vector3(
                treeRingSize + (float)(rng.NextDouble() * 3f), 0f,
                z + (float)(rng.NextDouble() * 1.5)), rng);
        }

        // A few interior trees for visual interest
        Vector3[] interiorTrees = {
            new(-3f, 0f, -4f), new(3.5f, 0f, 4f), new(-6f, 0f, -2f),
            new(6f, 0f, 2f), new(-2f, 0f, 7f), new(4f, 0f, -6f),
        };
        foreach (var pos in interiorTrees)
            SpawnTree(envRoot.transform, pos, rng);

        // Bushes, rocks, flowers, and grass scattered inside the castle
        SpawnNatureDetails(envRoot.transform, rng);

        // Additional rock formations for visual interest
        SpawnRockFormations(envRoot.transform, rng);

        // Extra flower beds between buildings
        SpawnFlowerBeds(envRoot.transform, rng);

        // Well (near center-right) — use fountain model
        SpawnWell(envRoot.transform, new Vector3(2f, 0f, 3f));

        // Campfire / gathering area
        SpawnCampfire(envRoot.transform, new Vector3(-1f, 0f, -3f));
    }

    /// <summary>
    /// Stone paths connecting key buildings — darker ground tiles for a lived-in feel.
    /// </summary>
    private void SpawnStonePaths(Transform parent)
    {
        Color pathColor = new Color(0.42f, 0.38f, 0.32f);

        // Path from gate (south) to keep (north-center)
        var pathGateToKeep = CreateVisualPrimitive(PrimitiveType.Cube, "PathGateToKeep");
        pathGateToKeep.transform.SetParent(parent, false);
        pathGateToKeep.transform.localPosition = new Vector3(0f, 0.008f, -3f);
        pathGateToKeep.transform.localScale = new Vector3(1.8f, 0.01f, 10f);
        ApplyColor(pathGateToKeep, pathColor);

        // Path from keep to barracks (east)
        var pathToBarracks = CreateVisualPrimitive(PrimitiveType.Cube, "PathToBarracks");
        pathToBarracks.transform.SetParent(parent, false);
        pathToBarracks.transform.localPosition = new Vector3(2.5f, 0.008f, -1f);
        pathToBarracks.transform.localScale = new Vector3(5f, 0.01f, 1.5f);
        ApplyColor(pathToBarracks, pathColor);

        // Path from keep to farm (west)
        var pathToFarm = CreateVisualPrimitive(PrimitiveType.Cube, "PathToFarm");
        pathToFarm.transform.SetParent(parent, false);
        pathToFarm.transform.localPosition = new Vector3(-2.5f, 0.008f, 1.5f);
        pathToFarm.transform.localScale = new Vector3(5f, 0.01f, 1.5f);
        ApplyColor(pathToFarm, pathColor);

        // Path to market stall (northeast)
        var pathToMarket = CreateVisualPrimitive(PrimitiveType.Cube, "PathToMarket");
        pathToMarket.transform.SetParent(parent, false);
        pathToMarket.transform.localPosition = new Vector3(1f, 0.008f, 3.5f);
        pathToMarket.transform.localScale = new Vector3(1.5f, 0.01f, 4f);
        ApplyColor(pathToMarket, pathColor);

        // Small widening around the well area
        var pathWellArea = CreateVisualPrimitive(PrimitiveType.Cube, "PathWellArea");
        pathWellArea.transform.SetParent(parent, false);
        pathWellArea.transform.localPosition = new Vector3(2f, 0.008f, 3f);
        pathWellArea.transform.localScale = new Vector3(2.5f, 0.01f, 2.5f);
        ApplyColor(pathWellArea, new Color(0.44f, 0.40f, 0.34f));
    }

    /// <summary>
    /// Raised stone border around the courtyard to define the central area.
    /// </summary>
    private void SpawnCourtyardBorder(Transform parent)
    {
        Color borderColor = new Color(0.48f, 0.44f, 0.38f);
        float size = 6.5f;
        float height = 0.08f;
        float width = 0.25f;

        // North border
        var north = CreateVisualPrimitive(PrimitiveType.Cube, "BorderN");
        north.transform.SetParent(parent, false);
        north.transform.localPosition = new Vector3(0f, height / 2f, size);
        north.transform.localScale = new Vector3(size * 2f, height, width);
        ApplyColor(north, borderColor);

        // South border (gap for path)
        var southL = CreateVisualPrimitive(PrimitiveType.Cube, "BorderSL");
        southL.transform.SetParent(parent, false);
        southL.transform.localPosition = new Vector3(-4f, height / 2f, -size);
        southL.transform.localScale = new Vector3(5f, height, width);
        ApplyColor(southL, borderColor);

        var southR = CreateVisualPrimitive(PrimitiveType.Cube, "BorderSR");
        southR.transform.SetParent(parent, false);
        southR.transform.localPosition = new Vector3(4f, height / 2f, -size);
        southR.transform.localScale = new Vector3(5f, height, width);
        ApplyColor(southR, borderColor);

        // West border
        var west = CreateVisualPrimitive(PrimitiveType.Cube, "BorderW");
        west.transform.SetParent(parent, false);
        west.transform.localPosition = new Vector3(-size, height / 2f, 0f);
        west.transform.localScale = new Vector3(width, height, size * 2f);
        ApplyColor(west, borderColor);

        // East border
        var east = CreateVisualPrimitive(PrimitiveType.Cube, "BorderE");
        east.transform.SetParent(parent, false);
        east.transform.localPosition = new Vector3(size, height / 2f, 0f);
        east.transform.localScale = new Vector3(width, height, size * 2f);
        ApplyColor(east, borderColor);
    }

    /// <summary>
    /// Cluster rock formations in a few spots for visual weight.
    /// </summary>
    private void SpawnRockFormations(Transform parent, System.Random rng)
    {
        Vector3[] clusterCenters = {
            new(-7f, 0f, 6f), new(7f, 0f, 5f), new(-6f, 0f, -6f),
        };
        foreach (var center in clusterCenters)
        {
            int count = 3 + rng.Next(3);
            for (int i = 0; i < count; i++)
            {
                float dx = (float)(rng.NextDouble() * 2.0 - 1.0);
                float dz = (float)(rng.NextDouble() * 2.0 - 1.0);
                float scale = 0.3f + (float)(rng.NextDouble() * 0.5);

                string model = rng.Next(2) == 0 ? "Nature/rock_smallA" : "Nature/rock_largeA";
                var rock = LoadModel(model, parent, "RockCluster");
                if (rock != null)
                {
                    rock.transform.localPosition = center + new Vector3(dx, 0f, dz);
                    rock.transform.localScale = Vector3.one * scale;
                    rock.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                }
                else
                {
                    // Fallback rock: slightly squashed sphere
                    var fallback = CreateVisualPrimitive(PrimitiveType.Sphere, "RockFallback");
                    fallback.transform.SetParent(parent, false);
                    fallback.transform.localPosition = center + new Vector3(dx, scale * 0.3f, dz);
                    fallback.transform.localScale = new Vector3(scale, scale * 0.6f, scale);
                    ApplyColor(fallback, new Color(0.45f, 0.42f, 0.38f));
                }
            }
        }
    }

    /// <summary>
    /// Extra flower beds between buildings for a lively garden feel.
    /// </summary>
    private void SpawnFlowerBeds(Transform parent, System.Random rng)
    {
        // Flower bed near the keep entrance
        Vector3[] bedCenters = {
            new(1.5f, 0f, 0.5f), new(-1.5f, 0f, 0.5f),
            new(3f, 0f, -2f), new(-3f, 0f, -1f),
        };
        string[] flowerModels = { "Nature/flower_redA", "Nature/flower_yellowA" };

        foreach (var center in bedCenters)
        {
            // Small patch of flowers in a cluster
            for (int i = 0; i < 4; i++)
            {
                float dx = (float)(rng.NextDouble() * 0.8 - 0.4);
                float dz = (float)(rng.NextDouble() * 0.8 - 0.4);
                string model = flowerModels[rng.Next(flowerModels.Length)];
                var flower = LoadModel(model, parent, "FlowerBed");
                if (flower != null)
                {
                    flower.transform.localPosition = center + new Vector3(dx, 0f, dz);
                    flower.transform.localScale = Vector3.one * (0.5f + (float)(rng.NextDouble() * 0.3));
                    flower.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
                }
            }
            // Add a small grass base under the flower bed
            var grassBase = LoadModel("Nature/grass", parent, "FlowerBedGrass");
            if (grassBase != null)
            {
                grassBase.transform.localPosition = center;
                grassBase.transform.localScale = Vector3.one * 0.6f;
            }
        }
    }

    private void SpawnTree(Transform parent, Vector3 position, System.Random rng)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = position;

        // Pick a random Kenney tree model
        string[] treeModels = { "Nature/tree_default", "Nature/tree_oak", "Nature/tree_pineRoundA" };
        string modelPath = treeModels[rng.Next(treeModels.Length)];
        float scale = 0.8f + (float)(rng.NextDouble() * 0.4);

        var model = LoadModel(modelPath, tree.transform, "TreeModel");
        if (model != null)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
        }
        else
        {
            // Fallback procedural tree
            float trunkHeight = 0.8f + (float)(rng.NextDouble() * 0.4);
            float canopySize = 0.7f + (float)(rng.NextDouble() * 0.4);

            var trunk = CreateVisualPrimitive(PrimitiveType.Cylinder, "Trunk");
            trunk.transform.SetParent(tree.transform, false);
            trunk.transform.localPosition = new Vector3(0f, trunkHeight / 2f, 0f);
            trunk.transform.localScale = new Vector3(0.15f, trunkHeight / 2f, 0.15f);
            ApplyColor(trunk, new Color(0.40f, 0.28f, 0.15f));

            var canopy = CreateVisualPrimitive(PrimitiveType.Sphere, "Canopy");
            canopy.transform.SetParent(tree.transform, false);
            canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopySize * 0.4f, 0f);
            canopy.transform.localScale = Vector3.one * canopySize;
            float greenVar = 0.35f + (float)(rng.NextDouble() * 0.15);
            ApplyColor(canopy, new Color(0.20f, greenVar, 0.12f));
        }

        // Collider on trunk so player walks around trees
        var col = tree.AddComponent<CapsuleCollider>();
        col.radius = 0.3f;
        col.height = 1.5f;
        col.center = new Vector3(0f, 0.75f, 0f);
    }

    /// <summary>
    /// Scatter Kenney bushes, rocks, flowers, and grass patches inside castle grounds.
    /// </summary>
    private void SpawnNatureDetails(Transform parent, System.Random rng)
    {
        // Bushes
        Vector3[] bushPositions = {
            new(-4f, 0f, 0f), new(4f, 0f, 1f), new(-1f, 0f, 5f),
            new(6f, 0f, -5f), new(-6f, 0f, 5f), new(3f, 0f, -4f),
            new(-3f, 0f, 6f), new(5f, 0f, 6f),
        };
        string[] bushModels = { "Nature/plant_bush", "Nature/plant_bushLarge" };
        foreach (var pos in bushPositions)
        {
            string model = bushModels[rng.Next(bushModels.Length)];
            var bush = LoadModel(model, parent, "Bush");
            if (bush != null)
            {
                bush.transform.localPosition = pos;
                float s = 0.8f + (float)(rng.NextDouble() * 0.4);
                bush.transform.localScale = Vector3.one * s;
                bush.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }

        // Rocks
        Vector3[] rockPositions = {
            new(-7f, 0f, 3f), new(7f, 0f, -3f), new(-3f, 0f, 8f),
            new(5f, 0f, -7f), new(-8f, 0f, -5f),
        };
        foreach (var pos in rockPositions)
        {
            string model = rng.Next(2) == 0 ? "Nature/rock_smallA" : "Nature/rock_largeA";
            var rock = LoadModel(model, parent, "Rock");
            if (rock != null)
            {
                rock.transform.localPosition = pos;
                float s = 0.6f + (float)(rng.NextDouble() * 0.6);
                rock.transform.localScale = Vector3.one * s;
                rock.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }

        // Flowers
        Vector3[] flowerPositions = {
            new(-2f, 0f, 1f), new(1f, 0f, -2f), new(-5f, 0f, -4f),
            new(3f, 0f, 2f), new(-1f, 0f, 6f), new(5f, 0f, 4f),
            new(-4f, 0f, -6f), new(2f, 0f, 7f),
        };
        string[] flowerModels = { "Nature/flower_redA", "Nature/flower_yellowA" };
        foreach (var pos in flowerPositions)
        {
            string model = flowerModels[rng.Next(flowerModels.Length)];
            var flower = LoadModel(model, parent, "Flower");
            if (flower != null)
            {
                flower.transform.localPosition = pos;
                flower.transform.localScale = Vector3.one * 0.8f;
                flower.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }

        // Grass patches
        Vector3[] grassPositions = {
            new(-1f, 0f, 0f), new(2f, 0f, -1f), new(-3f, 0f, 2f),
            new(4f, 0f, 3f), new(0f, 0f, 4f), new(-2f, 0f, -5f),
            new(6f, 0f, 0f), new(-5f, 0f, 1f),
        };
        string[] grassModels = { "Nature/grass", "Nature/grass_large" };
        foreach (var pos in grassPositions)
        {
            string model = grassModels[rng.Next(grassModels.Length)];
            var grass = LoadModel(model, parent, "Grass");
            if (grass != null)
            {
                grass.transform.localPosition = pos;
                grass.transform.localScale = Vector3.one * 0.8f;
                grass.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }
    }

    private void SpawnWell(Transform parent, Vector3 position)
    {
        var well = new GameObject("Well");
        well.transform.SetParent(parent, false);
        well.transform.localPosition = position;

        // Try Kenney fountain model as a well
        var fountain = LoadModel("Town/fountain-center", well.transform, "Fountain");
        if (fountain != null)
        {
            fountain.transform.localPosition = Vector3.zero;
            fountain.transform.localScale = Vector3.one * 1.0f;
        }
        else
        {
            // Fallback
            var ring = CreateVisualPrimitive(PrimitiveType.Cylinder, "WellRing");
            ring.transform.SetParent(well.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            ring.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
            ApplyColor(ring, new Color(0.50f, 0.48f, 0.45f));

            var water = CreateVisualPrimitive(PrimitiveType.Cylinder, "WellWater");
            water.transform.SetParent(well.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.15f, 0f);
            water.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
            ApplyColor(water, new Color(0.20f, 0.40f, 0.70f));
        }
    }

    private void SpawnCampfire(Transform parent, Vector3 position)
    {
        var campfire = new GameObject("Campfire");
        campfire.transform.SetParent(parent, false);
        campfire.transform.localPosition = position;

        // Stone ring
        var ring = CreateVisualPrimitive(PrimitiveType.Cylinder, "FireRing");
        ring.transform.SetParent(campfire.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        ring.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);
        ApplyColor(ring, new Color(0.4f, 0.38f, 0.35f));

        // Fire (orange sphere)
        var fire = CreateVisualPrimitive(PrimitiveType.Sphere, "Fire");
        fire.transform.SetParent(campfire.transform, false);
        fire.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        fire.transform.localScale = Vector3.one * 0.25f;
        ApplyColor(fire, new Color(0.90f, 0.45f, 0.10f));

        // Logs
        for (int i = 0; i < 3; i++)
        {
            var log = CreateVisualPrimitive(PrimitiveType.Cylinder, "Log");
            log.transform.SetParent(campfire.transform, false);
            float angle = i * 120f * Mathf.Deg2Rad;
            log.transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.2f, 0.06f, Mathf.Sin(angle) * 0.2f);
            log.transform.localScale = new Vector3(0.06f, 0.15f, 0.06f);
            log.transform.localRotation = Quaternion.Euler(0f, 0f, 70f);
            ApplyColor(log, new Color(0.40f, 0.25f, 0.12f));
        }
    }
}

/// <summary>
/// Makes a world-space UI element always face the camera.
/// Attached to interact prompts so they're readable from any angle.
/// </summary>
public class BillboardFaceCamera : MonoBehaviour
{
    private void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        transform.rotation = cam.transform.rotation;
    }
}
