---
name: M13 WASM null-function crash — RESOLVED
description: Multi-day "RuntimeError null function" hunt. Root cause was a Korean greeting YAML-escaped into Game.unity that hit TMP_FontAsset.TryAddCharacterInternal on the missing Hangul glyph, crashing the IL2CPP WebGL FreeType bridge.
type: project
date: 2026-04-11
status: RESOLVED
---

# M13 WASM null-function crash — resolution

## TL;DR

**Root cause**: `Assets/Scenes/Game.unity` had the Korean welcome message
"영주님의 성에 오신 것을 환영합니다…" baked into the `ObjectiveText` TMP label,
stored as YAML `\uC601\uC8FC…` escapes. Every byte-level "find non-ASCII"
scan in 4 prior debug sessions missed it because the file bytes are pure
ASCII (the YAML deserializer expands the escapes at load time).

**Why it crashed**: LiberationSans SDF has no Hangul glyphs. On the first
Canvas rebuild after `New Game`, TMP walked the fallback chain looking
for `U+C601`, didn't find it, fell through to
`TMP_FontAsset.TryAddCharacterInternal`, which calls into the FreeType
bridge — partially stripped on IL2CPP WebGL → `RuntimeError: null
function or function signature mismatch`.

**The fix**: empty the `m_text` line in `Game.unity:10270`. Single line
change. Commit `2b0b82f`.

## What unblocked it

The breakthrough was three diagnostic improvements, not bisect attempts:

1. **WebGL debug symbols** (`webGLDebugSymbols: 1` in
   `ProjectSettings.asset`). Turned the wasm stack from
   `wasm-function[62308]` into `TMP_FontAsset_TryAddCharacterInternal`.
   Without this, 18 prior bisect iterations never knew which TMP code
   path was crashing.

2. **`page.on('dialog')` handler** in `tools/playwright_test/live_test.js`.
   Unity's runtime errors are emitted via `window.alert(stack)`. Playwright
   auto-dismissed them silently. Adding the dialog handler captured the
   full multi-line stack, which we then matched against function indices
   once symbols were on.

3. **Runtime TMP label dump** in `CastleViewUI.Start`:
   ```csharp
   var tmps = GetComponentsInChildren<TMPro.TMP_Text>(true);
   foreach (var t in tmps) {
       int firstNonAscii = -1; int firstCp = 0;
       for (int j=0;j<t.text.Length;j++)
           if (t.text[j]>=128) { firstNonAscii=j; firstCp=t.text[j]; break; }
       Debug.Log(firstNonAscii>=0
           ? $"TMP[{i}] {t.name} NON-ASCII at {firstNonAscii} (U+{firstCp:X4}): \"{t.text}\""
           : $"TMP[{i}] {t.name} ascii: \"{t.text}\"");
   }
   ```
   First run after symbols + dialog handler: TMP[6] ObjectiveText flagged
   `NON-ASCII at 0 (U+C601)`. Caught the bug in one iteration.

## Why prior sessions missed it

Sessions 1-3 ran 18 bisect iterations totaling ~3 hours of CI cycles, each
testing a hypothesis about *what code path* was crashing. None of them
asked "what string is being parsed?" because byte-level scans of all
source/scene files reported zero non-ASCII characters in `m_text:` lines.

The Korean was stored as `m_text: "\uC601\uC8FC..."` — these characters
are all U+0030-U+007A in the file. ASCII bytes. Only the YAML loader
turns them into U+C601 etc. when Unity opens the scene at runtime. So:

- `rg "[^\x00-\x7f]" Assets/Scenes/Game.unity` → 0 hits ✓
- Python byte scan → 0 hits ✓
- But the runtime TMP label has full Korean text 💥

**Lesson**: when scanning Unity scene/prefab files for non-ASCII content,
also grep for `\\u[0-9a-fA-F]{4}` to catch YAML-escaped Unicode.

## Other things we changed (kept — they're real improvements)

These were applied during the hunt before the real cause was found. None
were necessary for the fix, but all are reasonable hardening:

| Commit | Change | Why kept |
|---|---|---|
| `f0ec949` | `Assets/link.xml` preserving TMP/UI/InputSystem | Belt-and-suspenders against IL2CPP stripping. Cheap. |
| `5973a1f` | `webGLDebugSymbols: 1` | Worth the binary size for any future wasm crash. |
| `40f9206` | All 58 baked TMP `m_isRichText: 0` + procedural labels also off | Eliminates an entire class of TMP rich-text parser crashes. None of our labels use `<color>/<b>` tags. |
| `d8898f6` | `m_GetFontFeaturesAtRuntime: 0`, `m_missingGlyphCharacter: 32`, `m_warningsDisabled: 1`, fallback list populated | Disables FreeType runtime feature lookups; renders space for missing glyphs instead of warning storm. |
| `fc1c928` | LiberationSans SDF Fallback set to Static atlas mode | Prevents the dynamic-add path from ever being reachable via fallback chain. |

These could safely be reverted but I'd recommend keeping them.

## Things to clean up next session

- Remove the `[Crash-Bisect]` Debug.Log instrumentation in
  `CastleViewUI.Start`, `GameManager.Update`, `GameManager.LateUpdate`,
  `Canvas.willRenderCanvases` hook.
- Remove the `#if UNITY_WEBGL` skip guards Agent A added for
  `StartTutorial`, `ShowWelcomeHint`, `PopulateNPCList` deferred
  coroutine — they're no longer necessary (the real crash is gone).
- Re-add Outline + TMP autosize to `CreateButton` in
  `SceneAutoBuilder.cs` if the UX tradeoff is worth it (Agent B added
  them, I disabled them as a wrong hypothesis test — they were never the
  problem).
- Consider scanning every other scene/prefab for YAML-escaped non-ASCII:
  `grep -rE '\\u[0-9a-fA-F]{4}' Assets/Scenes Assets/Resources/Prefabs`.

## Verification

```
$ node tools/playwright_test/live_test.js
═══ SUMMARY ═══
Total console messages: 116
Page errors: 0   ← was 1 before
Unity dialogs: 0 ← was 1 before
Console errors: 3 (all Gemini API key 403, unrelated)
[LiveTest] Done.
```

`grep TMP\\[ tools/playwright_test/screenshots/console.log` shows all 14
CastleViewPanel TMP labels are now `ascii: "..."`.

## Reproducer for the fix (if it ever returns)

```bash
cd C:/MakingGames/LittleLordMajesty
grep -nE '\\u[0-9a-fA-F]{4}' Assets/Scenes/Game.unity
# any hit is suspicious — likely a baked CJK string
node tools/playwright_test/live_test.js
grep -E 'NON-ASCII' tools/playwright_test/screenshots/console.log
# any hit is the bad label
```
