---
name: M13 WASM gear crash — resume session log
description: Continuation of session_handoff_m13_wasm_gear.md. Live re-test shows gear warning gone but null-function crash persists. New API key also leaked. Two parallel agents dispatched.
type: project
date: 2026-04-11
---

# M13 WASM Crash — Resume Session

## Re-test of `4c9f7af` (this session)
Ran `tools/playwright_test/live_test.js`. Results vs prior handoff:

| Symptom | Prior session | This run |
|---|---|---|
| `\u2699` TMP warning | YES | **GONE** |
| `null function` page error | YES | **STILL THERE** |
| `[GeminiImage] HTTP 403` | gone | **BACK** — new key also leaked |
| `unityInstance not detected within 120s` | n/a | **YES** (WebGL slow init) |

## Updated findings

1. **Gear glyph fix WORKED.** Three independent rg/python sweeps across `Assets/`, `Packages/`, `ProjectSettings/`, `Assets/TextMesh Pro/`, all `.prefab`/`.unity`/`.asset`, and all locale JSON files turned up zero `\u2699` outside of code comments. The handoff doc itself was the only file containing it.
2. **null-function crash is decoupled from gear warning.** Real cause is elsewhere — top hypotheses:
   - WebGL bundle not actually loaded when click fires (unityInstance probe timed out)
   - IL2CPP stripping in TMP/Unity assembly (no `Assets/link.xml` exists, TMP fallback list empty in `Assets/TextMesh Pro/Resources/TMP Settings.asset`)
   - Click handler reaching a stripped virtual via TMP_Text setter
3. **TMP fallback array is empty** in `TMP Settings.asset` line 32. Adding `LiberationSans SDF - Fallback` to fallbacks may fix any residual missing-glyph crashes.
4. **`managedStrippingLevel: 0` for WebGL** per `ProjectSettings.asset` line 780 — but `link.xml` is still worth adding to belt-and-suspenders TMP.
5. **CI workflow has no Library/Artifacts cleanup.** `actions/checkout@v4 clean: true` does NOT clean Unity's Library folder on the self-hosted runner at `C:\actions-runner\_work\...\Library\`.
6. **API key #2 (`AIzaSyBlEtU7ugG49P8KHM0ekpHGZOMu7e45Gro`) also leaked.** Google flagged it. Needs rotation, but unrelated to wasm crash.

## Plan
Two autonomous agents running in parallel:
- **Agent A (WASM-fix loop)**: iterate edit→commit→push→wait for CI→run live_test→read log→edit. Stop when `null function` is gone.
- **Agent B (UI/UX polish)**: independently improve the menu/HUD layout, spacing, contrast, button affordances. No overlap with crash files.

Both agents will append their findings to this file when they finish.

## UI/UX Polish Pass (Agent B)

### Problems found
1. **Non-ASCII glyphs still baked into `SceneAutoBuilder.BuildCastleViewPanel`** — four emoji (wood log, wheat, money bag, busts) on the resource strip defaults, a full Korean objective sentence at the top of the castle view, and a `♥` heart glyph on the loyalty label. Every one of these misses from `LiberationSans SDF` and is exactly the kind of string that trips the WebGL IL2CPP TMP dynamic-fallback null-function crash. A second copy of the wood/gold emoji lived in `CastleViewUI.BuildBuildingCard`.
2. **Ugly ASCII placeholders for "icon" buttons** — `*` for Options, `=` for Pause/Menu, `X` for Close (x2 — NPC dialogue and Settings), `>` for Send, `< Castle` for World-Map back button.
3. **Action-bar button sizing was inconsistent** — Options and Menu were crammed into 90×70 slots while the other four buttons were 150×70, so the row looked lopsided and the small ones truncated their labels.
4. **`CreateButton` produced flat unoutlined buttons** with no disabled / selected ColorBlock states, a hardcoded 28pt label, and no horizontal padding — long labels like "Launch Siege" or "Neues Spiel" or "Back to Castle" clipped the edge.
5. **Baked button labels never got localized at runtime.** `MainMenuUI` and `PauseUI` overwrite their own titles/labels at `Start()`, but `CastleViewUI` never touched its action bar, so ko/ja/zh/de/es/fr players saw "Build", "Save", "NPCs", "Map" in English.
6. **Resource HUD dropped its prefix after the first update tick** — the initial scene-baked "Wood 500" got replaced by just "500" on the next OnResourceChanged event, so the player had to guess which column was which after 1s.
7. **Missing loc keys** for btn_build/save/npcs/map/options/pause/send/back_to_castle/respond/talk and hud_loyalty/loading/thinking/voice across all seven locale files.

### Fixes applied
- **Commit 1 `99ba4d9` — purge non-ASCII glyphs from castle HUD + menu placeholders.** Emoji resource prefixes → "Wood"/"Food"/"Gold"/"Pop" words. Korean objective → English default. `♥` → "Loyalty". `* / = / X / > / < Castle` → Options / Pause / Close / Send / Back to Castle. Uniform 150×72 action-bar slots. Also fixed the wood/gold emoji in `CastleViewUI.BuildBuildingCard` cost row.
- **Commit 2 `6ed7c96` — button polish (outline, autosize, focus states) + localized HUD.** `CreateButton` now paints a 1.5px dark outline on every button, uses TMP auto-sizing 14..26 with bold white text and 10px horizontal inset, and wires a full ColorBlock (normal/highlighted/pressed/selected/disabled). `CastleViewUI.SetupButtons` reapplies localized labels to the six action-bar buttons at Start() via a new `SetButtonLabel` helper. Resource HUD now prepends the localized resource name on every update tick via `FormatNamedResource(locKey, value)`, so the columns stay readable over time.
- **Commit 3 `3042b5e` — add localization keys for castle HUD buttons and resource labels.** 15 new keys added to `en.json` / `ko.json` / `ja.json` / `zh.json` / `de.json` / `es.json` / `fr.json` (all seven locale files). All JSON validates.

### Files touched
- `Assets/Editor/SceneAutoBuilder.cs`
- `Assets/Scripts/UI/CastleViewUI.cs`
- `Assets/Resources/Localization/{en,ko,ja,zh,de,es,fr}.json`

### Not touched (other agent owns these this session)
- `Assets/link.xml`, `.github/workflows/build-local.yml`, `Assets/TextMesh Pro/Resources/TMP Settings.asset`, `Assets/Scripts/Core/GameManager.cs`, `tools/playwright_test/live_test.js`.

### Commit hashes
- `99ba4d9` ui: purge non-ASCII glyphs from castle HUD + menu placeholders
- `6ed7c96` ui: button polish (outline, autosize, focus states) + localized HUD
- `3042b5e` ui: add localization keys for castle HUD buttons and resource labels

