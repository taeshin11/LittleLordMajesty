using UnityEngine;
using TMPro;

/// <summary>
/// 3D roaming pivot — runtime bootstrap that spawns the procedural 3D world,
/// player character, NPCs, buildings, and environment.
///
/// 3D top-down perspective: camera angled ~45° looking down at XZ ground plane.
/// All characters are procedural primitives (capsule body + sphere head).
/// Buildings are simple colored cube compositions.
/// Trees are green spheres on brown cylinders.
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
        Debug.Log("[RoamingBootstrap] 3D roaming world built (procedural primitives)");
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
        _roamingCam.backgroundColor = new Color(0.45f, 0.70f, 0.90f); // Sky blue
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
        // Main ground plane
        var ground = CreateVisualPrimitive(PrimitiveType.Plane, "Ground");
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(3f, 1f, 3f); // 30x30 units
        ApplyColor(ground, new Color(0.30f, 0.55f, 0.20f)); // Grass green

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

        // Visual root for walk bob
        var visualRoot = new GameObject("Visual");
        visualRoot.transform.SetParent(root.transform, false);

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
    //  PROCEDURAL BUILDINGS
    // ---------------------------------------------------------------

    private void SpawnBuildings()
    {
        var buildRoot = new GameObject("Buildings");

        // Central Keep — the main castle building
        SpawnKeep(buildRoot.transform);

        // Barracks (long low building, east side)
        SpawnBarracks(buildRoot.transform);

        // Farm (small building + fence, west side)
        SpawnFarm(buildRoot.transform);

        // Market stall (canopy shape, northeast)
        SpawnMarketStall(buildRoot.transform);

        // Castle gate (south)
        SpawnCastleGate(buildRoot.transform);

        // Watchtower (northwest corner)
        SpawnWatchtower(buildRoot.transform);

        // Castle walls
        SpawnCastleWalls(buildRoot.transform);
    }

    private void SpawnKeep(Transform parent)
    {
        var keep = new GameObject("CentralKeep");
        keep.transform.SetParent(parent, false);
        keep.transform.localPosition = new Vector3(0f, 0f, 2f);

        var body = CreateVisualPrimitive(PrimitiveType.Cube, "KeepBody");
        body.transform.SetParent(keep.transform, false);
        body.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        body.transform.localScale = new Vector3(3f, 3f, 3f);
        ApplyColor(body, new Color(0.50f, 0.45f, 0.40f)); // Stone grey

        var roof = CreateVisualPrimitive(PrimitiveType.Cube, "KeepRoof");
        roof.transform.SetParent(keep.transform, false);
        roof.transform.localPosition = new Vector3(0f, 3.2f, 0f);
        roof.transform.localScale = new Vector3(3.2f, 0.4f, 3.2f);
        ApplyColor(roof, new Color(0.25f, 0.20f, 0.40f)); // Dark purple roof

        // Keep collider — player can't walk through the castle
        AddBuildingCollider(keep, new Vector3(3f, 3f, 3f), new Vector3(0f, 1.5f, 0f));

        // Flag pole
        var pole = CreateVisualPrimitive(PrimitiveType.Cylinder, "FlagPole");
        pole.transform.SetParent(keep.transform, false);
        pole.transform.localPosition = new Vector3(0f, 4.2f, 0f);
        pole.transform.localScale = new Vector3(0.05f, 0.8f, 0.05f);
        ApplyColor(pole, new Color(0.6f, 0.5f, 0.3f));

        // Flag (small colored cube)
        var flag = CreateVisualPrimitive(PrimitiveType.Cube, "Flag");
        flag.transform.SetParent(pole.transform, false);
        flag.transform.localPosition = new Vector3(0.4f, 0.6f, 0f);
        flag.transform.localScale = new Vector3(8f, 4f, 0.5f); // Relative to pole scale
        ApplyColor(flag, new Color(0.70f, 0.15f, 0.15f)); // Red banner
    }

    private void SpawnBarracks(Transform parent)
    {
        var barracks = new GameObject("Barracks");
        barracks.transform.SetParent(parent, false);
        barracks.transform.localPosition = new Vector3(5f, 0f, -3f);

        var body = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksBody");
        body.transform.SetParent(barracks.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        body.transform.localScale = new Vector3(3f, 1.2f, 2f);
        ApplyColor(body, new Color(0.50f, 0.30f, 0.20f)); // Dark wood

        var roof = CreateVisualPrimitive(PrimitiveType.Cube, "BarracksRoof");
        roof.transform.SetParent(barracks.transform, false);
        roof.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        roof.transform.localScale = new Vector3(3.2f, 0.15f, 2.2f);
        ApplyColor(roof, new Color(0.35f, 0.25f, 0.15f));

        AddBuildingCollider(barracks, new Vector3(3f, 1.2f, 2f), new Vector3(0f, 0.6f, 0f));
    }

    private void SpawnFarm(Transform parent)
    {
        var farm = new GameObject("Farm");
        farm.transform.SetParent(parent, false);
        farm.transform.localPosition = new Vector3(-5f, 0f, 3f);

        // Small barn
        var barn = CreateVisualPrimitive(PrimitiveType.Cube, "Barn");
        barn.transform.SetParent(farm.transform, false);
        barn.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        barn.transform.localScale = new Vector3(1.5f, 1f, 1.5f);
        ApplyColor(barn, new Color(0.50f, 0.30f, 0.15f));

        AddBuildingCollider(barn, new Vector3(1.5f, 1f, 1.5f), new Vector3(0f, 0.5f, 0f));

        var barnRoof = CreateVisualPrimitive(PrimitiveType.Cube, "BarnRoof");
        barnRoof.transform.SetParent(farm.transform, false);
        barnRoof.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        barnRoof.transform.localScale = new Vector3(1.7f, 0.15f, 1.7f);
        ApplyColor(barnRoof, new Color(0.60f, 0.25f, 0.10f));

        // Fence posts around plot
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

        // Counter
        var counter = CreateVisualPrimitive(PrimitiveType.Cube, "Counter");
        counter.transform.SetParent(market.transform, false);
        counter.transform.localPosition = new Vector3(0f, 0.4f, 0f);
        counter.transform.localScale = new Vector3(2f, 0.8f, 0.8f);
        ApplyColor(counter, new Color(0.55f, 0.40f, 0.20f));

        // Canopy (thin wide cube above)
        var canopy = CreateVisualPrimitive(PrimitiveType.Cube, "Canopy");
        canopy.transform.SetParent(market.transform, false);
        canopy.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        canopy.transform.localScale = new Vector3(2.5f, 0.08f, 1.5f);
        ApplyColor(canopy, new Color(0.80f, 0.30f, 0.10f)); // Red awning

        // Support poles
        for (int i = -1; i <= 1; i += 2)
        {
            var pole = CreateVisualPrimitive(PrimitiveType.Cylinder, "CanopyPole");
            pole.transform.SetParent(market.transform, false);
            pole.transform.localPosition = new Vector3(i * 0.9f, 0.75f, -0.5f);
            pole.transform.localScale = new Vector3(0.05f, 0.75f, 0.05f);
            ApplyColor(pole, new Color(0.45f, 0.35f, 0.20f));
        }
    }

    private void SpawnCastleGate(Transform parent)
    {
        var gate = new GameObject("CastleGate");
        gate.transform.SetParent(parent, false);
        gate.transform.localPosition = new Vector3(0f, 0f, -8f);

        // Two gate towers
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

        // Gate arch (cube spanning the gap)
        var arch = CreateVisualPrimitive(PrimitiveType.Cube, "GateArch");
        arch.transform.SetParent(gate.transform, false);
        arch.transform.localPosition = new Vector3(0f, 2.0f, 0f);
        arch.transform.localScale = new Vector3(2f, 0.4f, 0.6f);
        ApplyColor(arch, new Color(0.50f, 0.48f, 0.45f));
    }

    private void SpawnWatchtower(Transform parent)
    {
        var tower = new GameObject("Watchtower");
        tower.transform.SetParent(parent, false);
        tower.transform.localPosition = new Vector3(-7f, 0f, -7f);

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

    private void SpawnCastleWalls(Transform parent)
    {
        float wallSize = 9.5f;
        float wallH = 1.2f;
        float wallW = 0.4f;
        Color wallColor = new Color(0.50f, 0.48f, 0.45f);

        // North wall
        CreateWallSegment(parent, new Vector3(0f, wallH / 2, wallSize),
            new Vector3(wallSize * 2, wallH, wallW), wallColor, "WallNorth");
        // South wall — gap for gate
        CreateWallSegment(parent, new Vector3(-5.5f, wallH / 2, -wallSize),
            new Vector3(8f, wallH, wallW), wallColor, "WallSouthL");
        CreateWallSegment(parent, new Vector3(5.5f, wallH / 2, -wallSize),
            new Vector3(8f, wallH, wallW), wallColor, "WallSouthR");
        // West wall
        CreateWallSegment(parent, new Vector3(-wallSize, wallH / 2, 0f),
            new Vector3(wallW, wallH, wallSize * 2), wallColor, "WallWest");
        // East wall
        CreateWallSegment(parent, new Vector3(wallSize, wallH / 2, 0f),
            new Vector3(wallW, wallH, wallSize * 2), wallColor, "WallEast");

        // Corner towers
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
    //  ENVIRONMENT (Trees, decorations)
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

        // Well (near center-right)
        SpawnWell(envRoot.transform, new Vector3(2f, 0f, 3f));

        // Campfire / gathering area
        SpawnCampfire(envRoot.transform, new Vector3(-1f, 0f, -3f));
    }

    private void SpawnTree(Transform parent, Vector3 position, System.Random rng)
    {
        var tree = new GameObject("Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = position;

        float trunkHeight = 0.8f + (float)(rng.NextDouble() * 0.4);
        float canopySize = 0.7f + (float)(rng.NextDouble() * 0.4);

        // Trunk (brown cylinder)
        var trunk = CreateVisualPrimitive(PrimitiveType.Cylinder, "Trunk");
        trunk.transform.SetParent(tree.transform, false);
        trunk.transform.localPosition = new Vector3(0f, trunkHeight / 2f, 0f);
        trunk.transform.localScale = new Vector3(0.15f, trunkHeight / 2f, 0.15f);
        ApplyColor(trunk, new Color(0.40f, 0.28f, 0.15f));

        // Canopy (green sphere)
        var canopy = CreateVisualPrimitive(PrimitiveType.Sphere, "Canopy");
        canopy.transform.SetParent(tree.transform, false);
        canopy.transform.localPosition = new Vector3(0f, trunkHeight + canopySize * 0.4f, 0f);
        canopy.transform.localScale = Vector3.one * canopySize;
        float greenVar = 0.35f + (float)(rng.NextDouble() * 0.15);
        ApplyColor(canopy, new Color(0.20f, greenVar, 0.12f));

        // Small collider on trunk so player walks around trees
        var col = tree.AddComponent<CapsuleCollider>();
        col.radius = 0.3f;
        col.height = trunkHeight + canopySize;
        col.center = new Vector3(0f, (trunkHeight + canopySize) / 2f, 0f);
    }

    private void SpawnWell(Transform parent, Vector3 position)
    {
        var well = new GameObject("Well");
        well.transform.SetParent(parent, false);
        well.transform.localPosition = position;

        // Base ring (short cylinder)
        var ring = CreateVisualPrimitive(PrimitiveType.Cylinder, "WellRing");
        ring.transform.SetParent(well.transform, false);
        ring.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        ring.transform.localScale = new Vector3(0.6f, 0.2f, 0.6f);
        ApplyColor(ring, new Color(0.50f, 0.48f, 0.45f));

        // Water (blue disk inside)
        var water = CreateVisualPrimitive(PrimitiveType.Cylinder, "WellWater");
        water.transform.SetParent(well.transform, false);
        water.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        water.transform.localScale = new Vector3(0.5f, 0.02f, 0.5f);
        ApplyColor(water, new Color(0.20f, 0.40f, 0.70f));
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
