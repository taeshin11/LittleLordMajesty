using UnityEngine;

/// <summary>
/// M16 roaming pivot — runtime bootstrap that spawns the player avatar,
/// wires the follow camera, and loads the player sprite sheet.
///
/// 2D top-down version: orthographic camera, XY plane movement,
/// no 3D billboard rotation needed. Supports mobile via VirtualJoystick.
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector2 _playerSpawn = new Vector2(0f, -1f);
    [SerializeField] private float _playerWalkSpeed = 4f;

    private bool _spawned;
    private bool _subscribed;
    private GameObject _player;
    private readonly System.Collections.Generic.Dictionary<string, GameObject> _npcObjects = new();
    private Transform _npcRoot;
    private Camera _roamingCam;

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
        _spawned = true;
        Debug.Log("[RoamingBootstrap] 2D roaming world built");
    }

    // ─── Legacy UI Removal ──────────────────────────────────────────

    private void RetireLegacyUI()
    {
        string[] retireNames = {
            "CastleViewPanel",
            "DialoguePanel",
            "EventPanel",
            "TutorialOverlay",
            "TopHUD",
            "ActionBar",
        };
        foreach (var name in retireNames)
        {
            var go = FindIncludingInactive(name);
            if (go != null)
            {
                Destroy(go);
                Debug.Log($"[RoamingBootstrap] Destroyed legacy UI: {name}");
            }
        }
    }

    private static GameObject FindIncludingInactive(string name)
    {
        var all = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (var go in all)
        {
            if (go == null) continue;
            if (go.name != name) continue;
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

    // ─── 2D World Building ──────────────────────────────────────────

    private void BuildRoamingCamera()
    {
        // Disable any existing camera.
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.orthographic = true;
        _roamingCam.orthographicSize = 5.5f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.55f, 0.78f, 0.45f); // pastel grass
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 100f;
        _roamingCam.depth = 10;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        try { camGO.tag = "MainCamera"; } catch { }
        camGO.AddComponent<FollowCamera>();
    }

    private void BuildGround()
    {
        // 2D ground sprite — no rotation needed, just XY plane.
        var ground = new GameObject("RoamingGround");
        ground.transform.position = new Vector3(0f, 0f, 1f); // z=1 behind sprites
        var sr = ground.AddComponent<SpriteRenderer>();
        var bg = Resources.Load<Sprite>("Art/Generated/bg_castle_courtyard");
        if (bg != null)
        {
            sr.sprite = bg;
            // Scale to cover reasonable world area.
            // bg_castle_courtyard is likely 512x512 at 100 PPU = 5.12 world units.
            // Scale up to ~20x20 world units for the courtyard.
            float pixelsPerUnit = bg.pixelsPerUnit;
            float spriteWorldW = bg.rect.width / pixelsPerUnit;
            float spriteWorldH = bg.rect.height / pixelsPerUnit;
            float targetSize = 22f;
            float scaleX = targetSize / spriteWorldW;
            float scaleY = targetSize / spriteWorldH;
            ground.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        }
        else
        {
            sr.color = new Color(0.75f, 0.85f, 0.65f);
        }
        sr.sortingOrder = -100;
    }

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = new Vector3(_playerSpawn.x, _playerSpawn.y, 0f);

        // Sprite child — direct 2D, no "chest height" offset needed.
        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(_player.transform, false);
        spriteGO.transform.localPosition = Vector3.zero;
        // SDXL sprites are 512x768 at 100 PPU = 5.12x7.68 world units.
        // Scale down so the character is ~1 world unit tall.
        spriteGO.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        // Load 8 directional sprites: S0 S1 N0 N1 E0 E1 W0 W1.
        var sprites = new Sprite[8];
        string[] dirs = { "s", "n", "e", "w" };
        for (int d = 0; d < 4; d++)
        {
            for (int f = 0; f < 2; f++)
            {
                var s = Resources.Load<Sprite>($"Art/Sprites/player_{dirs[d]}_{f}");
                sprites[d * 2 + f] = s;
                if (s == null)
                    Debug.LogWarning($"[RoamingBootstrap] Missing sprite: player_{dirs[d]}_{f}");
            }
        }
        if (sprites[0] != null) sr.sprite = sprites[0];

        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.ConfigureAtRuntime(new PlayerController.RuntimeConfig {
            Sprite = sr,
            DirectionSprites = sprites,
            WalkSpeed = _playerWalkSpeed,
        });

        _player.AddComponent<InteractionFinder>();
    }

    private void WireCamera()
    {
        if (_roamingCam == null) return;
        var follow = _roamingCam.GetComponent<FollowCamera>()
                  ?? _roamingCam.gameObject.AddComponent<FollowCamera>();
        follow.SetTarget(_player.transform);
    }

    // ─── NPC Spawning ───────────────────────────────────────────────

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
        // Place NPC in 2D: use WorldPosition.x for X, WorldPosition.z for Y
        // (legacy 3D positions used XZ plane, now we map to XY).
        root.transform.position = new Vector3(npc.WorldPosition.x, npc.WorldPosition.z, 0f);

        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(root.transform, false);
        spriteGO.transform.localPosition = Vector3.zero;
        spriteGO.transform.localScale = new Vector3(0.15f, 0.15f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignSpriteRenderer(sr);

        _npcObjects[npc.Id] = root;
    }
}
