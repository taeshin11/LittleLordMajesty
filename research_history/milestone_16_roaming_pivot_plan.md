---
name: M16 — Zelda EoW roaming pivot (design + implementation plan)
description: Pivot from card-grid dialogue sim to free-movement chibi roaming RPG. Player walks the existing 3D castle, approaches NPCs by proximity, talks via a bottom-screen dialogue box. 2D billboard sprites for all characters (SDXL 4-dir sheets), raycast wall collision, follow-camera, reused state layer.
type: project
date: 2026-04-12
status: PLANNED
---

# Milestone 16 — Roaming pivot foundation

## Design decisions (locked 2026-04-12)

| Question | Decision |
|---|---|
| Render target | **Existing 3D castle scene**. No 2D tilemap. Camera just starts following an avatar. |
| Movement model | **Free / continuous** (Stardew, Undertale). Not tile-based. |
| Collision | **Manual raycast** against a `Walls` physics layer. No CharacterController, no Rigidbody, no NavMesh. Slide along tangent on block. |
| Player representation | **2D billboard sprite** on a quad, always yaw-aligned to the camera. No 3D chibi model. |
| NPC representation | Same — 2D billboard sprites. 4 facing directions × 2 walk frames each. |
| Sprite source | **SDXL base 1.0** via `tools/image_gen/generate_sprites.py` (new). Same 4090 pipeline as portraits/backgrounds. |
| NPC AI / routine | Reuse existing `NPCManager` + `NPCDailyRoutine`. Add `NPCMover` component driving waypoint traversal via `Vector3.MoveTowards`. |
| Interaction trigger | `Physics.OverlapSphere` (r=2 m) every 0.1 s from player. Closest NPC gets a floating "E  Talk" prompt billboarded over their head. |
| Interact key | `E` on PC. Virtual button on mobile (deferred). |
| Dialogue UI | Bottom-screen classic RPG box (portrait left, name+text right, input/choices at bottom). Replaces the full-screen chat scroll. |
| State layer | **Unchanged** — GameManager, ResourceManager, NPCManager, EventManager, GeminiAPIClient, LocalDialogueBank, LocalizationManager, Warfare/* all stay bit-for-bit. |

## Out of scope for M16 (explicit parking lot)

- **NavMesh** — not needed for free movement with raycast walls.
- **Mobile virtual stick** — PC-first; deferred to M23.
- **World Map walkability** — that's M18.
- **In-world event markers** (fire smoke, argue icons) — M19.
- **Warfare re-plumbing** (spy walks out gate, prisoner in dungeon scene) — M20.
- **Static-baked Hangul atlas** — M21. Korean still falls back to English in M16.
- **Tutorial flow rewrite** — tutorial overlay exists but its anchor text is the in-world player now; that's a polish pass, not M16.

## Build order

### 1. Art pipeline — 4-direction sprite sheets (GPU work)
- **New tool**: `tools/image_gen/generate_sprites.py`. Accepts `CHARACTERS = [(id, name, description), ...]`, emits `Assets/Resources/Art/Sprites/<id>_<dir>_<frame>.png` for 4 directions × 2 frames.
- Prompt scaffold per sprite: style anchor + unique character description + `"facing {north|south|east|west}"` + `"walk frame {0|1}, full body"`. Negative prompt blocks character sheets / multi-view (same as portraits).
- Same seed per character across all 8 sprites for consistency. Direction-specific suffix to deterministically vary pose.
- Fixed seed hash = `hash(character_id) & 0x7fffffff`. Walk frame 1 seed = `hash(id) + 1000003 * dir_idx * frame_idx`.
- **Output**: `player`, `vassal_01` (Aldric), `soldier_01` (Bram), `farmer_01` (Marta), `merchant_01` (Sivaro). That's 5 × 8 = 40 sprites at 256 × 384 (portrait aspect).
- VRAM budget: SDXL base 1.0 at fp16, ~12 GB with `--no-offload`. Registers with `gpu_lock.py` as `LittleLordMajesty_sprite_gen`, vram_mb=12000.
- Estimated runtime: ~3 s/sprite × 40 = 2 min.
- **Fallback**: if SDXL struggles with direction consistency, fall back to generating a single front-facing sprite per character and use only that (billboard always faces camera anyway — player visual cue comes from position, not facing).

### 2. Player controller (C#)
- **New component**: `Assets/Scripts/Player/PlayerController.cs`.
- Serialized fields: `_walkSpeed = 4f`, `_wallLayer = LayerMask`, `_raycastDistance = 0.3f`, `_sprite` (SpriteRenderer or Image on child quad).
- Update loop:
  1. Read input: `Input.GetAxisRaw("Horizontal")`, `"Vertical"` → `inputDir` (normalized).
  2. If `inputDir.sqrMagnitude < 0.01f`, state = Idle; stop walk animation.
  3. Else: compute `worldDir = Camera.main.right * x + Camera.main.forward * z` (camera-relative). Zero out Y. Normalize.
  4. Raycast from `transform.position + Vector3.up * 0.5f` in `worldDir` over `_raycastDistance`. If hit: compute slide = `worldDir - Vector3.Project(worldDir, hit.normal)`. Use slide as actual move dir.
  5. `transform.position += moveDir * _walkSpeed * Time.deltaTime`.
  6. Update facing enum: pick direction closest to `worldDir` (N/S/E/W). Swap sprite via `_sprite.sprite = _directionSprites[(int)facing * 2 + _walkFrame]`.
  7. Increment `_walkFrame` (0/1) every `0.25 s` while moving.
- Input disabled while dialogue box is open (subscribe to `DialogueBoxUI.OnOpen/OnClose`).

### 3. Follow camera (C#)
- **New component**: `Assets/Scripts/Player/FollowCamera.cs` on Main Camera (or new CameraRig).
- Serialized: `_target` (Transform), `_offset = (0, 8, -6)`, `_smooth = 5f`.
- LateUpdate: `transform.position = Vector3.Lerp(transform.position, _target.position + _offset, _smooth * Time.deltaTime); transform.LookAt(_target.position);`
- No collision clipping in M16. If the camera clips through a wall, we add a simple rear-raycast pushback in a polish pass (not M16 scope).

### 4. NPC roaming (C#)
- **New component**: `Assets/Scripts/Castle/NPCMover.cs`.
- Serialized: `_waypoints` (Transform[]), `_walkSpeed = 1.5f`, `_wallLayer`, `_sprite`, `_directionSprites[8]`.
- Hooks to existing `NPCDailyRoutine` to pick the current waypoint index based on time-of-day.
- Update: same raycast-slide pattern as player. Computes facing from `(target - position)`.
- Idle animation when distance-to-target < 0.1 m.

### 5. Interact trigger (C#)
- **New component**: `Assets/Scripts/Player/InteractionFinder.cs` on the player.
- Coroutine that ticks every 0.1 s: `Physics.OverlapSphere(transform.position, 2f, _npcLayer)` → sort by distance → closest one gets `ShowInteractPrompt(npc)`, others get `HideInteractPrompt()`.
- `InteractPromptUI` is a world-space canvas child of each NPC with a `TextMeshProUGUI` "E  Talk" label. Hidden by default. Shown when that NPC is the current closest.
- Input: `if (Input.GetKeyDown(KeyCode.E) && _currentTarget != null) DialogueBoxUI.Instance.Open(_currentTarget.GetComponent<NPC>());`.

### 6. Bottom dialogue box (C#)
- **New component**: `Assets/Scripts/UI/DialogueBoxUI.cs` — singleton, attached to a screen-space overlay canvas.
- **Built by**: `SceneAutoBuilder.BuildDialogueBox()` (new method). Layout:
  - Box anchored to bottom of screen, width 90% of canvas, height 260 px.
  - Portrait frame (140×140) at left, 16 px padding.
  - NPC name (bold 28 pt warm brown) above the text area.
  - Description text (22 pt warm brown) below name, word-wrapped, typewriter effect.
  - 4 `QuickAction` buttons along the bottom (from existing `QuickActionTemplates.GetContextualActions()`).
  - TMP input field on the right for free text commands.
  - Advance arrow bottom-right for scripted multi-line dialogue.
- Uses `PinCenterRect` helper from M16 UI overflow fix.
- Replaces the existing `NPCInteractionUI` call sites; the old full-screen chat scroll panel gets removed from the scene build.
- LocalDialogueBank greeting → first line. Gemini commands → streamed line.
- Same Korean-safe TMP guards as the old panel.

### 7. Scene prep (Editor script)
- **New method**: `SceneAutoBuilder.BuildRoamingCastle()` — adds player GameObject with `PlayerController` + `InteractionFinder`, spawns NPC prefabs at waypoints, bakes `Walls` layer on existing castle geometry, adds `FollowCamera` to Main Camera, wires `DialogueBoxUI` on the overlay canvas.
- **Retire**: `BuildCastleViewPanel` NPC card grid path. The panel is still built (for the fullscreen castle background fallback) but the card grid and inner buttons get stubbed to `null` until fully removed in a cleanup pass.

### 8. Live test update
- `tools/playwright_test/live_test.js` — extend the existing ko-KR test to:
  1. Wait for player spawn (canvas-based, detect via `SendMessage('Player', 'TestGetPosition', '')` echoed on a text field or similar).
  2. Dispatch synthetic key events to walk toward Aldric.
  3. Press `E`.
  4. Verify dialogue box opens + greeting renders without wasm crash.
- If the walking test is too flaky, fall back to SendMessage `TestTriggerInteract('vassal_01')` hook on `PlayerController`.

## Commit cadence

Each step is one PR-sized commit:
1. `m16-01 sprite gen tool + 40 sprites baked`
2. `m16-02 PlayerController + raycast wall collision`
3. `m16-03 FollowCamera`
4. `m16-04 NPCMover + waypoint traversal`
5. `m16-05 InteractionFinder + world-space interact prompt`
6. `m16-06 DialogueBoxUI replacement`
7. `m16-07 SceneAutoBuilder.BuildRoamingCastle + retire card grid wiring`
8. `m16-08 live_test ko-KR roaming smoke test`

Each commit goes through the existing Agent A loop: edit → commit → push → `gh workflow run` → `live_test.js` → log check → iterate. Target: 3 CI cycles per commit max.

## Risks & mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| SDXL struggles to produce consistent 4-dir sprites | High | Fall back to 1 front-facing sprite per character (billboard faces camera). Artistically fine. |
| Existing castle 3D scene has no walkable floor mesh | Medium | Add a flat invisible plane at y=0 with the `Ground` layer as a fallback; player transform is clamped to this plane. |
| Raycast wall collision feels sticky at corners | Medium | Add second raycast at 45° offset; pick the less-blocked direction. Polish pass, not blocker. |
| Dialogue box wordwrap regresses the M16 anchor fix | Low | `DialogueBoxUI` uses only `PinCenterRect`-pinned Create* helpers. No ad-hoc RectTransforms. |
| `NPCInteractionUI` removal breaks old save games that reference serialized fields | Low | Keep the class as a legacy stub for 1 release; remove in a cleanup milestone. |
| Sprite gen eats VRAM during SPINAI training | Medium | `gpu_lock.py` shared mode already handles this. Sprite gen declares 12000 MB; if SPINAI holds >12000 MB, sprite gen waits. |

## Definition of done for M16

- Player can walk around the castle courtyard with WASD.
- Approaching any of the 4 NPCs shows an "E Talk" prompt.
- Pressing E opens a bottom dialogue box with the NPC's portrait + greeting line (from LocalDialogueBank).
- Typing a free-text command and pressing Send fires the existing Gemini path and streams a response into the box.
- `live_test.js` ko-KR smoke passes with 0 page errors and 0 Unity dialogs.
- `https://taeshin11.github.io/LittleLordMajesty/` deploy shows the roaming view, not the old card grid.

## What carries forward (one more time, clearly)

Kept bit-for-bit: `GameManager`, `ResourceManager`, `NPCManager`, `EventManager`, `WorldMapManager`, `GeminiAPIClient`, `LocalDialogueBank`, `LocalizationManager` (+ HasCharacterInStaticChain guards from M15), Warfare/*, all `.json` data, all SDXL backgrounds + portraits, `PinCenterRect` from M16 UI fix, `gpu_lock.py` shared mode, the self-hosted CI runner, the Agent A loop pattern.

Retired: `NPCInteractionUI` full-screen chat scroll panel + its message prefabs, `BuildCastleViewPanel` card grid path, `CastleViewUI` fullscreen overlay background management, `bg_castle_courtyard.png` as a UI backdrop (the 3D scene *is* the courtyard now).
