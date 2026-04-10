# M13 — Autonomous Polish Pass

**Date:** 2026-04-10
**Mode:** User delegated full autonomy ("나한테 묻지말고 다 한다음에 히스토리 기록만 잘하고 gemini랑 상의해가면서 해")
**Goal:** Address the "buttons work but the game still looks empty and broken" feedback after the click-debugging saga ended.

---

## Feedback That Drove This Pass

After pause menu + worldmap navigation shipped working, the user opened the build again and reported:

1. **Pause Resume button wasn't wired** (fixed previous commit, but validation here)
2. **WorldMap screen was empty** (no territory tiles visible)
3. **"캐릭터들도 없고 그래픽이 하나도 안나오냐"** — no characters, no graphics visible
4. **"그래픽이 너무 구식이야"** — graphics look old / obsolete

Then: **"다 해"** → autonomous mode, no questions, commit & ship.

---

## Diagnosis

Several distinct issues compounding the "empty/broken" impression:

### A. Magenta scene rendering (shader fallback chain broken)

`CastleScene3D.GetSharedMaterial()` was calling:
```csharp
_sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
if (_sharedMaterial.shader == null)
    _sharedMaterial = new Material(Shader.Find("Standard"));
```

Two problems:
- This project uses Unity's Built-in Pipeline, not URP → `Universal Render Pipeline/Lit` returns null.
- `Standard` shader is not guaranteed to be in a WebGL build's bundled shader set unless it's explicitly listed in `ProjectSettings/GraphicsSettings.asset → m_AlwaysIncludedShaders`. It wasn't.

Result: `new Material(null)` produced a material whose shader fell back to Unity's magenta "missing shader" placeholder. Every 3D primitive in the castle scene (ground, walls, towers, keep, NPCs, buildings) rendered as bright magenta. That matched the bright-purple background in the user's pause-screen screenshot exactly.

### B. CastleScene3D never spawned existing NPCs

`CastleScene3D.Start()` subscribed to `NPCManager.OnNPCAdded` but never iterated `NPCManager.GetAllNPCs()`. If NPCs were created during bootstrap (which they are — `NewGame()` seeds the castle with the initial NPCs via `NPCManager.InitializeDefaultNPCs`), those existed *before* CastleScene3D's Start() ran, and the event never fired again for them. So even if shaders had worked, the scene would still be characterless.

### C. UI panels remained unwired for non-MainMenu paths

`SceneReferenceValidator` had previously flagged 72 null refs across Settings / WorldMap / Leaderboard / EventPanel / Pause. Most got explicitly skipped as "pre-existing gaps, M13 P2". But these gaps mean every time you open one of those panels, you see a mostly-empty screen with a title and maybe a close button.

### D. MainMenu and WorldMap had no background art at all

CastleView had Gemini-generated background wired in the previous commit, but MainMenu was still a flat dark panel and WorldMap was solid dark green. Visually boring, which fed the "too old" impression.

---

## Fixes

### 1. Shader fallback chain
`CastleScene3D.GetSharedMaterial` now tries an ordered list:
```
Universal Render Pipeline/Lit
Standard
Mobile/Diffuse
Legacy Shaders/Diffuse     ← guaranteed in Always Included Shaders (fileID 10753)
Unlit/Color                ← last-ditch, always available
```
First shader that resolves wins, logs which one was picked. `Legacy Shaders/Diffuse` is already in the project's `m_AlwaysIncludedShaders` list, so the material will pick it up and actually render lit diffuse colors.

### 2. Spawn existing NPCs on CastleScene3D startup
`CastleScene3D.Start()` now iterates `NPCManager.GetAllNPCs()` and calls `SpawnNPC3D` for each, *then* subscribes to the event for future additions. Catches the race between `NewGame()` seeding NPCs and `CastleScene3D.Start()` running.

### 3. Pause menu wired properly (previous commit, verified this pass)
`PauseUI.cs` MonoBehaviour + SceneAutoBuilder `BuildPausePanel` now wires Resume / Save / Main Menu. Resume returns to Castle state, Save persists then returns, Main Menu returns to the main menu. Localized via `pause_title`, `pause_resume`, `pause_save`, `pause_main_menu` keys.

### 4. WorldMap procedural tiles (previous commit, verified)
`WorldMapUI.BuildMapGrid` now builds tiles procedurally when no `_territoryTilePrefab` is wired. `GridLayoutGroup` auto-added to MapContainer at runtime. Each tile has terrain-colored background, ownership border, name label, scouted indicator, and a full-surface Button. `_closeButton` serialized field added and wired so "← Castle" actually transitions back to Castle state.

### 5. Gemini background art on MainMenu, WorldMap, Castle
All three panels now have a new full-screen `_backgroundArt` Image layer sitting behind the HUD. Each panel's `Start()` kicks off a deterministic Gemini 2.5 Flash Image generation call via `GeminiImageClient`. Cached to disk after first generation; returning visits are instant.

| Panel | Prompt style |
|-------|--------------|
| MainMenu | Young lord standing on castle balcony at dusk, cinematic oil painting |
| Castle | Medieval courtyard at golden hour, painterly, no characters, wide establishing |
| WorldMap | Ancient parchment cartography, hand-drawn territorial map, compass rose |

Each image is dimmed slightly (alpha 0.75–0.85) so HUD text stays legible.

### 6. NPC list auto-open on Castle entry (previous commit, verified)
`CastleViewUI.Start()` now sets `_npcListPanel.SetActive(true)` + calls `PopulateNPCList()` immediately. Previously you had to click the "NPCs" action-bar button to see anyone. Now characters are visible the moment you enter the castle.

### 7. Settings panel built out
`BuildSettingsPanel` in SceneAutoBuilder now creates:
- Title + close button
- Language dropdown (7 languages, driven by `TMP_Dropdown` with a minimal working template)
- Music volume slider (0–1, default 0.8)
- SFX volume slider (0–1, default 1.0)
- Save button

New helper methods `CreateDropdown` and `CreateSlider` build UI-safe TMP dropdowns and sliders at scene-build time. All `SettingsUI` serialized fields now wired via `SerializedObject`.

### 8. Gemini image client + disk cache (established previous commit)
`GeminiImageClient` singleton with SHA256-hashed disk cache at `persistentDataPath/generated_art/{hash}.png`. Identical prompts return from cache; zero API cost on repeat. Used by MainMenu, Castle, WorldMap backgrounds, and NPC portraits.

---

## What's Still Pending (NOT fixed this pass)

- **Real tutorial wiring** — `TutorialUI` has SerializedField refs that aren't set during SceneAutoBuilder. Tutorial never plays. (Low priority; alpha players can skip.)
- **Event panel** — `UIManager._eventTitleText`, `_eventDescText`, `_eventSubmitButton`, `_eventIcon` still null. When events trigger, panel shows empty. M13 P2 cleanup.
- **Leaderboard UI** — Never wired. M13 P2.
- **Kenney 3D asset integration** — M13 P2 proper visual upgrade. Current state: primitive cubes/cylinders/spheres with colored materials + Gemini 2D backgrounds. Not ideal but playable.
- **NPC animations** — None. Characters are static capsules.
- **Real save/load UI** — SaveSystem.Save() works but there's no save slot UI.

---

## Files Touched This Pass

- `Assets/Scripts/World/CastleScene3D.cs` — shader fallback chain + existing-NPC spawn iteration
- `Assets/Scripts/UI/MainMenuUI.cs` — `_backgroundArt` field + `RequestBackgroundArt()`
- `Assets/Scripts/UI/WorldMapUI.cs` — `_backgroundArt` field + `RequestBackgroundArt()`
- `Assets/Editor/SceneAutoBuilder.cs`:
  - MainMenu + WorldMap background Image layers wired
  - Full Settings panel build-out (dropdown + sliders + save button)
  - New `CreateDropdown` and `CreateSlider` helpers
- `research_history/milestone_13_autonomous_polish_pass.md` (this file)

---

## Commits In This Debug Arc (context)

| # | Hash | Intent |
|---|------|--------|
| 1 | `4605d40` | CJK font fallback + purge hardcoded text |
| 2 | `67d2d44` | ToastLayer raycast fix (unblock main menu clicks) |
| 3 | `f77ac4e` | Gemini image client + SceneReferenceValidator + MainMenu wiring fix |
| 4 | `1a26176` | Zero-scale canvas runtime guard (red herring, kept as defensive) |
| 5 | `f38a660` | `activeInputHandler: -1 → 0` (REAL click blocker) |
| 6 | `b3c4a37` | PauseUI + WorldMap tiles + Castle background art |
| 7 | (this commit) | Shader fallback chain + existing-NPC spawn + MainMenu/WorldMap backgrounds + Settings build-out |

---

## Principles I Followed In This Autonomous Mode

1. **Leverage Gemini for content generation**, not just text. Every "too old" visual complaint gets addressed by generating fresh art on first load instead of hand-drawing assets. Zero marginal cost after first generation thanks to the SHA256 disk cache.
2. **Procedural fallback > missing prefab** — when WorldMapUI/TerritoryTile required a prefab that wasn't authored, I made the script build tiles in code. Same pattern for NPC list cards if needed.
3. **Shader fallback chains are mandatory for WebGL**. Built-in pipeline shader availability is not guaranteed across render modes. Always include `Legacy Shaders/Diffuse` + `Unlit/Color` as escape hatches.
4. **Spawn iteration before event subscription** — anytime you subscribe to an "added" event and want to handle existing entries, iterate first. Otherwise you race the initializer.
5. **All user-facing text through LocalizationManager.Get()** — `pause_*` keys were added in the same format as existing keys (en + ko full, other locales fall back to en).
