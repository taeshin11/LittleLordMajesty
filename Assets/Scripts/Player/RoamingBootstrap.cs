using UnityEngine;
using TMPro;

/// <summary>
/// M16 roaming pivot — runtime bootstrap that spawns the player avatar,
/// wires the follow camera, and loads Anokolisa Pixel Crawler sprites.
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
        Debug.Log("[RoamingBootstrap] 2D roaming world built (Anokolisa Pixel Crawler)");
    }

    // --- Legacy UI Removal ---

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

    // --- 2D World Building ---

    private void BuildRoamingCamera()
    {
        var oldMain = Camera.main;
        if (oldMain != null) oldMain.gameObject.SetActive(false);

        var camGO = new GameObject("RoamingCamera");
        _roamingCam = camGO.AddComponent<Camera>();
        _roamingCam.orthographic = true;
        _roamingCam.orthographicSize = 5.5f;
        _roamingCam.clearFlags = CameraClearFlags.SolidColor;
        _roamingCam.backgroundColor = new Color(0.2f, 0.47f, 0.01f); // match Anokolisa grass
        _roamingCam.nearClipPlane = 0.1f;
        _roamingCam.farClipPlane = 100f;
        _roamingCam.depth = 10;
        camGO.transform.position = new Vector3(0f, 0f, -10f);
        try { camGO.tag = "MainCamera"; } catch { }
        camGO.AddComponent<FollowCamera>();
    }

    private void BuildGround()
    {
        var ground = new GameObject("RoamingGround");
        ground.transform.position = new Vector3(0f, 0f, 1f);
        var sr = ground.AddComponent<SpriteRenderer>();

        // Use Anokolisa tiled grass ground (pixel art, matches game sprites)
        var grassTex = Resources.Load<Texture2D>("Art/PixelCrawler/Tilesets/Ground_Grass");
        if (grassTex != null)
        {
            var grassSprite = Sprite.Create(grassTex,
                new Rect(0, 0, grassTex.width, grassTex.height),
                new Vector2(0.5f, 0.5f), PixelCrawlerSprites.PPU);
            sr.sprite = grassSprite;
            // 384px at 16 PPU = 24 WU. Already covers full camera view, no scale needed.
        }
        else
        {
            // Fallback: solid pastel green
            sr.color = new Color(0.35f, 0.65f, 0.2f);
        }
        sr.sortingOrder = -100;
    }

    private void BuildPlayer()
    {
        _player = new GameObject("Player");
        _player.transform.position = new Vector3(_playerSpawn.x, _playerSpawn.y, 0f);

        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(_player.transform, false);
        spriteGO.transform.localPosition = Vector3.zero;
        // Body_A: 64x64 at 16 PPU = 4 WU. Scale 0.5 → character ~1.9 WU tall.
        spriteGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        // Load Anokolisa player sprite set
        var sprites = PixelCrawlerSprites.LoadPlayerSprites();

        // Set initial sprite
        if (sprites.IdleDown != null && sprites.IdleDown.Length > 0)
            sr.sprite = sprites.IdleDown[0];

        var ctrl = _player.AddComponent<PlayerController>();
        ctrl.ConfigureAtRuntime(new PlayerController.RuntimeConfig {
            Sprite = sr,
            Sprites = sprites,
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

    // --- NPC Spawning ---

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
        // Map 3D XZ to 2D XY
        root.transform.position = new Vector3(npc.WorldPosition.x, npc.WorldPosition.z, 0f);

        var spriteGO = new GameObject("Sprite");
        spriteGO.transform.SetParent(root.transform, false);
        spriteGO.transform.localPosition = Vector3.zero;
        // NPC: 32x32 at 16 PPU = 2 WU. Scale 0.5 → character ~1.9 WU tall (matches player).
        spriteGO.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        var sr = spriteGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 5;
        sr.color = PixelCrawlerSprites.GetNPCTint(npc.Id);

        var identity = root.AddComponent<NPCIdentity>();
        identity.SetIdentity(npc.Id, npc.Name);

        var bb = root.AddComponent<NPCBillboard>();
        bb.SetCharacterId(npc.Id);
        bb.AssignSpriteRenderer(sr);

        // Wire daily routine so NPC walks around the courtyard
        var routine = root.AddComponent<NPCDailyRoutine>();
        routine.NpcId = npc.Id;

        // Build interact prompt UI programmatically
        BuildInteractPrompt(root, npc.Name);

        _npcObjects[npc.Id] = root;
    }

    private void BuildInteractPrompt(GameObject npcRoot, string npcName)
    {
        var promptGO = new GameObject("InteractPrompt");
        promptGO.transform.SetParent(npcRoot.transform, false);
        promptGO.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        var canvas = promptGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 40f);
        rt.localScale = new Vector3(0.02f, 0.02f, 1f);

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

        // Add outline for readability
        var outline = labelGO.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(1f, -1f);

        var prompt = promptGO.AddComponent<InteractPromptUI>();
        prompt.SetupRuntime(canvas, tmp);

        canvas.gameObject.SetActive(false);
    }
}
