---
name: M15 Aldrich dialogue wasm crash — RESOLVED
description: Same RuntimeError class as M13 but reached through a runtime-loaded Korean dialogue path. Root cause was a Dynamic-atlas NotoSansKR fallback plus a too-loose TMP.HasCharacter check.
type: project
date: 2026-04-12
status: RESOLVED
---

# M15 Aldrich dialogue wasm crash — resolution

## Symptom

Clicking to open the first dialogue with Aldrich (vassal_01) on the live WebGL
build triggered a `window.alert` with:

```
RuntimeError: null function or function signature mismatch
  at TMP_FontAsset_TryAddCharacterInternal
  at TMP_FontAssetUtilities_GetCharacterFromFontAsset_Internal
  at TMP_Text_GetTextElement
  at TMP_Text_ParseInputText
  at TextMeshProUGUI_SetArraySizes
  at TextMeshProUGUI_OnPreRenderCanvas
  at TextMeshProUGUI_Rebuild
  at dynCall_viii
  at CanvasUpdateRegistry_PerformUpdate
```

Same class of crash as M13 (partially-stripped FreeType bridge on IL2CPP
WebGL), but reached through a different path: runtime-loaded Korean strings
from `Resources/Dialogue/dialogue_lines.json` fed to a TMP label.

## Diagnosis

- **Font asset at fault**: `Assets/Fonts/NotoSansKR SDF.asset` —
  `m_AtlasPopulationMode: 1` (Dynamic), `m_GlyphTable: []`,
  `m_CharacterTable: []`. Wired as the second entry of
  `LiberationSans SDF.asset`'s `m_FallbackFontAssetTable`.
- **TMP label at fault**: the `DialoguePanel` (NPCInteractionUI) chat bubble,
  which receives the `LocalDialogueBank.GetRandom(...)` Korean greeting when
  the user clicks a Vassal NPC. Also in the same code path: the NPC profession
  label, "You approach {name}…", and the idle-task label — all fed from
  `LocalizationManager.Get(...)` → `ko.json`.
- **Why the guard failed**: both `LocalizationManager.CanRenderCJK()` and
  `LocalDialogueBank.CanRenderHangul()` used
  `TMP_FontAsset.HasCharacter('가', searchFallbacks: true)`. Against a
  Dynamic-atlas fallback, TMP reports `true` for ANY Unicode codepoint — it
  trusts the rasterizer to supply the glyph on demand. At render time that
  trust routes through `TMP_FontAsset.TryAddCharacterInternal` → FreeType
  bridge (partially stripped by IL2CPP WebGL) → `null function` wasm crash.
- **Why M13's LocalDialogueBank gate didn't save us**: M13's commit
  `2a2e7d3` wired the `CanRenderHangul` probe specifically to stop this
  crash, but used the same too-loose `HasCharacter(searchFallbacks: true)`
  check. On a machine where the NotoSansKR dynamic fallback was wired, the
  check returned true and Korean lines flowed through anyway.

## Fix (option B — trim the fallback, option (B+) harden the guard)

Commit `96be5aa` + `329f8c2`:

1. **Remove NotoSansKR SDF from LiberationSans SDF's
   `m_FallbackFontAssetTable`**. The remaining entry
   (`LiberationSans SDF - Fallback`) is static and harmless.
2. **Rewrite `CanRenderCJK` / `CanRenderHangul` to walk the chain manually**
   via `HasCharacterInStaticChain(...)`. A glyph only counts as "supported"
   if it literally sits in a Static atlas's `characterLookupTable`. Dynamic
   atlases are treated as "no" regardless of what TMP reports. Belt against
   anyone re-adding a Dynamic fallback in the future.
3. **`CastleViewUI.TestOpenNPCDialogue(string)`** — public hook so
   `live_test.js` can drive `NPCInteractionUI.OpenForNPC` via `SendMessage`
   without depending on fragile NPC-card click coordinates.
4. **Compile fix**: `AtlasPopulationMode` lives in the `TMPro` namespace in
   Unity 2022.3 + TMP 3.0.6, not `UnityEngine.TextCore.LowLevel`. Initial
   fix pass used the wrong qualifier and broke the CI build; follow-up
   commit `329f8c2` dropped it.

### What is NOT fixed yet (known followup)

The game currently forces Korean locale to English on WebGL because the
guards correctly see no static Hangul atlas. To actually render Korean UI,
`Assets/Editor/CJKFontSetup.cs` needs to be rewritten to:

- Create the TMP_FontAsset with `AtlasPopulationMode.Static`
- Pre-bake the full U+AC00..U+D7A3 Hangul Syllables block
  (+ common Latin + punctuation the Korean translations use)
- Commit the resulting atlas PNG to git

That's a Unity Editor batchmode task for another session. Until then, Korean
users see the English UI — which is a regression vs the broken "Korean-
crashing" state, but the English UI actually works.

## Verification

### `live_test.js` extensions (same commit)

- **ko-KR browser locale** via `browser.newContext({ locale: 'ko-KR' })`.
  Makes `Application.systemLanguage` return `Korean` on the Unity side,
  which exercises the localization pipe that crashes on a broken build.
  Without this the default en-US locale silently skipped the Hangul code
  path and reported "all good" against the crashing build.
- **`script.onload` interception** in `page.addInitScript` to wrap
  `createUnityInstance` with an "instance stasher". Unity 2022+ declares
  `createUnityInstance` as a function declaration which replaces any
  preinstalled accessor via `[[DefineOwnProperty]]`, so a plain setter
  trap on `window` never fires. Hooking `HTMLScriptElement.prototype.onload`
  runs our wrap-install immediately before the template's `onload` calls
  `createUnityInstance(canvas, config, ...)`, catching the Promise and
  stashing the resolved instance onto `window.unityInstance`.
- **New step 4**: after the castle has loaded, dispatch
  `SendMessage('CastleViewPanel', 'TestOpenNPCDialogue', 'vassal_01')` and
  wait 8 s for the Canvas rebuild. This is the exact user-reported path.

### Results

New deploy (after commits `96be5aa` + `329f8c2`, CI run `24287871612`):

```
Unity state: {"hasUnityInstance":true,"hasUnityGame":false,"foundUnity":true,
              "hasSendMessage":true,"lang":"ko-KR","sent":true}
═══ SUMMARY ═══
Total console messages: 77
Page errors: 0
Unity dialogs: 0
Console errors: 0
```

Relevant Unity console log excerpts:

```
[Localization] Detected Korean but no CJK font fallback is wired. Defaulting to English.
[Localization] Loaded 223 strings for English
[NPCManager] Initialized 4 starting NPCs
[CastleView] TestOpenNPCDialogue(vassal_01) — live_test hook
[LocalDialogueBank] Loaded 1000 pre-generated lines for 4 roles
[LocalDialogueBank] Hangul fallback font not present — Korean lines disabled
```

Every path that would have fed Hangul into TMP now either (a) has been
swapped to English by the LocalizationManager safety belt, or (b) has been
gated to null by LocalDialogueBank.

### Pre-fix reproducibility note

I did not run the extended live_test against the old deploy because only one
deploy exists at a time on `taeshin11.github.io`. The extended
`TestOpenNPCDialogue` hook didn't exist on the old build anyway, so
`SendMessage` would've silently no-op'd. What's verifiable: the bug mechanism
(Dynamic fallback + too-loose HasCharacter check) is visible in the git diff,
and the M13 writeup in `milestone_13_wasm_crash_resolved.md` already
documented the exact same crash class for the scene-baked case.

## Followups

1. **Proper static-baked NotoSansKR atlas** — rewrite `CJKFontSetup.cs` to
   generate a Static atlas with U+AC00..U+D7A3 pre-baked, re-wire as
   fallback, commit the atlas PNG. Then Korean users actually see Korean.
2. **TMP_InputField hardening** — the NPCInteractionUI command input is a
   TMP_InputField. If a user types Korean characters into it while the
   CJK font setup is still broken, the same crash class will fire. Either
   guard the input field to only accept ASCII, or install the static
   Hangul atlas first.
3. **Audit every locale** — `ja.json`, `zh.json` have CJK strings too. The
   current fix only tested Korean. Japanese/Chinese go through the same
   safety belt so should be fine, but deserve an explicit ko/ja/zh test
   matrix in live_test.
