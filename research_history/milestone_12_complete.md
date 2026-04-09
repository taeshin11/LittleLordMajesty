# Milestone 12 — Complete: Steam, Tutorial, LLM Streaming, Bug Fixes & Agent Audit

**Date:** 2026-04-09
**Commits:** 793a6ac → e1d293a (7 commits across Phases A–C)

---

## Summary

M12 delivered the three foundational pillars needed before alpha playtest: Steam integration, an in-game tutorial, and LLM streaming with model upgrade. A full 6-agent audit cycle caught and resolved 32 bugs on top of those features.

---

## Phase A — Steam + Build Pipeline + Error Handling

### SteamManager.cs
- Integrated Facepunch.Steamworks with `#if USE_STEAM` compile guard
- Defined 10 achievement identifiers; `UnlockAchievement()` helper
- Graceful no-op when Steam client is not running (dev builds safe)

### ErrorHandler.cs
- Hooked `Application.logMessageReceived` for global exception capture
- Writes `crash_log.txt` to `persistentDataPath` on first unhandled exception
- Shows recovery UI overlay (localized) without crashing the process

### CI — Self-Hosted Runner
- `.github/workflows/build-local.yml`: self-hosted runner on dev machine
- `SceneAutoBuilder.cs` gained `BuildWebGL()` and `BuildWindows()` static entry points callable from CLI (`-executeMethod`)
- WebGL Build Support module installed; 7-zip installed for artifact compression
- Last successful build: GitHub Actions run 24170875761, WebGL in 5 m 57 s

---

## Phase B — Tutorial System

### TutorialSystem.cs
- Added `DontDestroyOnLoad` so tutorial state survives scene transitions
- `SkipTutorial()` → calls `OnTutorialComplete()`; `ResetTutorial()` clears PlayerPrefs
- 7-step tutorial flow, each step gated by a trigger string

### TutorialUI.cs (new file)
- Full-screen overlay with typewriter effect via `maxVisibleCharacters`
- Highlights (pulsing border) on target UI elements
- Skip button wired to `TutorialSystem.SkipTutorial()`

### Gameplay Hooks
| Script | Trigger |
|--------|---------|
| `GameManager.NewGame()` | Calls `ResetTutorial()` then `StartTutorial()` |
| `UIManager.OpenDialogue()` | Completes step "talk_to_aldric" |
| `UIManager.SendCommand()` | Completes step "issue_command" |
| `BuildingManager.TryBuild()` | Completes step "build_farm" |

---

## Phase C — LLM Optimization

### GeminiAPIClient.cs
- **Model upgraded:** `gemini-1.5-flash` → `gemini-2.0-flash-lite`
- Uses `system_instruction` field natively (Gemini 2.0 API spec)
- `SendMessageStreaming()` added: SSE-based streaming, first token < 500 ms typical
- `NullValueHandling.Ignore` on JSON serialization to reduce payload size

### NPCPersonaSystem.cs
- `ConversationSummary` field added to persona data
- `CompressHistory()`: when history exceeds 10 entries, summarizes oldest 8 into one summary entry
- `GetContextualHistory()`: returns summary + recent turns, capping context window

### NPCManager.cs
- Replaced full history pass with `GetContextualHistory()` to stay within token limits

---

## 32-Bug Fix Sprint (Agent QA/CodeReview/Backend Audit)

Highlights from three audit batches:

| # | File | Bug | Fix |
|---|------|-----|-----|
| 1 | `MonetizationManager.cs:84` | Inverted logic drained scrolls instead of adding | Flipped sign |
| 2 | `GameManager.cs:87` | `WorldMapManager` never initialized via `AddComponent` | Added component in `InitializeSystems()` |
| 3 | `CastleViewUI.cs` | `FindObjectOfType` on every NPC button click | Cached in `Start()` |
| 4 | `GameBootstrap.cs:96` | Created managers without `DontDestroyOnLoad` | Captured `go`, applied DDOL |
| 5–32 | Various | Memory leaks, division-by-zero, null refs, backend path bugs | See commits c10138b, d114873, 1c58c44 |

---

## Completion Status vs M12 Plan

| Item | Plan | Actual |
|------|------|--------|
| Steam integration | P0 | Done |
| Windows CI build | P0 | Done |
| Global error handler | P0 | Done |
| Tutorial (7 steps) | P0 | Done |
| LLM streaming | P1 | Done |
| Memory/context compression | P2 | Done |
| Gemini model upgrade (2.0-flash-lite) | — | Done (bonus) |
| 32 bug fixes | P1 | Done |
| Alpha playtest (10x no error) | P0 | **Not yet — M13 P0** |
| Steam metadata | P0 | **Not yet — M13 P1** |
| Kenney 3D assets / animations | P1 | **Not yet — M13 P2** |

---

## Build State at M12 Close

- Compile: no errors
- CI: WebGL build passing on self-hosted runner
- Gemini API key: set in `GameConfig.asset` (gitignored)
- Runner: installed at `C:\actions-runner\`, auto-starts via Startup shortcut

---

## Known Gaps / Carry-Forwards

- `SaveSystem.cs` does not have a `TutorialCompleted` field — tutorial state lives in PlayerPrefs only
- Steam achievement IDs need Steamworks Partner dashboard registration
- Alpha playtest (Bootstrap → Castle → Tutorial → NPC → WorldMap flow) not yet executed
