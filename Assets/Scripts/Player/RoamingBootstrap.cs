using UnityEngine;
using TMPro;

/// <summary>
/// 2D Top-down roaming bootstrap — builds the world from Kenney Tiny Town
/// + Tiny Dungeon sprite packs (16x16 pixel art).
///
/// All visuals are SpriteRenderers on the XY plane at z=0.
/// Camera is orthographic at z=-10. Sprites are 16x16 at PPU=16
/// giving each tile a 1x1 world-unit footprint.
/// Sort order is based on Y position (lower Y = drawn in front).
///
/// Village layout: 20x15 grid centered on (0,0).
///   Row 0-2:   Trees along north edge
///   Row 3-5:   Castle keep (stone walls)
///   Row 6:     Open courtyard + path
///   Row 7-8:   Houses (west + east) + props between
///   Row 9:     Path + well + market stalls
///   Row 10-11: Gate entrance (south)
///   Row 12-14: Trees along south edge + path leading out
///
/// BRIGHT, CUTE, ZELDA-LIKE — no dark dungeon aesthetic.
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector3 _playerSpawn = new Vector3(10f, 5f, 0f);
    [SerializeField] private float _playerWalkSpeed = 3.5f;

    private bool _spawned;
    private bool _subscribed;
    private GameObject _player;
    private readonly System.Collections.Generic.Dictionary<string, GameObject> _npcObjects = new();
    private Transform _npcRoot;
    private Camera _roamingCam;

    // Top-down tile layout: each tile = 1x1 world unit (16px at PPU=16)
    private const float TileSize = 1f;

    // Character scale — slightly larger than tiles for visibility
    private const float CharacterScale = 1.3f;

    // Grid dimensions (village is 20 wide x 15 tall, origin at bottom-left)
    private const int GridW = 20;
    private const int GridH = 15;

    // Bright grass-green background color (matches Kenney Tiny Town grass)
    private static readonly Color GrassGreen = new Color(0.42f, 0.75f, 0.27f);

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
        BuildVillage();
        BuildPlayer();
        WireCamera();
        SpawnNPCs();
        _spawned = true;
        Debug.Log("[RoamingBootstrap] 2D top-down village built (Kenney Tiny Town + Tiny Dungeon 16x16)");
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
    //  2D CAMERA — orthographic, top-down
    // ---------------------------------------------------------------

    private void BuildRoamingCamera()
    {
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.orthographic = true;
        _roamingCam.orthographicSize = 5.0f; // see ~15 units vertically — whole village
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = GrassGreen;
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 100f;
        _roamingCam.depth = 10;
        camGO.transform.position = new Vector3(10f, 7f, -10f);
        camGO.transform.rotation = Quaternion.identity;
        try { camGO.tag = "MainCamera"; } catch { }

        _roamingCam.transparencySortMode = TransparencySortMode.CustomAxis;
        _roamingCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        var follow = camGO.AddComponent<FollowCamera>();
        follow.SetOrthoSize(5.0f);
    }

    // ---------------------------------------------------------------
    //  SPRITE HELPERS
    // ---------------------------------------------------------------

    private static Sprite LoadSprite(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            Debug.LogWarning($"[RoamingBootstrap] Sprite not found: {resourcePath}");
        return sprite;
    }

    private static GameObject PlaceSprite(string resourcePath, Vector3 position,
        Transform parent, string name = null, int sortingOrder = 0)
    {
        var sprite = LoadSprite(resourcePath);
        if (sprite == null) return null;

        var go = new GameObject(name ?? sprite.name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = sortingOrder;

        return go;
    }

    private static GameObject PlaceSortedSprite(string resourcePath, Vector3 position,
        Transform parent, string name = null, int sortingOffset = 0)
    {
        var go = PlaceSprite(resourcePath, position, parent, name,
            Mathf.RoundToInt(-position.y * 100f) + sortingOffset);
        return go;
    }

    /// <summary>Place a tile by TinyTown index at grid position.</summary>
    private static GameObject PlaceTile(int tileIndex, int gridX, int gridY,
        Transform parent, int sortingOrder = 0, string name = null)
    {
        string path = TinyTileset.TT(tileIndex);
        return PlaceSprite(path, new Vector3(gridX, gridY, 0f), parent,
            name ?? $"tile_{tileIndex}_{gridX}_{gridY}", sortingOrder);
    }

    /// <summary>Place a sorted tile (Y-based sorting) by TinyTown index.</summary>
    private static GameObject PlaceSortedTile(int tileIndex, int gridX, int gridY,
        Transform parent, int sortingOffset = 0, string name = null)
    {
        string path = TinyTileset.TT(tileIndex);
        return PlaceSortedSprite(path, new Vector3(gridX, gridY, 0f), parent,
            name ?? $"tile_{tileIndex}_{gridX}_{gridY}", sortingOffset);
    }

    // ---------------------------------------------------------------
    //  VILLAGE LAYOUT — single method builds everything
    // ---------------------------------------------------------------
    //
    //  Grid: 20 wide (x: 0..19), 15 tall (y: 0..14).
    //  Y=14 is top (north), Y=0 is bottom (south).
    //
    //  Row 12-14: Trees (north edge)
    //  Row 9-11:  Castle keep (stone walls, center)
    //  Row 8:     Open courtyard + path
    //  Row 6-7:   House 1 (west), House 2 (east), props between
    //  Row 5:     Path + well + market stalls
    //  Row 3-4:   Gate entrance (south)
    //  Row 0-2:   Trees (south edge) + path leading out

    private void BuildVillage()
    {
        // --- Ground layer ---
        var groundRoot = new GameObject("Ground");
        groundRoot.transform.position = Vector3.zero;
        FillGround(groundRoot.transform);

        // --- Paths ---
        var pathRoot = new GameObject("Paths");
        pathRoot.transform.position = Vector3.zero;
        BuildPaths(pathRoot.transform);

        // --- Castle ---
        var castleRoot = new GameObject("Castle");
        castleRoot.transform.position = Vector3.zero;
        BuildCastle(castleRoot.transform);

        // --- Houses ---
        var houseRoot = new GameObject("Houses");
        houseRoot.transform.position = Vector3.zero;
        BuildHouses(houseRoot.transform);

        // --- Gate ---
        var gateRoot = new GameObject("Gate");
        gateRoot.transform.position = Vector3.zero;
        BuildGate(gateRoot.transform);

        // --- Props ---
        var propRoot = new GameObject("Props");
        propRoot.transform.position = Vector3.zero;
        BuildProps(propRoot.transform);

        // --- Trees & nature ---
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;
        BuildEnvironment(envRoot.transform);
    }

    // ---------- GROUND ----------

    private void FillGround(Transform parent)
    {
        // Use only tile 0 (plain grass) — no sparkle/variant tiles
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                PlaceTile(0, x, y, parent, -1000, $"Grass_{x}_{y}");
            }
        }
    }

    // ---------- PATHS ----------
    // Stone paths (tile 40=light stone, 42=cobblestone) connecting buildings.

    private void BuildPaths(Transform parent)
    {
        // Main north-south spine: x=10, from y=1 up to y=11 (south gate to castle)
        for (int y = 1; y <= 11; y++)
            PlaceTile(40, 10, y, parent, -999, $"PathNS_{y}");

        // East-west path at y=8 connecting houses to center
        for (int x = 4; x <= 16; x++)
        {
            if (x == 10) continue; // already placed by NS path
            PlaceTile(40, x, 8, parent, -999, $"PathEW_{x}");
        }

        // Path south exit: x=10, y=0
        PlaceTile(40, 10, 0, parent, -999, "PathOut");
    }

    // ---------- CASTLE ----------
    // 4-wide x 3-tall stone castle centered at x=8..11, y=9..11

    private void BuildCastle(Transform parent)
    {
        var castleParent = new GameObject("CastleKeep");
        castleParent.transform.SetParent(parent, false);
        castleParent.transform.position = Vector3.zero;

        // Top row (y=11): tiles 48, 49, 49, 50
        PlaceSortedTile(48, 8, 11, castleParent.transform, 0, "Castle_TL");
        PlaceSortedTile(49, 9, 11, castleParent.transform, 0, "Castle_TC1");
        PlaceSortedTile(49, 10, 11, castleParent.transform, 0, "Castle_TC2");
        PlaceSortedTile(50, 11, 11, castleParent.transform, 0, "Castle_TR");

        // Middle row (y=10): tiles 60, -, -, 63 (walls on sides, empty in middle)
        PlaceSortedTile(60, 8, 10, castleParent.transform, 0, "Castle_ML");
        PlaceSortedTile(63, 11, 10, castleParent.transform, 0, "Castle_MR");

        // Bottom row (y=9): tiles 61, 62, 62, 63
        PlaceSortedTile(61, 8, 9, castleParent.transform, 0, "Castle_BL");
        PlaceSortedTile(62, 9, 9, castleParent.transform, 0, "Castle_BC1");
        PlaceSortedTile(62, 10, 9, castleParent.transform, 0, "Castle_BC2");
        PlaceSortedTile(63, 11, 9, castleParent.transform, 0, "Castle_BR");

        // Top wall collider (y=11, 4 tiles wide)
        var topWallCol = castleParent.AddComponent<BoxCollider2D>();
        topWallCol.size = new Vector2(4f, 1f);
        topWallCol.offset = new Vector2(9.5f, 11f);

        // Left wall collider (y=9-10, 1 tile wide, 2 tall)
        var goLeftWall = new GameObject("CastleLeftWallCol");
        goLeftWall.transform.SetParent(castleParent.transform, false);
        var leftWallCol = goLeftWall.AddComponent<BoxCollider2D>();
        leftWallCol.size = new Vector2(1f, 2f);
        leftWallCol.offset = new Vector2(8f, 9.5f);

        // Right wall collider (y=9-10, 1 tile wide, 2 tall)
        var goRightWall = new GameObject("CastleRightWallCol");
        goRightWall.transform.SetParent(castleParent.transform, false);
        var rightWallCol = goRightWall.AddComponent<BoxCollider2D>();
        rightWallCol.size = new Vector2(1f, 2f);
        rightWallCol.offset = new Vector2(11f, 9.5f);

        // Bottom wall collider (y=9, 4 tiles wide)
        var goBotWall = new GameObject("CastleBotWallCol");
        goBotWall.transform.SetParent(castleParent.transform, false);
        var botWallCol = goBotWall.AddComponent<BoxCollider2D>();
        botWallCol.size = new Vector2(4f, 1f);
        botWallCol.offset = new Vector2(9.5f, 9f);
    }

    // ---------- HOUSES ----------
    // House = 3 wide x 2 tall: roof row on top, wall row on bottom.

    private void PlaceHouse(int gridX, int gridY, Transform parent, string tag,
        bool grayStyle = false)
    {
        // gridX, gridY = top-left of roof row (top row)
        // House is 3 wide x 2 tall: roof at gridY, walls at gridY-1

        var houseParent = new GameObject(tag);
        houseParent.transform.SetParent(parent, false);
        houseParent.transform.position = Vector3.zero;

        int roofL = grayStyle ? 88 : 52;
        int roofC = grayStyle ? 89 : 53;
        int roofR = grayStyle ? 90 : 54;
        int wallL = grayStyle ? 76 : 72;
        int wallC = grayStyle ? 77 : 73; // door
        int wallR = grayStyle ? 78 : 74;

        // Dirt foundation under the house (like official Kenney sample)
        PlaceTile(25, gridX, gridY, houseParent.transform, -999, $"{tag}_DirtT1");
        PlaceTile(25, gridX + 1, gridY, houseParent.transform, -999, $"{tag}_DirtT2");
        PlaceTile(25, gridX + 2, gridY, houseParent.transform, -999, $"{tag}_DirtT3");
        PlaceTile(37, gridX, gridY - 1, houseParent.transform, -999, $"{tag}_DirtB1");
        PlaceTile(37, gridX + 1, gridY - 1, houseParent.transform, -999, $"{tag}_DirtB2");
        PlaceTile(37, gridX + 2, gridY - 1, houseParent.transform, -999, $"{tag}_DirtB3");

        // Roof row on top, wall row below
        PlaceSortedTile(roofL, gridX, gridY, houseParent.transform, 10, $"{tag}_RoofL");
        PlaceSortedTile(roofC, gridX + 1, gridY, houseParent.transform, 10, $"{tag}_RoofC");
        PlaceSortedTile(roofR, gridX + 2, gridY, houseParent.transform, 10, $"{tag}_RoofR");
        PlaceSortedTile(wallL, gridX, gridY - 1, houseParent.transform, 11, $"{tag}_WallL");
        PlaceSortedTile(wallC, gridX + 1, gridY - 1, houseParent.transform, 11, $"{tag}_WallC");
        PlaceSortedTile(wallR, gridX + 2, gridY - 1, houseParent.transform, 11, $"{tag}_WallR");

        // Add collider covering the 3x2 house area
        var col = houseParent.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3f, 2f);
        col.offset = new Vector2(gridX + 1f, gridY - 0.5f);
    }

    private void BuildHouses(Transform parent)
    {
        // Red house 1 (west): roof at y=8, walls at y=7 — x=3..5
        PlaceHouse(3, 8, parent, "House1", false);

        // Gray house (east): roof at y=8, walls at y=7 — x=14..16
        PlaceHouse(14, 8, parent, "House2", true);

        // Red house 2 (near castle): roof at y=12, walls at y=11 — x=14..16
        PlaceHouse(14, 12, parent, "House3", false);
    }

    // ---------- GATE ----------
    // South gate: 2 wide x 2 tall at x=9..10, y=3..4

    private void BuildGate(Transform parent)
    {
        // Simplified gate: 2 stone wall pillars (tile 49) with a gap between for entrance
        // Left pillar at x=9, right pillar at x=10 — gap at x=10 (NS path)
        // Actually: pillar at x=8, y=3-4 and pillar at x=11, y=3-4, gap at x=9-10

        // Left pillar (2 tall)
        var leftPillar = new GameObject("GatePillarL");
        leftPillar.transform.SetParent(parent, false);
        PlaceSortedTile(49, 8, 4, leftPillar.transform, 5, "Gate_LT");
        PlaceSortedTile(62, 8, 3, leftPillar.transform, 5, "Gate_LB");
        var colL = leftPillar.AddComponent<BoxCollider2D>();
        colL.size = new Vector2(1f, 2f);
        colL.offset = new Vector2(8f, 3.5f);

        // Right pillar (2 tall)
        var rightPillar = new GameObject("GatePillarR");
        rightPillar.transform.SetParent(parent, false);
        PlaceSortedTile(49, 11, 4, rightPillar.transform, 5, "Gate_RT");
        PlaceSortedTile(62, 11, 3, rightPillar.transform, 5, "Gate_RB");
        var colR = rightPillar.AddComponent<BoxCollider2D>();
        colR.size = new Vector2(1f, 2f);
        colR.offset = new Vector2(11f, 3.5f);

        // Gap at x=9-10 is open — player can walk through
    }

    // ---------- PROPS ----------

    private void BuildProps(Transform parent)
    {
        // Minimal props — only verified tiles that look good at game scale
        PlaceSortedTile(45, 5, 7, parent, 5, "Crate1"); // crate near house1
        PlaceSortedTile(45, 17, 7, parent, 5, "Crate2"); // crate near house2
    }

    // ---------- ENVIRONMENT — trees & nature ----------

    private void BuildEnvironment(Transform parent)
    {
        // North edge trees
        for (int x = 0; x < GridW; x += 2)
            PlaceTreeWithCollider(5, x, 14, parent, $"TreeN_{x}");

        // South edge trees (gap at x=9-10 for gate)
        for (int x = 0; x < GridW; x += 2)
        {
            if (x >= 9 && x <= 11) continue;
            PlaceTreeWithCollider(5, x, 0, parent, $"TreeS_{x}");
        }

        // West edge trees
        for (int y = 2; y <= 13; y += 2)
            PlaceTreeWithCollider(5, 0, y, parent, $"TreeW_{y}");

        // East edge trees
        for (int y = 2; y <= 13; y += 2)
            PlaceTreeWithCollider(5, 19, y, parent, $"TreeE_{y}");

        // A few interior trees
        PlaceTreeWithCollider(5, 3, 10, parent, "IntTree1");
        PlaceTreeWithCollider(5, 16, 5, parent, "IntTree2");
    }

    private void PlaceTreeWithCollider(int tileIndex, int gridX, int gridY,
        Transform parent, string name)
    {
        var go = PlaceSortedTile(tileIndex, gridX, gridY, parent, 0, name);
        if (go != null)
        {
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.3f;
        }
    }

    // ---------------------------------------------------------------
    //  PLAYER — Tiny Dungeon character tile
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;
        _player.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

        var sr = _player.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;

        // Load the knight character as player sprite
        var idle = LoadSprite(TinyTileset.TD_Knight);
        if (idle != null) sr.sprite = idle;

        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.ConfigureAtRuntime(_playerWalkSpeed, sr, idle, null);

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
    /// Returns the Tiny Dungeon character tile path for a given NPC profession.
    /// </summary>
    private static string GetNPCSpritePath(NPCManager.NPCData npc)
    {
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal    => TinyTileset.TD_Knight,      // armored knight
            NPCPersona.NPCProfession.Soldier   => TinyTileset.TD_RedHair,     // tough warrior
            NPCPersona.NPCProfession.Farmer    => TinyTileset.TD_BrownHair,   // simple farmer
            NPCPersona.NPCProfession.Merchant  => TinyTileset.TD_Archer,      // merchant
            NPCPersona.NPCProfession.Mage      => TinyTileset.TD_Wizard,      // purple hat wizard
            NPCPersona.NPCProfession.Priest    => TinyTileset.TD_Warrior,     // priest
            NPCPersona.NPCProfession.Scout     => TinyTileset.TD_DarkHair,   // scout
            NPCPersona.NPCProfession.Blacksmith => TinyTileset.TD_BrownHair, // blacksmith
            _ => TinyTileset.TD_Bard,
        };
    }

    /// <summary>
    /// Returns a default spawn position for an NPC based on profession,
    /// placing them near relevant buildings.
    /// </summary>
    private static Vector3 GetNPCDefaultPosition(NPCManager.NPCData npc)
    {
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal   => new Vector3(10f, 9f, 0f),   // Near castle
            NPCPersona.NPCProfession.Soldier  => new Vector3(8f, 8f, 0f),    // Courtyard
            NPCPersona.NPCProfession.Farmer   => new Vector3(6f, 5f, 0f),    // Near well
            NPCPersona.NPCProfession.Merchant => new Vector3(13f, 7f, 0f),   // Near House 2
            _ => new Vector3(10f, 6f, 0f),  // Center courtyard
        };
    }

    private void SpawnOneNPC(NPCManager.NPCData npc)
    {
        if (npc == null || string.IsNullOrEmpty(npc.Id)) return;
        if (_npcObjects.ContainsKey(npc.Id)) return;

        var root = new GameObject($"NPC_{npc.Id}");
        root.transform.SetParent(_npcRoot, false);

        // Use default position based on profession if WorldPosition is at origin
        Vector3 pos = (npc.WorldPosition.sqrMagnitude < 0.01f)
            ? GetNPCDefaultPosition(npc)
            : new Vector3(npc.WorldPosition.x, npc.WorldPosition.z, 0f);

        root.transform.position = pos;
        root.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

        var sr = root.AddComponent<SpriteRenderer>();
        string spritePath = GetNPCSpritePath(npc);
        var idle = LoadSprite(spritePath);
        if (idle != null) sr.sprite = idle;
        sr.sortingOrder = Mathf.RoundToInt(-root.transform.position.y * 100f);

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignSpriteRenderer(sr);
        bb.SetIdleSprite(idle);

        var routine = root.AddComponent<NPCDailyRoutine>();
        routine.NpcId = npc.Id;

        BuildInteractPrompt(root, npc.Name);

        _npcObjects[npc.Id] = root;
    }

    private void BuildInteractPrompt(GameObject npcRoot, string npcName)
    {
        var promptGO = new GameObject("InteractPrompt");
        promptGO.transform.SetParent(npcRoot.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var canvas = promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 50f);
        rt.localScale = new Vector3(0.008f, 0.008f, 0.008f);

        var bgGO = new GameObject("PromptBG");
        bgGO.transform.SetParent(promptGO.transform, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.95f, 0.9f, 0.7f, 0.85f);
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(-10f, -4f);
        bgRT.offsetMax = new Vector2(10f, 4f);

        var borderGO = new GameObject("PromptBorder");
        borderGO.transform.SetParent(bgGO.transform, false);
        var borderImg = borderGO.AddComponent<UnityEngine.UI.Image>();
        borderImg.color = new Color(0.6f, 0.4f, 0.2f, 0.9f);
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
        tmp.color = new Color(0.2f, 0.15f, 0.1f);
        tmp.richText = false;
        tmp.enableWordWrapping = false;
        var lblRT = tmp.rectTransform;
        lblRT.anchorMin = Vector2.zero;
        lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero;
        lblRT.offsetMax = Vector2.zero;

        var outline = labelGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.3f);
        outline.effectDistance = new Vector2(1f, -1f);

        var prompt = promptGO.AddComponent<InteractPromptUI>();
        prompt.SetupRuntime(canvas, tmp);

        canvas.gameObject.SetActive(false);
    }
}
