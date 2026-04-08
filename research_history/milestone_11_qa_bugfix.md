# Milestone 11 — QA Audit & Bug Fixes

**Date:** 2026-04-09
**Commit:** ed724b3

## QA Audit Results

Ran a full codebase quality audit using an Explore subagent, inspecting:
- Singleton initialization order
- NullReferenceException risks
- Serialized field wiring
- Game state and scene flow

## Critical Bugs Fixed

### 1. MonetizationManager.AddScrolls — Inverted Logic
**File:** `Assets/Scripts/Multiplayer/MonetizationManager.cs:84`
**Bug:** `WisdomScrollsToday - amount` was draining scrolls instead of adding them.
**Fix:** Changed to `WisdomScrollsToday + amount`.

### 2. GameManager.WorldMapManager — Never Initialized
**File:** `Assets/Scripts/Core/GameManager.cs:87`
**Bug:** `InitializeSystems()` created ResourceManager, NPCManager, EventManager — but WorldMapManager was declared as a public field and never AddComponent'd. Every `WorldMapManager.Instance?.X` call in the game silently returned null.
**Fix:** Added `if (WorldMapManager == null) WorldMapManager = gameObject.AddComponent<WorldMapManager>();`

### 3. CastleViewUI.PopulateNPCList — FindObjectOfType Per Click
**File:** `Assets/Scripts/UI/CastleViewUI.cs`
**Bug:** `FindObjectOfType<NPCInteractionUI>()` was called every time an NPC button was tapped — slow O(n) scene walk on every interaction.
**Fix:** Cached `_npcInteractionUI = FindObjectOfType<NPCInteractionUI>()` in `Start()`, used cached reference in listener.

### 4. GameBootstrap.EnsureManager — Missing DontDestroyOnLoad
**File:** `Assets/Scripts/Core/GameBootstrap.cs:96`
**Bug:** Manager GameObjects created without `DontDestroyOnLoad()` — if a scene reload happened before the manager's own `Awake()` ran, managers could be destroyed.
**Fix:** Captured the created `go` and called `DontDestroyOnLoad(go)` explicitly.

## Architecture Verified (No Bugs)

- Tap → NPC3DClickHandler.OnMouseDown → UIManager.OpenDialogue + NPCInteractionUI.OpenForNPC ✓
- NPCInteractionUI.OpenForNPC exists and is properly implemented ✓
- GeminiAPIClient initialized by GameBootstrap.EnsureManager ✓
- Single-scene panel-switching via UIManager + GameState events ✓
- Scholar/Priest/Spy enum values present in NPCProfession ✓
- Compile: Unity exit 0, no errors ✓

## CI Status

- `validate` job (syntax check, no license): runs on every push ✓
- `build-webgl` + `build-android`: gated on `vars.UNITY_BUILD_ENABLED == 'true'`
- Missing secrets: `UNITY_EMAIL`, `UNITY_PASSWORD`
- To activate: run `bash setup_ci.sh` from the repo root

## Remaining Before First Playtest

1. Run `bash setup_ci.sh` to set CI secrets (requires Unity account credentials)
2. Open Unity Editor → Play Bootstrap.unity → enter lord name → verify NPC tap → Gemini response
3. Art assets: placeholder geometry/materials in place; real sprites/models are stretch goal
