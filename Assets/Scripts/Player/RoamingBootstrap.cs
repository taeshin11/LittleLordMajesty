using UnityEngine;
using TMPro;

/// <summary>
/// 2D Isometric roaming bootstrap — builds the world from Kenney Isometric
/// Miniature sprite packs (Overworld + Farm + Dungeon).
///
/// All visuals are SpriteRenderers on the XY plane at z=0.
/// Camera is orthographic at z=-10. Sprites are 256x512 at PPU=128
/// giving each tile a 2x4 world-unit footprint.
/// Sort order is based on Y position (lower Y = drawn in front).
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector3 _playerSpawn = new Vector3(0f, -3f, 0f);
    [SerializeField] private float _playerWalkSpeed = 4f;

    private bool _spawned;
    private bool _subscribed;
    private GameObject _player;
    private readonly System.Collections.Generic.Dictionary<string, GameObject> _npcObjects = new();
    private Transform _npcRoot;
    private Camera _roamingCam;

    // Isometric tile layout constants
    // Sprites are 256x512 at PPU=128 → 2x4 world units.
    // The "ground diamond" of an isometric tile occupies roughly the
    // bottom 2x2 portion. We space tiles by ~1.8 to overlap slightly
    // and form a seamless ground.
    private const float TileSpacingX = 1.8f;
    private const float TileSpacingY = 0.9f; // half of X for isometric feel

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
        Debug.Log("[RoamingBootstrap] 2D isometric world built (Kenney Isometric Miniature sprites)");
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
    //  2D CAMERA
    // ---------------------------------------------------------------

    private void BuildRoamingCamera()
    {
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.orthographic = true;
        _roamingCam.orthographicSize = 5.5f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.53f, 0.81f, 0.92f); // Sky blue
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 100f;
        _roamingCam.depth = 10;
        // Flat 2D — camera faces +Z, positioned at z=-10
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        camGO.transform.rotation = Quaternion.identity;
        try { camGO.tag = "MainCamera"; } catch { }

        // Set transparency sort axis so sprites sort by Y correctly
        _roamingCam.transparencySortMode = TransparencySortMode.CustomAxis;
        _roamingCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        var follow = camGO.AddComponent<FollowCamera>();
        follow.SetOrthoSize(5.5f);
    }

    // ---------------------------------------------------------------
    //  SPRITE HELPERS
    // ---------------------------------------------------------------

    /// <summary>
    /// Load a sprite from Resources/Art/Iso/{subPath} (without .png extension).
    /// </summary>
    private static Sprite LoadIsoSprite(string subPath)
    {
        var sprite = Resources.Load<Sprite>($"Art/Iso/{subPath}");
        if (sprite == null)
            Debug.LogWarning($"[RoamingBootstrap] Sprite not found: Art/Iso/{subPath}");
        return sprite;
    }

    /// <summary>
    /// Create a GameObject with a SpriteRenderer at the given position.
    /// sortingOrder is auto-computed from Y position.
    /// </summary>
    private static GameObject PlaceSprite(string spritePath, Vector3 position,
        Transform parent, string name = null, int sortingOffset = 0)
    {
        var sprite = LoadIsoSprite(spritePath);
        if (sprite == null) return null;

        var go = new GameObject(name ?? sprite.name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = Mathf.RoundToInt(-position.y * 100f) + sortingOffset;

        return go;
    }

    // ---------------------------------------------------------------
    //  2D GROUND — isometric grass tile grid
    // ---------------------------------------------------------------

    private void BuildGround()
    {
        var groundRoot = new GameObject("Ground");
        groundRoot.transform.position = Vector3.zero;

        // Tile a grid of grass sprites.
        // Grid extends well beyond the visible area.
        int halfExtent = 8; // tiles in each direction
        for (int row = -halfExtent; row <= halfExtent; row++)
        {
            for (int col = -halfExtent; col <= halfExtent; col++)
            {
                float x = col * TileSpacingX;
                float y = row * TileSpacingY;
                // Stagger every other row for isometric grid
                if (Mathf.Abs(row) % 2 == 1)
                    x += TileSpacingX * 0.5f;

                var go = PlaceSprite("Ground/grass_S", new Vector3(x, y, 0f),
                    groundRoot.transform, $"Grass_{row}_{col}", -10000);
                if (go != null)
                {
                    // Ground always behind everything
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sortingOrder = -10000;
                }
            }
        }

        // Scatter some path tiles for the courtyard
        string[] pathTiles = {
            "Ground/grassPathStraight_S",
            "Ground/grassPathStraight_S",
            "Ground/grassPathCrossing_S",
            "Ground/grassPathStraight_S",
            "Ground/grassPathStraight_S",
        };
        float pathY = -5f;
        for (int i = 0; i < pathTiles.Length; i++)
        {
            PlaceSprite(pathTiles[i], new Vector3(0f, pathY + i * TileSpacingY, 0f),
                groundRoot.transform, $"Path_{i}", -9999);
        }
    }

    // ---------------------------------------------------------------
    //  2D PLAYER — sprite with run animation
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;

        var sr = _player.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100; // Will be updated dynamically

        // Load idle sprite
        var idle = LoadIsoSprite("Characters/Male_0_Idle0");
        if (idle != null) sr.sprite = idle;

        // Load run frames
        var runFrames = new Sprite[10];
        for (int i = 0; i < 10; i++)
        {
            runFrames[i] = LoadIsoSprite($"Characters/Male_0_Run{i}");
        }

        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.ConfigureAtRuntime(_playerWalkSpeed, sr, idle, runFrames);

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
    /// Returns the character sprite variant index (0-4) for a given NPC profession.
    /// </summary>
    private static int GetNPCCharacterVariant(NPCManager.NPCData npc)
    {
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal   => 1,  // Aldric
            NPCPersona.NPCProfession.Soldier   => 2,  // Bram
            NPCPersona.NPCProfession.Farmer    => 3,  // Marta
            NPCPersona.NPCProfession.Merchant  => 4,  // Sivaro
            _ => 0,
        };
    }

    private void SpawnOneNPC(NPCManager.NPCData npc)
    {
        if (npc == null || string.IsNullOrEmpty(npc.Id)) return;
        if (_npcObjects.ContainsKey(npc.Id)) return;

        var root = new GameObject($"NPC_{npc.Id}");
        root.transform.SetParent(_npcRoot, false);
        // Convert 3D XZ position to 2D XY
        root.transform.position = new Vector3(npc.WorldPosition.x, npc.WorldPosition.z, 0f);

        int variant = GetNPCCharacterVariant(npc);
        var sr = root.AddComponent<SpriteRenderer>();

        // Load idle sprite
        var idle = LoadIsoSprite($"Characters/Male_{variant}_Idle0");
        if (idle != null) sr.sprite = idle;
        sr.sortingOrder = Mathf.RoundToInt(-root.transform.position.y * 100f);

        // Load run frames for animation
        var runFrames = new Sprite[10];
        for (int i = 0; i < 10; i++)
            runFrames[i] = LoadIsoSprite($"Characters/Male_{variant}_Run{i}");

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignSpriteRenderer(sr);
        bb.SetRunFrames(idle, runFrames);

        var routine = root.AddComponent<NPCDailyRoutine>();
        routine.NpcId = npc.Id;

        BuildInteractPrompt(root, npc.Name);

        _npcObjects[npc.Id] = root;
    }

    private void BuildInteractPrompt(GameObject npcRoot, string npcName)
    {
        var promptGO = new GameObject("InteractPrompt");
        promptGO.transform.SetParent(npcRoot.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 3.5f, 0f); // Above the sprite

        var canvas = promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 50f);
        rt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        // Semi-transparent background
        var bgGO = new GameObject("PromptBG");
        bgGO.transform.SetParent(promptGO.transform, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.12f, 0.75f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(-10f, -4f);
        bgRT.offsetMax = new Vector2(10f, 4f);

        // Border accent
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

        canvas.gameObject.SetActive(false);
    }

    // ---------------------------------------------------------------
    //  BUILDINGS — isometric sprite castle
    // ---------------------------------------------------------------

    private void SpawnBuildings()
    {
        var buildRoot = new GameObject("Buildings");
        buildRoot.transform.position = Vector3.zero;

        // Castle walls — place stone wall sprites around the perimeter
        float wallSpacing = 1.8f;
        float castleHalf = 7f;

        // South wall (left and right of gate)
        for (float x = -castleHalf; x < -1f; x += wallSpacing)
            PlaceSprite("Buildings/stoneWall_S", new Vector3(x, -castleHalf, 0f),
                buildRoot.transform, "WallS");
        for (float x = 1f; x <= castleHalf; x += wallSpacing)
            PlaceSprite("Buildings/stoneWall_S", new Vector3(x, -castleHalf, 0f),
                buildRoot.transform, "WallS");

        // Gate opening in south wall
        PlaceSprite("Buildings/stoneWallGateOpen_S", new Vector3(0f, -castleHalf, 0f),
            buildRoot.transform, "CastleGate");

        // North wall
        for (float x = -castleHalf; x <= castleHalf; x += wallSpacing)
            PlaceSprite("Buildings/stoneWall_S", new Vector3(x, castleHalf, 0f),
                buildRoot.transform, "WallN");

        // West wall
        for (float y = -castleHalf + wallSpacing; y < castleHalf; y += wallSpacing)
            PlaceSprite("Buildings/stoneWall_S", new Vector3(-castleHalf, y, 0f),
                buildRoot.transform, "WallW");

        // East wall
        for (float y = -castleHalf + wallSpacing; y < castleHalf; y += wallSpacing)
            PlaceSprite("Buildings/stoneWall_S", new Vector3(castleHalf, y, 0f),
                buildRoot.transform, "WallE");

        // Corner towers (using stoneWallColumn)
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(-castleHalf, -castleHalf, 0f),
            buildRoot.transform, "TowerSW", 10);
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(castleHalf, -castleHalf, 0f),
            buildRoot.transform, "TowerSE", 10);
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(-castleHalf, castleHalf, 0f),
            buildRoot.transform, "TowerNW", 10);
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(castleHalf, castleHalf, 0f),
            buildRoot.transform, "TowerNE", 10);

        // Central keep — stacked wall structure
        PlaceSprite("Buildings/stoneWallStructure_S", new Vector3(0f, 2f, 0f),
            buildRoot.transform, "Keep", 5);
        PlaceSprite("Buildings/stoneWallTop_S", new Vector3(0f, 4f, 0f),
            buildRoot.transform, "KeepTop", 5);

        // Windmill area (west side) — wood wall building
        PlaceSprite("Buildings/woodWall_S", new Vector3(-4f, -1f, 0f),
            buildRoot.transform, "WindmillBase");
        PlaceSprite("Buildings/roof_S", new Vector3(-4f, 1f, 0f),
            buildRoot.transform, "WindmillRoof", 5);
        PlaceSprite("Buildings/chimneyTop_S", new Vector3(-3.5f, 2.5f, 0f),
            buildRoot.transform, "WindmillChimney", 6);

        // Market area (east side) — wood wall with open door
        PlaceSprite("Buildings/woodWallDoorOpen_S", new Vector3(4f, -1f, 0f),
            buildRoot.transform, "MarketStall");
        PlaceSprite("Buildings/roofSingle_S", new Vector3(4f, 1f, 0f),
            buildRoot.transform, "MarketRoof", 5);

        // Barrels near market
        PlaceSprite("Props/barrels_S", new Vector3(5.5f, -1.5f, 0f),
            buildRoot.transform, "MarketBarrels");

        // Window building (north side)
        PlaceSprite("Buildings/stoneWallWindow_S", new Vector3(-3f, 4f, 0f),
            buildRoot.transform, "NorthBuilding");
        PlaceSprite("Buildings/stoneWallWindow_S", new Vector3(3f, 4f, 0f),
            buildRoot.transform, "NorthBuilding2");
    }

    // ---------------------------------------------------------------
    //  ENVIRONMENT — trees, rocks, props
    // ---------------------------------------------------------------

    private void SpawnEnvironment()
    {
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;

        System.Random rng = new System.Random(42);
        float castleHalf = 7f;
        float outerStart = castleHalf + 2f;
        float outerEnd = castleHalf + 8f;

        // Trees outside the castle walls
        for (int i = 0; i < 30; i++)
        {
            float angle = (float)(rng.NextDouble() * 360.0);
            float dist = outerStart + (float)(rng.NextDouble() * (outerEnd - outerStart));
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * dist;
            float y = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;

            string[] treePaths = { "Nature/treePineLarge_S", "Nature/treePineSmall_S", "Nature/treePineHuge_S" };
            string treePath = treePaths[rng.Next(treePaths.Length)];
            PlaceSprite(treePath, new Vector3(x, y, 0f), envRoot.transform, $"Tree_{i}");
        }

        // Dead trees (sparse)
        for (int i = 0; i < 5; i++)
        {
            float angle = (float)(rng.NextDouble() * 360.0);
            float dist = outerStart + (float)(rng.NextDouble() * (outerEnd - outerStart));
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * dist;
            float y = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;
            PlaceSprite("Nature/treeDeadLarge_S", new Vector3(x, y, 0f),
                envRoot.transform, $"DeadTree_{i}");
        }

        // Courtyard trees (small, near corners)
        PlaceSprite("Nature/treePineSmall_S", new Vector3(-5f, 4f, 0f), envRoot.transform, "CourtyardTree1");
        PlaceSprite("Nature/treePineSmall_S", new Vector3(5f, 4f, 0f), envRoot.transform, "CourtyardTree2");
        PlaceSprite("Nature/treePineSmall_S", new Vector3(-5f, -4f, 0f), envRoot.transform, "CourtyardTree3");
        PlaceSprite("Nature/treePineSmall_S", new Vector3(5f, -4f, 0f), envRoot.transform, "CourtyardTree4");

        // Rocks outside walls
        PlaceSprite("Ground/grassStoneLarge_S", new Vector3(-10f, -10f, 0f), envRoot.transform, "Rock1");
        PlaceSprite("Ground/grassStoneSmall_S", new Vector3(10f, -10f, 0f), envRoot.transform, "Rock2");
        PlaceSprite("Ground/grassStoneLarge_S", new Vector3(-10f, 10f, 0f), envRoot.transform, "Rock3");
        PlaceSprite("Ground/grassStoneSmall_S", new Vector3(10f, 10f, 0f), envRoot.transform, "Rock4");

        // Props inside courtyard
        PlaceSprite("Props/barrel_S", new Vector3(-2f, -5f, 0f), envRoot.transform, "Barrel1");
        PlaceSprite("Props/chestClosed_S", new Vector3(2f, -5f, 0f), envRoot.transform, "Chest1");
        PlaceSprite("Props/tableRoundChairs_S", new Vector3(-2f, 0f, 0f), envRoot.transform, "Table1");
        PlaceSprite("Props/hay_S", new Vector3(-5f, -2f, 0f), envRoot.transform, "Hay1");
        PlaceSprite("Props/sack_S", new Vector3(5f, -2f, 0f), envRoot.transform, "Sack1");
        PlaceSprite("Props/woodenCrate_S", new Vector3(3f, -4f, 0f), envRoot.transform, "Crate1");
        PlaceSprite("Props/corn_S", new Vector3(-5f, 1f, 0f), envRoot.transform, "Crops1");
        PlaceSprite("Props/cornDouble_S", new Vector3(-4.5f, 1.5f, 0f), envRoot.transform, "Crops2");

        // Ground detail: tree stumps scattered
        PlaceSprite("Ground/grassTreeStump_S", new Vector3(8f, 0f, 0f), envRoot.transform, "Stump1");
        PlaceSprite("Ground/grassTreeStump_S", new Vector3(-8f, -3f, 0f), envRoot.transform, "Stump2");
    }
}
