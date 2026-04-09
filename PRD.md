# PRD: LittleLordMajesty (LLM)

> **Note:** This is a living document. Update whenever architecture or scope changes.

---

## 1. Project Overview

| Item | Value |
|------|-------|
| **Product Name** | LittleLordMajesty (LLM) |
| **Platforms** | Android, iOS, iPad/Tablet — **AND Steam/PC (Windows + macOS)** |
| **Genre** | 3D Isometric Strategy + AI NPC Interaction + Territory Conquest |
| **Art Style** | Low-poly 3D isometric (Clash of Clans / Frostpunk aesthetic) |
| **Core Concept** | "Rule the realm and overcome crises with a single word." Start as "Little Lord"; command NPCs with text prompts; conquer the continent to become "Majesty." |

---

## 2. Core Game Loop

1. **Instruction & Communication** — Issue work orders and mediate conflicts via text prompts to AI NPCs (Gemini 2.0 Flash Lite).
2. **Internal Affairs** — Strengthen the castle through resource management and building tech tree.
3. **Conquest & Expansion** — Deploy armies on 3D World Map; occupy AI lords' castles; expand territory.
4. **Crisis Management** — Defend against Orc raids; resolve internal crises (fires, food shortages, NPC conflicts).

---

## 3. Key Feature Requirements

### 3.1. AI NPC & Interaction System
- **Gemini 2.0 Flash Lite API** — All NPC dialogue, command interpretation, event outcomes.
- **Persona System** — Unique personality, background, speech style per profession (Vassal, Soldier, Merchant, Farmer, Scholar, Priest, Spy).
- **Voice Output (TTS)** — Google Cloud TTS with MD5-based local audio cache (near-zero repeat cost).
- **Request Queue** — Max 1–2 concurrent Gemini requests; 1-second inter-request interval to stay within free-tier quota.

### 3.2. Territory & World Map (3D)
- **3D Isometric Castle** — Low-poly terrain, buildings, NPC characters rendered behind screen-space UI canvas.
- **Camera** — Perspective 45° FOV, 55° tilt; pinch-zoom (5–20 units); swipe pan.
- **World Map** — 35 territories; 3 AI lords; scout / siege / diplomacy.

### 3.3. Events & Visitors
- **LLM-based Events** — Orc raids, NPC conflicts, fires, food shortages resolved via player text commands.
- **Mysterious Visitors** — 8 visitor types; LLM generates false identity; player must interrogate.

### 3.4. Localization & UI/UX
- **7 languages** — en, ko, ja, zh, fr, de, es (JSON-based, auto-detect system language).
- **No hardcoded strings** in game code.
- **Responsive UI** —
  - Mobile portrait: 1080×1920 reference
  - PC/Steam landscape: 1920×1080 reference
  - Tablet: 1536×2048 reference
- **Screen-space Canvas overlay** on top of 3D world.

### 3.5. Platform Support
| Platform | Target | Status |
|----------|--------|--------|
| Android | API 24+ | Planned |
| iOS | iOS 14+ | Planned |
| Steam Windows | Win10 64-bit | In development |
| Steam macOS | macOS 12+ | In development |

### 3.6. Warfare & Espionage

Six interlocking systems that enable multiplayer shadow-war alongside direct military conflict.

| System | Mechanic | LLM Role |
|--------|----------|----------|
| **Spy Infiltration** | Dispatch a disguised spy to corrupt enemy NPC morale over 7 days | Generates satisfaction/loyalty deltas and detection narrative; roleplays spy under interrogation |
| **Prisoner System** | Capture enemy commander after battle; interrogate in dungeon | Maintains defender's pre-written persona (DefensePhilosophy); responds with resist/lie/partial/full/broken outcomes |
| **Propaganda** | Hire bards to fabricate and spread fake news across 2–5 world players | Transforms blunt slander instructions into believable medieval-flavoured gossip |
| **Trader Bot** | Dispatch an autonomous merchant with secret pricing floor and negotiation style | Negotiates multi-turn deals with human buyers, honouring the seller's minimum price without revealing it |
| **Total War Bundle** | Morale speeches, general trait evolution, natural language tactical orders, province governor rebellion | Evaluates rhetoric quality; generates personality traits from battle history; parses free-text commands into NavMesh unit orders; writes governor rebellion proclamations |
| **Quick Actions** | One-tap template buttons for all LLM input points (NPC/Battle/Diplomacy/Economy/Espionage) | Reduces input friction; context-aware button set generated from `QuickActionTemplates.GetContextualActions()` |

All Warfare systems use Firebase Realtime DB for async cross-player state. Firebase paths: `spies/`, `prisoners/`, `rumors/`, `traders/`, `debunkBroadcasts/`, `notifications/`.

### 3.7. LLM Latency Strategy

Gemini 1.5 Flash cold-start on mobile 4G is typically 1.5–3 seconds. Five strategies are used to keep interactions feeling responsive:

| Strategy | Description | Status |
|----------|-------------|--------|
| **Streaming** | Replace single-callback `SendMessage()` with streaming endpoint; first token < 500ms | Implemented — `SendMessageStreaming()` SSE in `GeminiAPIClient.cs` (M12) |
| **Action-First** | Trigger visual feedback (character animation, dialogue window open) immediately at call time, before LLM response | Implemented in all Warfare coroutines |
| **Thinking Animation** | Display "..." or idle animation during `while (!done)` wait loop; masks latency gap perceptually | Implemented via coroutine polling pattern |
| **Response Length Limits** | Every prompt includes explicit length cap: JSON-only for structured outputs, "2–3 sentences max" for prose | Implemented in all 6 warfare system prompts |
| **Pre-generation Cache** | Pre-generate common responses (daily NPC greetings, merchant openers) at session start; serve from memory on first interaction, refresh in background | Planned — `GeminiAPIClient` cache layer, keyed by system-prompt hash |

### 3.8. Persistence & Cloud
- **Local save** — JSON, platform-aware path (`%APPDATA%` on PC, `persistentDataPath` on mobile).
- **Firebase** — Global leaderboard (Firebase Realtime DB via REST API, no SDK).
- **Steam** — Achievements + Steam leaderboard (Facepunch.Steamworks — future integration).

---

## 4. Tech Stack

| Layer | Technology |
|-------|-----------|
| Engine | Unity 2022.3 LTS (C#) |
| Rendering | 3D low-poly, URP (Universal Render Pipeline) |
| AI | Gemini 2.0 Flash Lite REST API |
| TTS | Google Cloud TTS REST API |
| Backend | Firebase REST (no SDK) |
| Steam | Facepunch.Steamworks (pending) |
| Localization | Custom JSON + LocalizationManager |
| Version Control | Git + GitHub (taeshin11/LittleLordMajesty) |
| Development | CLI-first; automated with Editor scripts |

---

## 5. Phased Development Roadmap

| Phase | Name | Status |
|-------|------|--------|
| 1 | Foundation (30 scripts, project structure) | ✅ Complete |
| 2 | Voice & Localization (TTS, 7 languages) | ✅ Complete |
| 3 | Polish & AI (Gemini personas, events, world map) | ✅ Complete |
| 4 | 3D Scene + Editor Automation | ✅ Complete |
| 5 | Bug Fixes (Agent Team) + Steam/PC Support | ✅ Complete |
| 6 | LordNet Multiplayer + Freemium Monetization | ✅ Complete |
| 7 | Anno/Civilization Systems (Production, Population, Decrees, Research) | ✅ Complete |
| 8 | Warfare & Espionage (Spy/Prisoner/Propaganda/TraderBot/TotalWar/QuickActions) + LLM Latency Strategy | ✅ Complete |
| 9 | Unity Scene Setup + API Keys + QuickAction UI Wiring | ✅ Complete |
| 10 | Scenes + Assets + Bootstrap/Game.unity wired | ✅ Complete |
| 11 | QA Audit + Critical Bug Fixes (32 bugs) | ✅ Complete |
| 12 | Steam Integration + Tutorial System + LLM Streaming (Gemini 2.0 Flash Lite) | ✅ Complete |
| 13 | Alpha Playtest Verification + Steam Store Prep + Visual Upgrade (Kenney) | 🔲 Next |
| 14 | Android/iOS/Steam Build + Final QA | 🔲 Backlog |
| 15 | App Store + Steam Store Submission | 🔲 Backlog |

---

## 6. Development Rules

- 중요 마일스톤 달성시 반드시 `research_history/` 에 기록 후 `git push`.
- 막힐 때 CLI로 해결 가능하면 무조건 자동화.
- 유니티 에디터 작업은 Editor Script로 자동화 (SceneAutoBuilder, AssetCreator).
- 모든 UI 텍스트는 LocalizationManager 키를 사용. 하드코딩 금지.
- API 키는 GameConfig.asset에만 저장. 절대 코드에 하드코딩 금지.
- Agent Team (Quality Tester / Backend Engineer / UI Engineer / Code Reviewer) 주기적 리뷰.
