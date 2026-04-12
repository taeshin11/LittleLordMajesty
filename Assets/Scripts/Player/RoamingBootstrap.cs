using UnityEngine;
using TMPro;

/// <summary>
/// 3D roaming pivot — runtime bootstrap that spawns the 3D world using
/// Kenney FBX models from Resources/Models/, with procedural fallbacks.
///
/// 3D top-down perspective: camera angled ~45° looking down at XZ ground plane.
/// Buildings use Castle Kit + Fantasy Town Kit FBX models.
/// Trees/nature use Nature Kit FBX models.
/// Characters keep procedural primitives (capsule+sphere) with better colors.
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
        _roamingCam.orthographic = false;  // 3D perspective
        _roamingCam.fieldOfView = 40f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.53f, 0.81f, 0.92f); // Sky blue
        _roamingCam.nearClipPlane = 0.3f;
        _roamingCam.farClipPlane = 200f;
        _roamingCam.depth = 10;
        // Initial position — will be overridden by FollowCamera
        camGO.transform.position = new Vector3(0f, 12f, -10f);
        camGO.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
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

        // Visual root for walk bob
        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(_player.transform, false);

        // Scale down entire character for miniature/cozy feel
        visualRoot.transform.localScale = Vector3.one * 0.65f;

        // Body (capsule) — royal purple
        var body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body");
        body.transform.SetParent(visualRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        body.transform.localScale = new Vector3(0.5f, 0.55f, 0.5f);
        ApplyColor(body, new Color(0.45f, 0.25f, 0.65f)); // Royal purple

        // Head (sphere) — skin tone
        var head = CreateVisualPrimitive(PrimitiveType.Sphere, "Head");
        head.transform.SetParent(visualRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        head.transform.localScale = Vector3.one * 0.4f;
        ApplyColor(head, new Color(0.85f, 0.72f, 0.58f));

        // Crown (small gold cube on top of head)
        var crown = CreateVisualPrimitive(PrimitiveType.Cube, "Crown");
        crown.transform.SetParent(visualRoot.transform, false);
        crown.transform.localPosition = new Vector3(0f, 1.78f, 0f);
        crown.transform.localScale = new Vector3(0.3f, 0.1f, 0.3f);
        ApplyColor(crown, new Color(0.85f, 0.70f, 0.10f)); // Gold

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

        // Determine colors based on profession
        Color bodyColor = npc.Profession switch
        {
            NPCPersona.NPCProfession.Soldier  => new Color(0.55f, 0.25f, 0.15f), // Red-brown armor
            NPCPersona.NPCProfession.Farmer   => new Color(0.35f, 0.60f, 0.25f), // Green tunic
            NPCPersona.NPCProfession.Merchant => new Color(0.70f, 0.55f, 0.10f), // Gold/yellow
            NPCPersona.NPCProfession.Vassal   => new Color(0.45f, 0.30f, 0.65f), // Purple
            NPCPersona.NPCProfession.Scholar  => new Color(0.25f, 0.45f, 0.70f), // Blue robes
            NPCPersona.NPCProfession.Priest   => new Color(0.85f, 0.85f, 0.80f), // White robes
            NPCPersona.NPCProfession.Spy      => new Color(0.15f, 0.15f, 0.20f), // Dark cloak
            _                                  => new Color(0.5f, 0.5f, 0.5f),
        };
        Color skinColor = new Color(0.85f, 0.72f, 0.58f);

        // Visual root for walk bob — miniature scale for cozy feel
        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(root.transform, false);
        visualRoot.transform.localScale = Vector3.one * 0.65f;

        // Body (capsule)
        var body = CreateVisualPrimitive(PrimitiveType.Capsule, "Body");
        body.transform.SetParent(visualRoot.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        body.transform.localScale = new Vector3(0.45f, 0.50f, 0.45f);
        ApplyColor(body, bodyColor);

        // Head (sphere)
        var head = CreateVisualPrimitive(PrimitiveType.Sphere, "Head");
        head.transform.SetParent(visualRoot.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        head.transform.localScale = Vector3.one * 0.35f;
        ApplyColor(head, skinColor);

        // Profession accessory
        SpawnProfessionAccessory(visualRoot.transform, npc.Profession);

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

    private void SpawnProfessionAccessory(Transform parent, NPCPersona.NPCProfession profession)
    {
        switch (profession)
        {
            case NPCPersona.NPCProfession.Soldier:
                // Helmet — small dark sphere on top of head
                var helmet = CreateVisualPrimitive(PrimitiveType.Sphere, "Helmet");
                helmet.transform.SetParent(parent, false);
                helmet.transform.localPosition = new Vector3(0f, 1.65f, 0f);
                helmet.transform.localScale = Vector3.one * 0.25f;
                ApplyColor(helmet, new Color(0.4f, 0.4f, 0.45f));
                break;

            case NPCPersona.NPCProfession.Merchant:
                // Hat — flat cube on head
                var hat = CreateVisualPrimitive(PrimitiveType.Cube, "Hat");
                hat.transform.SetParent(parent, false);
                hat.transform.localPosition = new Vector3(0f, 1.63f, 0f);
                hat.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
                ApplyColor(hat, new Color(0.55f, 0.40f, 0.08f));
                break;

            case NPCPersona.NPCProfession.Farmer:
                // Straw hat — wider flat disk
                var strawHat = CreateVisualPrimitive(PrimitiveType.Cylinder, "StrawHat");
                strawHat.transform.SetParent(parent, false);
                strawHat.transform.localPosition = new Vector3(0f, 1.62f, 0f);
                strawHat.transform.localScale = new Vector3(0.45f, 0.04f, 0.45f);
                ApplyColor(strawHat, new Color(0.75f, 0.65f, 0.30f));
                break;

            case NPCPersona.NPCProfession.Priest:
                // Tall mitre (thin tall cube)
                var mitre = CreateVisualPrimitive(PrimitiveType.Cube, "Mitre");
                mitre.transform.SetParent(parent, false);
                mitre.transform.localPosition = new Vector3(0f, 1.72f, 0f);
                mitre.transform.localScale = new Vector3(0.15f, 0.2f, 0.15f);
                ApplyColor(mitre, new Color(0.90f, 0.88f, 0.80f));
                break;

            case NPCPersona.NPCProfession.Scholar:
                // Book — small cube held in front
                var book = CreateVisualPrimitive(PrimitiveType.Cube, "Book");
                book.transform.SetParent(parent, false);
                book.transform.localPosition = new Vector3(0f, 0.8f, 0.3f);
                book.transform.localScale = new Vector3(0.15f, 0.2f, 0.1f);
                ApplyColor(book, new Color(0.35f, 0.20f, 0.10f));
                break;

            case NPCPersona.NPCProfession.Spy:
                // Hood — dark sphere slightly forward on head
                var hood = CreateVisualPrimitive(PrimitiveType.Sphere, "Hood");
                hood.transform.SetParent(parent, false);
                hood.transform.localPosition = new Vector3(0f, 1.50f, 0.05f);
                hood.transform.localScale = Vector3.one * 0.38f;
                ApplyColor(hood, new Color(0.12f, 0.12f, 0.15f));
                break;
        }
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
        rt.sizeDelta = new Vector2(200f, 40f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var labelGO = new GameObject("Label");
        labelGO.transform.SetParent(promptGO.transform, false);
        var tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = $"E   Talk to {npcName}";
        tmp.fontSize = 24;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        var lblRT = tmp.rectTransform;
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        var outline = labelGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

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

        // Try Fantasy Town Kit wall + roof for a proper building look
        var wallModel = LoadModel("Town/wall-window-shutters", barracks.transform, "BarracksWall");
        if (wallModel != null)
        {
            wallModel.transform.localPosition = Vector3.zero;
            wallModel.transform.localScale = Vector3.one * 1.5f;

            // Add a second wall beside it for width
            var wall2 = LoadModel("Town/wall-window-shutters", barracks.transform, "BarracksWall2");
            if (wall2 != null)
            {
                wall2.transform.localPosition = new Vector3(1.5f, 0f, 0f);
                wall2.transform.localScale = Vector3.one * 1.5f;
            }

            // Roof
            var roofModel = LoadModel("Town/roof-gable", barracks.transform, "BarracksRoof");
            if (roofModel != null)
            {
                roofModel.transform.localPosition = new Vector3(0.75f, 1.5f, 0f);
                roofModel.transform.localScale = Vector3.one * 1.5f;
            }

            // Door
            var doorModel = LoadModel("Town/wall-door", barracks.transform, "BarracksDoor");
            if (doorModel != null)
            {
                doorModel.transform.localPosition = new Vector3(-0.75f, 0f, 0f);
                doorModel.transform.localScale = Vector3.one * 1.5f;
            }
        }
        else
        {
            // Fallback
            var body = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksBody");
            body.transform.SetParent(barracks.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(3f, 1.2f, 2f);
            ApplyColor(body, new Color(0.50f, 0.30f, 0.20f));

            var roof = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksRoof");
            roof.transform.SetParent(barracks.transform, false);
            roof.transform.localPosition = new Vector3(0f, 1.35f, 0f);
            roof.transform.localScale = new Vector3(3.2f, 0.15f, 2.2f);
            ApplyColor(roof, new Color(0.35f, 0.25f, 0.15f));
        }

        AddBuildingCollider(barracks, new Vector3(3f, 1.2f, 2f), new Vector3(0f, 0.6f, 0f));
    }

    private void SpawnFarm(Transform parent)
    {
        var farm = new GameObject("Farm");
        farm.transform.SetParent(parent, false);
        farm.transform.localPosition = new Vector3(-5f, 0f, 3f);

        // Try Kenney fence pieces
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

        // Small barn — fallback to primitive since we don't have a barn model
        var barn = CreateVisualPrimitive(PrimitiveType.Cube, "Barn");
        barn.transform.SetParent(farm.transform, false);
        barn.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        barn.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
        ApplyColor(barn, new Color(0.50f, 0.30f, 0.15f));

        var barnRoof = CreateVisualPrimitive(PrimitiveType.Cube, "BarnRoof");
        barnRoof.transform.SetParent(farm.transform, false);
        barnRoof.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        barnRoof.transform.localScale = new Vector3(1.7f, 0.15f, 1.7f);
        ApplyColor(barnRoof, new Color(0.60f, 0.25f, 0.10f));

        AddBuildingCollider(barn, new Vector3(1.5f, 1f, 1.5f), new Vector3(0f, 0.5f, 0f));

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

        // Well (near center-right) — use fountain model
        SpawnWell(envRoot.transform, new Vector3(2f, 0f, 3f));

        // Campfire / gathering area
        SpawnCampfire(envRoot.transform, new Vector3(-1f, 0f, -3f));
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
