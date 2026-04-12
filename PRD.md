# PRD: LittleLordMajesty (LLM)

> **Note:** Living document. Update whenever architecture or scope changes.
>
> **2026-04-12 — Full rebuild pivot.** From card-grid dialogue sim → **2D top-down cozy
> roaming RPG** (Zelda: Echoes of Wisdom inspiration). Art assets switch from
> SDXL-generated portraits to **Anokolisa Pixel Crawler** (16×16 hand-crafted pixel art).
> SDXL retired for in-game sprites; kept only for menu backgrounds and dialogue portraits.
> AI NPC core, localization, and infrastructure carry forward.

---

## 1. Project Overview

| Item | Value |
|------|-------|
| **Product Name** | LittleLordMajesty (LLM) |
| **Platforms** | WebGL (primary, mobile browser), then Steam PC, Android / iOS |
| **Genre** | Cozy Kingdom Roaming RPG + AI NPC Interaction |
| **Art Style** | 16×16 pixel art top-down (Anokolisa Pixel Crawler pack). Consistent single-artist aesthetic — no mixed asset packs. |
| **Core Concept** | "Walk your kingdom. Rule with a single word." Inherit a tiny pixel-art castle, explore on foot, talk to NPCs in natural language, resolve crises through conversation, grow from "Little Lord" to "Majesty." |

---

## 2. Core Game Loop

1. **Explore** — Walk the castle courtyard as the Little Lord. Orthographic 2D camera follows top-down. Tilemap-based environment from Anokolisa tileset.
2. **Approach & Talk** — Walk near an NPC → interaction prompt appears. Tap/press to open dialogue box.
3. **Instruct via AI** — NPCs respond through Gemini 2.0 Flash Lite. Issue work orders, mediate conflicts, interrogate visitors via free-text.
4. **Consequences & Growth** — Dialogue outcomes feed Resource/Loyalty/Crisis systems; castle visibly changes over days; unlock new areas.
5. **Crisis Management** — Orc raids, fires, food shortages arrive as in-world events the player must walk to and resolve.

---

## 3. Key Feature Requirements

### 3.1. Art & Assets *(new — Anokolisa Pixel Crawler)*

| Category | Source | Details |
|----------|--------|---------|
| **Player + NPC sprites** | Anokolisa Pixel Crawler (free, 16×16) | 3 hero types, idle/walk/run/attack/death/fishing animations |
| **Enemies** | Anokolisa Pixel Crawler | 8 types (skeleton × 4, orc × 4), death/run/idle animations |
| **Weapons** | Anokolisa Pixel Crawler | 50+ (swords, axes, bows, staffs, shields, tools) |
| **Tileset — environment** | Anokolisa Pixel Crawler | Dungeon, Forest, Farm, Mines, Snow, Cave |
| **Dialogue portraits** | SDXL base 1.0 (local) | Per-NPC portrait for dialogue box only |
| **Menu backgrounds** | SDXL base 1.0 (local) | Main menu, loading screens |
| **UI elements** | Procedural (code-built) | Dialogue box, HUD, joystick — built via `PinCenterRect` pattern |

**Why not SDXL for game sprites?**
- SDXL generates 512×768 portrait-style images, not game-ready 16×16 sprites
- No animation consistency between frames
- GPU cost (30s/image × 40 = 20min VRAM)
- Inconsistent style across characters
- Can't generate tilesets

**Expansion path:** Same artist's paid packs (tone-matched) when more content is needed.

### 3.2. Player Avatar & Movement

- **Pure 2D orthographic** — Camera (orthoSize 5.5) tracking player in XY. Camera at z=-10, sprites at z=0, tilemap at z=1.
- **Free movement** — `transform.position += dir * speed * dt` in XY plane. No NavMesh, no Rigidbody.
- **Sprite animations** — Anokolisa sprite sheets: idle (4 frames), walk (4 frames), per cardinal direction. Sliced in Unity Sprite Editor at 16×16.
- **Input — mobile-first:**
  - **Mobile**: Virtual joystick (bottom-left) + tap right side to interact
  - **PC**: WASD/arrows + `E` to interact + `Esc` for menu
- **Interaction trigger** — `InteractionFinder` iterates `NPCIdentity.RegisteredNPCs` at 10 Hz. Closest NPC within range shows prompt.

### 3.3. World Building

- **Tilemap-based** — Unity Tilemap with Anokolisa tilesets (Farm for courtyard, Dungeon for basement, Forest for outskirts). Collision via Tilemap Collider 2D.
- **Castle courtyard** — Main play area. Grid-painted in Unity Tilemap from Anokolisa Farm + town tiles.
- **World Map** — Second scene. Player icon traverses overworld tilemap.
- **Scene transitions** — Castle ↔ World Map via scene load, avatar position preserved.

### 3.4. NPC Roaming & Daily Routines

- **NPCs walk the courtyard** along waypoint routines (time-of-day driven). `Vector3.MoveTowards` between waypoints.
- **Anokolisa NPC sprites** — Each NPC uses one of the 3 hero character types with palette-swap or accessory differentiation.
- **Dialogue door** — Walk up + interact → dialogue box with SDXL portrait + Gemini conversation.
- **Reused state layer** — `NPCManager`, `NPCDailyRoutine`, `ResourceManager`, `EventManager`, `GameManager`, `LocalDialogueBank`, `GeminiAPIClient` — all untouched.

### 3.5. AI NPC & Dialogue System *(unchanged)*

- **Gemini 2.0 Flash Lite API** — NPC dialogue, command interpretation, event outcomes.
- **Persona System** — Unique personality per profession (Vassal, Soldier, Merchant, Farmer, Scholar, Priest, Spy).
- **Pre-generated dialogue bank** — `LocalDialogueBank` (EXAONE-baked lines) for greetings/idle at 0ms, 0 cost.
- **Request Queue** — Max 1–2 concurrent Gemini requests; 1s inter-request interval.
- **UI** — Bottom-screen RPG dialogue box (SDXL portrait left, name+text right, input at bottom).

### 3.6. Events & Visitors

- **In-world events** — Orc raids, fires, food shortages spawn as animated markers (Anokolisa enemy sprites for raids, particle effects for fires). Player walks to them to resolve.
- **Mysterious Visitors** — Visitor appears at gate using Anokolisa NPC sprite; player interrogates via dialogue.

### 3.7. Localization & UI/UX

- **7 languages** — en, ko, ja, zh, fr, de, es (JSON-based).
- **No hardcoded strings.**
- **Responsive UI** — 1080×1920 mobile portrait reference (primary), 1920×1080 PC landscape.
- **Korean font stack** — LiberationSans SDF + Static-baked NotoSansKR atlas.

### 3.8. Platform Support

| Platform | Target | Status |
|----------|--------|--------|
| WebGL (mobile browser) | GitHub Pages | Primary — active |
| Steam Windows | Win10 64-bit | Planned |
| Android | API 24+ | Planned |
| iOS | iOS 14+ | Planned |

### 3.9. Warfare & Espionage *(deferred, systems preserved)*

Six systems in `Assets/Scripts/Warfare/`. Re-plumbed for in-world triggers post-M16.

| System | Mechanic |
|--------|----------|
| Spy Infiltration | Dispatch spy → physically walks out gate |
| Prisoner System | Capture + interrogate in dungeon scene |
| Propaganda | Hire bards to spread rumours |
| Trader Bot | Autonomous merchant negotiation |
| Total War Bundle | Morale, tactics, governor rebellion |
| Quick Actions | Context-aware template buttons |

### 3.10. LLM Latency Strategy *(unchanged)*

| Strategy | Status |
|----------|--------|
| SSE Streaming (first-token < 500ms) | ✅ Implemented |
| Action-First visual feedback | ✅ Implemented |
| "..." thinking animation | ✅ Implemented |
| Response length limits | ✅ Implemented |
| LocalDialogueBank pre-gen cache | ✅ Implemented |

### 3.11. Persistence & Cloud

- **Local save** — JSON, `persistentDataPath`. Persists avatar position + scene + game state.
- **Firebase** — Global leaderboard (REST, no SDK).

---

## 4. Tech Stack

| Layer | Technology |
|-------|-----------|
| Engine | Unity 2022.3.62f1 LTS (C#) |
| Rendering | 2D orthographic, SpriteRenderer + Unity Tilemap |
| Art — game sprites | Anokolisa Pixel Crawler (16×16 pixel art, free commercial) |
| Art — portraits/BG | SDXL base 1.0 (local fp16) — dialogue portraits + menu only |
| Player/NPC movement | Custom transform-based 2D controller + VirtualJoystick |
| AI Dialogue | Gemini 2.0 Flash Lite REST (runtime) + EXAONE 3.5 7.8B (build-time) |
| TTS | Google Cloud TTS REST |
| Backend | Firebase REST (no SDK) |
| Localization | Custom JSON + LocalizationManager |
| CI | Self-hosted runner on `4090-desktop`, `build-local.yml` |
| Version Control | Git + GitHub (taeshin11/LittleLordMajesty) |
| Cross-project GPU | `scripts/gpu_lock.py` shared-mode (schema v2) |

---

## 5. Phased Development Roadmap

### Pre-pivot (M1–M15) — ✅ Complete

Foundation, voice/localization, AI personas, 3D scene, bug fixes, multiplayer, Anno systems, warfare, scene setup, QA, Steam, tutorial, WebGL fixes, GPU migration, TMP crash fix.

### Post-pivot Roadmap

| Phase | Name | Status |
|-------|------|--------|
| M16 | **2D Roaming Foundation** — Anokolisa asset integration, 2D orthographic camera, tilemap courtyard, player controller, virtual joystick, NPC spawn, legacy card UI retirement, dialogue box | 🔲 **Active** |
| M17 | **NPC Routines & Animations** — Anokolisa walk/idle animations wired, waypoint routines, time-of-day | 🔲 Planned |
| M18 | **Tilemap World Building** — Full courtyard tilemap (Farm tileset), collision, buildings, decorations | 🔲 Planned |
| M19 | **In-world Events** — Orc raids (Anokolisa enemy sprites), fire/crisis markers, walk-to-resolve | 🔲 Planned |
| M20 | **World Map Overworld** — Walkable tilemap overworld, castle ↔ world transitions | 🔲 Planned |
| M21 | **Warfare Re-plumb** — Spy/prisoner/trader as in-world actors with Anokolisa sprites | 🔲 Planned |
| M22 | **Korean Font Atlas** — Static Hangul atlas (blocks Korean launch) | 🔲 Planned |
| M23 | **Alpha Playtest + Polish** | 🔲 Backlog |
| M24 | **Store Submission** — Steam / App Store / Google Play | 🔲 Backlog |

### Pivot carryover matrix

| Area | Status |
|------|--------|
| `GameManager`, `ResourceManager`, `NPCManager`, `EventManager` state layer | ✅ Kept |
| `LocalDialogueBank`, EXAONE dialogue pipeline | ✅ Kept |
| `LocalizationManager`, 7-language JSON, TMP Korean guards | ✅ Kept |
| `GeminiAPIClient` streaming + TTS + request queue | ✅ Kept |
| Warfare systems (6 subsystems) | ✅ Kept, re-plumbed later |
| `PinCenterRect` anchor fix pattern | ✅ Kept for procedural UI |
| SDXL pipeline | ⚠️ Partially kept — portraits + menu BG only |
| SDXL game sprites (`generate_sprites.py`, 40 PNGs) | ❌ Retired — replaced by Anokolisa |
| Card-grid UI (CastleViewPanel, NPC cards) | ❌ Retired |
| NPCInteractionUI full-screen chat | ❌ Retired |
| 3D scene geometry / billboard rotation | ❌ Retired |

---

## 6. Development Rules

- 중요 마일스톤 달성시 `research_history/`에 기록 후 `git push`.
- 막힐 때 CLI로 해결 가능하면 자동화.
- 모든 UI 텍스트는 LocalizationManager 키 사용. 하드코딩 금지.
- API 키는 GameConfig.asset에만 저장.
- **게임 내 스프라이트는 Anokolisa Pixel Crawler만 사용. 다른 에셋 팩 혼합 금지 (톤 불일치 방지).**
- SDXL은 대화 초상화 + 메뉴 배경 전용. 게임 내 캐릭터/타일에 사용 금지.
- 공유 GPU: `scripts/gpu_lock.py` 우선순위 LLM > SPINAI > PillScan.
- 프로시저럴 UI: `PinCenterRect`로 앵커 명시.
- Agent A 패턴 (edit→commit→CI→live_test 루프) 적극 활용.
