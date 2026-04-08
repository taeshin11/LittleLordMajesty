# Milestone 04 — 3D Scene & Editor Automation

**Date:** 2026-04-09
**Status:** Complete

## Summary

Switched the game from 2D pixel art to **3D isometric** (low-poly, perspective camera).
Built full Unity Editor automation tools so scenes can be set up with one click.

---

## Architecture Decision: 2D → 3D

| Aspect | Before (2D) | After (3D) |
|--------|-------------|-----------|
| Camera | Orthographic | Perspective 45° FOV, isometric angle (55° tilt) |
| NPC representation | Canvas Image sprites | 3D Capsule+Sphere primitives (`CastleScene3D`) |
| Buildings | Canvas Image placeholders | 3D Cube primitives with variation |
| World | Tilemap / flat | 3D terrain (Plane + Walls + Towers) |
| UI | Screen-space Canvas | Screen-space Canvas (unchanged) |
| Physics | 2D Physics | 3D Physics |

**Reason:** Mobile 3D strategy (Clash of Clans-style) gives more visual depth and is easier to extend with real 3D assets later.

---

## New Files

### Editor Tools (Assets/Editor/)
| File | Purpose |
|------|---------|
| `SceneAutoBuilder.cs` | One-click: builds Bootstrap + Game scenes with full UI hierarchy wired |
| `AssetCreator.cs` | One-click: creates GameConfig.asset, UITheme.asset, all UI chat prefabs |

**Usage after opening Unity:**
1. `LittleLordMajesty > Create Config Assets` — creates ScriptableObjects
2. `LittleLordMajesty > Create Message Prefabs` — creates chat/list UI prefabs
3. `LittleLordMajesty > Build All Scenes` — builds Bootstrap.unity + Game.unity

### 3D World (Assets/Scripts/World/)
| File | Purpose |
|------|---------|
| `CastleScene3D.cs` | 3D castle world: terrain, castle keep, building slots, 3D NPC characters |
| `NPC3DClickHandler` | (inner class) Raycasted tap → opens NPC dialogue |
| `Placeholder3DColors` | (inner class) Color palette for low-poly buildings |

### Input (Assets/Scripts/Input/)
| File | Purpose |
|------|---------|
| `InputHandler.cs` | Singleton: touch (tap, long-press, swipe, pinch-zoom) + mouse in editor |

### Utilities (Assets/Scripts/Utils/)
| File | Purpose |
|------|---------|
| `PlaceholderArtGenerator.cs` | UI portrait/icon textures only (not world sprites) |
| `DebugConsole.cs` | In-game console (dev builds only): cheat commands + log viewer |

---

## Modified Files

| File | Change |
|------|--------|
| `NPCManager.cs` | Added `Vector3 WorldPosition` to NPCData; updated starting NPC positions to 3D coords |
| `CastleViewUI.cs` | Removed 2D NPC sprite spawning (handled by CastleScene3D now) |
| `CastleViewUI.cs` | Removed `NPCSpriteController` class (replaced by `NPC3DClickHandler`) |

---

## 3D Castle Scene Layout

```
[CastleScene3D]
  ├── Terrain/
  │   ├── Ground (Plane, 20×20 units, green)
  │   ├── WallNorth/South/East/West (Cube)
  │   └── Tower ×4 (Cylinder + Cube cap)
  ├── Buildings/
  │   ├── CentralKeep (Cube body + flat roof + flagpole)
  │   ├── BuildingSlot ×8 (invisible floor markers)
  │   └── [dynamically spawned built buildings]
  └── NPCs/
      └── NPC_<id> (Capsule body + Sphere head + invisible tap collider)
```

**Camera:** Perspective, FOV=45°, position=(0,12,-10), rotation=(55°,0°,0°)
**Pan:** Swipe input via InputHandler → moves pan target on XZ plane
**Zoom:** Pinch/scroll → adjusts camera distance (5–20 units)

---

## Backlog (Milestone 05+)

1. **Real 3D models** — Replace primitive placeholders with low-poly `.fbx` assets (Kenney.nl free assets or Blender exports)
2. **Animations** — NPC idle/walk animations (Animator Controller)
3. **World Map 3D** — Convert WorldMapManager territory tiles to 3D terrain chunks
4. **Lighting** — Add directional light + ambient for outdoor castle look
5. **API Keys** — Fill in GameConfig.asset (Gemini, Google TTS, Firebase)
6. **Android/iOS build** — Configure signing + test on device
