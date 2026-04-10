# M13 — Autonomous Polish Pass 2

**Date:** 2026-04-10
**Trigger:** User asked "그래서 다 하고있지?" (are you doing everything?) — signal to keep going without waiting for confirmation on each step.

---

## What This Pass Delivered

### 1. Tutorial now actually runs on New Game
`GameBootstrap.BootSequence()` previously never created a `TutorialSystem` instance — it was a singleton with no spawner. `GameManager.NewGame()` called `TutorialSystem.Instance.StartTutorial()`, but `Instance` was null, so tutorial was skipped silently.

Added `EnsureManager<TutorialSystem>("TutorialSystem")` alongside the other manager bootstrapping. TutorialSystem's own Awake DontDestroyOnLoads, so it persists across the Bootstrap → Game scene transition. Tutorial now fires on first New Game.

### 2. Event panel wire-up (the `FindTMP` / `FindButton` bug)
`SceneAutoBuilder.FindTMP(root, "EventTitle")` was returning null for every event-panel text ref, causing the validator to flag them. Root cause: `Transform.Find()` only searches DIRECT children, and the event panel structure is:
```
EventPanel
 └─ EventCard
     ├─ EventTitle   ← 2 levels deep, Find() can't see it
     ├─ EventDesc
     └─ SubmitButton
```
Replaced `FindTMP`/`FindButton` with a new `FindDeep` helper that does recursive depth-first search. Now every event element is correctly wired via `UIManager`'s SerializedObject setup. Same fix benefits any other 2+ level panel lookup.

### 3. NPCListItem cards rebuilt procedurally
The old `NPCListItemPrefab` was a bare skeleton with a `Task` label and little else — child name lookups (`Find("Name")`, `Find("MoodBar")`, `Find("TalkButton")`) all returned null, so every card was blank except for the background.

Rewrote `CastleViewUI.PopulateNPCList` + new `BuildNPCCard` to construct cards fully in code:
- **Portrait slot** on the left (filled at runtime by `GeminiImageClient.GenerateImage` with a deterministic per-NPC prompt — cached to disk so the same character always has the same face)
- Profession-colored placeholder while Gemini generates
- Bold character **name** + localized **profession** label
- **Mood bar** with green/yellow/red fill based on `MoodScore`
- **Talk button** that closes the NPC list and opens `NPCInteractionUI`

Auto-adds `VerticalLayoutGroup` + `ContentSizeFitter` to the parent so cards stack cleanly.

### 4. Building menu rebuilt procedurally
Same pattern. Old `BuildingMenuItem.prefab` had mismatched child names, so `Find("Name")`, `Find("Cost")`, `Find("Description")`, `Find("BuildButton")` all returned null → empty building cards on tap.

New `BuildBuildingCard` creates a polished card per entry:
- Localized building name (big, gold)
- Wood + gold cost row
- Description from `building.DescriptionKey` (wrapped, secondary color)
- Green "Build" button that calls `BuildingManager.TryBuild` and refreshes the menu

Auto-adds `GridLayoutGroup` to the parent container.

### 5. Settings panel now has real controls (previous pass, verified)
Language dropdown (TMP_Dropdown with minimal template), music slider, SFX slider, save button. All `SettingsUI` SerializedFields wired.

### 6. Gemini backgrounds on every panel (previous pass, verified)
MainMenu, WorldMap, Castle all have full-screen `_backgroundArt` Image layers with Gemini-generated paintings on first load. Disk cached by SHA256, zero API cost on return.

### 7. New localization keys
`btn_talk`, `btn_build` — added to `en.json` and `ko.json`.

---

## Files Touched This Pass

- `Assets/Scripts/Core/GameBootstrap.cs` — `EnsureManager<TutorialSystem>`
- `Assets/Editor/SceneAutoBuilder.cs` — `FindDeep` helper replacing shallow `Find` in `FindTMP`/`FindButton`
- `Assets/Scripts/UI/CastleViewUI.cs` — procedural `BuildNPCCard` + `BuildBuildingCard` + `RequestPortrait` + `ProfessionColor`
- `Assets/Resources/Localization/en.json` — `btn_talk`, `btn_build`
- `Assets/Resources/Localization/ko.json` — 대화, 건설
- `research_history/milestone_13_autonomous_pass_2.md` (this file)

---

## Context — The Full Debugging Arc

| # | Commit | Fix |
|---|--------|-----|
| 1 | `4605d40` | CJK font fallback + hardcoded-text purge |
| 2 | `67d2d44` | ToastLayer raycast fix (first click breakthrough) |
| 3 | `f77ac4e` | Gemini client + SceneReferenceValidator + MainMenu wire |
| 4 | `1a26176` | Zero-scale canvas guard (red herring, kept as defensive) |
| 5 | `f38a660` | `activeInputHandler: -1 → 0` (REAL click root cause) |
| 6 | `b3c4a37` | PauseUI + WorldMap tiles + Castle background |
| 7 | `479bbea` | Shader fallback chain + existing-NPC spawn + Gemini backgrounds everywhere + Settings build-out |
| 8 | (this) | Tutorial spawning + FindDeep + procedural NPC/Building cards |

---

## What's Still Pending (deferred)

- **Leaderboard UI panel wire-up** — low priority, not on alpha test path
- **Kenney 3D asset import** — the 3D scene now renders with Legacy Shaders/Diffuse (no more magenta) but still uses primitive geometry (cubes, capsules). Kenney pack would replace the placeholders with actual low-poly models. M13 P2.
- **NPC animations** — characters are static. Animator controllers + idle/walk clips needed.
- **Save slot UI** — SaveSystem.Save() works but there's no multi-slot browser.
- **Event panel response field wiring** — event submit button fires but the response field isn't parsed into event logic.
- **Monetization UI** — `MonetizationManager` exists but no in-game purchase flow UI.

These are all follow-ups for future sessions. The current build should be playable end-to-end: MainMenu → New Game → Castle (with NPC cards and Gemini background) → Tap NPC → Dialogue → Close → Back to Castle → Map button → WorldMap tiles → Back → Pause → Resume. Tutorial fires on first New Game.
