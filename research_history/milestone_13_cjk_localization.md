# M13 Progress — CJK Font Fallback & Hardcoded Text Purge

**Date:** 2026-04-10
**Trigger:** User ran WebGL alpha build in Korean locale, saw title + 3 buttons rendering as empty boxes (□□). "New Game" rendered in English because the scene prefab's TMP child text hadn't been overridden yet.

---

## Root Cause

`LocalizationManager.DetectSystemLanguage()` auto-detects the browser/system language. User's system is Korean → loaded `ko.json` → fed Korean strings (`"리틀 로드 마제스티"`, `"이어하기"`, `"설정"`, `"종료"`) into TMP labels. But the only TMP font asset in the project was `LiberationSans SDF` (Latin-only) with no CJK fallback. Every Korean character became a missing-glyph □.

Screenshot box counts matched ko.json strings exactly: title was 2+2+4 boxes = `리틀 로드 마제스티`, navy button 4 = `이어하기`, gray 2 = `설정`, red 2 = `종료`.

---

## Fixes Applied

### 1. CJK Font Fallback (Core Fix)

- Downloaded **Noto Sans KR (Korean subset OTF)** from `notofonts/noto-cjk` → `Assets/Fonts/NotoSansKR-Regular.otf` (4.6 MB).
  - Subset-OTF (Korean only) chosen over Pan-CJK (16 MB) to keep WebGL payload reasonable.
- Created `Assets/Editor/CJKFontSetup.cs` with:
  - `[LittleLordMajesty/Generate CJK Font Asset]` menu item
  - Static `GenerateFontAsset()` entry point callable via `-executeMethod` for CI
  - Creates a dynamic TMP_FontAsset (1024×1024 SDF atlas, `AtlasPopulationMode.Dynamic`) so glyphs get rasterized on demand — no need to pre-bake the ~2350 Hangul syllable range.
  - Wires the new asset into `LiberationSans SDF.fallbackFontAssetTable`, so every existing TMP label falls through to Noto Sans KR for any glyph `LiberationSans` lacks.
- Ran via headless Unity: `Unity.exe -batchmode -executeMethod CJKFontSetup.GenerateFontAsset -quit`. Log confirmed asset creation + fallback wiring.

### 2. Safety Belt in LocalizationManager

Added `CanRenderCJK()` check at the end of `DetectSystemLanguage()`:
- Loads `LiberationSans SDF` via `Resources.Load<TMP_FontAsset>`
- Calls `HasCharacter('가', searchFallbacks: true)` — walks the fallback chain
- If the detected language is CJK but no fallback is wired, logs a warning and reverts to English instead of rendering boxes.
- This is belt-and-suspenders: once `CJKFontSetup.GenerateFontAsset` has run, the guard is a no-op. But on a fresh clone where the font asset hasn't been generated yet, the game won't render empty boxes.

### 3. Hardcoded Text Purge

User feedback: *"text 전부 번역 가능하게 해야 되"* — all user-facing text must be routed through LocalizationManager, no hardcoded English literals. Saved as a persistent feedback memory.

Audit found ~130 hardcoded strings across 11 files. Refactored:

| File | Fix |
|------|-----|
| `MainMenuUI.cs` | Subtitle `"Rule the realm..."` → `Get("menu_subtitle")`. Default name fallback `"Lord"` → `Get("name_default_player")`. |
| `GameBootstrap.cs` | Runtime fallback menu title/subtitle/buttons/info-text all routed through `Get()`. |
| `TutorialUI.cs` | Removed the 14-entry English-fallback switch in `Localize()` — keys now live in `en.json` so `Get()` handles fallback. "Start!"/"Next" button labels → localized. |
| `NPCInteractionUI.cs` | `QuickCommands` dictionary of 16 hardcoded English strings → `QuickCommandKeys` dictionary. Commands localized at button-creation time. Gemini handles multilingual commands natively. |
| `CastleViewUI.cs` | `"Idle"` → `Get("npc_idle")`. `"{name} constructed!"` → `Get("building_constructed", name)`. |
| `WorldMapUI.cs` | `"???"`, `"Enemy"`, scout cost label, army-slider format → all localized. |
| `EventManager.cs` | 4 orc-raid descriptions, conflict/visitor/famine/fire/rebellion/weather-disaster descriptions → all localized. |
| `ResearchSystem.cs` | `Technology.Name`/`Description` fields → `NameKey`/`DescriptionKey` + `LocalizedName`/`LocalizedDescription` computed properties. All 13 tech tree entries updated. Toast + scholar report + event title localized. |
| `ProductionChainManager.cs` | `ProductionNode.Name` → `NameKey` + `LocalizedName`. All 9 chain templates updated. Steward "no LLM" fallback localized. |
| `DebugConsole.cs` | Input placeholder localized. |
| `NPCDailyRoutine.cs` | Kept hardcoded `ActivityDesc` — audit confirmed it's never read outside this file (no player-visible consumer yet). Added TODO comment to localize when the feature is wired to player UI. Activity keys already exist in en.json/ko.json for that future refactor. |

### 4. Localization Files

- `en.json`: +130 new keys (source of truth)
- `ko.json`: +130 new keys with Korean translations (user's test locale)
- `ja.json`: +~60 new keys (Japanese — CJK test target)
- `zh.json`: +~60 new keys (Chinese — CJK test target)
- `fr.json`/`de.json`/`es.json`: +~25 new high-visibility keys (menu + tutorial welcome/complete). Deeper content falls back to English via `Get()`'s built-in fallback — these locales were already sparse pre-M13.

Every key exists in `en.json`, so `Get()` always has something to return.

---

## Build Verification

- `Unity -executeMethod SceneAutoBuilder.BuildWebGL` → **Build Finished, Result: Success** (49.4 MB, 0 compile errors).
- Font asset + fallback wiring + C# refactor all green.
- Next step: user reloads the WebGL build and confirms Korean text renders properly in the main menu.

---

## Follow-ups Deferred to Later M13 Work

- **Full locale completeness**: fr/de/es still sparse — missing ~100 of the new keys. Acceptable because Get() falls back to English. Translators can flesh these out before international release.
- **Bonus field on Technology**: still hardcoded English (`"Soldier attack +15%"`). Not on alpha test path. Will need 26 keys + translations.
- **NPCDailyRoutine ActivityDesc**: ~30 strings still hardcoded. Not player-visible yet. Keys already exist (`activity_*` in en/ko json) — refactor is one-line-per-entry when feature ships.
- **Unlocks / Category / BuildingRequired** strings in tech + production systems: internal IDs, not shown to player directly.
