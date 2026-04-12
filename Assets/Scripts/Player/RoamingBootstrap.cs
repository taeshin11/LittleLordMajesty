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
/// BRIGHT, CUTE, ZELDA-LIKE — no dark dungeon aesthetic.
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector3 _playerSpawn = new Vector3(15f, 5f, 0f);
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

    // Grid dimensions
    private const int GridW = 30;
    private const int GridH = 30;

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
        BuildGround();
        BuildPaths();
        BuildCastle();
        BuildVillage();
        BuildFarm();
        BuildBarracks();
        BuildEnvironment();
        BuildPlayer();
        WireCamera();
        SpawnNPCs();
        _spawned = true;
        Debug.Log("[RoamingBootstrap] 2D top-down world built (Kenney Tiny Town + Tiny Dungeon 16x16)");
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
        _roamingCam.orthographicSize = 5f; // ~10 units vertically visible
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = GrassGreen;
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 100f;
        _roamingCam.depth = 10;
        camGO.transform.position = new Vector3(15f, 15f, -10f);
        camGO.transform.rotation = Quaternion.identity;
        try { camGO.tag = "MainCamera"; } catch { }

        _roamingCam.transparencySortMode = TransparencySortMode.CustomAxis;
        _roamingCam.transparencySortAxis = new Vector3(0f, 1f, 0f);

        var follow = camGO.AddComponent<FollowCamera>();
        follow.SetOrthoSize(5f);
    }

    // ---------------------------------------------------------------
    //  SPRITE HELPERS
    // ---------------------------------------------------------------

    /// <summary>
    /// Load a sprite from Resources (without .png extension).
    /// Supports both TinyTown and TinyDungeon paths.
    /// </summary>
    private static Sprite LoadSprite(string resourcePath)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
            Debug.LogWarning($"[RoamingBootstrap] Sprite not found: {resourcePath}");
        return sprite;
    }

    /// <summary>
    /// Create a GameObject with a SpriteRenderer at the given position.
    /// </summary>
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

    /// <summary>
    /// Place a sprite with Y-based sorting (for objects that overlap).
    /// </summary>
    private static GameObject PlaceSortedSprite(string resourcePath, Vector3 position,
        Transform parent, string name = null, int sortingOffset = 0)
    {
        var go = PlaceSprite(resourcePath, position, parent, name,
            Mathf.RoundToInt(-position.y * 100f) + sortingOffset);
        return go;
    }

    // ---------------------------------------------------------------
    //  GROUND — grass tile grid (30x30, top-down, no stagger)
    // ---------------------------------------------------------------

    private void BuildGround()
    {
        var groundRoot = new GameObject("Ground");
        groundRoot.transform.position = Vector3.zero;

        // Fill with grass tiles — alternate between two grass variants
        for (int y = 0; y < GridH; y++)
        {
            for (int x = 0; x < GridW; x++)
            {
                // Use different grass variants for visual interest
                string tile;
                int variant = (x + y * 3) % 5;
                switch (variant)
                {
                    case 0: tile = TinyTileset.TT_Grass; break;
                    case 1: tile = TinyTileset.TT_GrassAlt1; break;
                    case 2: tile = TinyTileset.TT_GrassAlt2; break;
                    case 3: tile = TinyTileset.TT_GrassFlower1; break;
                    default: tile = TinyTileset.TT_Grass; break;
                }

                PlaceSprite(tile, new Vector3(x, y, 0f),
                    groundRoot.transform, $"Grass_{x}_{y}", -1000);
            }
        }
    }

    // ---------------------------------------------------------------
    //  PATHS — stone paths connecting buildings
    // ---------------------------------------------------------------

    private void BuildPaths()
    {
        var pathRoot = new GameObject("Paths");
        pathRoot.transform.position = Vector3.zero;

        // Main north-south path from gate (bottom) to castle (center)
        for (int y = 2; y <= 18; y++)
        {
            PlaceSprite(TinyTileset.TT_PathVert, new Vector3(15f, y, 0f),
                pathRoot.transform, $"PathNS_{y}", -999);
        }

        // East-west path crossing at center
        for (int x = 8; x <= 22; x++)
        {
            if (x == 15) continue; // Skip the crossing point
            PlaceSprite(TinyTileset.TT_PathHoriz, new Vector3(x, 12f, 0f),
                pathRoot.transform, $"PathEW_{x}", -999);
        }

        // Crossing tile at intersection
        PlaceSprite(TinyTileset.TT_PathCross, new Vector3(15f, 12f, 0f),
            pathRoot.transform, "PathCross", -999);

        // Path branch to farm (north-east)
        for (int x = 16; x <= 20; x++)
        {
            PlaceSprite(TinyTileset.TT_PathHoriz, new Vector3(x, 18f, 0f),
                pathRoot.transform, $"PathFarm_{x}", -999);
        }

        // Path branch to barracks (west)
        for (int x = 5; x <= 7; x++)
        {
            PlaceSprite(TinyTileset.TT_PathHoriz, new Vector3(x, 12f, 0f),
                pathRoot.transform, $"PathBarracks_{x}", -999);
        }

        // South entrance tee
        PlaceSprite(TinyTileset.TT_PathTeeUp, new Vector3(15f, 2f, 0f),
            pathRoot.transform, "PathGateTee", -999);
    }

    // ---------------------------------------------------------------
    //  CASTLE — central keep (stone walls + towers)
    // ---------------------------------------------------------------

    private void BuildCastle()
    {
        var castleRoot = new GameObject("Castle");
        castleRoot.transform.position = Vector3.zero;

        // Castle center at (14, 15) — a 3x3 structure with towers

        // Bottom wall row (with gate)
        PlaceSortedSprite(TinyTileset.TT_StoneWallL, new Vector3(13f, 14f, 0f),
            castleRoot.transform, "CastleWallBL");
        PlaceSortedSprite(TinyTileset.TT_StoneGate, new Vector3(14f, 14f, 0f),
            castleRoot.transform, "CastleGate");
        PlaceSortedSprite(TinyTileset.TT_StoneWallR, new Vector3(15f, 14f, 0f),
            castleRoot.transform, "CastleWallBR");

        // Middle wall row (windows)
        PlaceSortedSprite(TinyTileset.TT_CastleWallL, new Vector3(13f, 15f, 0f),
            castleRoot.transform, "CastleWallML");
        PlaceSortedSprite(TinyTileset.TT_CastleWindow, new Vector3(14f, 15f, 0f),
            castleRoot.transform, "CastleMidWindow");
        PlaceSortedSprite(TinyTileset.TT_CastleWallR, new Vector3(15f, 15f, 0f),
            castleRoot.transform, "CastleWallMR");

        // Top wall / roof row
        PlaceSortedSprite(TinyTileset.TT_CastleRoofL, new Vector3(13f, 16f, 0f),
            castleRoot.transform, "CastleRoofL");
        PlaceSortedSprite(TinyTileset.TT_CastleRoofM, new Vector3(14f, 16f, 0f),
            castleRoot.transform, "CastleRoofM");
        PlaceSortedSprite(TinyTileset.TT_CastleRoofR, new Vector3(15f, 16f, 0f),
            castleRoot.transform, "CastleRoofR");

        // Towers on corners
        PlaceSortedSprite(TinyTileset.TT_CastleTower, new Vector3(12f, 14f, 0f),
            castleRoot.transform, "TowerBL", 10);
        PlaceSortedSprite(TinyTileset.TT_CastleTower, new Vector3(16f, 14f, 0f),
            castleRoot.transform, "TowerBR", 10);
        PlaceSortedSprite(TinyTileset.TT_CastleTower, new Vector3(12f, 16f, 0f),
            castleRoot.transform, "TowerTL", 10);
        PlaceSortedSprite(TinyTileset.TT_CastleTower, new Vector3(16f, 16f, 0f),
            castleRoot.transform, "TowerTR", 10);

        // Flags on top towers
        PlaceSortedSprite(TinyTileset.TT_CastleFlag, new Vector3(12f, 17f, 0f),
            castleRoot.transform, "FlagL", 20);
        PlaceSortedSprite(TinyTileset.TT_CastleFlag, new Vector3(16f, 17f, 0f),
            castleRoot.transform, "FlagR", 20);

        // Perimeter stone walls (partial — suggests boundary without full enclosure)
        // West wall segments
        for (int y = 14; y <= 16; y++)
        {
            PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(11f, y, 0f),
                castleRoot.transform, $"WallW_{y}");
        }
        // East wall segments
        for (int y = 14; y <= 16; y++)
        {
            PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(17f, y, 0f),
                castleRoot.transform, $"WallE_{y}");
        }
    }

    // ---------------------------------------------------------------
    //  VILLAGE — market/houses (east side)
    // ---------------------------------------------------------------

    private void BuildVillage()
    {
        var villageRoot = new GameObject("Village");
        villageRoot.transform.position = Vector3.zero;

        // ---- HOUSE 1: East market house (3x2) at (20, 11) ----
        // Bottom row: walls + door
        PlaceSortedSprite(TinyTileset.TT_HouseWallL, new Vector3(19f, 11f, 0f),
            villageRoot.transform, "House1_WallBL");
        PlaceSortedSprite(TinyTileset.TT_HouseDoor, new Vector3(20f, 11f, 0f),
            villageRoot.transform, "House1_Door");
        PlaceSortedSprite(TinyTileset.TT_HouseWallR, new Vector3(21f, 11f, 0f),
            villageRoot.transform, "House1_WallBR");
        // Top row: roof
        PlaceSortedSprite(TinyTileset.TT_RoofL, new Vector3(19f, 12f, 0f),
            villageRoot.transform, "House1_RoofL");
        PlaceSortedSprite(TinyTileset.TT_RoofM, new Vector3(20f, 12f, 0f),
            villageRoot.transform, "House1_RoofM");
        PlaceSortedSprite(TinyTileset.TT_RoofR, new Vector3(21f, 12f, 0f),
            villageRoot.transform, "House1_RoofR");
        // Chimney
        PlaceSortedSprite(TinyTileset.TT_RoofChimney, new Vector3(21f, 13f, 0f),
            villageRoot.transform, "House1_Chimney", 5);

        // ---- HOUSE 2: East-south house (3x2) at (20, 8) ----
        PlaceSortedSprite(TinyTileset.TT_HouseWallL, new Vector3(19f, 8f, 0f),
            villageRoot.transform, "House2_WallBL");
        PlaceSortedSprite(TinyTileset.TT_HouseWindow, new Vector3(20f, 8f, 0f),
            villageRoot.transform, "House2_Window");
        PlaceSortedSprite(TinyTileset.TT_HouseWallR, new Vector3(21f, 8f, 0f),
            villageRoot.transform, "House2_WallBR");
        PlaceSortedSprite(TinyTileset.TT_RoofL, new Vector3(19f, 9f, 0f),
            villageRoot.transform, "House2_RoofL");
        PlaceSortedSprite(TinyTileset.TT_RoofM, new Vector3(20f, 9f, 0f),
            villageRoot.transform, "House2_RoofM");
        PlaceSortedSprite(TinyTileset.TT_RoofR, new Vector3(21f, 9f, 0f),
            villageRoot.transform, "House2_RoofR");

        // Market props near houses
        PlaceSortedSprite(TinyTileset.TT_Barrel, new Vector3(22f, 11f, 0f),
            villageRoot.transform, "MarketBarrel1");
        PlaceSortedSprite(TinyTileset.TT_Crate, new Vector3(22f, 10f, 0f),
            villageRoot.transform, "MarketCrate1");
        PlaceSortedSprite(TinyTileset.TT_Bench, new Vector3(18f, 10f, 0f),
            villageRoot.transform, "MarketBench");
        PlaceSortedSprite(TinyTileset.TT_Cart, new Vector3(22f, 8f, 0f),
            villageRoot.transform, "MarketCart");

        // Well in village square
        PlaceSortedSprite(TinyTileset.TT_Well, new Vector3(17f, 12f, 0f),
            villageRoot.transform, "VillageWell");

        // Sign post near market
        PlaceSortedSprite(TinyTileset.TT_SignPost, new Vector3(18f, 12f, 0f),
            villageRoot.transform, "MarketSign");
    }

    // ---------------------------------------------------------------
    //  FARM — north area with fences, crops, animals
    // ---------------------------------------------------------------

    private void BuildFarm()
    {
        var farmRoot = new GameObject("Farm");
        farmRoot.transform.position = Vector3.zero;

        // Fenced area (18-24, 19-23)
        // Top fence
        for (int x = 18; x <= 24; x++)
        {
            PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(x, 23f, 0f),
                farmRoot.transform, $"FarmFenceT_{x}");
        }
        // Bottom fence with gate
        for (int x = 18; x <= 24; x++)
        {
            if (x == 21)
            {
                PlaceSortedSprite(TinyTileset.TT_FenceGate, new Vector3(x, 19f, 0f),
                    farmRoot.transform, "FarmGate");
            }
            else
            {
                PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(x, 19f, 0f),
                    farmRoot.transform, $"FarmFenceB_{x}");
            }
        }
        // Left fence
        for (int y = 20; y <= 22; y++)
        {
            PlaceSortedSprite(TinyTileset.TT_FenceVert, new Vector3(18f, y, 0f),
                farmRoot.transform, $"FarmFenceL_{y}");
        }
        // Right fence
        for (int y = 20; y <= 22; y++)
        {
            PlaceSortedSprite(TinyTileset.TT_FenceVert, new Vector3(24f, y, 0f),
                farmRoot.transform, $"FarmFenceR_{y}");
        }

        // Crops inside fence
        PlaceSortedSprite(TinyTileset.TT_Crop1, new Vector3(19f, 21f, 0f),
            farmRoot.transform, "Crop1");
        PlaceSortedSprite(TinyTileset.TT_Crop2, new Vector3(20f, 21f, 0f),
            farmRoot.transform, "Crop2");
        PlaceSortedSprite(TinyTileset.TT_Crop3, new Vector3(21f, 21f, 0f),
            farmRoot.transform, "Crop3");
        PlaceSortedSprite(TinyTileset.TT_Crop1, new Vector3(22f, 21f, 0f),
            farmRoot.transform, "Crop4");
        PlaceSortedSprite(TinyTileset.TT_Crop2, new Vector3(23f, 21f, 0f),
            farmRoot.transform, "Crop5");

        PlaceSortedSprite(TinyTileset.TT_Crop3, new Vector3(19f, 22f, 0f),
            farmRoot.transform, "Crop6");
        PlaceSortedSprite(TinyTileset.TT_Crop1, new Vector3(20f, 22f, 0f),
            farmRoot.transform, "Crop7");
        PlaceSortedSprite(TinyTileset.TT_Crop2, new Vector3(21f, 22f, 0f),
            farmRoot.transform, "Crop8");
        PlaceSortedSprite(TinyTileset.TT_Crop3, new Vector3(22f, 22f, 0f),
            farmRoot.transform, "Crop9");
        PlaceSortedSprite(TinyTileset.TT_Crop1, new Vector3(23f, 22f, 0f),
            farmRoot.transform, "Crop10");

        // Hay bale and scarecrow
        PlaceSortedSprite(TinyTileset.TT_Hay, new Vector3(19f, 20f, 0f),
            farmRoot.transform, "FarmHay");
        PlaceSortedSprite(TinyTileset.TT_Scarecrow, new Vector3(23f, 20f, 0f),
            farmRoot.transform, "FarmScarecrow");

        // Animals outside fence
        PlaceSortedSprite(TinyTileset.TT_AnimalChicken, new Vector3(25f, 20f, 0f),
            farmRoot.transform, "Chicken1");
        PlaceSortedSprite(TinyTileset.TT_AnimalCow, new Vector3(25f, 22f, 0f),
            farmRoot.transform, "Cow1");
        PlaceSortedSprite(TinyTileset.TT_AnimalPig, new Vector3(26f, 21f, 0f),
            farmRoot.transform, "Pig1");

        // Farmhouse (north-west of farm area)
        PlaceSortedSprite(TinyTileset.TT_HouseWallL2, new Vector3(18f, 24f, 0f),
            farmRoot.transform, "Farmhouse_WL");
        PlaceSortedSprite(TinyTileset.TT_HouseDoor2, new Vector3(19f, 24f, 0f),
            farmRoot.transform, "Farmhouse_Door");
        PlaceSortedSprite(TinyTileset.TT_HouseWallR2, new Vector3(20f, 24f, 0f),
            farmRoot.transform, "Farmhouse_WR");
        PlaceSortedSprite(TinyTileset.TT_RoofL2, new Vector3(18f, 25f, 0f),
            farmRoot.transform, "Farmhouse_RL");
        PlaceSortedSprite(TinyTileset.TT_RoofM2, new Vector3(19f, 25f, 0f),
            farmRoot.transform, "Farmhouse_RM");
        PlaceSortedSprite(TinyTileset.TT_RoofR2, new Vector3(20f, 25f, 0f),
            farmRoot.transform, "Farmhouse_RR");

        // Wheelbarrow near farmhouse
        PlaceSortedSprite(TinyTileset.TT_Wheelbarrow, new Vector3(21f, 24f, 0f),
            farmRoot.transform, "FarmWheelbarrow");
    }

    // ---------------------------------------------------------------
    //  BARRACKS — west side stone buildings
    // ---------------------------------------------------------------

    private void BuildBarracks()
    {
        var barracksRoot = new GameObject("Barracks");
        barracksRoot.transform.position = Vector3.zero;

        // Stone barracks building (3x2) at (5, 11)
        PlaceSortedSprite(TinyTileset.TT_StoneWallL, new Vector3(5f, 11f, 0f),
            barracksRoot.transform, "Barracks_WBL");
        PlaceSortedSprite(TinyTileset.TT_StoneGate, new Vector3(6f, 11f, 0f),
            barracksRoot.transform, "Barracks_Door");
        PlaceSortedSprite(TinyTileset.TT_StoneWallR, new Vector3(7f, 11f, 0f),
            barracksRoot.transform, "Barracks_WBR");
        PlaceSortedSprite(TinyTileset.TT_StoneWallTop, new Vector3(5f, 12f, 0f),
            barracksRoot.transform, "Barracks_WTL");
        PlaceSortedSprite(TinyTileset.TT_StoneWallTop, new Vector3(6f, 12f, 0f),
            barracksRoot.transform, "Barracks_WTM");
        PlaceSortedSprite(TinyTileset.TT_StoneWallTop, new Vector3(7f, 12f, 0f),
            barracksRoot.transform, "Barracks_WTR");

        // Training area — crates and barrels
        PlaceSortedSprite(TinyTileset.TT_Crate, new Vector3(4f, 11f, 0f),
            barracksRoot.transform, "BarracksCrate1");
        PlaceSortedSprite(TinyTileset.TT_Barrel, new Vector3(4f, 12f, 0f),
            barracksRoot.transform, "BarracksBarrel1");
        PlaceSortedSprite(TinyTileset.TT_Barrel, new Vector3(8f, 11f, 0f),
            barracksRoot.transform, "BarracksBarrel2");

        // Small guard post (south-west)
        PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(5f, 8f, 0f),
            barracksRoot.transform, "GuardPost1");
        PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(6f, 8f, 0f),
            barracksRoot.transform, "GuardPost2");
        PlaceSortedSprite(TinyTileset.TT_StoneWallTop, new Vector3(5f, 9f, 0f),
            barracksRoot.transform, "GuardPostTop1");
        PlaceSortedSprite(TinyTileset.TT_StoneWallTop, new Vector3(6f, 9f, 0f),
            barracksRoot.transform, "GuardPostTop2");

        // Fence enclosure for training yard
        PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(4f, 10f, 0f),
            barracksRoot.transform, "TrainFence1");
        PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(5f, 10f, 0f),
            barracksRoot.transform, "TrainFence2");
        PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(6f, 10f, 0f),
            barracksRoot.transform, "TrainFence3");
        PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(7f, 10f, 0f),
            barracksRoot.transform, "TrainFence4");
        PlaceSortedSprite(TinyTileset.TT_FenceHoriz, new Vector3(8f, 10f, 0f),
            barracksRoot.transform, "TrainFence5");
    }

    // ---------------------------------------------------------------
    //  ENVIRONMENT — trees, flowers, bushes, water
    // ---------------------------------------------------------------

    private void BuildEnvironment()
    {
        var envRoot = new GameObject("Environment");
        envRoot.transform.position = Vector3.zero;

        System.Random rng = new System.Random(42);

        // ---- PERIMETER TREES (ring around the village) ----
        string[] treeTiles = {
            TinyTileset.TT_TreeGreen, TinyTileset.TT_TreeGreenAlt,
            TinyTileset.TT_TreeAutumn, TinyTileset.TT_TreeAutumnAlt,
            TinyTileset.TT_TreePine
        };

        // Top edge trees
        for (int x = 0; x < GridW; x++)
        {
            if (rng.NextDouble() < 0.7)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(x, GridH - 1, 0f), envRoot.transform, $"TreeT_{x}");
            if (rng.NextDouble() < 0.5)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(x, GridH - 2, 0f), envRoot.transform, $"TreeT2_{x}");
        }
        // Bottom edge trees (gap for entrance)
        for (int x = 0; x < GridW; x++)
        {
            if (x >= 13 && x <= 17) continue; // Gate opening
            if (rng.NextDouble() < 0.7)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(x, 0f, 0f), envRoot.transform, $"TreeB_{x}");
            if (rng.NextDouble() < 0.5)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(x, 1f, 0f), envRoot.transform, $"TreeB2_{x}");
        }
        // Left edge trees
        for (int y = 2; y < GridH - 2; y++)
        {
            if (rng.NextDouble() < 0.7)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(0f, y, 0f), envRoot.transform, $"TreeL_{y}");
            if (rng.NextDouble() < 0.5)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(1f, y, 0f), envRoot.transform, $"TreeL2_{y}");
        }
        // Right edge trees
        for (int y = 2; y < GridH - 2; y++)
        {
            if (rng.NextDouble() < 0.7)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(GridW - 1, y, 0f), envRoot.transform, $"TreeR_{y}");
            if (rng.NextDouble() < 0.5)
                PlaceSortedSprite(treeTiles[rng.Next(treeTiles.Length)],
                    new Vector3(GridW - 2, y, 0f), envRoot.transform, $"TreeR2_{y}");
        }

        // ---- INTERIOR DECORATIVE TREES (scattered) ----
        PlaceSortedSprite(TinyTileset.TT_TreeGreen, new Vector3(10f, 10f, 0f),
            envRoot.transform, "VTree1");
        PlaceSortedSprite(TinyTileset.TT_TreeGreenAlt, new Vector3(13f, 8f, 0f),
            envRoot.transform, "VTree2");
        PlaceSortedSprite(TinyTileset.TT_TreeAutumn, new Vector3(17f, 8f, 0f),
            envRoot.transform, "VTree3");
        PlaceSortedSprite(TinyTileset.TT_TreePine, new Vector3(10f, 15f, 0f),
            envRoot.transform, "VTree4");
        PlaceSortedSprite(TinyTileset.TT_TreeGreen, new Vector3(20f, 15f, 0f),
            envRoot.transform, "VTree5");

        // ---- BUSHES near paths ----
        PlaceSortedSprite(TinyTileset.TT_BushGreen, new Vector3(14f, 10f, 0f),
            envRoot.transform, "Bush1");
        PlaceSortedSprite(TinyTileset.TT_BushGreenAlt, new Vector3(16f, 10f, 0f),
            envRoot.transform, "Bush2");
        PlaceSortedSprite(TinyTileset.TT_BushAutumn, new Vector3(12f, 12f, 0f),
            envRoot.transform, "Bush3");

        // ---- FLOWERS (bright, cute!) ----
        PlaceSortedSprite(TinyTileset.TT_FlowerRed, new Vector3(11f, 14f, 0f),
            envRoot.transform, "Flower1");
        PlaceSortedSprite(TinyTileset.TT_FlowerYellow, new Vector3(18f, 14f, 0f),
            envRoot.transform, "Flower2");
        PlaceSortedSprite(TinyTileset.TT_FlowerBlue, new Vector3(13f, 17f, 0f),
            envRoot.transform, "Flower3");
        PlaceSortedSprite(TinyTileset.TT_FlowerRed, new Vector3(17f, 17f, 0f),
            envRoot.transform, "Flower4");
        PlaceSortedSprite(TinyTileset.TT_FlowerYellow, new Vector3(9f, 6f, 0f),
            envRoot.transform, "Flower5");
        PlaceSortedSprite(TinyTileset.TT_FlowerBlue, new Vector3(21f, 6f, 0f),
            envRoot.transform, "Flower6");

        // ---- ROCKS ----
        PlaceSortedSprite(TinyTileset.TT_RockSmall, new Vector3(3f, 5f, 0f),
            envRoot.transform, "Rock1");
        PlaceSortedSprite(TinyTileset.TT_RockLarge, new Vector3(26f, 5f, 0f),
            envRoot.transform, "Rock2");
        PlaceSortedSprite(TinyTileset.TT_RockSmall, new Vector3(3f, 20f, 0f),
            envRoot.transform, "Rock3");

        // ---- SMALL POND (water tiles) ----
        // A small 3x2 pond south-west of center
        PlaceSortedSprite(TinyTileset.TT_WaterCornerTL, new Vector3(8f, 6f, 0f),
            envRoot.transform, "PondTL");
        PlaceSortedSprite(TinyTileset.TT_WaterEdgeN, new Vector3(9f, 6f, 0f),
            envRoot.transform, "PondTM");
        PlaceSortedSprite(TinyTileset.TT_WaterCornerTR, new Vector3(10f, 6f, 0f),
            envRoot.transform, "PondTR");
        PlaceSortedSprite(TinyTileset.TT_WaterCornerBL, new Vector3(8f, 5f, 0f),
            envRoot.transform, "PondBL");
        PlaceSortedSprite(TinyTileset.TT_WaterEdgeS, new Vector3(9f, 5f, 0f),
            envRoot.transform, "PondBM");
        PlaceSortedSprite(TinyTileset.TT_WaterCornerBR, new Vector3(10f, 5f, 0f),
            envRoot.transform, "PondBR");
        // Water lily
        PlaceSortedSprite(TinyTileset.TT_WaterLily, new Vector3(9f, 5.5f, 0f),
            envRoot.transform, "PondLily");

        // ---- BRIDGE over pond approach ----
        PlaceSortedSprite(TinyTileset.TT_BridgeHoriz, new Vector3(11f, 5f, 0f),
            envRoot.transform, "Bridge1");
        PlaceSortedSprite(TinyTileset.TT_BridgeHoriz, new Vector3(11f, 6f, 0f),
            envRoot.transform, "Bridge2");

        // ---- VILLAGE ENTRANCE (south) ----
        // Stone gate entrance
        PlaceSortedSprite(TinyTileset.TT_StoneTowerL, new Vector3(13f, 2f, 0f),
            envRoot.transform, "GateTowerL");
        PlaceSortedSprite(TinyTileset.TT_StoneTowerR, new Vector3(17f, 2f, 0f),
            envRoot.transform, "GateTowerR");
        PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(14f, 2f, 0f),
            envRoot.transform, "GateWallL");
        PlaceSortedSprite(TinyTileset.TT_StoneWallM, new Vector3(16f, 2f, 0f),
            envRoot.transform, "GateWallR");

        // Lamp posts at entrance
        PlaceSortedSprite(TinyTileset.TT_Lamp, new Vector3(14f, 3f, 0f),
            envRoot.transform, "GateLampL");
        PlaceSortedSprite(TinyTileset.TT_Lamp, new Vector3(16f, 3f, 0f),
            envRoot.transform, "GateLampR");

        // ---- STUMPS (logging area, east edge) ----
        PlaceSortedSprite(TinyTileset.TT_Stump, new Vector3(25f, 10f, 0f),
            envRoot.transform, "Stump1");
        PlaceSortedSprite(TinyTileset.TT_Stump, new Vector3(26f, 11f, 0f),
            envRoot.transform, "Stump2");
        PlaceSortedSprite(TinyTileset.TT_LogHoriz, new Vector3(25f, 11f, 0f),
            envRoot.transform, "Log1");

        // ---- MUSHROOMS (cute!) ----
        PlaceSortedSprite(TinyTileset.TT_Mushroom, new Vector3(3f, 15f, 0f),
            envRoot.transform, "Mushroom1");
        PlaceSortedSprite(TinyTileset.TT_Mushroom, new Vector3(27f, 18f, 0f),
            envRoot.transform, "Mushroom2");
    }

    // ---------------------------------------------------------------
    //  PLAYER — single Tiny Dungeon character tile
    // ---------------------------------------------------------------

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;
        _player.transform.localScale = new Vector3(CharacterScale, CharacterScale, 1f);

        var sr = _player.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 100;

        // Load the knight character tile as player sprite
        var idle = LoadSprite(TinyTileset.TD_KnightM);
        if (idle != null) sr.sprite = idle;

        var ctrl = _player.AddComponent<PlayerController>();
        // No run frames for 16x16 single-tile characters — just idle
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
    /// Each NPC gets a distinct character sprite.
    /// </summary>
    private static string GetNPCSpritePath(NPCManager.NPCData npc)
    {
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal   => TinyTileset.TD_KnightF,   // Aldric — armored knight
            NPCPersona.NPCProfession.Soldier   => TinyTileset.TD_Viking,    // Bram — tough warrior
            NPCPersona.NPCProfession.Farmer    => TinyTileset.TD_Peasant,   // Marta — simple farmer
            NPCPersona.NPCProfession.Merchant  => TinyTileset.TD_Rogue,     // Sivaro — shifty merchant
            NPCPersona.NPCProfession.Mage      => TinyTileset.TD_Mage,
            NPCPersona.NPCProfession.Priest    => TinyTileset.TD_Priest,
            NPCPersona.NPCProfession.Scout     => TinyTileset.TD_Ranger,
            NPCPersona.NPCProfession.Blacksmith => TinyTileset.TD_Dwarf,
            _ => TinyTileset.TD_Elf,
        };
    }

    /// <summary>
    /// Returns a default spawn position for an NPC based on profession,
    /// placing them near their relevant buildings.
    /// </summary>
    private static Vector3 GetNPCDefaultPosition(NPCManager.NPCData npc)
    {
        return npc.Profession switch
        {
            NPCPersona.NPCProfession.Vassal   => new Vector3(14f, 13f, 0f),  // Near castle
            NPCPersona.NPCProfession.Soldier   => new Vector3(6f, 10f, 0f),   // Near barracks
            NPCPersona.NPCProfession.Farmer    => new Vector3(21f, 19f, 0f),  // Near farm
            NPCPersona.NPCProfession.Merchant  => new Vector3(20f, 10f, 0f),  // Near market
            _ => new Vector3(15f, 10f, 0f),  // Center
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
        // Position above the character (scaled up by CharacterScale)
        promptGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        var canvas = promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 500;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(280f, 50f);
        rt.localScale = new Vector3(0.008f, 0.008f, 0.008f);

        // Bright semi-transparent background (cute style, not dark)
        var bgGO = new GameObject("PromptBG");
        bgGO.transform.SetParent(promptGO.transform, false);
        var bgImg = bgGO.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0.95f, 0.9f, 0.7f, 0.85f); // Warm parchment
        var bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = new Vector2(-10f, -4f);
        bgRT.offsetMax = new Vector2(10f, 4f);

        // Border accent (warm brown)
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
        tmp.color = new Color(0.2f, 0.15f, 0.1f); // Dark brown text on light bg
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
