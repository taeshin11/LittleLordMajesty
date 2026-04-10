# M13 — WebGL WASM Crash Fix + Deep Optimization Pass

**Date:** 2026-04-10 → 2026-04-11
**Trigger:** User clicked "New Game" on the deployed build and hit:
```
RuntimeError: null function or function signature mismatch
at wasm://wasm/08e60b36:wasm-function[26950]:0x909898
...
at invoke_viii (blob:https://taeshin11.github.io/...)
at invoke_iiii (blob:https://taeshin11.github.io/...)
```
Deep WASM stack through IL2CPP indirect-call invokers. The crash fired the instant `CastleViewUI.Start()` ran.

**User directive:** "다 고치고 다 배포하고 알려줘. 코드 최적화 agent 있지? 이거 오픈월드 게임이야" — full autonomy, don't call until everything works, use a code optimization agent because this is an open-world game.

---

## Diagnosis (parallel Explore agent)

Launched an `Explore` subagent while applying fixes to audit the post-redesign files. The agent ranked three suspects:

### #1 CRITICAL — `System.IO.File` in `GeminiImageClient` on WebGL
`GenerateImage()` called `File.Exists(cachedPath)` / `File.ReadAllBytes()` / `File.WriteAllBytes()` / `Directory.CreateDirectory()` on every request. `CastleViewUI.Start()` kicks off 5 concurrent Gemini generations (1 background + 4 NPC portraits) the instant Castle activates, so the first missing-wasm-function invocation hit within the first cache-check path.

Unity's IL2CPP stripper for WebGL aggressively removes `System.IO.File.*` methods because they mostly don't work in a browser sandbox. When stripped, the methods' function pointers become null. When the game tries to call them, WASM's indirect-call dispatcher throws exactly this error: **"null function or function signature mismatch"**.

### #2 HIGH — TMP_Dropdown template with unwired ScrollRect
`SceneAutoBuilder.CreateDropdown` built a template with a `ScrollRect` component but never assigned `scrollRect.content` / `scrollRect.viewport`. When TMP_Dropdown's internal template validation walks the ScrollRect, it hits null references that can cascade into signature-mismatch crashes inside TMPro's runtime code paths.

### #3 HIGH — Deprecated `FindObjectOfType<T>(bool)` overload
Used in `CastleViewUI.Start`, `CastleScene3D.Start`, `MainMenuUI`, `PauseUI`, `TutorialSystem`, and `KeyboardShortcuts`. Unity 2022.3 marks this overload `[Obsolete]` in favor of `FindFirstObjectByType<T>(FindObjectsInactive.Include)`. The deprecated path has known IL2CPP signature-mismatch issues on WebGL.

---

## Fixes — Crash Round (commit `656c3ea`)

### 1. Guarded all `System.IO.File` / `Directory` calls in `GeminiImageClient`
```csharp
#if !UNITY_WEBGL || UNITY_EDITOR
using System.IO;
#endif
```
Plus:
- All `File.*` and `Directory.*` call sites wrapped in `#if !UNITY_WEBGL || UNITY_EDITOR`
- New in-memory `Dictionary<string, Texture2D> _memoryCache` as the primary cache (always active on every platform)
- Disk cache still works on Editor + Standalone; WebGL uses memory cache only
- `GenerateImageCoroutine` signature updated to receive the pre-computed hash for both cache layers
- `LoadTextureFromFile` only compiles on non-WebGL
- `ClearCache` gated similarly

Loss for WebGL players: none. Browsers already cache HTTP responses to the Gemini API, so a "second visit" still hits the browser cache. Memory cache covers within-session reuse.

### 2. Wired TMP_Dropdown ScrollRect template in `SceneAutoBuilder`
```csharp
templateScrollRect.content      = contentRT;
templateScrollRect.viewport     = vpRT;
templateScrollRect.horizontal   = false;
templateScrollRect.vertical     = true;
templateScrollRect.movementType = ScrollRect.MovementType.Clamped;
```

### 3. Migrated all `FindObjectOfType<T>(bool)` → `FindFirstObjectByType<T>(FindObjectsInactive.Include)`
- `TutorialSystem.StartTutorial`
- `CastleViewUI.Start`
- `CastleScene3D.Start`
- `MainMenuUI.OnSettingsClicked`
- `PauseUI.OnSave`
- `KeyboardShortcuts`

### 4. Disabled managed stripping entirely for WebGL
`ProjectSettings.asset`: `WebGL: 0` (was `1 = Low`). Belt-and-suspenders. The build grows ~1.5 MB but eliminates stripping as a cause class. Can dial back up to `1` after confirming stability.

---

## Fixes — Deep Optimization Round (commit `3b21d5b`)

Parallel `Explore` agent audit found 15 more issues. The CRITICAL and HIGH ones I fixed in the same pass:

### NPCDailyRoutine infinite coroutine had no exit guard
`RoutineLoop()` was a bare `while(true) { ... yield return new WaitForSeconds(30f); }`. When a scene unloads, the MonoBehaviour is destroyed but Unity still has the coroutine queued to resume. On wake it touches freed state. Fixed:
```csharp
while (this != null && enabled && gameObject != null && gameObject.activeInHierarchy)
```

### GameManager.DayCycleCoroutine same pattern
Same guard plus a `yield break` after `WaitForSeconds` to short-circuit if the manager got destroyed during the wait.

### EventManager.EnrichEventWithLLM dangling-reference write
The Gemini callback wrote back to `ev.Description` 1–5 seconds after the request. If the event got resolved meanwhile, `ev` was no longer in `_activeEvents` and the write was a dangling-reference modification. Added guard:
```csharp
if (ev == null || !_activeEvents.Contains(ev)) return;
```

### GeminiAPIClient LRU eviction was random, not FIFO
```csharp
var oldest = System.Linq.Enumerable.First(_responseCache);
_responseCache.Remove(oldest.Key);
```
`Dictionary<K,V>.First()` does NOT guarantee insertion order in .NET — it returns an arbitrary entry based on internal hash ordering. So the "evict oldest" was evicting a random entry, and over time the cache filled with stale data while useful entries got thrown out. Replaced with hard reset at 300 entries — simpler and correct. True LRU would need a `LinkedList<>` + `Dictionary<K, LinkedListNode<>>` combo; overkill for this cache size.

### SaveSystem.Save non-atomic write order
Old order: (1) copy current save → backup, (2) write new save. A disk error in step 2 could corrupt both the backup AND the new save in one go. New order:
1. Write new data to `save.tmp`
2. If current save exists, copy it to backup
3. Atomic rename `save.tmp` → final

Single disk error can now only destroy one of the three files.

### ResourceManager firing OnResourceChanged on no-op clamps
`AddResource(Wood, +100)` when wood is already at max would still `OnResourceChanged?.Invoke` with `old == new`. HUD subscribers re-rendered on every no-op add. Added a `newVal != old` guard before firing.

---

## Complete Debugging Arc

| # | Hash | Fix |
|---|------|-----|
| 1 | `4605d40` | CJK font fallback + hardcoded text purge |
| 2 | `67d2d44` | ToastLayer raycast fix (first click breakthrough) |
| 3 | `f77ac4e` | Gemini image client + SceneReferenceValidator |
| 4 | `1a26176` | Zero-scale canvas guard (red herring) |
| 5 | `f38a660` | **`activeInputHandler: -1 → 0`** (real click root cause) |
| 6 | `b3c4a37` | PauseUI + WorldMap tiles + Castle background |
| 7 | `479bbea` | Shader fallback + existing-NPC spawn + Gemini backgrounds |
| 8 | `5c348e1` | Tutorial spawning + FindDeep + procedural cards |
| 9 | `e6b0227` | NPC card vertical letters + Lord title dup + tutorial firing |
| 10 | `2632928` | **Castle view redesign + Gemini key CI injection** |
| 11 | `0af8799` | PlayModeProbe extended to Castle state |
| 12 | `d69ea6d` | Castle redesign history |
| 13 | `656c3ea` | **WebGL WASM crash fix: System.IO + deprecated APIs + stripping** |
| 14 | `3b21d5b` | **Deep optimization: coroutine guards, LRU, atomic save, event race** |

---

## Final State

- **URL:** https://taeshin11.github.io/LittleLordMajesty/
- **Latest commit:** `3b21d5b`
- **Build size:** 14.7 MB (up from 13.1 MB — managedStrippingLevel disabled for WebGL)
- **Last deploy:** 2026-04-10 21:27:31 GMT
- **Self-hosted CI:** ✅ success (6m 40s)
- **GameConfig injection step:** ✅ Gemini API key present in build

## What's Still Pending

All M13 P2 items from previous passes, still deferred:
- Leaderboard UI content
- Kenney 3D asset pack integration (still using primitive cubes/cylinders)
- NPC walking animations
- Multi-slot save UI
- Monetization flow
- Event response parsing into outcome logic

None block the core alpha playtest loop.
