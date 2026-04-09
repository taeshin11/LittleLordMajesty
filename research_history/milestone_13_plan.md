# Milestone 13 Plan: Alpha Playtest Verification, Steam Prep & Visual Upgrade

**Date:** 2026-04-09
**Prerequisite:** M12 complete (Steam, Tutorial, LLM streaming, 32 bug fixes)

---

## Goal

Reach a state where a real person can sit down, launch the game from the Steam client (or a Windows build), and play through the full Bootstrap → Castle → Tutorial → NPC Interaction → WorldMap flow without a crash or null-reference error, and where the Steam store page is ready to go live.

---

## M13 Progress Summary

### Completed: 6-Agent Audit & Bug Fixes

**Commits:**
- `0ea03e9` fix: M13 critical bug fixes from 6-agent audit
- `27249ca` fix: M13 medium bug fixes — save/load, buildings, events, performance
- `e76d101` fix: remove dead `_cachedUI` field from NPC3DClickHandler
- `3d275a4` fix: add missing `using System` for `Action<>` in CastleViewUI

The 6-agent audit (QA / Backend / UI / CodeReview / Historian / ProjectManager) found **36 issues total**: 13 CRITICAL, 9 HIGH, 11 MEDIUM, 3 LOW.

#### CRITICAL Fixes (all resolved)

| Issue | Fix |
|-------|-----|
| DayCycle state guard missing | Added guard to prevent double-start on scene reload |
| TypewriterEffect GC alloc per frame | Cached string builder; eliminated per-frame allocation |
| CastleViewUI event leak | Unsubscribed all events in `OnDestroy()` |
| API key guard absent | Added null/empty check before any Gemini call |
| Tutorial timing race | Deferred tutorial start until all managers initialized |
| TutorialUI cache fake-null | Fixed `??` check against Unity Object — now uses explicit null comparison |
| NPC click double-fire | Removed dead `_cachedUI` field; eliminated redundant click path |
| Shader.Find cache missing | Cached Shader.Find result in static field; no per-frame lookup |

#### MEDIUM Fixes (all resolved)

| Issue | Fix |
|-------|-----|
| SaveSystem missing building save/restore | Buildings now serialized and restored on load |
| 8 missing BuildingData entries | Added all 8 entries to BuildingDatabase |
| Rebellion event not firing | Event registration corrected in EventManager |
| WeatherDisaster event not firing | Event registration corrected in EventManager |
| `moralEffect` not applied | Applied morale delta in event resolution path |
| TriggerManualEvent missing type param | Added generic type parameter to method signature |
| NPC/Building Dictionary lookup O(n) | Switched to Dictionary-backed lookups |
| NPC routine not cached | Cached routine reference; eliminated repeated GetComponent calls |

QA re-verification confirmed all fixes clean — zero regressions introduced.

### Completed: GitHub Pages Auto-Deploy

**Commits:**
- `7ec99b8` feat: auto-deploy WebGL build to GitHub Pages
- `d5a78ab` fix: add write permissions for GitHub Pages deployment

CI pipeline now automatically deploys the WebGL build to GitHub Pages on every push to main. Permissions fix applied to the workflow so the deploy step can write to the `gh-pages` branch.

---

## P0 — Alpha Playtest Code-Level Verification

**Status: TODO — highest priority**

These tasks must be done before any external tester touches the build.

### P0.1 Bootstrap → Castle Flow Verification

**Goal:** Confirm every scene transition is wired correctly and loads without errors.

Steps:
1. Open Unity Editor → open `Bootstrap.unity` → Enter Play Mode
2. Verify `GameBootstrap.Awake()` runs and all managers initialize (`DontDestroyOnLoad` confirmed)
3. Enter lord name → press Start → confirm transition to `Game.unity`
4. Confirm `CastleViewUI` loads, NPC list populates, resource bars show non-zero values
5. Click each nav button (Castle, WorldMap, Events, Build) — no `MissingReferenceException`
6. Perform 10 full round-trips: MainMenu → NewGame → Castle → WorldMap → back → quit — zero Unity console errors

**Pass criteria:** 10 consecutive runs, zero red errors in Unity console.

### P0.2 Null Reference Sweep

Run a static analysis pass over all scripts for the highest-risk null-ref patterns:

| Pattern | Script(s) to check |
|---------|-------------------|
| `GetComponent<T>()` result used without null check | All UI scripts |
| `Instance` singleton accessed before `Awake()` | GameManager, UIManager, GeminiAPIClient |
| `FindObjectOfType<T>()` called outside `Start()` | Verify all removed (M11 fixed CastleViewUI; confirm others) |
| `Resources.Load<T>()` returned null used directly | LocalizationManager, TutorialUI |
| `JsonUtility.FromJson` on empty/malformed string | GeminiAPIClient, SaveSystem |

Fix any remaining null-ref risks before the playtest build.

### P0.3 Scene Reference Audit

All `[SerializeField]` references wired in `Game.unity` and `Bootstrap.unity` must be non-null at runtime.

Steps:
1. Open each scene in Unity Editor
2. Select every GameObject with a MonoBehaviour
3. Confirm all serialized fields in Inspector are assigned (no "None (GameObject)" slots)
4. Write an `Editor/SceneReferenceValidator.cs` menu item that scans all scenes and reports missing references — run before every build

**Acceptance:** `SceneReferenceValidator` reports zero missing references.

### P0.4 Tutorial Flow Verification

1. Start New Game → confirm `TutorialSystem.StartTutorial()` called
2. Walk all 7 steps: intro overlay → tap Aldric → issue command → open build → build farm → open WorldMap → complete
3. Confirm `PlayerPrefs` key `TutorialCompleted` = 1 after completion
4. Start New Game a second time → confirm tutorial does NOT re-show (or resets correctly per design intent)
5. Test Skip button mid-tutorial → confirm `OnTutorialComplete()` fires

**Note:** Decide and document whether tutorial replays on New Game or only on first-ever launch.

### P0.5 Gemini Streaming Smoke Test

1. Ensure `GameConfig.asset` has valid API key
2. Tap any NPC → confirm `SendMessageStreaming()` is called
3. Confirm dialogue window opens immediately (action-first pattern)
4. Confirm text streams in token-by-token (typewriter driven by SSE chunks)
5. Confirm no timeout or JSON parse error after 10 interactions
6. Check Unity console: no `NullReferenceException` in `GeminiAPIClient.cs`

---

## P1 — Steam Store Metadata Preparation

**Status: TODO**

### P1.1 Steamworks Partner Account
- Register on Steamworks Partner (partner.steamgames.com)
- Pay the $100 app fee
- Receive App ID (replace placeholder `480` in `SteamManager.cs`)

### P1.2 Store Page Content

| Asset | Spec | Status |
|-------|------|--------|
| Short description | 1 sentence, < 300 chars, English | To write |
| Long description | 1500–3000 words, HTML-formatted | To write |
| Tags | 5–10 from Steam taxonomy (Strategy, AI, Indie, Medieval, Isometric) | To select |
| Capsule image (header) | 460×215 px | To create |
| Capsule image (large) | 1920×620 px | To create |
| Screenshots | Min 5, 1920×1080 | To capture in-editor |
| Trailer | 30–90 sec gameplay, 1080p | To record |
| System requirements | Win10 64-bit, 4 GB RAM, DX11 | To fill |
| Supported languages | en, ko, ja, zh, fr, de, es | Already implemented |

### P1.3 Build Upload Script

Create `tools/upload_steam_build.sh`:
- Calls `steamcmd +login <user> +run_app_build appbuild.vdf +quit`
- `appbuild.vdf` points to the Windows build output directory
- Uploads as branch `alpha` (not default/public)

### P1.4 Achievement Registration

Register the 10 achievement IDs from `SteamManager.cs` in Steamworks backend:
- `FIRST_NPC_COMMAND`, `FIRST_BUILDING`, `FIRST_BATTLE`, `REACH_FIVE_TERRITORIES`,
  `REACH_ALL_TERRITORIES`, `DEFEAT_ORC_RAID`, `FIRST_SPY`, `INTERROGATION_MASTER`,
  `TRADE_DEAL`, `BECOME_MAJESTY`

---

## P2 — Visual Upgrade

**Status: TODO**

### P2.1 Kenney Asset Integration

Replace all placeholder Unity primitive meshes with Kenney.nl low-poly assets.

| Placeholder | Kenney Pack | Target file |
|------------|-------------|-------------|
| White cubes (buildings) | Kenney Medieval Kit | Castle tower, house, farm, market |
| Cylinder trees | Kenney Nature Kit | Oak, pine, bush variants |
| Flat plane (terrain) | Kenney Terrain Kit | Grass tile, dirt path, water tile |
| Sphere NPCs | Kenney Character Pack | Villager, knight, merchant, scholar |

Steps:
1. Download packs from kenney.nl (free, CC0)
2. Import `.obj` / `.gltf` files into `Assets/Art/Kenney/`
3. Create prefabs in `Assets/Prefabs/Buildings/`, `Assets/Prefabs/NPCs/`
4. Update `SceneAutoBuilder.cs` or write a new `AssetSwapper.cs` Editor script to replace primitives in `Game.unity` automatically

### P2.2 NPC Animations

| Animation | Clip Name | Trigger |
|-----------|-----------|---------|
| Idle | `npc_idle` | Default state |
| Walk | `npc_walk` | Moving between waypoints |
| Talk | `npc_talk` | During dialogue |
| Work | `npc_work` | While assigned to building |

Steps:
1. Use Kenney Character animations if included, otherwise rig simple root-motion clips in Blender
2. Create `Animator` controller per NPC profession type (Vassal, Soldier, Merchant, Farmer, Scholar, Priest, Spy)
3. Wire state transitions: enter `npc_talk` when `NPCInteractionUI.OpenForNPC()` is called; return to `npc_idle` on close

### P2.3 Particle Effects

| Event | Effect |
|-------|--------|
| Building placed | Dust puff + sparkle |
| Building complete | Golden shimmer burst |
| Orc raid incoming | Red smoke on WorldMap territory |
| Tutorial step complete | Green check pulse |

Use Unity's built-in Particle System. Store prefabs in `Assets/Prefabs/VFX/`.

---

## M13 Completion Criteria

| Criteria | Priority | Status |
|----------|----------|--------|
| 6-agent audit complete, all CRITICAL + MEDIUM issues fixed | P0 | DONE |
| GitHub Pages auto-deploy wired in CI | P0 | DONE |
| QA re-verification: zero regressions | P0 | DONE |
| 10 consecutive Bootstrap → Castle runs, zero errors | P0 | TODO |
| Null-ref sweep clean, `SceneReferenceValidator` passes | P0 | TODO |
| Tutorial 7-step flow verified and edge cases handled | P0 | TODO |
| Gemini streaming: 10 NPC interactions without timeout | P0 | TODO |
| Steam App ID registered, store page content drafted | P1 | TODO |
| At least 5 store screenshots captured | P1 | TODO |
| Kenney assets replacing all primitives in Game.unity | P2 | TODO |
| NPC idle and talk animations playing correctly | P2 | TODO |

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|-----------|
| Kenney animation rigs incompatible with existing NPC prefabs | Medium | Medium | Keep old prefabs as fallback; swap mesh only |
| Steam $100 fee delay | Low | Low | Prepare all metadata assets locally while waiting |
| Gemini 2.0-flash-lite quota exceeded during playtest | Medium | High | Mock mode flag in `GeminiAPIClient` for offline testing |
| Scene references broken after asset swap | Medium | High | Run `SceneReferenceValidator` after every asset import |
| Tutorial flow breaks on scene reload edge case | Low | Medium | Add integration test in `GameBootstrap` that walks tutorial headlessly |
