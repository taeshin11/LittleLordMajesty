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
    // Grass diamond is 256x144px at the bottom of the 256x512 image.
    // Diamond width = 2.0 WU, diamond height = 1.125 WU.
    // Slightly reduced spacing so tiles overlap and eliminate gaps.
    private const float TileSpacingX = 1.95f;
    private const float TileSpacingY = 1.05f;

    // Character scale — people should be smaller than buildings
    private const float CharacterScale = 0.5f;

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
        _roamingCam.orthographicSize = 4.5f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.33f, 0.30f, 0.11f); // Match grass tile edge color
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
        follow.SetOrthoSize(4.5f);
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

        // Tile a large grid of grass sprites so no background peeks through.
        int halfExtent = 14; // generous — covers well beyond camera view
        for (int row = -halfExtent; row <= halfExtent; row++)
        {
            for (int col = -halfExtent; col <= halfExtent; col++)
            {
                float x = col * TileSpacingX;
                float y = row * TileSpacingY;
                // Stagger odd rows by half a tile width for isometric layout
                if (Mathf.Abs(row) % 2 == 1)
                    x += TileSpacingX * 0.5f;

                var go = PlaceSprite("Ground/grass_S", new Vector3(x, y, 0f),
                    groundRoot.transform, $"Grass_{row}_{col}", -10000);
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    sr.sortingOrder = -10000;
                }
            }
        }

        // Path leading from gate to village center
        for (int i = 0; i < 6; i++)
        {
            float py = -4f + i * TileSpacingY;
            string pathTile = (i == 3) ? "Ground/grassPathCrossing_S" : "Ground/grassPathStraight_S";
            PlaceSprite(pathTile, new Vector3(0f, py, 0f),
                groundRoot.transform, $"Path_{i}", -9999);
        }

        // East-west path branching from the crossing
        for (int i = 1; i <= 3; i++)
        {
            PlaceSprite("Ground/grassPathStraight_S", new Vector3(i * TileSpacingX, -4f + 3 * TileSpacingY, 0f),
                groundRoot.transform, $"PathE_{i}", -9999);
            PlaceSprite("Ground/grassPathStraight_S", new Vector3(-i * TileSpacingX, -4f + 3 * TileSpacingY, 0f),
                groundRoot.transform, $"PathW_{i}", -9999);
        }
    }

    // ---------------------------------------------------------------
    //  2D PLAYER — sprite with run animation
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;
        _player.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

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
        root.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

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
        promptGO.transform.localPosition = new Vector3(0f, 5f, 0f); // Above scaled sprite (localScale=0.5 so need larger local offset)

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

    /// <summary>
    /// Helper to compose a building from wall + roof sprites at a given anchor.
    /// Walls are placed at (x,y), roof on top with slight offsets for depth.
    /// </summary>
    private void PlaceBuilding(string wallSprite, string roofSprite,
        Vector3 pos, Transform parent, string label, float roofOffsetY = 0.55f)
    {
        PlaceSprite($"Buildings/{wallSprite}", pos, parent, $"{label}_Wall");
        if (roofSprite != null)
            PlaceSprite($"Buildings/{roofSprite}", pos + new Vector3(0f, roofOffsetY, 0f),
                parent, $"{label}_Roof", 1);
    }

    private void SpawnBuildings()
    {
        var buildRoot = new GameObject("Buildings");
        buildRoot.transform.position = Vector3.zero;

        // ---- CENTRAL KEEP (stone, prominent) ----
        // Two-wall wide structure with pointed roof
        PlaceSprite("Buildings/stoneWallStructure_S", new Vector3(-0.5f, 2f, 0f),
            buildRoot.transform, "Keep_L");
        PlaceSprite("Buildings/stoneWallWindow_S", new Vector3(0.5f, 2f, 0f),
            buildRoot.transform, "Keep_R");
        PlaceSprite("Buildings/roof_S", new Vector3(-0.5f, 2.55f, 0f),
            buildRoot.transform, "Keep_Roof_L", 1);
        PlaceSprite("Buildings/roof_S", new Vector3(0.5f, 2.55f, 0f),
            buildRoot.transform, "Keep_Roof_R", 1);
        PlaceSprite("Buildings/stoneWallTop_S", new Vector3(0f, 3.1f, 0f),
            buildRoot.transform, "Keep_Top", 2);
        // Chimney on keep
        PlaceSprite("Buildings/chimneyTop_S", new Vector3(0.7f, 3.3f, 0f),
            buildRoot.transform, "Keep_Chimney", 3);

        // ---- SOUTH GATE ----
        PlaceSprite("Buildings/stoneWallGateOpen_S", new Vector3(0f, -4f, 0f),
            buildRoot.transform, "Gate");
        // Gate flanking walls
        PlaceSprite("Buildings/stoneWall_S", new Vector3(-1.5f, -4f, 0f),
            buildRoot.transform, "GateWall_L");
        PlaceSprite("Buildings/stoneWall_S", new Vector3(1.5f, -4f, 0f),
            buildRoot.transform, "GateWall_R");
        // Gate columns
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(-2.5f, -4f, 0f),
            buildRoot.transform, "GateCol_L", 2);
        PlaceSprite("Buildings/stoneWallColumn_S", new Vector3(2.5f, -4f, 0f),
            buildRoot.transform, "GateCol_R", 2);

        // ---- WEST: Barracks / wooden buildings ----
        // Barracks building 1
        PlaceBuilding("woodWall_S", "roofSingle_S",
            new Vector3(-4f, 0f, 0f), buildRoot.transform, "Barracks1");
        PlaceBuilding("woodWallDoorOpen_S", "roofSingle_S",
            new Vector3(-4f, -1.1f, 0f), buildRoot.transform, "Barracks2");
        // Wooden fence enclosure
        PlaceSprite("Buildings/fenceLow_S", new Vector3(-5.5f, -0.5f, 0f),
            buildRoot.transform, "BarracksFence1");
        PlaceSprite("Buildings/fenceLow_S", new Vector3(-5.5f, 0.5f, 0f),
            buildRoot.transform, "BarracksFence2");

        // ---- EAST: Market / workshop ----
        PlaceBuilding("woodWallWindow_S", "roofSingle_S",
            new Vector3(4f, 0f, 0f), buildRoot.transform, "Market1");
        PlaceBuilding("woodWallDoorOpen_S", "roofSingleWall_S",
            new Vector3(4f, -1.1f, 0f), buildRoot.transform, "MarketShop");
        // Market props cluster
        PlaceSprite("Props/barrels_S", new Vector3(5.2f, -0.5f, 0f),
            buildRoot.transform, "MarketBarrels");
        PlaceSprite("Props/woodenCrates_S", new Vector3(5.2f, 0.5f, 0f),
            buildRoot.transform, "MarketCrates");
        PlaceSprite("Props/tableRoundChairs_S", new Vector3(3f, -1.8f, 0f),
            buildRoot.transform, "MarketTable");

        // ---- NORTH: Farm area ----
        // Farmhouse
        PlaceBuilding("stoneWallDoor_S", "roof_S",
            new Vector3(-3f, 4f, 0f), buildRoot.transform, "Farmhouse");
        PlaceSprite("Buildings/chimneyBase_S", new Vector3(-2.5f, 4.8f, 0f),
            buildRoot.transform, "FarmChimney", 3);
        // Fenced crop area
        PlaceSprite("Buildings/fenceLow_S", new Vector3(-1.5f, 3.5f, 0f),
            buildRoot.transform, "FarmFence1");
        PlaceSprite("Buildings/fenceLow_S", new Vector3(-0.5f, 3.5f, 0f),
            buildRoot.transform, "FarmFence2");
        PlaceSprite("Buildings/fenceLow_S", new Vector3(0.5f, 3.5f, 0f),
            buildRoot.transform, "FarmFence3");
        PlaceSprite("Props/corn_S", new Vector3(-1f, 4f, 0f),
            buildRoot.transform, "Crops1");
        PlaceSprite("Props/cornDouble_S", new Vector3(0f, 4f, 0f),
            buildRoot.transform, "Crops2");
        PlaceSprite("Props/corn_S", new Vector3(0.8f, 4f, 0f),
            buildRoot.transform, "Crops3");
        PlaceSprite("Props/hay_S", new Vector3(-1.5f, 4.5f, 0f),
            buildRoot.transform, "FarmHay");
        PlaceSprite("Props/hayBales_S", new Vector3(1f, 4.5f, 0f),
            buildRoot.transform, "FarmHayBales");

        // North-east storage building
        PlaceBuilding("stoneWallWindow_S", "roofCorner_S",
            new Vector3(3f, 4f, 0f), buildRoot.transform, "Storage");

        // ---- PERIMETER ACCENT WALLS (compact, not full fortress) ----
        // Just a few stone walls to suggest village boundary, not a full rectangle
        // West boundary hints
        PlaceSprite("Buildings/stoneWallHalf_S", new Vector3(-6f, -2f, 0f),
            buildRoot.transform, "BoundW1");
        PlaceSprite("Buildings/stoneWallHalf_S", new Vector3(-6f, 2f, 0f),
            buildRoot.transform, "BoundW2");
        // East boundary hints
        PlaceSprite("Buildings/stoneWallHalf_S", new Vector3(6f, -2f, 0f),
            buildRoot.transform, "BoundE1");
        PlaceSprite("Buildings/stoneWallHalf_S", new Vector3(6f, 2f, 0f),
            buildRoot.transform, "BoundE2");
    }

    // ---------------------------------------------------------------
    //  ENVIRONMENT — trees, rocks, props
    // ---------------------------------------------------------------

    private void SpawnEnvironment()
    {
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;

        // ---- PERIMETER TREES (ring around village, radius ~8-12) ----
        System.Random rng = new System.Random(42);
        float innerRing = 7.5f;
        float outerRing = 12f;
        for (int i = 0; i < 40; i++)
        {
            float angle = (float)(rng.NextDouble() * 360.0);
            float dist = innerRing + (float)(rng.NextDouble() * (outerRing - innerRing));
            float x = Mathf.Cos(angle * Mathf.Deg2Rad) * dist;
            float y = Mathf.Sin(angle * Mathf.Deg2Rad) * dist;

            // Skip the south (gate approach) — leave an opening
            if (y < -4f && Mathf.Abs(x) < 3f) continue;

            string[] treePaths = { "Nature/treePineLarge_S", "Nature/treePineSmall_S", "Nature/treePineHuge_S" };
            PlaceSprite(treePaths[rng.Next(treePaths.Length)], new Vector3(x, y, 0f),
                envRoot.transform, $"Tree_{i}");
        }

        // A few dead trees for variety
        PlaceSprite("Nature/treeDeadLarge_S", new Vector3(-9f, -6f, 0f), envRoot.transform, "DeadTree1");
        PlaceSprite("Nature/treeDeadSmall_S", new Vector3(10f, 3f, 0f), envRoot.transform, "DeadTree2");

        // ---- VILLAGE INTERIOR TREES (decorative, small) ----
        PlaceSprite("Nature/treePineSmall_S", new Vector3(-2f, 1f, 0f), envRoot.transform, "VillageTree1");
        PlaceSprite("Nature/treePineSmall_S", new Vector3(2f, 1f, 0f), envRoot.transform, "VillageTree2");

        // ---- ROCKS (scattered near perimeter) ----
        PlaceSprite("Ground/grassStoneLarge_S", new Vector3(-7f, -5f, 0f), envRoot.transform, "Rock1");
        PlaceSprite("Ground/grassStoneSmall_S", new Vector3(7f, -5f, 0f), envRoot.transform, "Rock2");
        PlaceSprite("Ground/grassStoneLarge_S", new Vector3(-7f, 5f, 0f), envRoot.transform, "Rock3");
        PlaceSprite("Ground/grassStoneSmall_S", new Vector3(8f, 5f, 0f), envRoot.transform, "Rock4");

        // ---- COURTYARD PROPS ----
        // Near the gate entrance
        PlaceSprite("Props/barrel_S", new Vector3(-1.5f, -3f, 0f), envRoot.transform, "Barrel1");
        PlaceSprite("Props/sack_S", new Vector3(1.5f, -3f, 0f), envRoot.transform, "Sack1");

        // Central village square — well/gathering area
        PlaceSprite("Props/chestClosed_S", new Vector3(1.5f, 0.5f, 0f), envRoot.transform, "Chest1");
        PlaceSprite("Props/woodenPile_S", new Vector3(-1.5f, -1.5f, 0f), envRoot.transform, "WoodPile1");

        // Near barracks (west)
        PlaceSprite("Props/woodenCrate_S", new Vector3(-5f, -1.5f, 0f), envRoot.transform, "Crate1");
        PlaceSprite("Props/sacksCrate_S", new Vector3(-5f, 0.8f, 0f), envRoot.transform, "SacksCrate1");

        // Near market (east)
        PlaceSprite("Props/barrelsStacked_S", new Vector3(5.5f, -1.5f, 0f), envRoot.transform, "BarrelsStacked1");
        PlaceSprite("Props/chair_S", new Vector3(3.5f, -2.5f, 0f), envRoot.transform, "Chair1");

        // Stumps
        PlaceSprite("Ground/grassTreeStump_S", new Vector3(6f, 3f, 0f), envRoot.transform, "Stump1");
        PlaceSprite("Nature/grassTreeStumpAxe_S", new Vector3(-6f, 3f, 0f), envRoot.transform, "Stump2");
    }
}
