# PRD: LittleLordMajesty (LLM)

> **Note:** Living document. Update whenever architecture or scope changes.
>
> **2026-04-12 — Cute 16x16 pixel art top-down pivot.** From dark isometric sprites →
> **Bright cute top-down 2D roaming RPG**. Art style: Kenney Tiny Town + Tiny Dungeon
> packs (16x16 pixel art, PPU=16) with **orthographic camera**, top-down Zelda-style view.
> Characters are single 16x16 tiles. Zelda Echoes of Wisdom-inspired bright colorful aesthetic.
> AI NPC core, localization, infrastructure carry forward.

---

## 1. Project Overview

| Item | Value |
|------|-------|
| **Product Name** | LittleLordMajesty (LLM) |
| **Platforms** | WebGL (primary, mobile browser), then Steam PC, Android / iOS |
| **Genre** | Cozy Kingdom Roaming RPG + AI NPC Interaction |
| **Art Style** | **Cute 16x16 pixel art top-down** — Kenney Tiny Town + Tiny Dungeon packs (16x16 at PPU=16). Bright colorful Zelda-inspired aesthetic. Orthographic 2D camera, top-down view. Characters are single 16x16 tiles, scaled 1.3x for visibility. Point-filtered pixel art. |
| **Core Concept** | "Walk your kingdom. Rule with a single word." Inherit a tiny low-poly castle, explore on foot, talk to NPCs in natural language, resolve crises through conversation, grow from "Little Lord" to "Majesty." |

---

## 2. Core Game Loop

1. **Explore** — Walk the castle courtyard as the Little Lord. Orthographic 2D camera follows top-down. Tilemap-based environment from Anokolisa tileset.
2. **Approach & Talk** — Walk near an NPC → interaction prompt appears. Tap/press to open dialogue box.
3. **Instruct via AI** — NPCs respond through Gemini 2.0 Flash Lite. Issue work orders, mediate conflicts, interrogate visitors via free-text.
4. **Consequences & Growth** — Dialogue outcomes feed Resource/Loyalty/Crisis systems; castle visibly changes over days; unlock new areas.
5. **Crisis Management** — Orc raids, fires, food shortages arrive as in-world events the player must walk to and resolve.

---

## 3. Key Feature Requirements

### 3.1. Art & Assets *(Kenney Tiny Town + Tiny Dungeon)*

| Category | Source | Details |
|----------|--------|---------|
| **Overworld tiles** | Kenney Tiny Town (CC0, 16×16) | 132 tiles: grass, paths, trees, houses (orange roofs), castle walls, water, bridges, fences, flowers, crops, animals |
| **Characters + items** | Kenney Tiny Dungeon (CC0, 16×16) | 132 tiles: knights, mages, rogues, monsters, swords, shields, potions, dungeon floors/walls, furniture |
| **Dialogue portraits** | SDXL base 1.0 (local) | Per-NPC portrait for dialogue box only |
| **Menu backgrounds** | SDXL base 1.0 (local) | Main menu, loading screens |
| **UI elements** | Procedural (code-built) | Dialogue box, HUD, joystick — built via `PinCenterRect` pattern |

**Why Kenney Tiny packs?**
- Bright, cute, Zelda-like aesthetic (matching Echoes of Wisdom target)
- 16x16 pixel art — crisp at any scale with Point filtering
- CC0 license — zero legal risk
- Consistent style across Town + Dungeon packs
- 264 total tiles covers village, characters, items, dungeon
- No animation needed — single tile per character

**Expansion path:** Other Kenney 16x16 packs (Tiny Ski, Tiny Battle, etc.) for tone-matched expansion.

### 3.2. Player Avatar & Movement

- **Pure 2D orthographic** — Camera (orthoSize 5) tracking player in XY. Camera at z=-10, sprites at z=0, tiles at PPU=16 (1 tile = 1 world unit).
- **Free movement** — `transform.position += dir * speed * dt` in XY plane. Speed ~3.5 WU/s. No NavMesh, no Rigidbody.
- **Single sprite per character** — Kenney Tiny Dungeon character tiles (16×16). Sprite flips horizontally for left/right. No animation frames.
- **Input — mobile-first:**
  - **Mobile**: Virtual joystick (bottom-left) + tap right side to interact
  - **PC**: WASD/arrows + `E` to interact + `Esc` for menu
- **Interaction trigger** — `InteractionFinder` iterates `NPCIdentity.RegisteredNPCs` at 10 Hz. Closest NPC within range shows prompt.

### 3.3. World Building

- **Tile grid** — 30x30 Kenney Tiny Town tiles placed procedurally by RoamingBootstrap. Grass base with paths, buildings, trees, water.
- **Castle courtyard** — Main play area. Central castle (stone walls + towers), east market houses, west barracks, north farm with fences/crops.
- **World Map** — Second scene. Player icon traverses overworld tilemap.
- **Scene transitions** — Castle ↔ World Map via scene load, avatar position preserved.

### 3.4. NPC Roaming & Daily Routines

- **NPCs walk the courtyard** along waypoint routines (time-of-day driven). `Vector3.MoveTowards` between waypoints.
- **Tiny Dungeon NPC sprites** — Each NPC uses a distinct character tile (knight, viking, peasant, rogue, etc.).
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
| Rendering | 2D orthographic, SpriteRenderer (procedural tile placement) |
| Art — game sprites | Kenney Tiny Town + Tiny Dungeon (16×16 pixel art, CC0) |
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
| M16 | **2D Roaming Foundation** — Kenney Tiny Town/Dungeon asset integration, 2D orthographic camera, procedural tile courtyard, player controller, virtual joystick, NPC spawn, legacy card UI retirement, dialogue box | ✅ **Complete** |
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
| SDXL game sprites (`generate_sprites.py`, 40 PNGs) | ❌ Retired — replaced by Kenney Tiny packs |
| Isometric Miniature sprites (256x512) | ❌ Retired — replaced by 16x16 top-down |
| Anokolisa Pixel Crawler sprites | ❌ Retired — replaced by Kenney Tiny packs |
| Card-grid UI (CastleViewPanel, NPC cards) | ❌ Retired |
| NPCInteractionUI full-screen chat | ❌ Retired |
| 3D scene geometry / billboard rotation | ❌ Retired |

---

## 7. Technical Specification

> This section contains enough detail to rebuild the entire project from an empty folder.

### 7.1. GameManager State Machine

```
enum GameState {
  MainMenu, Loading, Castle, WorldMap, Battle,
  Dialogue, Event, Paused, GameOver, Victory
}
```

- `SetGameState(newState)` is the sole transition method; fires `OnGameStateChanged(oldState, newState)`.
- Day cycle coroutine: 5 real minutes = 1 in-game day. Runs only in `Castle`, `WorldMap`, `Dialogue`, `Event`. Day 365 rolls to Day 1, Year+1.
- `TogglePause()` stores pre-pause state, sets `Time.timeScale = 0f`; restores on unpause.
- `NewGame()` always transitions to `Castle`.
- Lord title auto-updates by territory count: <3 = Little Lord, ≥3 = Baron, ≥5 = Lord, ≥7 = High Lord, ≥10 = Majesty.
- `PlayTimeSeconds` accumulates in `Update()` in all non-Paused, non-MainMenu states.

### 7.2. NPC Data Model

```
NPCData {
  Id: string              // e.g. "vassal_01"
  Name: string            // e.g. "Aldric"
  Profession: NPCProfession
  Personality: NPCPersonality
  LoyaltyToLord: int      // 0–100
  CurrentTask: string     // e.g. "Farming", "Patrol"
  TaskState: NPCTaskState // Idle, Working, Combat, Resting, Fleeing, InDialogue
  MoodScore: float        // 0–100
  IsAvailable: bool
  BackgroundStory: string
  WorldPosition: Vector3
}
```

**Starting NPC roster:**

| Id | Name | Profession | Personality | Loyalty | Mood |
|----|------|-----------|-------------|---------|------|
| vassal_01 | Aldric | Vassal | Loyal | 80 | 75 |
| soldier_01 | Bram | Soldier | Brave | 65 | 60 |
| farmer_01 | Marta | Farmer | Hardworking | 70 | 65 |
| merchant_01 | Sivaro | Merchant | Greedy | 40 | 70 |

### 7.3. NPC Professions & Personalities

**NPCProfession (15):** Vassal, Soldier, Merchant, Farmer, Blacksmith, Healer, Scout, Guard, Builder, Mage, Scholar, Priest, Spy, OrcRaider, MysteriousVisitor

**NPCPersonality (12):** Loyal, Greedy, Cowardly, Brave, Lazy, Hardworking, Suspicious, Cheerful, Grumpy, Wise, Naive, Cunning

### 7.4. Resource System

```
enum ResourceType { Wood, Food, Gold, Population }
```

| Resource | Initial | Max | Daily Production | Notes |
|----------|---------|-----|-----------------|-------|
| Wood | 100 | 1000 | 10 × WoodMultiplier | — |
| Food | 200 | 1000 | 15 × FoodMultiplier | Minus 2 × population consumed/day |
| Gold | 50 | 9999 | 5 × GoldMultiplier | — |
| Population | 10 | 100 | (none) | Drives food consumption |

Critical threshold: Food < population×5 OR Wood < 10 → fires `OnResourcesCritical`.

### 7.5. Gemini API Integration

**Model:** `gemini-2.0-flash-lite`
**Endpoint:** `https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash-lite:generateContent`
**Streaming:** `...gemini-2.0-flash-lite:streamGenerateContent?alt=sse`

**Request shape:**
```json
{
  "system_instruction": { "parts": [{ "text": "<systemPrompt>" }] },
  "contents": [
    { "role": "user|model", "parts": [{ "text": "..." }] }
  ],
  "generationConfig": {
    "maxOutputTokens": 512,
    "temperature": 0.85,
    "topP": 0.9
  },
  "safetySettings": [
    { "category": "HARM_CATEGORY_HARASSMENT", "threshold": "BLOCK_MEDIUM_AND_ABOVE" },
    { "category": "HARM_CATEGORY_HATE_SPEECH", "threshold": "BLOCK_MEDIUM_AND_ABOVE" }
  ]
}
```

**Operational limits:** Max 2 concurrent, 1s min gap, 3 retries (429→3-5s, 5xx→1-3s), SHA256 response cache (max 300), 30s timeout.

**NPC persona prompt template:**
```
You are {Name}, a {professionDesc} in the medieval castle of {lordTitle} {lordName}.

PERSONALITY: You are {personalityDesc}. Loyalty: {LoyaltyToLord}/100.
WORK ETHIC: {WorkEthic}/100. COURAGE: {Courage}/100.
BACKGROUND: {BackgroundStory}
SPEECH STYLE: {quirks}. Keep responses short (1-3 sentences) and in character.
NEVER break character. NEVER use modern slang unless your character would.

{languageInstruction}  // e.g. "Respond entirely in Korean (한국어)."

Current context: You are speaking directly to {lordTitle} {lordName}...
```

### 7.6. LocalDialogueBank

**Generated by:** `tools/dialogue_gen/generate.py` (local EXAONE 3.5 7.8B)
**File:** `Resources/Dialogue/dialogue_lines.json`

```json
{
  "vassal": {
    "greeting": ["환영합니다, 영주님.", ...],
    "idle": ["오늘 날씨가 좋군요.", ...],
    "accept": ["알겠습니다, 영주님.", ...],
    "refuse": ["그건 좀...", ...],
    "good_news": ["좋은 소식입니다!", ...],
    "bad_news": ["안 좋은 소식이 있습니다.", ...]
  },
  "soldier": { ... },
  "farmer": { ... },
  "merchant": { ... }
}
```

Supported professions for offline lines: Vassal, Soldier, Farmer, Merchant (4 of 15). Others use Gemini only. Runtime Hangul-renderability check; falls back to Gemini if static atlas lacks glyphs.

### 7.7. Localization

**Files:** `Resources/Localization/{en|ko|ja|zh|fr|de|es}.json`
**Format:** Flat key-value dictionary.

```json
{
  "app_name": "Little Lord Majesty",
  "btn_new_game": "New Game",
  "hud_wood": "Wood",
  "npc_opened_conversation": "You approach {0}...",
  "profession_vassal": "Vassal",
  "event_orc_raid_title": "Orc Raid!"
}
```

Auto-detect from `Application.systemLanguage`. Override via `PlayerPrefs["SelectedLanguage"]`. Falls back to English if CJK font unavailable. Missing key returns key verbatim.

### 7.8. Save System

**Path:** `{Application.persistentDataPath}/save.json` (backup: `save_backup.json`)
**Format:** Indented JSON (Newtonsoft.Json). Atomic write: `.tmp` → backup old → rename.

```
SaveData {
  PlayerName: string
  LordTitle: string
  Day: int
  Year: int
  PlayTimeSeconds: float
  Wood, Food, Gold, Population: int
  TerritoriesOwned: int
  CompletedBuildings: string[]
  ActiveQuestIds: string[]
  NPCStates: NPCSaveData[]  // Id, CurrentTask, MoodScore, LoyaltyToLord, WorldPosition
  SaveTimestamp: string (UTC)
  GameVersion: string
}
```

### 7.9. Firebase REST

**Auth:** Anonymous sign-in only (Spark plan, no SDK).

| Purpose | Method | Path |
|---------|--------|------|
| Auth | POST | `identitytoolkit.googleapis.com/v1/accounts:signInAnonymously?key={apiKey}` |
| Cloud save | PATCH | `firestore.googleapis.com/.../documents/saves/{userId}` |
| Leaderboard | PATCH | `firestore.googleapis.com/.../documents/leaderboard/{userId}_{category}` |

Feature-flagged via `GameConfig.EnableFirebase`.

### 7.10. GameConfig (ScriptableObject)

**Asset path:** `Resources/Config/GameConfig`

| Field | Type | Default |
|-------|------|---------|
| GeminiAPIKey | string | "" |
| GoogleCloudAPIKey | string | "" (TTS) |
| FirebaseAPIKey | string | "" |
| FirebaseProjectId | string | "" |
| FirebaseAuthDomain | string | "" |
| FirebaseDatabaseURL | string | "" |
| EnableTTS | bool | true |
| EnableFirebase | bool | true |
| EnableDebugLogs | bool | false |
| TTSSpeakingRate | float | 1.0 |
| GeminiTemperature | float | 0.85 |
| GeminiMaxOutputTokens | int | 512 |

Keys injectable at runtime via env vars: `GEMINI_API_KEY`, `GCP_API_KEY`.

### 7.11. Scene Structure

| Scene | Purpose |
|-------|---------|
| `Bootstrap.unity` | Startup/initialization, loads managers |
| `Game.unity` | Main gameplay (castle, world map, dialogue, events) |

### 7.12. Folder Structure

```
Assets/
├── Scenes/          Bootstrap.unity, Game.unity
├── Scripts/
│   ├── AI/          GeminiAPIClient, NPCPersonaSystem
│   ├── Core/        GameManager, ResourceManager, SaveSystem
│   ├── NPC/         NPCManager, NPCDailyRoutine, LocalDialogueBank
│   ├── Player/      PlayerController, FollowCamera, InteractionFinder, RoamingBootstrap
│   ├── UI/          DialogueBoxUI, VirtualJoystick, InteractPromptUI
│   ├── Castle/      NPCBillboard, NPCIdentity
│   ├── Localization/ LocalizationManager
│   ├── Firebase/    FirebaseManager
│   ├── Events/      EventManager
│   ├── Warfare/     (6 subsystems — deferred)
│   ├── World/       WorldMapManager, CastleScene3D
│   └── Utils/       Helpers
├── Resources/
│   ├── Art/
│   │   ├── TinyTown/       Kenney Tiny Town tiles (132 × 16×16)
│   │   ├── TinyDungeon/    Kenney Tiny Dungeon tiles (132 × 16×16)
│   │   └── Generated/      SDXL portraits + menu BGs
│   ├── Config/      GameConfig.asset
│   ├── Dialogue/    dialogue_lines.json
│   └── Localization/ en.json, ko.json, ja.json, zh.json, fr.json, de.json, es.json
└── Plugins/         Newtonsoft.Json

tools/
├── dialogue_gen/    generate.py (EXAONE dialogue baking)
└── image_gen/       generate.py (SDXL portraits only)

.github/workflows/   build-local.yml
```

### 7.13. Unity Package Dependencies

| Package | Version |
|---------|---------|
| com.unity.2d.animation | 9.2.0 |
| com.unity.2d.tilemap | 1.0.0 |
| com.unity.inputsystem | 1.14.0 |
| com.unity.nuget.newtonsoft-json | 3.2.1 |
| com.unity.textmeshpro | 3.0.9 |
| com.unity.addressables | 1.22.3 |
| com.unity.test-framework | 1.1.33 |

### 7.14. CI/CD — `build-local.yml`

**Runner:** Self-hosted on `4090-desktop` (Windows)
**Triggers:** Push to `master` (Assets/\*\*, ProjectSettings/\*\*, Packages/\*\*) OR `workflow_dispatch`

**Jobs:**
1. **build-webgl** (always on push)
   - Copies `GameConfig.asset` from local path (API keys not in repo)
   - `Unity.exe -batchmode -buildTarget WebGL -executeMethod SceneAutoBuilder.BuildWebGL`
   - Deploys to GitHub Pages via `peaceiris/actions-gh-pages@v4`
2. **build-windows** (`workflow_dispatch` only)
   - `Unity.exe -buildTarget StandaloneWindows64 -executeMethod SceneAutoBuilder.BuildWindows`

---

## 6. Development Rules

- 중요 마일스톤 달성시 `research_history/`에 기록 후 `git push`.
- 막힐 때 CLI로 해결 가능하면 자동화.
- 모든 UI 텍스트는 LocalizationManager 키 사용. 하드코딩 금지.
- API 키는 GameConfig.asset에만 저장.
- **게임 내 스프라이트는 Kenney Tiny Town + Tiny Dungeon만 사용. 다른 에셋 팩 혼합 금지 (톤 불일치 방지).**
- SDXL은 대화 초상화 + 메뉴 배경 전용. 게임 내 캐릭터/타일에 사용 금지.
- 공유 GPU: `scripts/gpu_lock.py` 우선순위 LLM > SPINAI > PillScan.
- 프로시저럴 UI: `PinCenterRect`로 앵커 명시.
- Agent A 패턴 (edit→commit→CI→live_test 루프) 적극 활용.
