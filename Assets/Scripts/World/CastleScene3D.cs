using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the 3D castle world scene.
/// Spawns low-poly 3D building and NPC objects behind the UI canvas.
/// Camera uses isometric perspective (top-down 45° angle).
///
/// Architecture:
///   [Main Camera] → [3D Castle World] + [Screen-Space UI Canvas]
///   All gameplay UI stays in Canvas; 3D models are purely visual.
/// </summary>
public class CastleScene3D : MonoBehaviour
{
    public static CastleScene3D Instance { get; private set; }

    [Header("Scene Roots")]
    [SerializeField] private Transform _buildingsRoot;
    [SerializeField] private Transform _npcsRoot;
    [SerializeField] private Transform _terrainRoot;

    [Header("Camera")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private float  _cameraHeight   = 12f;
    [SerializeField] private float  _cameraAngle    = 55f;   // degrees from horizontal
    [SerializeField] private float  _cameraDistance = 10f;
    [SerializeField] private float  _zoomMin        = 5f;
    [SerializeField] private float  _zoomMax        = 20f;
    [SerializeField] private float  _zoomSpeed      = 4f;
    [SerializeField] private float  _panSpeed        = 0.015f;

    private float _currentZoom;
    private Vector3 _panTarget;

    // Tracks spawned 3D objects
    private readonly Dictionary<string, GameObject> _npcObjects      = new();
    private readonly Dictionary<BuildingManager.BuildingType, GameObject> _buildingObjects = new();

    // Cached primitive meshes — avoids calling GameObject.CreatePrimitive which
    // auto-adds a Collider and crashes on WebGL builds where the Physics module
    // is stripped ("Can't add component because class 'BoxCollider' doesn't exist"
    // → null function pointer → wasm "null function" runtime error).
    // We cache one mesh per primitive type and instantiate GameObjects manually
    // with just MeshFilter + MeshRenderer, no Collider.
    private static readonly System.Collections.Generic.Dictionary<PrimitiveType, Mesh> _primitiveMeshes = new();

    private static Mesh GetPrimitiveMesh(PrimitiveType type)
    {
        if (_primitiveMeshes.TryGetValue(type, out var cached) && cached != null)
            return cached;

        // Create a throwaway GameObject via CreatePrimitive ONLY to extract its
        // mesh, then destroy it. We do this once per primitive type, and only
        // on platforms where CreatePrimitive works. For WebGL this would crash,
        // so we guard with a safer path below.
#if UNITY_EDITOR || !UNITY_WEBGL
        var temp = GameObject.CreatePrimitive(type);
        // Immediately strip the auto-added collider to avoid any Physics touch
        var autoCollider = temp.GetComponent<Collider>();
        if (autoCollider != null) DestroyImmediate(autoCollider);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        _primitiveMeshes[type] = mesh;
        DestroyImmediate(temp);
        return mesh;
#else
        // WebGL path: load primitive mesh from Unity's built-in resources via
        // Resources.GetBuiltinResource. This path does NOT touch Physics.
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

    /// <summary>
    /// Creates a visual-only primitive GameObject: MeshFilter + MeshRenderer,
    /// no Collider, no Rigidbody. Safe on WebGL where Physics is stripped.
    /// Replaces GameObject.CreatePrimitive which crashes in that scenario.
    /// </summary>
    private static GameObject CreateVisualPrimitive(PrimitiveType type, string name = "Primitive")
    {
        var go = new GameObject(name);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = GetPrimitiveMesh(type);
        go.AddComponent<MeshRenderer>();
        return go;
    }

    // Cached shared material — avoids new Material() per-object.
    // Tries several shaders in order of preference; falls through to Unlit/Color
    // which is ALWAYS included in WebGL builds. Standard and URP/Lit may not be
    // bundled depending on the project's render pipeline and "Always Included
    // Shaders" list, and missing shaders render as Unity's magenta fallback.
    private static Material _sharedMaterial;
    private static Material GetSharedMaterial()
    {
        if (_sharedMaterial != null) return _sharedMaterial;

        string[] shaderNames = {
            "Universal Render Pipeline/Lit",
            "Standard",
            "Mobile/Diffuse",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",          // always available
        };

        foreach (var name in shaderNames)
        {
            var shader = Shader.Find(name);
            if (shader != null)
            {
                _sharedMaterial = new Material(shader);
                Debug.Log($"[CastleScene3D] Using shader: {name}");
                return _sharedMaterial;
            }
        }

        // Last-ditch: create material with default shader (will be whatever Unity picks)
        _sharedMaterial = new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Unlit/Color"));
        Debug.LogError("[CastleScene3D] No usable shader found, using error shader");
        return _sharedMaterial;
    }

    // Cached UI reference
    private NPCInteractionUI _npcInteractionUI;

    // ─────────────────────────────────────────────────────────────
    //  LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_mainCamera == null) _mainCamera = Camera.main;
        _currentZoom = _cameraDistance;
        _panTarget = Vector3.zero;

        EnsureRoots();
    }

    private void Start()
    {
        // The 3D roaming world is now built by RoamingBootstrap.
        // CastleScene3D is retired — disable to prevent duplicate world
        // construction and camera conflicts.
        Debug.Log("[CastleScene3D] Disabled — 3D world is now managed by RoamingBootstrap");
        enabled = false;
    }

    private void OnDestroy()
    {
        if (InputHandler.Instance != null)
        {
            InputHandler.Instance.OnPinchZoom -= HandleZoom;
            InputHandler.Instance.OnSwipe     -= HandlePan;
        }
        if (GameManager.Instance?.NPCManager != null)
            GameManager.Instance.NPCManager.OnNPCAdded -= SpawnNPC3D;
        if (BuildingManager.Instance != null)
            BuildingManager.Instance.OnBuildingConstructed -= SpawnBuilding3D;
    }

    private void LateUpdate()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // Belt-and-suspenders: even if `enabled = false` in Start didn't take
        // effect (e.g. Unity re-enabled), never touch the camera on WebGL.
        return;
#else
        UpdateCameraPosition();
#endif
    }

    // ─────────────────────────────────────────────────────────────
    //  CAMERA
    // ─────────────────────────────────────────────────────────────

    private void UpdateCameraPosition()
    {
        if (_mainCamera == null) return;

        // Smooth zoom
        _cameraDistance = Mathf.Lerp(_cameraDistance, _currentZoom, Time.deltaTime * _zoomSpeed);

        // Isometric angle: offset = (0, sin(angle), -cos(angle)) * distance
        float rad = _cameraAngle * Mathf.Deg2Rad;
        var offset = new Vector3(0, Mathf.Sin(rad), -Mathf.Cos(rad)) * _cameraDistance;

        _mainCamera.transform.position = _panTarget + offset;
        _mainCamera.transform.LookAt(_panTarget);
    }

    private void HandleZoom(float delta)
    {
        _currentZoom = Mathf.Clamp(_currentZoom - delta * _zoomSpeed, _zoomMin, _zoomMax);
    }

    private void HandlePan(Vector2 swipeDir)
    {
        // Translate swipe to world pan (XZ plane)
        _panTarget += new Vector3(-swipeDir.x, 0, -swipeDir.y) * _panSpeed * _currentZoom;
        _panTarget.x = Mathf.Clamp(_panTarget.x, -8f, 8f);
        _panTarget.z = Mathf.Clamp(_panTarget.z, -8f, 8f);
    }

    // ─────────────────────────────────────────────────────────────
    //  TERRAIN
    // ─────────────────────────────────────────────────────────────

    private void SetupTerrain()
    {
        // Ground plane
        var ground = CreateVisualPrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(_terrainRoot);
        ground.transform.localScale = new Vector3(2f, 1f, 2f); // 20x20 units
        ground.transform.localPosition = Vector3.zero;
        var groundMat = new Material(GetSharedMaterial()) { color = new Color(0.22f, 0.35f, 0.18f) };
        ground.GetComponent<Renderer>().material = groundMat;

        // Castle walls border (4 low wall segments)
        float wallSize = 9.5f;
        float wallH    = 0.8f;
        float wallW    = 0.3f;
        Color wallColor = new Color(0.45f, 0.42f, 0.38f);

        CreateWall(_terrainRoot, new Vector3(0,         wallH/2, -wallSize), new Vector3(wallSize*2, wallH, wallW), wallColor, "WallNorth");
        CreateWall(_terrainRoot, new Vector3(0,         wallH/2,  wallSize), new Vector3(wallSize*2, wallH, wallW), wallColor, "WallSouth");
        CreateWall(_terrainRoot, new Vector3(-wallSize, wallH/2,  0),        new Vector3(wallW, wallH, wallSize*2), wallColor, "WallWest");
        CreateWall(_terrainRoot, new Vector3( wallSize, wallH/2,  0),        new Vector3(wallW, wallH, wallSize*2), wallColor, "WallEast");

        // Corner towers
        float[] cx = { -wallSize, wallSize, -wallSize,  wallSize };
        float[] cz = { -wallSize,-wallSize,  wallSize,  wallSize };
        for (int i = 0; i < 4; i++)
            CreateTower(_terrainRoot, new Vector3(cx[i], 0, cz[i]), wallColor);
    }

    private void CreateWall(Transform parent, Vector3 pos, Vector3 scale, Color color, string name)
    {
        var wall = CreateVisualPrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        ApplyColor(wall, color);
    }

    private void CreateTower(Transform parent, Vector3 basePos, Color color)
    {
        var tower = CreateVisualPrimitive(PrimitiveType.Cylinder);
        tower.name = "Tower";
        tower.transform.SetParent(parent);
        tower.transform.localPosition = basePos + Vector3.up * 1f;
        tower.transform.localScale = new Vector3(0.9f, 1.2f, 0.9f);
        ApplyColor(tower, color * 0.85f);

        // Parapet top
        var top = CreateVisualPrimitive(PrimitiveType.Cube);
        top.name = "TowerTop";
        top.transform.SetParent(tower.transform);
        top.transform.localPosition = new Vector3(0, 1.1f, 0);
        top.transform.localScale = new Vector3(1.2f, 0.2f, 1.2f);
        ApplyColor(top, color);
    }

    // ─────────────────────────────────────────────────────────────
    //  CASTLE BUILDINGS
    // ─────────────────────────────────────────────────────────────

    private void SetupCastle()
    {
        // Central keep (main castle building)
        SpawnKeep();

        // Pre-place empty building slots as ghost indicators
        PlaceBuildingSlotIndicators();

        // Spawn any already-built buildings
        var buildings = BuildingManager.Instance?.GetBuiltBuildings();
        if (buildings != null)
            foreach (var b in buildings)
                SpawnBuilding3D(b);
    }

    private void SpawnKeep()
    {
        // Main keep - tall cube with a slightly different shade
        var keep = new GameObject("CentralKeep");
        keep.transform.SetParent(_buildingsRoot);
        keep.transform.localPosition = Vector3.zero;

        var body = CreateVisualPrimitive(PrimitiveType.Cube);
        body.transform.SetParent(keep.transform, false);
        body.transform.localPosition = new Vector3(0, 1.5f, 0);
        body.transform.localScale = new Vector3(2.5f, 3f, 2.5f);
        ApplyColor(body, new Color(0.42f, 0.38f, 0.35f));

        // Roof pyramid
        var roof = CreateVisualPrimitive(PrimitiveType.Cube); // use cube as flat roof cap
        roof.transform.SetParent(keep.transform, false);
        roof.transform.localPosition = new Vector3(0, 3.2f, 0);
        roof.transform.localScale = new Vector3(2.6f, 0.4f, 2.6f);
        ApplyColor(roof, new Color(0.25f, 0.2f, 0.4f)); // Dark purple roof

        // Flag pole
        var pole = CreateVisualPrimitive(PrimitiveType.Cylinder);
        pole.transform.SetParent(keep.transform, false);
        pole.transform.localPosition = new Vector3(0, 4.5f, 0);
        pole.transform.localScale = new Vector3(0.05f, 0.7f, 0.05f);
        ApplyColor(pole, new Color(0.6f, 0.5f, 0.3f));
    }

    private static readonly Vector3[] BuildingSlotPositions =
    {
        new(-4f, 0, -4f), new(-4f, 0,  0f), new(-4f, 0,  4f),
        new( 0f, 0, -6f), new( 0f, 0,  6f),
        new( 4f, 0, -4f), new( 4f, 0,  0f), new( 4f, 0,  4f),
    };

    private void PlaceBuildingSlotIndicators()
    {
        foreach (var pos in BuildingSlotPositions)
        {
            var slot = CreateVisualPrimitive(PrimitiveType.Cube);
            slot.name = "BuildingSlot";
            slot.transform.SetParent(_buildingsRoot);
            slot.transform.position = pos + Vector3.up * 0.05f;
            slot.transform.localScale = new Vector3(1.8f, 0.1f, 1.8f);
            ApplyColor(slot, new Color(0.3f, 0.3f, 0.3f, 0.4f));
        }
    }

    public void SpawnBuilding3D(BuildingManager.BuildingData building)
    {
        if (_buildingObjects.ContainsKey(building.Type)) return;

        var slot = GetNextFreeBuildingSlot();
        var go = Create3DBuilding(building.Type, slot);
        _buildingObjects[building.Type] = go;
    }

    private Vector3 GetNextFreeBuildingSlot()
    {
        int idx = _buildingObjects.Count % BuildingSlotPositions.Length;
        return BuildingSlotPositions[idx];
    }

    private GameObject Create3DBuilding(BuildingManager.BuildingType type, Vector3 position)
    {
        var go = new GameObject(type.ToString());
        go.transform.SetParent(_buildingsRoot);
        go.transform.position = position;

        Color c = Placeholder3DColors.Building(type);

        // Base
        var body = CreateVisualPrimitive(PrimitiveType.Cube);
        body.transform.SetParent(go.transform, false);
        body.transform.localPosition = new Vector3(0, 0.5f, 0);
        body.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
        ApplyColor(body, c);

        // Roof style varies by type
        switch (type)
        {
            case BuildingManager.BuildingType.Sawmill:
            case BuildingManager.BuildingType.Farm:
                // Flat roof with chimney
                var chimney = CreateVisualPrimitive(PrimitiveType.Cube);
                chimney.transform.SetParent(go.transform, false);
                chimney.transform.localPosition = new Vector3(0.3f, 1.3f, 0.3f);
                chimney.transform.localScale = new Vector3(0.2f, 0.4f, 0.2f);
                ApplyColor(chimney, c * 0.7f);
                break;

            case BuildingManager.BuildingType.Barracks:
            case BuildingManager.BuildingType.Archery:
                // Wider and shorter with battlement hint
                body.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
                var top = CreateVisualPrimitive(PrimitiveType.Cube);
                top.transform.SetParent(go.transform, false);
                top.transform.localPosition = new Vector3(0, 0.9f, 0);
                top.transform.localScale = new Vector3(1.7f, 0.2f, 1.7f);
                ApplyColor(top, c * 0.85f);
                break;

            case BuildingManager.BuildingType.Watchtower:
            case BuildingManager.BuildingType.MageTower:
                // Tall tower
                body.transform.localScale = new Vector3(0.8f, 2.2f, 0.8f);
                body.transform.localPosition = new Vector3(0, 1.1f, 0);
                var towerCap = CreateVisualPrimitive(PrimitiveType.Cylinder);
                towerCap.transform.SetParent(go.transform, false);
                towerCap.transform.localPosition = new Vector3(0, 2.6f, 0);
                towerCap.transform.localScale = new Vector3(1f, 0.3f, 1f);
                ApplyColor(towerCap, c * 0.7f);
                break;
        }

        // Name label (3D text via floating TextMesh, no TMP needed in world space)
        // Skipped for now — handled by UI overlay on tap

        return go;
    }

    // ─────────────────────────────────────────────────────────────
    //  NPC CHARACTERS
    // ─────────────────────────────────────────────────────────────

    public void SpawnNPC3D(NPCManager.NPCData npc)
    {
        if (_npcObjects.ContainsKey(npc.Id)) return;

        var go = Create3DCharacter(npc);
        go.transform.SetParent(_npcsRoot);
        go.transform.position = new Vector3(npc.WorldPosition.x, 0, npc.WorldPosition.z);
        _npcObjects[npc.Id] = go;
    }

    private GameObject Create3DCharacter(NPCManager.NPCData npc)
    {
        var root = new GameObject($"NPC_{npc.Id}");

        Color bodyColor = npc.Profession switch
        {
            NPCPersona.NPCProfession.Soldier  => new Color(0.55f, 0.25f, 0.15f),
            NPCPersona.NPCProfession.Farmer   => new Color(0.35f, 0.60f, 0.25f),
            NPCPersona.NPCProfession.Merchant => new Color(0.70f, 0.55f, 0.10f),
            NPCPersona.NPCProfession.Vassal   => new Color(0.45f, 0.30f, 0.65f),
            NPCPersona.NPCProfession.Scholar  => new Color(0.25f, 0.45f, 0.70f),
            NPCPersona.NPCProfession.Priest   => new Color(0.85f, 0.85f, 0.80f),
            NPCPersona.NPCProfession.Spy      => new Color(0.15f, 0.15f, 0.20f),
            _                                  => new Color(0.5f, 0.5f, 0.5f),
        };
        Color skinColor = new Color(0.85f, 0.72f, 0.58f);

        // Body (capsule)
        var body = CreateVisualPrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0, 0.8f, 0);
        body.transform.localScale = new Vector3(0.4f, 0.5f, 0.4f);
        ApplyColor(body, bodyColor);

        // Head (sphere)
        var head = CreateVisualPrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0, 1.45f, 0);
        head.transform.localScale = Vector3.one * 0.35f;
        ApplyColor(head, skinColor);

        // Tap collider (invisible flat cylinder at base for easy tap detection)
        var tapCollider = CreateVisualPrimitive(PrimitiveType.Cylinder);
        tapCollider.name = "TapCollider";
        tapCollider.transform.SetParent(root.transform, false);
        tapCollider.transform.localPosition = new Vector3(0, 0.05f, 0);
        tapCollider.transform.localScale = new Vector3(0.7f, 0.05f, 0.7f);
        var renderer = tapCollider.GetComponent<Renderer>();
        if (renderer) renderer.enabled = false; // Invisible but collider stays

        // NPC3DClickHandler
        var handler = root.AddComponent<NPC3DClickHandler>();
        handler.NpcId = npc.Id;

        return root;
    }

    public void RefreshNPCPositions()
    {
        var npcs = NPCManager.Instance?.GetAllNPCs();
        if (npcs == null) return;

        foreach (var npc in npcs)
        {
            if (_npcObjects.TryGetValue(npc.Id, out var go))
                go.transform.position = new Vector3(npc.WorldPosition.x, 0, npc.WorldPosition.z);
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────────────────────

    private void EnsureRoots()
    {
        if (_buildingsRoot == null)
        {
            var go = new GameObject("Buildings");
            go.transform.SetParent(transform);
            _buildingsRoot = go.transform;
        }
        if (_npcsRoot == null)
        {
            var go = new GameObject("NPCs");
            go.transform.SetParent(transform);
            _npcsRoot = go.transform;
        }
        if (_terrainRoot == null)
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(transform);
            _terrainRoot = go.transform;
        }
    }

    private static void ApplyColor(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;
        // Instantiate from shared base to avoid per-object Shader.Find() calls
        var mat = new Material(GetSharedMaterial()) { color = color };
        renderer.material = mat;
    }
}

/// <summary>
/// Attached to each 3D NPC object. Detects raycasted taps and opens dialogue.
/// </summary>
public class NPC3DClickHandler : MonoBehaviour
{
    public string NpcId;

    private void OnMouseDown()
    {
        UIManager.Instance?.OpenDialogue(NpcId);
    }
}

/// <summary>
/// Color palette for 3D placeholder buildings.
/// </summary>
public static class Placeholder3DColors
{
    public static Color Building(BuildingManager.BuildingType type) => type switch
    {
        BuildingManager.BuildingType.Sawmill    => new Color(0.50f, 0.30f, 0.10f),
        BuildingManager.BuildingType.Farm       => new Color(0.30f, 0.60f, 0.20f),
        BuildingManager.BuildingType.Market     => new Color(0.70f, 0.50f, 0.10f),
        BuildingManager.BuildingType.Mine       => new Color(0.40f, 0.40f, 0.50f),
        BuildingManager.BuildingType.Barracks   => new Color(0.50f, 0.20f, 0.10f),
        BuildingManager.BuildingType.Archery    => new Color(0.40f, 0.30f, 0.20f),
        BuildingManager.BuildingType.Stable     => new Color(0.50f, 0.35f, 0.15f),
        BuildingManager.BuildingType.Watchtower => new Color(0.30f, 0.30f, 0.45f),
        BuildingManager.BuildingType.Warehouse  => new Color(0.45f, 0.35f, 0.20f),
        BuildingManager.BuildingType.Granary    => new Color(0.60f, 0.50f, 0.20f),
        BuildingManager.BuildingType.Well       => new Color(0.20f, 0.40f, 0.60f),
        BuildingManager.BuildingType.Hospital   => new Color(0.70f, 0.20f, 0.20f),
        BuildingManager.BuildingType.ThroneRoom => new Color(0.60f, 0.45f, 0.05f),
        BuildingManager.BuildingType.Library    => new Color(0.30f, 0.20f, 0.50f),
        BuildingManager.BuildingType.MageTower  => new Color(0.50f, 0.20f, 0.70f),
        BuildingManager.BuildingType.CastleWalls=> new Color(0.40f, 0.40f, 0.45f),
        _                                        => Color.gray,
    };
}
