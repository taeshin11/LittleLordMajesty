# 👑 LittleLordMajesty (LLM)

> **"Rule the realm and overcome crises with a single word."**

A 2D retro pixel-art strategy simulation game for Mobile (Android/iOS) and Tablet. You start as a humble "Little Lord" and use AI-powered text commands to manage your castle, interact with NPCs, and conquer the continent to become **Majesty**.

[![Unity](https://img.shields.io/badge/Unity-2022.3%20LTS-black?logo=unity)](https://unity.com/)
[![Gemini](https://img.shields.io/badge/Gemini-1.5%20Flash-blue?logo=google)](https://ai.google.dev/)
[![Firebase](https://img.shields.io/badge/Firebase-Spark-orange?logo=firebase)](https://firebase.google.com/)
[![Platform](https://img.shields.io/badge/Platform-Android%20%7C%20iOS-green)](https://unity.com/)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 🎮 Core Game Loop

```
Issue Commands (Text Prompt)
        ↓
 NPC Responds via Gemini AI
        ↓
Internal Affairs (Build / Manage)
        ↓
 Crisis Events (Raids, Conflicts)
        ↓
  World Map Conquest
        ↓
     Become MAJESTY
```

## ✨ Key Features

| Feature | Description |
|---|---|
| 🤖 **AI NPC Dialogue** | Gemini 1.5 Flash powers all NPC conversations with persistent personas |
| 🗣️ **Voice Narration** | Google Cloud TTS with local file caching (near-zero cost) |
| 🌍 **7 Languages** | Auto-detects: EN, KO, JA, ZH, FR, DE, ES |
| 🏰 **Castle Management** | Build tech tree, manage Wood/Food/Gold/Population |
| 🗺️ **World Conquest** | 35-territory map, 3 AI lords, LLM-generated battle narratives |
| ⚔️ **Dynamic Events** | Orc raids, NPC conflicts, fires, mysterious visitors |
| 📱 **Mobile + Tablet** | Fully responsive CanvasScaler for all screen sizes |
| ☁️ **Cloud Save** | Firebase Firestore sync (Spark free tier) |

## 🏗️ Architecture

```
Assets/Scripts/
├── Core/           GameManager, ResourceManager, SaveSystem, GameBootstrap
├── AI/             GeminiAPIClient, NPCPersonaSystem
├── NPC/            NPCManager (mood, loyalty, task assignment)
├── Events/         EventManager (LLM-powered crisis system)
├── World/          WorldMapManager (territory, siege, AI lords)
├── GameSystems/    BuildingManager, CombatSystem
├── Audio/          TTSManager (TTS + local cache)
├── Localization/   LocalizationManager (JSON-based, 7 languages)
├── Firebase/       FirebaseManager (pure REST, no SDK)
├── UI/             UIManager, CastleViewUI, NPCInteractionUI,
│                   WorldMapUI, MainMenuUI, SettingsUI
└── Data/           GameConfig, NPCDatabase
```

## 🚀 Setup

### Prerequisites
- Unity 2022.3 LTS
- Android / iOS build support modules
- API Keys:
  - [Google AI Studio](https://aistudio.google.com/) → Gemini 1.5 Flash key
  - [Google Cloud Console](https://console.cloud.google.com/) → Cloud TTS key
  - [Firebase Console](https://console.firebase.google.com/) → Spark plan project

### Quick Start

```bash
# Clone the repo
git clone https://github.com/taeshin11/LittleLordMajesty.git
cd LittleLordMajesty

# Open in Unity 2022.3 LTS
# File > Open Project > select this folder
```

### API Key Configuration

1. In Unity: `Assets/Create/LLM/Game Config` → create `GameConfig` asset
2. Save it to `Assets/Resources/Config/GameConfig.asset`
3. Fill in your API keys in the Inspector

**Or use environment variables (recommended for CI/CD):**
```bash
export GEMINI_API_KEY="your_gemini_key"
export GCP_API_KEY="your_gcp_key"
```
The `GameConfig.LoadFromEnvironment()` method picks these up automatically.

> ⚠️ **Never commit API keys.** They are gitignored via `Resources/Config/secrets.json`.

## 📦 Dependencies (Packages/manifest.json)

| Package | Purpose |
|---|---|
| `com.unity.textmeshpro` | Rich text UI |
| `com.unity.2d.animation` | NPC sprite animation |
| `com.unity.inputsystem` | Touch + keyboard input |
| `com.unity.nuget.newtonsoft-json` | JSON parsing for API responses |
| `com.unity.addressables` | Asset streaming |

## 💰 Cost Strategy

| Service | Cost | Optimization |
|---|---|---|
| Gemini 1.5 Flash | ~$0.075/1M tokens | In-memory response cache |
| Google Cloud TTS | $4/1M chars | **MD5 file cache** – repeat phrases are FREE |
| Firebase Spark | **Free** | Anonymous auth only |

## 🗺️ Development Phases

- [x] **Phase 1** – Foundation (GameManager, resources, Gemini API)
- [x] **Phase 2** – Voice & Localization (TTS cache, 7-language support)
- [x] **Phase 3** – Internal Affairs (NPC system, building tech tree)
- [x] **Phase 4** – War & Events (World map, siege, LLM events)
- [x] **Phase 5** – UI/UX (Responsive layout, chat UI, world map)
- [ ] **Phase 6** – Art & Assets (Pixel art sprites, animations)
- [ ] **Phase 7** – Polish & Beta Testing

## 🎨 Art Style

2D Retro Pixel Art with a **cute/chibi** aesthetic and a **dark medieval** color palette.

Color Palette:
- Background: `#211733` (deep purple-black)
- Primary: `#7B5EA7` (royal purple)
- Accent: `#F0C040` (gold)
- Success: `#3EBD63` (emerald)
- Danger: `#E03030` (crimson)

## 📝 License

MIT License — see [LICENSE](LICENSE)

---

*Built with Unity + Gemini AI + Google Cloud TTS + Firebase*
