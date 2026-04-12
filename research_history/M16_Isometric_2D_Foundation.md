# M16: Isometric 2D Foundation — Complete

**Date:** 2026-04-12
**Status:** Complete

## Art Style Evolution
1. Anokolisa Pixel Crawler 16x16 → too pixelated, size mismatch
2. Kenney RPG Urban Pack 16x16 tiles → too flat, not charming
3. Kenney 3D FBX models (Castle Kit etc.) → too complex, FBX import issues
4. **Kenney Isometric Miniature packs → CURRENT** — pre-rendered isometric sprites

## Current Art Stack
- **Isometric Miniature Overworld** — grass, paths, trees, rocks (76 sprites)
- **Isometric Miniature Farm** — wood walls, roofs, fences, crops (228 sprites)
- **Isometric Miniature Dungeon** — stone walls, props, 8 character variants with animations (747 sprites)
- Resolution: 256x512 per sprite, PPU=128
- Camera: 2D orthographic, z=-10

## Key Systems Working
- Isometric sprite world (grass grid, buildings, trees)
- Character run animation (10-frame cycle)
- NPC daily routines with isometric movement
- Dialogue system with LocalDialogueBank fallback
- Modern UI (frosted glass dialog, colored HUD)
- Mobile joystick
- WebGL deployment via GitHub Pages CI
