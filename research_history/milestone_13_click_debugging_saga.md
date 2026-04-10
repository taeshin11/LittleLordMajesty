# M13 — The Click Debugging Saga

**Date:** 2026-04-10
**Duration:** Several hours of onion-peeling
**Trigger:** User opened the alpha WebGL build, all buttons were dead — "아무것도 클릭이 안되네"

---

## TL;DR — Root Cause

`ProjectSettings/ProjectSettings.asset` had `activeInputHandler: -1` (a corrupted/invalid value). Valid values are 0 (old Input Manager), 1 (new Input System package), or 2 (both). With `-1`, Unity's `StandaloneInputModule` silently failed to activate at runtime, so `EventSystem.currentInputModule == null`, so no pointer events were processed, so every button click was dropped.

Raycasting still worked — `GraphicRaycaster.RaycastAll` correctly hit `StartButton` — but with no input module, nothing forwarded the click to the button's `onClick` handler.

**Fix:** Set `activeInputHandler: 0` in `ProjectSettings.asset`, plus a defensive runtime guard in `UIManager.Awake` that creates/re-enables `StandaloneInputModule` on the EventSystem if it's missing, so fresh clones with stale project settings still work.

---

## The Onion — Every "Bug" I Thought I Found

Along the way I fixed several **real but unrelated** bugs before finding the actual culprit. Each one felt like "the answer" until the next probe revealed it wasn't.

### Layer 1: Missing TMP fallback font (the original visible symptom)

- **Symptom:** Title and 3 buttons rendered as empty boxes (□□) on a Korean-locale browser
- **Cause:** `LiberationSans SDF` font atlas had no CJK glyphs; the `LocalizationManager` was feeding Korean strings to a Latin-only font
- **Fix (commit `4605d40`):**
  - Downloaded Noto Sans KR (Korean subset OTF, 4.6 MB) into `Assets/Fonts/`
  - New `Assets/Editor/CJKFontSetup.cs` generates a dynamic TMP_FontAsset with SDF atlas and wires it as a fallback on `LiberationSans SDF`
  - `LocalizationManager.CanRenderCJK()` safety belt reverts detected CJK locale to English if no fallback is present
- **Status:** Real bug, properly fixed. Not the click blocker though.

### Layer 2: Hardcoded English strings everywhere

- **Symptom:** Even after font fix, subtitle/labels were still English on Korean locale
- **Cause:** ~130 hardcoded string literals across 11 files bypassed `LocalizationManager`
- **Fix (same commit):**
  - Audit via sub-agent identified every `.text = "..."`, `SetButtonText(...)`, etc.
  - Refactored `MainMenuUI`, `GameBootstrap`, `TutorialUI`, `NPCInteractionUI`, `CastleViewUI`, `WorldMapUI`, `EventManager`, `ResearchSystem`, `ProductionChainManager`, `DebugConsole` — all user-facing strings now route through `LocalizationManager.Get(key)`
  - Added ~130 new keys to `en.json` (source of truth) and `ko.json` (full Korean translations); ~60 new keys to `ja.json`/`zh.json`; high-visibility keys to `fr`/`de`/`es`
  - `ResearchSystem.Technology` and `ProductionChainManager.ProductionNode` got `NameKey`/`DescriptionKey` fields with `LocalizedName`/`LocalizedDescription` getters
  - Saved persistent `feedback_no_hardcoded_text.md` memory so future sessions follow the rule
- **Status:** Real architectural fix. Still not the click blocker.

### Layer 3: ToastLayer raycast-blocking overlay

- **Symptom:** Probed every panel's raycast state; ToastLayer was a full-screen transparent (`Color.clear`) Image with `raycastTarget=true`, sibling-ordered after MainMenuPanel
- **Cause:** `CreatePanel` in `SceneAutoBuilder` always added an `Image` with `raycastTarget=true`. An invisible fullscreen Image on top of MainMenuPanel silently ate every click before it could reach any button.
- **Fix (commit `67d2d44`):**
  - Explicit `toastLayerImg.raycastTarget = false` after creation
  - Defensive: `CreatePanel` now auto-sets `raycastTarget=false` when `color.a <= 0.001` — so this class of bug can never recur via any other CreatePanel call site
- **Status:** Real click-blocking bug, properly fixed. Not the final blocker though.

### Layer 4: Wrong button field wiring + Scene validator

- **Symptom:** After ToastLayer fix, clicks still dead. Added `SceneReferenceValidator` editor tool to find missing serialized refs.
- **Finding:** 72 null `[SerializeField]` refs in `Game.unity`. Most were pre-existing SceneAutoBuilder wire-up gaps in panels off the alpha test path (`WorldMapUI`, `SettingsUI`, `LeaderboardUI`) — left them documented with skip-list + M13 P2 follow-up. Critical gap: `MainMenuUI._newGameButton` was null because SceneAutoBuilder wired `startBtn` to `_startButton` (the unused modal-confirm button) instead of `_newGameButton`.
- **Fix (commit `f77ac4e`):**
  - Rewired SceneAutoBuilder: `_newGameButton`, `_subtitleText`, `_versionText` now all wired
  - Simplified `MainMenuUI.OnNewGameClicked()` — removed the dead modal path that tried to activate a non-existent `_nameInputPanel`. The button now directly reads the inline input field and starts the game.
  - Also wired `GeminiImageClient` + NPC portrait auto-generation (see "Bonus features" below)
- **Status:** Real semantic wiring bug, properly fixed. **Still** not the final click blocker.

### Layer 5: The "scale is zero" red herring

- **Symptom:** Grepped Game.unity YAML. MainCanvas `RectTransform.m_LocalScale: {x: 0, y: 0, z: 0}`. Out of 131 rect transforms in the scene, exactly one was zero — the root MainCanvas. Looked like a smoking gun: ScreenSpaceOverlay renders at screen coordinates (so UI is still visible even at scale 0), but GraphicRaycaster tests hits in world coordinates — a zero-scale canvas would collapse every child to the origin.
- **Chased this hard (commit `1a26176`):**
  - Tried `rt.localScale = Vector3.one` after `AddComponent<Canvas>()` — didn't persist through SaveScene
  - Tried pre-specifying `new GameObject(name, typeof(RectTransform))` — still saved as zero
  - Tried `SerializedObject.ApplyModifiedProperties` on every field — `Debug.Log` showed scale=(1,1,1) after apply, but YAML still wrote 0
  - Fell back to runtime fix in `UIManager.Awake`: `if (rt.localScale.sqrMagnitude < 0.001f) rt.localScale = Vector3.one`
- **Then the `PlayModeProbe` told the truth:**
  ```
  [Probe] MainCanvas.localScale = (0.31, 0.31, 0.31)
  ```
  The scale IS zero in the YAML file, but at runtime `CanvasScaler` computes `scaleFactor = 0.3125` (ScaleWithScreenSize matching 2048×1536 reference to 640×480 editor viewport) and **Unity's Canvas internally writes the computed scale onto the RectTransform's localScale**. So at runtime, scale is never zero. The YAML zero is just an uninitialized serialized default that gets overwritten on the first frame.
- **Status:** Not a bug. Total red herring. The runtime normalization code is now defensive (harmless no-op unless scale actually is near-zero).

### Layer 6 (finally): The real bug — `activeInputHandler: -1`

- **PlayModeProbe output:**
  ```
  [Probe] EventSystem: FOUND, current=<b>Selected:</b>
          No module
  ```
  Wait. `No module`?
  
  Looked at `ProjectSettings/ProjectSettings.asset`:
  ```
  activeInputHandler: -1
  ```
  
  Valid values are 0, 1, 2. `-1` is garbage, probably left over from some earlier project setup mishap. With an invalid active input handler, Unity disables **both** the old `StandaloneInputModule` AND the new `InputSystemUIInputModule`. The EventSystem has no module, no pointer events are processed, every click is dropped silently.
  
  And `Packages/manifest.json` has `com.unity.inputsystem: 1.14.0` installed — so the new Input System package is present, which may have been what tried to bump `activeInputHandler` and left it broken.

- **The raycast was working the whole time.** `GraphicRaycaster.RaycastAll` at screen center correctly returned `StartButton` as the topmost hit. The button was there, the raycast found it, but nothing was listening.

- **Fix (this commit):**
  - `ProjectSettings.asset`: `activeInputHandler: 0` (old Input Manager only — the code uses `Input.*` APIs and `StandaloneInputModule`)
  - `UIManager.Awake`: defensive guard that finds the scene's EventSystem and, if `currentInputModule == null`, creates or re-enables a `StandaloneInputModule`. So even a fresh clone with stale project settings gets a working event system on game start.
  - Kept the (now no-op) scale normalization as a harmless belt — the `sqrMagnitude < 0.001` check won't fire since CanvasScaler already provides a valid scale.

### Verification

`PlayModeProbe` re-run after fix:
```
[Probe] EventSystem: FOUND, current=<b>Selected:</b>
        Pointer Input Module of type: UnityEngine.EventSystems.StandaloneInputModule
```
Module is present and active. Clicks will now reach button onClick handlers.

---

## Bonus Features Landed In This Saga

While debugging the click issue, also delivered:

### Gemini 2.5 Flash Image integration
`Assets/Scripts/AI/GeminiImageClient.cs` — singleton with SHA256-hashed disk cache (`persistentDataPath/generated_art/{hash}.png`). Identical prompts return from cache with zero API cost. `NPCInteractionUI.RequestPortrait()` kicks off portrait generation when a dialogue opens; cached portraits swap in on subsequent visits. Deterministic per-NPC prompts (including the NPC's id) so the same character always resolves from cache after first generation.

### SceneReferenceValidator
`Assets/Editor/SceneReferenceValidator.cs` with menu item + `-executeMethod` CLI entry point. Scans every Build Settings scene for null `UnityEngine.Object` serialized references. Caught 72 null refs on first run; skip-list now covers the pre-existing SceneAutoBuilder gaps so the validator focuses on regressions.

### PlayModeProbe
`Assets/Editor/PlayModeProbe.cs` — headless play-mode runtime introspection. Opens Bootstrap.unity, enters play mode, waits 3 seconds, writes a report of MainCanvas state, EventSystem module, button world positions, and raycast hits. This is the tool that finally exposed `No module`. Keeping it for future debugging of similar silent-failure UI bugs.

### ClickFixVerifier
`Assets/Editor/ClickFixVerifier.cs` — edit-time precondition verifier for click preconditions (canvas, raycaster, event system, panel hierarchy, button components). Runs in edit mode so no play-mode overhead. Useful as a quick pre-push smoke check.

### Localization architecture
~130 new keys, all refactored to go through `LocalizationManager.Get()`. User's `feedback_no_hardcoded_text.md` memory locked the rule.

### CJK font pipeline
Auto-generates a dynamic TMP font asset from Noto Sans KR and wires it as a fallback on LiberationSans SDF. Callable from CLI for CI.

---

## Commits

| Hash | Title |
|------|-------|
| `4605d40` | feat: CJK font fallback + purge all hardcoded user-facing text |
| `67d2d44` | fix: disable raycast on invisible ToastLayer that blocked every MainMenu click |
| `f77ac4e` | feat: Gemini image generation + SceneReferenceValidator + MainMenu wiring fix |
| `1a26176` | fix: normalize zero-scale MainCanvas RectTransform at runtime (red herring, kept as defensive guard) |
| (this commit) | fix: activeInputHandler was -1 — EventSystem had no input module. Real click blocker. |

---

## Lessons Learned

1. **Symptoms can be misleading.** Every layer of the onion had me convinced I'd found the bug. The only way through was runtime introspection — `PlayModeProbe` showed the real state (`No module`, `listeners=0`, scale=0.31). Without that tool I'd still be chasing red herrings.

2. **When UI renders but doesn't respond, check the input module, not the raycaster.** GraphicRaycaster was doing its job perfectly the whole time — it just had no downstream consumer.

3. **Invalid enum values in ProjectSettings are silent failures.** `activeInputHandler: -1` isn't rejected by Unity — it just disables all input modules and the game runs in a degraded state without any error or warning. Next time I see a "silent" UI failure, check `activeInputHandler` first.

4. **Defensive runtime guards are worth it.** The `UIManager.Awake` EventSystem check costs ~10 lines of code and makes the game resilient to a fresh-clone scenario where somebody else's ProjectSettings got corrupted. Cheap insurance.

5. **Every red-herring fix was still a real bug.** ToastLayer really was blocking raycasts (even if the input module was also broken). MainMenuUI really had the wrong button field wired. 130 hardcoded strings really did bypass localization. The Korean TMP font really wasn't in the atlas. Not a wasted round — but without runtime introspection, I had no way to tell which fix was the one.

---

## Current State of M13 Alpha Playtest Path

| Item | Status |
|------|--------|
| Bootstrap → Game scene transition | ✅ Working (probe confirms) |
| MainCanvas + GraphicRaycaster + CanvasScaler | ✅ |
| EventSystem + StandaloneInputModule active | ✅ (after fix) |
| MainMenuPanel active with 4 buttons wired | ✅ |
| No raycast-blocking overlays | ✅ (ToastLayer fix) |
| Button onClick listeners wired at runtime | ✅ (MainMenuUI.SetupUI) |
| New Game flow: inline input → NewGame() → Castle scene | Needs playtest verification |
| Tutorial 7-step flow | Needs playtest verification |
| Gemini streaming for NPC dialogue | Needs playtest verification |
| NPC portrait auto-generation | Needs playtest verification |

Next session: actual in-browser playtest to verify the end-to-end flow. If clicks work, the alpha is effectively playable.
