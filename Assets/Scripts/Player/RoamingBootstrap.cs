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
    [SerializeField] private Vector3 _playerSpawn = new Vector3(0f, 0f, -8f); // just inside the south gate
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
        _roamingCam.orthographicSize = 7f;
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
        // FollowCamera now uses orthoSize, not height/distance
        follow.SetOrthoSize(7f);
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

        // Courtyard area — sandy stone ground inside the castle walls (20x20)
        var courtyard = CreateVisualPrimitive(PrimitiveType.Plane, "Courtyard");
        courtyard.transform.position = new Vector3(0f, 0.01f, 0f); // Slightly above to avoid z-fight
        courtyard.transform.localScale = new Vector3(2f, 1f, 2f); // 20x20 units (Plane is 10x10 at scale 1)
        ApplyColor(courtyard, new Color(0.55f, 0.50f, 0.40f)); // Sandy stone
    }

    // ---------------------------------------------------------------
    //  3D PLAYER
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;

        // Try Kenney Mini Character FBX first, fall back to CharacterBuilder cubes
        GameObject visualRoot = null;
        var fbxPrefab = Resources.Load<GameObject>("Models/Characters/character-male-a");
        if (fbxPrefab != null)
        {
            visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(_player.transform, false);
            var model = Instantiate(fbxPrefab);
            model.name = "CharacterModel";
            model.transform.SetParent(visualRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 1.0f;
        }
        else
        {
            // Fallback: blocky humanoid via CharacterBuilder
            var playerConfig = new CharacterBuilder.CharacterConfig
            {
                bodyColor  = new Color(0.45f, 0.25f, 0.65f),  // royal purple
                pantsColor = new Color(0.3f, 0.15f, 0.45f),   // dark purple
                skinColor  = new Color(0.92f, 0.78f, 0.62f),  // warm skin
                hairColor  = new Color(0.4f, 0.25f, 0.12f),   // brown
                hasHair    = true,
                accessory  = CharacterBuilder.AccessoryType.Crown,
            };
            visualRoot = CharacterBuilder.BuildCharacter(_player.transform, playerConfig);
        }

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

    /// <summary>
    /// Returns the Kenney Mini Character FBX resource path for a given NPC, or null if unknown.
    /// </summary>
    private static string GetNPCCharacterModel(NPCManager.NPCData npc)
    {
        // Map by profession (matching the known cast)
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal   => "Models/Characters/character-male-c",   // Aldric
            NPCPersona.NPCProfession.Soldier   => "Models/Characters/character-male-d",   // Bram
            NPCPersona.NPCProfession.Farmer    => "Models/Characters/character-female-a",  // Marta
            NPCPersona.NPCProfession.Merchant  => "Models/Characters/character-male-e",   // Sivaro
            _ => null,
        };
    }

    private void SpawnOneNPC(NPCManager.NPCData npc)
    {
        if (npc == null || string.IsNullOrEmpty(npc.Id)) return;
        if (_npcObjects.ContainsKey(npc.Id)) return;

        var root = new GameObject($"NPC_{npc.Id}");
        root.transform.SetParent(_npcRoot, false);
        // Use 3D world positions directly (XZ ground plane)
        root.transform.position = new Vector3(npc.WorldPosition.x, 0f, npc.WorldPosition.z);

        // Try Kenney Mini Character FBX first, fall back to CharacterBuilder cubes
        GameObject visualRoot = null;
        string modelPath = GetNPCCharacterModel(npc);
        GameObject fbxPrefab = modelPath != null ? Resources.Load<GameObject>(modelPath) : null;

        if (fbxPrefab != null)
        {
            visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);
            var model = Instantiate(fbxPrefab);
            model.name = "CharacterModel";
            model.transform.SetParent(visualRoot.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 1.0f;
        }
        else
        {
            // Fallback: blocky humanoid via CharacterBuilder
            var config = GetNPCCharacterConfig(npc);
            visualRoot = CharacterBuilder.BuildCharacter(root.transform, config);
        }

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
    //  BUILDINGS — Kenney Castle Kit FBX castle compound
    // ---------------------------------------------------------------

    // Castle compound dimensions — towers at corners, walls between them
    // Layout is ~20x20 units centered on origin.
    // Kenney Castle Kit pieces are ~1 unit per segment at scale 1.
    // We use scale 2 so each piece spans ~2 units, making the compound feel substantial.
    private const float CastleScale = 2f;       // scale applied to all castle kit pieces
    private const float SegmentSize = 2f;        // world-units per wall segment at CastleScale
    private const float HalfCastle = 10f;        // half-extent of the compound (corners at +/-10)
    private const float TowerSegH = 2f;          // height per tower segment at CastleScale

    private void SpawnBuildings()
    {
        var buildRoot = new GameObject("Buildings");

        // 1. Four corner towers (stacked: base + mid + roof)
        SpawnCornerTower(buildRoot.transform, new Vector3(-HalfCastle, 0f,  HalfCastle), "TowerNW", 0f);
        SpawnCornerTower(buildRoot.transform, new Vector3( HalfCastle, 0f,  HalfCastle), "TowerNE", 0f);
        SpawnCornerTower(buildRoot.transform, new Vector3(-HalfCastle, 0f, -HalfCastle), "TowerSW", 0f);
        SpawnCornerTower(buildRoot.transform, new Vector3( HalfCastle, 0f, -HalfCastle), "TowerSE", 0f);

        // 2. Walls connecting the towers
        // North wall (full span between NW and NE towers)
        SpawnWallRun(buildRoot.transform,
            new Vector3(-HalfCastle + SegmentSize, 0f, HalfCastle),
            new Vector3( HalfCastle - SegmentSize, 0f, HalfCastle),
            0f, "WallN");

        // South wall — split for gate in the center
        float gateHalfGap = SegmentSize; // leave a 2-segment gap for the gate
        SpawnWallRun(buildRoot.transform,
            new Vector3(-HalfCastle + SegmentSize, 0f, -HalfCastle),
            new Vector3(-gateHalfGap, 0f, -HalfCastle),
            0f, "WallSL");
        SpawnWallRun(buildRoot.transform,
            new Vector3(gateHalfGap, 0f, -HalfCastle),
            new Vector3(HalfCastle - SegmentSize, 0f, -HalfCastle),
            0f, "WallSR");

        // West wall
        SpawnWallRun(buildRoot.transform,
            new Vector3(-HalfCastle, 0f, -HalfCastle + SegmentSize),
            new Vector3(-HalfCastle, 0f,  HalfCastle - SegmentSize),
            90f, "WallW");

        // East wall
        SpawnWallRun(buildRoot.transform,
            new Vector3(HalfCastle, 0f, -HalfCastle + SegmentSize),
            new Vector3(HalfCastle, 0f,  HalfCastle - SegmentSize),
            90f, "WallE");

        // 3. Gate (south center)
        SpawnGate(buildRoot.transform);

        // 4. Central keep — taller stacked tower
        SpawnCentralKeep(buildRoot.transform);

        // 5. Courtyard buildings
        SpawnWindmill(buildRoot.transform);
        SpawnMarketArea(buildRoot.transform);
        SpawnFountain(buildRoot.transform);
    }

    /// <summary>
    /// Build a corner tower: tower-square-base + tower-square-mid + tower-square-roof.
    /// Falls back to procedural cylinder if FBX missing.
    /// </summary>
    private void SpawnCornerTower(Transform parent, Vector3 position, string name, float yRot)
    {
        var tower = new GameObject(name);
        tower.transform.SetParent(parent, false);
        tower.transform.localPosition = position;

        var tBase = LoadModel("Castle/tower-square-base", tower.transform, $"{name}_Base");
        if (tBase != null)
        {
            tBase.transform.localPosition = Vector3.zero;
            tBase.transform.localScale = Vector3.one * CastleScale;
            tBase.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);

            var tMid = LoadModel("Castle/tower-square-mid", tower.transform, $"{name}_Mid");
            if (tMid != null)
            {
                tMid.transform.localPosition = new Vector3(0f, TowerSegH, 0f);
                tMid.transform.localScale = Vector3.one * CastleScale;
                tMid.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            }

            var tRoof = LoadModel("Castle/tower-square-roof", tower.transform, $"{name}_Roof");
            if (tRoof != null)
            {
                tRoof.transform.localPosition = new Vector3(0f, TowerSegH * 2f, 0f);
                tRoof.transform.localScale = Vector3.one * CastleScale;
                tRoof.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
            }
        }
        else
        {
            // Fallback: procedural cylinder tower
            var body = CreateVisualPrimitive(PrimitiveType.Cylinder, $"{name}_Body");
            body.transform.SetParent(tower.transform, false);
            body.transform.localPosition = new Vector3(0f, 2f, 0f);
            body.transform.localScale = new Vector3(1.8f, 2f, 1.8f);
            ApplyColor(body, new Color(0.50f, 0.48f, 0.45f));

            var top = CreateVisualPrimitive(PrimitiveType.Cube, $"{name}_Top");
            top.transform.SetParent(tower.transform, false);
            top.transform.localPosition = new Vector3(0f, 4.2f, 0f);
            top.transform.localScale = new Vector3(2.2f, 0.3f, 2.2f);
            ApplyColor(top, new Color(0.45f, 0.43f, 0.40f));
        }

        AddBuildingCollider(tower, new Vector3(2.2f, TowerSegH * 3f, 2.2f),
            new Vector3(0f, TowerSegH * 1.5f, 0f));
    }

    /// <summary>
    /// Spawn a continuous run of wall.fbx segments between two points.
    /// Walls face outward; yRot=0 for X-aligned walls, yRot=90 for Z-aligned walls.
    /// Each segment gets placed adjacent with no gaps.
    /// A single BoxCollider spans the entire run.
    /// </summary>
    private void SpawnWallRun(Transform parent, Vector3 from, Vector3 to, float yRot, string prefix)
    {
        Vector3 dir = to - from;
        float length = dir.magnitude;
        if (length < 0.1f) return;
        dir.Normalize();

        int count = Mathf.Max(1, Mathf.RoundToInt(length / SegmentSize));
        float actualSpacing = length / count;

        for (int i = 0; i < count; i++)
        {
            // Place segment at the center of each slot
            Vector3 pos = from + dir * ((i + 0.5f) * actualSpacing);
            var seg = LoadModel("Castle/wall", parent, $"{prefix}_{i}");
            if (seg != null)
            {
                seg.transform.localPosition = pos;
                seg.transform.localRotation = Quaternion.Euler(0f, yRot, 0f);
                // Scale walls slightly larger than CastleScale to eliminate gaps between segments
                seg.transform.localScale = Vector3.one * (CastleScale * 1.05f);
            }
        }

        // Full-span physics collider
        var colliderGO = new GameObject($"{prefix}_Collider");
        colliderGO.transform.SetParent(parent, false);
        Vector3 center = (from + to) * 0.5f + Vector3.up * (TowerSegH * 0.5f);
        colliderGO.transform.localPosition = center;
        var box = colliderGO.AddComponent<BoxCollider>();
        float dx = Mathf.Abs(to.x - from.x);
        float dz = Mathf.Abs(to.z - from.z);
        box.size = new Vector3(
            Mathf.Max(dx + SegmentSize, 0.6f),
            TowerSegH,
            Mathf.Max(dz + SegmentSize, 0.6f)
        );
    }

    /// <summary>
    /// South gate using wall-doorway.fbx (or gate.fbx fallback), centered at south wall gap.
    /// </summary>
    private void SpawnGate(Transform parent)
    {
        var gate = new GameObject("CastleGate");
        gate.transform.SetParent(parent, false);
        gate.transform.localPosition = new Vector3(0f, 0f, -HalfCastle);

        // Try wall-doorway first (archway that matches wall height)
        var doorway = LoadModel("Castle/wall-doorway", gate.transform, "GateDoorway");
        if (doorway != null)
        {
            doorway.transform.localPosition = Vector3.zero;
            doorway.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            doorway.transform.localScale = Vector3.one * CastleScale;
        }
        else
        {
            // Try gate.fbx
            var gateModel = LoadModel("Castle/gate", gate.transform, "GateModel");
            if (gateModel != null)
            {
                gateModel.transform.localPosition = Vector3.zero;
                gateModel.transform.localScale = Vector3.one * CastleScale;
            }
            else
            {
                // Procedural fallback: arch between two pillars
                for (int side = -1; side <= 1; side += 2)
                {
                    var pillar = CreateVisualPrimitive(PrimitiveType.Cube, "GatePillar");
                    pillar.transform.SetParent(gate.transform, false);
                    pillar.transform.localPosition = new Vector3(side * 1.2f, TowerSegH * 0.5f, 0f);
                    pillar.transform.localScale = new Vector3(0.6f, TowerSegH, 0.6f);
                    ApplyColor(pillar, new Color(0.50f, 0.48f, 0.45f));
                }
                var arch = CreateVisualPrimitive(PrimitiveType.Cube, "GateArch");
                arch.transform.SetParent(gate.transform, false);
                arch.transform.localPosition = new Vector3(0f, TowerSegH, 0f);
                arch.transform.localScale = new Vector3(3f, 0.4f, 0.6f);
                ApplyColor(arch, new Color(0.50f, 0.48f, 0.45f));
            }
        }

        // Side colliders only (leave opening passable)
        var colL = new GameObject("GateColL");
        colL.transform.SetParent(gate.transform, false);
        colL.transform.localPosition = new Vector3(-1.5f, TowerSegH * 0.5f, 0f);
        var boxL = colL.AddComponent<BoxCollider>();
        boxL.size = new Vector3(0.6f, TowerSegH, 1f);

        var colR = new GameObject("GateColR");
        colR.transform.SetParent(gate.transform, false);
        colR.transform.localPosition = new Vector3(1.5f, TowerSegH * 0.5f, 0f);
        var boxR = colR.AddComponent<BoxCollider>();
        boxR.size = new Vector3(0.6f, TowerSegH, 1f);
    }

    /// <summary>
    /// Central keep — taller than corner towers:
    /// tower-square-base + tower-square-mid-windows + tower-square-mid + tower-square-top-roof-high + flag.
    /// </summary>
    private void SpawnCentralKeep(Transform parent)
    {
        var keep = new GameObject("CentralKeep");
        keep.transform.SetParent(parent, false);
        keep.transform.localPosition = new Vector3(0f, 0f, 3f); // slightly north of center

        float s = CastleScale * 1.25f; // keep is 25% bigger than corner towers
        float segH = TowerSegH * 1.25f;

        var kBase = LoadModel("Castle/tower-square-base", keep.transform, "KeepBase");
        if (kBase != null)
        {
            kBase.transform.localPosition = Vector3.zero;
            kBase.transform.localScale = Vector3.one * s;

            var kMidWin = LoadModel("Castle/tower-square-mid-windows", keep.transform, "KeepMidWin");
            if (kMidWin != null)
            {
                kMidWin.transform.localPosition = new Vector3(0f, segH, 0f);
                kMidWin.transform.localScale = Vector3.one * s;
            }

            var kMid2 = LoadModel("Castle/tower-square-mid", keep.transform, "KeepMid2");
            if (kMid2 != null)
            {
                kMid2.transform.localPosition = new Vector3(0f, segH * 2f, 0f);
                kMid2.transform.localScale = Vector3.one * s;
            }

            var kRoof = LoadModel("Castle/tower-square-top-roof-high", keep.transform, "KeepRoof");
            if (kRoof != null)
            {
                kRoof.transform.localPosition = new Vector3(0f, segH * 3f, 0f);
                kRoof.transform.localScale = Vector3.one * s;
            }

            // Flag on top
            var flag = LoadModel("Castle/flag", keep.transform, "KeepFlag");
            if (flag != null)
            {
                flag.transform.localPosition = new Vector3(0f, segH * 4f, 0f);
                flag.transform.localScale = Vector3.one * s;
            }
        }
        else
        {
            // Procedural fallback keep
            var body = CreateVisualPrimitive(PrimitiveType.Cube, "KeepBody");
            body.transform.SetParent(keep.transform, false);
            body.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            body.transform.localScale = new Vector3(3.5f, 5f, 3.5f);
            ApplyColor(body, new Color(0.50f, 0.45f, 0.40f));

            var roof = CreateVisualPrimitive(PrimitiveType.Cube, "KeepRoof");
            roof.transform.SetParent(keep.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            roof.transform.localScale = new Vector3(3.8f, 0.5f, 3.8f);
            ApplyColor(roof, new Color(0.25f, 0.20f, 0.40f));

            var pole = CreateVisualPrimitive(PrimitiveType.Cylinder, "FlagPole");
            pole.transform.SetParent(keep.transform, false);
            pole.transform.localPosition = new Vector3(0f, 6.5f, 0f);
            pole.transform.localScale = new Vector3(0.05f, 1f, 0.05f);
            ApplyColor(pole, new Color(0.6f, 0.5f, 0.3f));

            var flagFb = CreateVisualPrimitive(PrimitiveType.Cube, "Flag");
            flagFb.transform.SetParent(pole.transform, false);
            flagFb.transform.localPosition = new Vector3(0.4f, 0.6f, 0f);
            flagFb.transform.localScale = new Vector3(8f, 4f, 0.5f);
            ApplyColor(flagFb, new Color(0.70f, 0.15f, 0.15f));
        }

        AddBuildingCollider(keep, new Vector3(3.5f, segH * 4f, 3.5f), new Vector3(0f, segH * 2f, 0f));
    }

    /// <summary>Windmill inside courtyard, west side.</summary>
    private void SpawnWindmill(Transform parent)
    {
        var windmill = new GameObject("Windmill");
        windmill.transform.SetParent(parent, false);
        windmill.transform.localPosition = new Vector3(-4f, 0f, -3f);

        var model = LoadModel("Town/windmill", windmill.transform, "WindmillModel");
        if (model != null)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 1.2f;
        }
        else
        {
            // Procedural fallback
            var body = CreateVisualPrimitive(PrimitiveType.Cylinder, "WindmillBody");
            body.transform.SetParent(windmill.transform, false);
            body.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            body.transform.localScale = new Vector3(1f, 1.2f, 1f);
            ApplyColor(body, new Color(0.55f, 0.50f, 0.42f));

            var roof = CreateVisualPrimitive(PrimitiveType.Cube, "WindmillRoof");
            roof.transform.SetParent(windmill.transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            roof.transform.localScale = new Vector3(1.3f, 0.2f, 1.3f);
            ApplyColor(roof, new Color(0.35f, 0.25f, 0.15f));
        }

        AddBuildingCollider(windmill, new Vector3(2f, 3f, 2f), new Vector3(0f, 1.5f, 0f));
    }

    /// <summary>Market stall + cart inside courtyard, east side.</summary>
    private void SpawnMarketArea(Transform parent)
    {
        var market = new GameObject("MarketArea");
        market.transform.SetParent(parent, false);
        market.transform.localPosition = new Vector3(4f, 0f, -3f);

        var stall = LoadModel("Town/stall-red", market.transform, "Stall");
        if (stall == null)
            stall = LoadModel("Town/stall", market.transform, "Stall");
        if (stall != null)
        {
            stall.transform.localPosition = Vector3.zero;
            stall.transform.localScale = Vector3.one * 1.2f;
        }
        else
        {
            // Procedural fallback
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
        }

        // Cart beside the stall
        var cart = LoadModel("Town/cart", market.transform, "Cart");
        if (cart != null)
        {
            cart.transform.localPosition = new Vector3(2.5f, 0f, 0f);
            cart.transform.localScale = Vector3.one * 1.0f;
        }
    }

    /// <summary>Fountain in the center of the courtyard.</summary>
    private void SpawnFountain(Transform parent)
    {
        var fountain = new GameObject("Fountain");
        fountain.transform.SetParent(parent, false);
        fountain.transform.localPosition = new Vector3(0f, 0f, -3f); // south of keep, center of courtyard

        var model = LoadModel("Town/fountain-center", fountain.transform, "FountainModel");
        if (model != null)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localScale = Vector3.one * 1.2f;
        }
        else
        {
            // Procedural fallback
            var ring = CreateVisualPrimitive(PrimitiveType.Cylinder, "FountainRing");
            ring.transform.SetParent(fountain.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            ring.transform.localScale = new Vector3(1f, 0.25f, 1f);
            ApplyColor(ring, new Color(0.50f, 0.48f, 0.45f));

            var water = CreateVisualPrimitive(PrimitiveType.Cylinder, "FountainWater");
            water.transform.SetParent(fountain.transform, false);
            water.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            water.transform.localScale = new Vector3(0.85f, 0.02f, 0.85f);
            ApplyColor(water, new Color(0.20f, 0.40f, 0.70f));
        }
    }

    // ---------------------------------------------------------------
    //  ENVIRONMENT — Kenney Nature Kit FBX models
    // ---------------------------------------------------------------

    private void SpawnEnvironment()
    {
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;

        System.Random rng = new System.Random(42);

        // Stone paths inside courtyard
        SpawnStonePaths(envRoot.transform);

        // Trees OUTSIDE the castle walls (beyond +/-HalfCastle)
        float outerStart = HalfCastle + 2f;  // start 2 units beyond walls
        float outerEnd   = HalfCastle + 10f; // extend 10 units out
        for (float x = -outerEnd; x <= outerEnd; x += 3.5f)
        {
            SpawnTree(envRoot.transform, new Vector3(
                x + (float)(rng.NextDouble() * 1.5), 0f,
                outerStart + (float)(rng.NextDouble() * (outerEnd - outerStart))), rng);
            SpawnTree(envRoot.transform, new Vector3(
                x + (float)(rng.NextDouble() * 1.5), 0f,
                -outerStart - (float)(rng.NextDouble() * (outerEnd - outerStart))), rng);
        }
        for (float z = -outerEnd; z <= outerEnd; z += 3.5f)
        {
            SpawnTree(envRoot.transform, new Vector3(
                -outerStart - (float)(rng.NextDouble() * (outerEnd - outerStart)), 0f,
                z + (float)(rng.NextDouble() * 1.5)), rng);
            SpawnTree(envRoot.transform, new Vector3(
                outerStart + (float)(rng.NextDouble() * (outerEnd - outerStart)), 0f,
                z + (float)(rng.NextDouble() * 1.5)), rng);
        }

        // Castle Kit trees (tree-large, tree-small) scattered outside walls
        SpawnCastleKitTrees(envRoot.transform, rng);

        // Rocks outside the walls using Castle Kit rocks
        SpawnOutsideRocks(envRoot.transform, rng);

        // A couple of small courtyard trees for visual interest (not blocking paths)
        Vector3[] courtyardTrees = {
            new(-7f, 0f, 5f), new(7f, 0f, 5f), new(-7f, 0f, -5f), new(7f, 0f, -5f),
        };
        foreach (var pos in courtyardTrees)
            SpawnTree(envRoot.transform, pos, rng);

        // Bushes, flowers, and grass inside the courtyard
        SpawnNatureDetails(envRoot.transform, rng);

        // Flower beds near the keep
        SpawnFlowerBeds(envRoot.transform, rng);

        // Campfire / gathering area in courtyard
        SpawnCampfire(envRoot.transform, new Vector3(-2f, 0f, -6f));
    }

    /// <summary>
    /// Place Kenney Castle Kit tree-large and tree-small outside the castle walls.
    /// </summary>
    private void SpawnCastleKitTrees(Transform parent, System.Random rng)
    {
        string[] models = { "Castle/tree-large", "Castle/tree-small" };
        // Scatter around the exterior
        for (int i = 0; i < 12; i++)
        {
            float angle = i * (360f / 12f) + (float)(rng.NextDouble() * 20f);
            float dist = HalfCastle + 3f + (float)(rng.NextDouble() * 6f);
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * dist;
            float z = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;

            string model = models[rng.Next(models.Length)];
            var tree = LoadModel(model, parent, $"CastleTree_{i}");
            if (tree != null)
            {
                tree.transform.localPosition = new Vector3(x, 0f, z);
                float s = 1.5f + (float)(rng.NextDouble() * 1.0);
                tree.transform.localScale = Vector3.one * s;
                tree.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }
    }

    /// <summary>
    /// Place Castle Kit rocks-large and rocks-small outside the castle walls.
    /// </summary>
    private void SpawnOutsideRocks(Transform parent, System.Random rng)
    {
        string[] models = { "Castle/rocks-large", "Castle/rocks-small" };
        Vector3[] positions = {
            new(-14f, 0f, -14f), new(14f, 0f, -14f),
            new(-14f, 0f, 14f), new(14f, 0f, 14f),
            new(-16f, 0f, 0f), new(16f, 0f, 0f),
            new(0f, 0f, 16f), new(0f, 0f, -16f),
        };
        for (int i = 0; i < positions.Length; i++)
        {
            string model = models[rng.Next(models.Length)];
            var rock = LoadModel(model, parent, $"OutsideRock_{i}");
            if (rock != null)
            {
                float dx = (float)(rng.NextDouble() * 2.0 - 1.0);
                float dz = (float)(rng.NextDouble() * 2.0 - 1.0);
                rock.transform.localPosition = positions[i] + new Vector3(dx, 0f, dz);
                float s = 1.5f + (float)(rng.NextDouble() * 1.5);
                rock.transform.localScale = Vector3.one * s;
                rock.transform.localRotation = Quaternion.Euler(0f, rng.Next(360), 0f);
            }
        }
    }

    /// <summary>
    /// Stone paths inside the courtyard connecting gate to keep and side buildings.
    /// </summary>
    private void SpawnStonePaths(Transform parent)
    {
        Color pathColor = new Color(0.42f, 0.38f, 0.32f);

        // Main path: gate (south, z=-10) to keep (z=3)
        var pathGateToKeep = CreateVisualPrimitive(PrimitiveType.Cube, "PathGateToKeep");
        pathGateToKeep.transform.SetParent(parent, false);
        pathGateToKeep.transform.localPosition = new Vector3(0f, 0.008f, -3.5f);
        pathGateToKeep.transform.localScale = new Vector3(2.5f, 0.01f, 13f);
        ApplyColor(pathGateToKeep, pathColor);

        // Path to windmill (west, x=-4)
        var pathToWindmill = CreateVisualPrimitive(PrimitiveType.Cube, "PathToWindmill");
        pathToWindmill.transform.SetParent(parent, false);
        pathToWindmill.transform.localPosition = new Vector3(-2f, 0.008f, -3f);
        pathToWindmill.transform.localScale = new Vector3(4f, 0.01f, 2f);
        ApplyColor(pathToWindmill, pathColor);

        // Path to market stall (east, x=4)
        var pathToMarket = CreateVisualPrimitive(PrimitiveType.Cube, "PathToMarket");
        pathToMarket.transform.SetParent(parent, false);
        pathToMarket.transform.localPosition = new Vector3(2f, 0.008f, -3f);
        pathToMarket.transform.localScale = new Vector3(4f, 0.01f, 2f);
        ApplyColor(pathToMarket, pathColor);

        // Widened area around fountain (z=-3)
        var pathFountainArea = CreateVisualPrimitive(PrimitiveType.Cube, "PathFountainArea");
        pathFountainArea.transform.SetParent(parent, false);
        pathFountainArea.transform.localPosition = new Vector3(0f, 0.008f, -3f);
        pathFountainArea.transform.localScale = new Vector3(4f, 0.01f, 4f);
        ApplyColor(pathFountainArea, new Color(0.44f, 0.40f, 0.34f));
    }

    /// <summary>
    /// Extra flower beds between buildings for a lively garden feel.
    /// </summary>
    private void SpawnFlowerBeds(Transform parent, System.Random rng)
    {
        // Flower beds near the keep and along paths
        Vector3[] bedCenters = {
            new(2f, 0f, 1f), new(-2f, 0f, 1f),
            new(3f, 0f, -5f), new(-3f, 0f, -5f),
            new(6f, 0f, 0f), new(-6f, 0f, 0f),
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
        // Bushes (inside courtyard, avoiding building positions)
        Vector3[] bushPositions = {
            new(-6f, 0f, 0f), new(6f, 0f, 0f), new(-2f, 0f, 7f),
            new(8f, 0f, -6f), new(-8f, 0f, 7f), new(5f, 0f, -7f),
            new(-5f, 0f, 8f), new(7f, 0f, 7f),
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

        // Small rocks inside courtyard
        Vector3[] rockPositions = {
            new(-8f, 0f, 3f), new(8f, 0f, -3f), new(-3f, 0f, 8f),
            new(6f, 0f, -8f), new(-8f, 0f, -7f),
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

        // Flowers scattered inside courtyard
        Vector3[] flowerPositions = {
            new(-3f, 0f, 2f), new(2f, 0f, -1f), new(-6f, 0f, -4f),
            new(5f, 0f, 3f), new(-1f, 0f, 7f), new(7f, 0f, 5f),
            new(-5f, 0f, -7f), new(3f, 0f, 8f),
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
            new(-1f, 0f, 1f), new(3f, 0f, -1f), new(-4f, 0f, 4f),
            new(5f, 0f, 5f), new(1f, 0f, 6f), new(-3f, 0f, -6f),
            new(7f, 0f, 1f), new(-7f, 0f, 2f),
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
