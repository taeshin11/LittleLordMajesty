# Milestone 3: Polish, Missing Features & Bug Fixes
**Date:** 2026-04-09
**Status:** ✅ COMPLETE

## What Was Added

### New Features
1. **TutorialSystem** (`Assets/Scripts/GameSystems/TutorialSystem.cs`)
   - Step-based tutorial for first-time players
   - 7 steps: Welcome → Resources → Talk to NPC → Issue Command → Build → World Map → Complete
   - ForcedAction steps require player interaction before advancing
   - Skippable; PlayerPrefs flag prevents repeat showing

2. **ToastNotification** (`Assets/Scripts/UI/ToastNotification.cs`)
   - Queued, animated toast messages (slide in/out from right)
   - Types: Info, Success, Warning, Error, Resource
   - `ShowResource(type, amount)` shortcut for +/- resource changes
   - Max 3 visible at once; queues additional

3. **NPCPromotion** (`Assets/Scripts/NPC/NPCPromotion.cs`)
   - 5-rank progression: Apprentice → Journeyman → Expert → Master → Champion
   - Profession-specific abilities unlock at each rank
   - XP gained from player interactions and completed tasks
   - Bonuses: food production, gold income, combat strength

4. **LeaderboardUI** (`Assets/Scripts/UI/LeaderboardUI.cs`)
   - Firebase Firestore global leaderboard
   - 3 categories: Territories, Gold, Days Survived
   - Highlights player's own entry
   - Paginated (top 20 per category)

### Bug Fixes
5. **DiplomacySystem.cs** - Removed broken custom LINQ extension, replaced with proper for-loop
6. **GameManager.cs** - WorldMapManager now referenced and initialized properly
7. **.gitattributes** - Added to enforce LF line endings across all text files

## Total Project Stats
- C# Scripts: 30 files
- Localization strings: 7 languages × ~70 keys each
- Systems: 15 major systems
- Total Lines of Code: ~5,400+

## Architecture Overview
```
GameBootstrap
    ↓ initializes
LocalizationManager → GeminiAPIClient → TTSManager → FirebaseManager → GameManager
                                                                          ↓
                                                              ResourceManager
                                                              NPCManager ← NPCPromotion
                                                              EventManager
                                                              WorldMapManager ← DiplomacySystem
                                                              BuildingManager
                                                              CombatSystem
                                                              TutorialSystem
                                                              MysteriousVisitorSystem
                                                              AudioManager
                                                                          ↓
                                                                      UIManager
                                                                      ├── MainMenuUI
                                                                      ├── CastleViewUI
                                                                      ├── NPCInteractionUI
                                                                      ├── WorldMapUI
                                                                      ├── EventUI
                                                                      ├── SettingsUI
                                                                      ├── LeaderboardUI
                                                                      └── ToastNotification
```

## What Remains for Beta
1. Unity Editor scene setup (Hierarchy, prefabs, UI Canvas)
2. Pixel art sprite assets
3. Audio tracks (music + SFX)
4. Physical device testing
5. App Store submission preparation
