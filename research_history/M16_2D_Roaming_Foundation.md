# M16: 2D Roaming Foundation — Complete

**Date:** 2026-04-12
**Status:** Complete

## Deliverables

1. **Anokolisa Pixel Crawler Integration**
   - Extracted Free Pack 2.0.4 into `Assets/Resources/Art/PixelCrawler/`
   - Player: Body_A sprites (64x64, Walk 6 frames, Idle 4 frames, 3 directions)
   - NPCs: Knight/Rogue/Wizzard (32x32 Idle 4 frames, front-facing)
   - Tilesets: Dungeon, Floor, Wall, Water tiles (16x16)
   - Environment: Farm, Vegetation, Buildings, Props

2. **PlayerController (3-direction + flip)**
   - Rewritten for Anokolisa 3-direction system (Down/Side/Up)
   - Horizontal flip for Left facing using SpriteRenderer.flipX
   - Walk (6 frames) and Idle (4 frames) animations
   - WASD/arrows + VirtualJoystick input

3. **NPCBillboard (Anokolisa NPCs)**
   - Front-facing idle animation (4 frames per NPC type)
   - NPC ID → sprite type mapping (vassal→Knight, soldier→Knight, farmer→Rogue, merchant→Wizzard)
   - Color tinting for visual differentiation

4. **PixelCrawlerSprites Utility**
   - Runtime sprite sheet loading and slicing
   - Frame caching to avoid repeat work
   - Clean resource paths (no spaces)

5. **RoamingBootstrap Updated**
   - Loads Anokolisa sprites via PixelCrawlerSprites utility
   - Builds InteractPromptUI programmatically on each NPC
   - SDXL background fallback → Anokolisa tileset → solid color

6. **DialogueBoxUI Portrait Fallback**
   - SDXL portrait → Anokolisa NPC idle frame → placeholder

7. **CI/CD**
   - WebGL build passing on self-hosted runner
   - Deployed to https://taeshin11.github.io/LittleLordMajesty/
   - Quality check workflow passing

## Architecture Notes

- Old SDXL sprites (Art/Sprites/) removed — replaced by Anokolisa
- SDXL portraits + menu backgrounds (Art/Generated/) retained
- All .meta files generated with Point filtering + no compression for pixel art
- GameConfig.asset template created (empty API keys, env var fallback)
