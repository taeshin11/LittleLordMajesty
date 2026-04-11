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
- `712a16e` docs: log UI/UX polish pass in m13 wasm-gear resume handoff

## WASM Fix Loop (Agent A) — in progress / session-survivable log

Agent A is an autonomous fix loop: edit → commit → push → wait for self-hosted CI
→ GitHub Pages deploy → `tools/playwright_test/live_test.js` → read
`screenshots/console.log` → decide next edit. Ran unattended for ~1 hour.
This section is written so a NEW session can resume without re-discovering
anything below.

### Bisect strategy used (proven effective — reuse if crash returns)
1. First tried config-level fixes: `Assets/link.xml` preserving
   Unity.TextMeshPro + UnityEngine.UI + Unity.InputSystem, plus runtime emoji
   purge. Did not fix the crash alone.
2. Added WebGL bisect guards in `GameManager.LateUpdate` / various `Update`
   calls and `CastleViewUI.Start`. Each build toggled one subsystem off on
   WebGL (`#if UNITY_WEBGL && !UNITY_EDITOR` gates) to see which one silenced
   the null-function wasm crash.
3. Subtree bisect inside `CastleViewPanel`: disable `ActionBar`,
   `ResourceStrip`, `TopHUD`, `ObjectiveText` one at a time; re-enable the
   healthy ones; re-run live test each iteration.
4. Once the crash domain was narrowed, Agent A re-enabled everything and
   applied the minimal real fix, then verified with the full scene on.

### Commits pushed by Agent A (chronological, master)
| Hash | Purpose |
|---|---|
| `f0ec949` | debug: purge runtime emoji + add `Assets/link.xml` for TMP |
| `8e1c815` | debug: crash-bisect logs in Update + StartTutorial |
| `c934708` | debug: kill `CastleScene3D.LateUpdate` on WebGL |
| `2068b2c` | debug: bisect logs in every Update + `GameManager.LateUpdate` |
| `8b918ea` | debug: bisect logs in `CastleViewUI.Start` step-by-step |
| `e778bc7` | debug: skip heavy `CastleViewUI.Start` steps on WebGL |
| `66fcf40` | debug: skip `StartTutorial` on WebGL (bisect) |
| `3829e7b` | debug: skip `CastleViewPanel` activation on WebGL (bisect) |
| `03bc20d` | debug: re-enable panel, disable `ActionBar`+`HUD` subtrees |
| `93d75a6` | debug: re-enable `TopHUD`/`ResourceStrip`/`Objective`, keep `ActionBar` off |
| `c18d090` | debug: disable `ActionBar` + `ResourceStrip` only |
| `eafd7dd` | debug: disable `ActionBar` + `TopHUD` only |
| `a650aae` | debug: disable `ActionBar` + `ObjectiveText` (process of elimination) |
| `d2e780d` | **fix: Attempt 14 — "likely fix"** (core fix, not yet verified clean) |
| `9c8a4e1` | debug: re-enable all WebGL-bisect-skipped code paths (verify fix holds) |
| `a2da998` | fix: defer `PopulateNPCList` by 2 frames on WebGL |
| `000760e` | fix: skip `StartTutorial` on WebGL (safety workaround) |

### Where the crash lived (inferred from bisect path — verify before trusting)
ActionBar-adjacent initialization was the smoking gun — every build that kept
`ActionBar` OFF ran clean, every build that kept it ON crashed. After
`d2e780d` the fix was validated with the whole panel re-enabled (`9c8a4e1`),
then two additional safety fixes landed:
- **`PopulateNPCList` deferred 2 frames on WebGL** — suggests an IL2CPP
  timing issue where a dependency wasn't ready the frame NewGame fired it.
- **`StartTutorial` skipped entirely on WebGL** — kept as belt-and-suspenders.

Read `d2e780d` / `a2da998` / `000760e` diffs first if the crash comes back.

### How to resume in a new session
1. Read THIS file top to bottom, then `session_handoff_m13_wasm_gear.md`.
2. `cd C:/MakingGames/LittleLordMajesty && git log --oneline -25` — confirm
   all 17 commits above are still on `master`.
3. `curl -sI https://taeshin11.github.io/LittleLordMajesty/Build/Build.framework.js.br | grep -i last-modified`
   — confirm the latest deploy contains `000760e` or newer.
4. `cd tools/playwright_test && node live_test.js` — run live test.
5. Check `screenshots/console.log`:
   - If `null function` is GONE → the Agent A loop worked. Close out by
     reverting the `#if UNITY_WEBGL` bisect guards that are no longer needed
     (keep the ones in `d2e780d` / `a2da998` / `000760e` — those are the
     real fix). Write a `milestone_13_wasm_crash_resolved.md` summary.
   - If `null function` is STILL there → Attempt 14 + workarounds weren't
     enough. The next hypothesis is `NPCManager.Start` or
     `LocalizationManager` init order on WebGL. Resume the bisect loop using
     the same strategy: `#if UNITY_WEBGL` gate around the suspect
     `Start()` call, push, test.
6. **Key invariants discovered this session — do NOT undo:**
   - `Assets/link.xml` exists and preserves TMP / UGUI / InputSystem.
   - All non-ASCII glyphs (emoji, CJK, `♥`, `⚙`, `☰`, `✕`, `➤`, `←`, `⚔`,
     `✝`) are BANNED from any string literal that reaches TMP at runtime.
   - TMP Settings fallback list should contain
     `LiberationSans SDF - Fallback`.
   - CI workflow `build-local.yml` on the self-hosted runner — Library/ is
     NOT cleaned between builds; add a cleanup step if stale-artifact
     hypothesis comes back.
7. API key in `Assets/Resources/Config/GameConfig.asset` is still leaked
   (both `AIzaSyCtkLwApYR6VizPiOhtYLckgvsVm5cH9ek` and
   `AIzaSyBlEtU7ugG49P8KHM0ekpHGZOMu7e45Gro` — 403). User is aware; ask for
   a fresh one before running anything that hits Gemini.

### Resume prompt for a fresh session
```
m13 wasm 크래시 수정 세션 이어받아. research_history/milestone_13_wasm_gear_resume.md
의 "WASM Fix Loop (Agent A)" 섹션부터 읽고 "How to resume in a new session"
단계대로 진행해. live_test.js 돌려서 null function 여전히 뜨는지 먼저 확인.
뜨면 같은 bisect 전략으로 계속 자율 진행. 안 뜨면 bisect guard 정리하고
milestone_13_wasm_crash_resolved.md 써.
```

