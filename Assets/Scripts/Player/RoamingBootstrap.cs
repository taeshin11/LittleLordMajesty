using UnityEngine;

/// <summary>
/// M16 roaming pivot — runtime bootstrap that spawns the player avatar,
/// wires the follow camera, and loads the player sprite sheet. Sits on
/// CastleScene3D (or any gameplay scene root) and fires on the first
/// entry to GameManager.GameState.Castle.
///
/// This replaces the Editor-side SceneAutoBuilder.BuildRoamingCastle
/// approach because we don't have to rebuild the scene YAML to start
/// seeing a walking avatar — just spawn a GameObject from code on scene
/// load. Makes the pivot incremental: the card grid and fullscreen
/// chat scroll can still open today, but the avatar is now visible and
/// walkable in the 3D castle underneath them.
/// </summary>
public class RoamingBootstrap : MonoBehaviour
{
    [SerializeField] private Vector3 _playerSpawn = new Vector3(0f, 0f, -2f);
    [SerializeField] private float _playerWalkSpeed = 4f;

    private bool _spawned;
    private GameObject _player;
    private readonly System.Collections.Generic.Dictionary<string, GameObject> _npcObjects = new();
    private Transform _npcRoot;

    /// <summary>
    /// Auto-install helper — RuntimeInitializeOnLoadMethod is a Unity hook
    /// that runs on scene load BEFORE any MonoBehaviour Start. We use it to
    /// attach a RoamingBootstrap component to the first loaded scene's root
    /// GameObject, so the incremental pivot doesn't need a scene YAML edit.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        // Skip if one already exists (someone wired it via SceneAutoBuilder).
        if (FindFirstObjectByType<RoamingBootstrap>() != null) return;
        var host = new GameObject("RoamingBootstrap");
        host.AddComponent<RoamingBootstrap>();
        Object.DontDestroyOnLoad(host);
    }

    private void Start()
    {
        // Wait until GameManager is up. CastleScene3D already defers NPC
        // spawns the same way — we piggyback on the same lifecycle.
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStateChanged += OnStateChanged;
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

        // M16-07 retire: hide the card-grid CastleViewPanel and legacy
        // DialoguePanel. They're still in the scene (UIManager references
        // survive), but they're inactive so the 3D roaming world is visible.
        RetireLegacyCardUI();
        // The old tutorial flow ('tap on Aldric') assumes card taps — skip
        // it entirely on the roaming path until we author a walk-around
        // tutorial.
        SuppressLegacyTutorial();

        BuildGround();
        BuildRoamingCamera();
        BuildPlayer();
        WireCamera();
        SpawnNPCs();
        _spawned = true;
        Debug.Log("[RoamingBootstrap] Retired legacy UI, built roaming world");
    }

    private void RetireLegacyCardUI()
    {
        // Walk all UI panels and kill the ones that are part of the
        // pre-pivot card experience. Identify by GameObject name instead
        // of adding a new tag — avoids scene YAML churn.
        string[] retireNames = {
            "CastleViewPanel",
            "DialoguePanel",
        };
        foreach (var name in retireNames)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                go.SetActive(false);
                Debug.Log($"[RoamingBootstrap] Retired legacy UI: {name}");
            }
        }
    }

    private void SuppressLegacyTutorial()
    {
        var overlay = GameObject.Find("TutorialOverlay");
        if (overlay != null)
        {
            overlay.SetActive(false);
            Debug.Log("[RoamingBootstrap] Suppressed legacy tutorial overlay");
        }
        // Also tell TutorialSystem to consider it done so it doesn't
        // reassert itself next frame.
        var tut = TutorialSystem.Instance;
        if (tut != null)
        {
            try { tut.enabled = false; } catch { }
        }
    }

    private void BuildGround()
    {
        // A single flat quad at y=0 showing the pastel castle courtyard.
        // No Unity Primitive API (the legacy CastleScene3D path crashed on
        // WebGL with CreatePrimitive stripped colliders). Just a world-space
        // sprite renderer rotated to lie flat. Lives in the active scene,
        // not under the DontDestroyOnLoad bootstrap host.
        var ground = new GameObject("RoamingGround");
        ground.transform.position = Vector3.zero;
        ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        ground.transform.localScale = new Vector3(30f, 18f, 1f);
        var sr = ground.AddComponent<SpriteRenderer>();
        var bg = Resources.Load<Sprite>("Art/Generated/bg_castle_courtyard");
        if (bg != null) sr.sprite = bg;
        else sr.color = new Color(0.75f, 0.85f, 0.65f); // pastel green fallback
        sr.sortingOrder = -100;
    }

    private Camera _roamingCam;

    private void BuildRoamingCamera()
    {
        // Disable any existing MainCamera so Camera.main resolves to ours.
        // This also stops the legacy camera-pan crash path cold because
        // nothing touches the old camera transform anymore.
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        // NOTE: don't parent under this GameObject (which gets DontDestroyOnLoad)
        // — the camera must live in the active scene so it renders correctly.
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.78f, 0.92f, 0.96f); // pastel sky
        _roamingCam.fieldOfView = 55f;
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 500f;
        _roamingCam.depth = 10; // draw after any existing cameras
        try { camGO.tag = "MainCamera"; } catch { /* tag might be unused */ }
        camGO.AddComponent<FollowCamera>();
    }

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
        root.transform.position = new Vector3(npc.WorldPosition.x, 0f, npc.WorldPosition.z);

        // Billboard sprite child, same layout as the player.
        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(root.transform, false);
        spriteGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        spriteGO.transform.localScale    = new Vector3(1.4f, 2.1f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignSpriteRenderer(sr);

        _npcObjects[npc.Id] = root;
    }

    private void BuildPlayer()
    {
        // Spawn in the active scene, not under the DontDestroyOnLoad host.
        _player = new GameObject("Player");
        _player.transform.position = _playerSpawn;

        // Sprite holder — same layout as NPCs: child quad at chest height,
        // yaw-aligned to camera by PlayerController's sprite swap logic.
        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(_player.transform, false);
        spriteGO.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        spriteGO.transform.localScale    = new Vector3(1.4f, 2.1f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();

        // Load all 8 directional sprites from Resources/Art/Sprites/
        // (emitted by tools/image_gen/generate_sprites.py as
        // player_<dir>_<frame>.png). Order must match PlayerController
        // enum: S0 S1 N0 N1 E0 E1 W0 W1.
        var sprites = new Sprite[8];
        string[] dirs = { "s", "n", "e", "w" };
        for (int d = 0; d < 4; d++)
        {
            for (int f = 0; f < 2; f++)
            {
                var s = Resources.Load<Sprite>($"Art/Sprites/player_{dirs[d]}_{f}");
                sprites[d * 2 + f] = s;
                if (s == null)
                    Debug.LogWarning($"[RoamingBootstrap] Missing player sprite: player_{dirs[d]}_{f}");
            }
        }
        if (sprites[0] != null) sr.sprite = sprites[0];

        // Controller uses reflection-free wiring via a tiny runtime helper
        // just like NPCBillboard. Keeps the serialized fields private.
        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.SendMessage("ConfigureAtRuntime",
            new PlayerController.RuntimeConfig {
                Sprite = sr,
                DirectionSprites = sprites,
                WalkSpeed = _playerWalkSpeed,
            },
            SendMessageOptions.DontRequireReceiver);

        _player.AddComponent<InteractionFinder>();
    }

    private void WireCamera()
    {
        if (_roamingCam == null) return;
        var follow = _roamingCam.GetComponent<FollowCamera>()
                  ?? _roamingCam.gameObject.AddComponent<FollowCamera>();
        follow.SetTarget(_player.transform);
    }
}
