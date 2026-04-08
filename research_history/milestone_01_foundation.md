# Milestone 1: Foundation - Project Setup & Core Architecture
**Date:** 2026-04-09
**Status:** ✅ COMPLETE

## What Was Done

### GitHub Repository
- Created public GitHub repo: `taeshin11/LittleLordMajesty`
- URL: https://github.com/taeshin11/LittleLordMajesty
- Initialized git, configured `.gitignore` for Unity

### Unity Project Structure
Full Unity project scaffold created manually (Unity not required for structure):
```
Assets/
  Scripts/
    Core/          - GameManager, ResourceManager, SaveSystem, GameBootstrap
    AI/            - GeminiAPIClient, NPCPersonaSystem  
    NPC/           - NPCManager
    UI/            - UIManager, CastleViewUI, NPCInteractionUI, WorldMapUI, MainMenuUI, SettingsUI
    Audio/         - TTSManager
    Localization/  - LocalizationManager
    Events/        - EventManager
    World/         - WorldMapManager
    GameSystems/   - BuildingManager
    Firebase/      - FirebaseManager
    Data/          - GameConfig
  Resources/
    Localization/  - en.json, ko.json, ja.json (+ fr, de, es, zh planned)
  Prefabs/         - To be created in Unity Editor
  Sprites/         - To be created (pixel art assets)
  Scenes/          - To be created in Unity Editor
```

### Phase 1 Complete: Foundation
- [x] GameManager singleton (state machine, day cycle)
- [x] ResourceManager (Wood, Food, Gold, Population)
- [x] SaveSystem (JSON to persistent storage + backup)
- [x] GameBootstrap (loading sequence, manager initialization)
- [x] GeminiAPIClient (HTTP REST to Gemini 1.5 Flash, retry logic, response caching)
- [x] NPCPersonaSystem (ScriptableObject personas, system prompt generation)
- [x] UIManager (responsive mobile/tablet layout with CanvasScaler)
- [x] GameConfig (API key management, environment variable loading)
- [x] Packages/manifest.json (Unity package dependencies)
- [x] ProjectSettings.asset (Android/iOS build targets)

### Phase 2 Complete: Voice & Localization
- [x] TTSManager (Google Cloud TTS REST API + MD5-based local file caching)
- [x] LocalizationManager (auto-detect system language, JSON-based strings)
- [x] Localization files: English, Korean, Japanese (French/German/Spanish/Chinese templates)
- [x] TTS cost optimization (cache = near-zero cost after first play)

### Phase 3 Complete: Internal Affairs
- [x] NPCManager (4 starting NPCs: Aldric/Vassal, Bram/Soldier, Marta/Farmer, Sivaro/Merchant)
- [x] Task assignment system (Farm, Build, Patrol, Scout)
- [x] Mood & loyalty system (affected by player commands)
- [x] BuildingManager (full tech tree: Sawmill, Farm, Market, Barracks, Walls, etc.)

### Phase 4 Complete: War & Events
- [x] WorldMapManager (7x5 map, 3 AI lords, scouting, siege battles)
- [x] EventManager (LLM-powered: Orc raids, NPC conflicts, food shortage, fire, mysterious visitor)
- [x] Battle narrative generation via Gemini API
- [x] AI lord turn processing (aggressive lords attack player)

### Phase 5: UI/UX
- [x] CastleViewUI (NPC sprites, building menu, notification system)
- [x] NPCInteractionUI (chat-style dialogue, typewriter effect, quick commands, TTS toggle)
- [x] WorldMapUI (territory tiles, siege panel, battle overlay)
- [x] MainMenuUI (animated title, name input, settings)
- [x] SettingsUI (language, TTS, volume, API usage stats)

## Technical Decisions

### Architecture
- Pure singleton managers with DontDestroyOnLoad
- No Firebase SDK - pure REST API calls to reduce APK size
- Gemini in-memory response caching (hash-keyed, 500 entry limit)
- TTS MD5-hashed filename caching for near-zero repeat cost
- ScriptableObject-based NPC personas for data-driven character design

### Cost Optimization
- Gemini: Response cache prevents duplicate API calls for same prompts
- TTS: File cache eliminates re-generation of already-heard audio
- Firebase: Spark free tier, anonymous auth only

### Responsive UI
- CanvasScaler with Screen.width/height ratio detection
- Tablet threshold: width/height >= 0.75 (4:3 aspect)
- Phone: 1080x1920 reference, Tablet: 1536x2048 reference

## Next Steps (Backlog)
1. Open Unity Editor and build actual scenes/prefabs/UI hierarchy
2. Commission or create pixel art assets (NPCs, buildings, map tiles)
3. Add remaining localization files (zh.json, fr.json, de.json, es.json)
4. Add combat resolution mini-game
5. Add music system with dynamic tracks
6. Polish animations and transitions
7. Beta testing on actual Android/iOS devices
