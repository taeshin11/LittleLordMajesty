# Milestone 05 — Agent Team Bug Fixes + Steam/PC Support

**Date:** 2026-04-09
**Status:** Complete

## Agent Team Review Results

4 specialized agents (Quality Tester, Backend Engineer, UI Engineer, Code Reviewer) reviewed the codebase and found **28 issues** (8 Critical, 12 High, 8 Medium).

---

## Bugs Fixed This Milestone

### Critical Fixes

| File | Bug | Fix |
|------|-----|-----|
| `NPCManager.cs` | `_gemini` used without null check — NullReferenceException crash | Added null guard + fallback response |
| `NPCManager.cs` | `_conversationStates[npcId]` throws KeyNotFoundException | Replaced indexer with `TryGetValue` |
| `NPCManager.cs` | `WorldPosition` (Vector3) never written/read in save data | Fixed `GetSaveData` / `LoadSaveData` to use `WorldPosition` |
| `SaveSystem.cs` | `static readonly` paths init before Unity engine ready | Changed to lazy `=>` properties |
| `SaveSystem.cs` | Hardcoded `persistentDataPath` — wrong on PC | Routed through `PlatformManager.SaveDirectory` |
| `DiplomacySystem.cs` | `InitializeDiplomacy()` in Awake — WorldMapManager not ready yet | Moved to `Start()` |
| `EventManager.cs` | `"success"` string match causes false positives (e.g. "no success") | Strict JSON key match + semantic exclusion |
| `CastleScene3D.cs` | `new Material(Shader.Find())` called per-object at scene load | Cached `_sharedMaterial` static field; `new Material(base)` per-object |

### High Fixes

| File | Bug | Fix |
|------|-----|-----|
| `GameManager.cs` | `TogglePause` always restored to `Castle` state | Added `_prepauseState` field; restores correct state |
| `GameManager.cs` | `DayCycleCoroutine` not stopped in `OnDestroy` | Added `_dayCycleCoroutine` reference + `StopCoroutine` in `OnDestroy` |
| `CastleScene3D.cs` | Event subscriptions (`OnNPCAdded`, `OnBuildingConstructed`) never unsubscribed | Added unsubscription in `OnDestroy` |
| `CastleScene3D.cs` | `FindObjectOfType<NPCInteractionUI>` on every NPC tap | Cached in `Start()` + lazy refresh |
| `NPC3DClickHandler` | `FindObjectOfType<NPCInteractionUI>` in `OnMouseDown()` (per-click) | Cached in `Awake()` |

---

## Steam / PC Support Added

### New Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Core/PlatformManager.cs` | Platform detection: Mobile vs Desktop. Platform-specific save paths, resolution, touch detection |
| `Assets/Scripts/Input/KeyboardShortcuts.cs` | PC keyboard hotkeys (Esc, B, M, N, Tab, F5/F9, F11, `) |

### Keyboard Shortcuts (PC)

| Key | Action |
|-----|--------|
| `Esc` | Close dialogue / Pause |
| `B` | Toggle Build menu |
| `M` | World Map |
| `N` | NPC list |
| `Tab` | Cycle NPCs |
| `F5` | Quick save |
| `F9` | Quick load |
| `F11` | Toggle fullscreen |
| `` ` `` | Debug console |

### Platform-Aware Save Paths

| Platform | Save location |
|----------|---------------|
| Windows | `%APPDATA%/LittleLordMajesty/` |
| macOS | `~/Library/Application Support/LittleLordMajesty/` |
| Android/iOS | `Application.persistentDataPath` |

---

## Known Issues Still Open (Next Milestone)

| Issue | Priority |
|-------|---------|
| SettingsUI SerializedFields not wired by SceneAutoBuilder | High |
| ToastNotification prefab + container not wired | High |
| Chat bubble prefabs (PlayerMessage, NPCMessage) not assigned | High |
| UIManager resource bars look for TMP in `TopHUD` but they live in `ResourceStrip` | High |
| Building state / diplomacy state not saved across sessions | High |
| Gemini API rate limiting / request queue missing | Medium |
| Active events lost on app background/resume | Medium |
| LocalizationManager: `Resources.Load` on every key miss | Medium |
| UI layout not adapted for PC landscape (1920×1080) | Medium |

---

## Build Targets

Current: Android, iOS
Added: StandaloneWindows64, StandaloneOSX (via PlatformManager; build profile config needed in Unity Editor)

Steam integration: Pending — requires Facepunch.Steamworks package import after Unity is installed.
