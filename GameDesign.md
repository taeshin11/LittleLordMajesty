# LittleLordMajesty — Game Design Document

> **Purpose:** A design-focused summary for discussing game direction.
> Updated each milestone with player-experience details, not code internals.
> Last updated: M16 (2026-04-12)

---

## M16: 2D Roaming Foundation

### 1. World & Lore

- **Setting:** A tiny pixel-art castle courtyard, ringed by trees. The world is deliberately small — you are a *Little* Lord, not a king. Your domain is a handful of buildings and four loyal subjects.
- **Narrative beat:** No cutscene, no exposition. The game drops you into the courtyard immediately. The smallness of the domain IS the story — grow it through conversation and command.
- **Time:** Each real-world 5 minutes = one in-game day. NPC schedules shift throughout the day (farming at dawn, eating at noon, resting at dusk). The HUD shows "Year 1, Day 1" and ticks forward. Resources produce daily.
- **Environment:** Grass courtyard with scattered bushes inside and a tree perimeter marking the playable boundary. A cozy, enclosed feeling — like a village green.

### 2. NPC Roster & Persona

Four starting NPCs, each with a distinct Tiny Dungeon character tile and AI personality:

| Name | Profession | Personality | Loyalty | Visual | Dialogue Character |
|------|-----------|-------------|---------|--------|-------------------|
| **Aldric** | Vassal | Loyal | 80 | Tiny Dungeon knight (female variant) | Formal, deferential. 20 years of service. Calls the player "my lord" without irony. Pragmatic — pushes for safe decisions. |
| **Bram** | Soldier | Brave | 65 | Tiny Dungeon viking | Blunt, eager. Young and hungry for glory. Short punchy sentences. Enthusiastic about combat, drags feet on farming. |
| **Marta** | Farmer | Hardworking | 70 | Tiny Dungeon peasant | Warm, plain-spoken. Knows the land. Pushes back on orders that waste food. Gives practical seasonal advice. |
| **Sivaro** | Merchant | Greedy | 40 | Tiny Dungeon rogue | Slippery, calculating. Low loyalty — here for profit. Resists charity, responds to gold incentives. |

**AI Persona System:** Each NPC's Gemini system prompt encodes personality, loyalty, mood, background, current task, and current location. NPCs stay in character, use period-appropriate language, keep to 1-3 sentences. Personality directly shapes responses to orders.

### 3. Core Fun & Interactions

- **Walk up → Talk:** Walk near an NPC → "E - Talk to [Name]" prompt appears → open dialogue box with their portrait.
- **Free-text commands:** Type anything: "Go farm the wheat field." "What do you think of Sivaro?" "I'm doubling your pay." Gemini interprets in character.
- **Resource feedback loop:**
  - "farm" / "harvest" → assigns Farming task
  - "build" / "construct" → assigns Construction
  - "patrol" / "guard" → assigns Patrol
  - "reward" / "praise" → mood +5, loyalty +2
  - "punish" / "threaten" → mood -10, loyalty -5
- **Quick command buttons:** 4 profession-specific shortcuts in the dialogue box (e.g., Farmer: "Check Crops", "Water Fields", "Harvest", "Store Grain").
- **NPC daily routines:** NPCs walk between locations on a Stardew Valley-style schedule (Marta: wheat field 6am → granary 10am → town square noon → vegetable plot 1pm). Finding them at different locations yields different conversational context ("You are currently harvesting wheat at the Wheat Field. The lord has just approached you here.").
- **Resource HUD:** Top bar always shows Wood, Food, Gold, Population + current day/year. Resources tick daily based on assigned tasks.

### 4. Events & Crises

*Not yet implemented (planned for M19).* Systems exist but aren't wired to in-world markers yet. Design intent:
- Events spawn as visible markers on the map (orc sprite at gate, fire particle at building)
- Player must physically walk to the event marker to trigger crisis dialogue
- Resolution through conversation with relevant NPCs (order Bram to fight orcs, order Marta to ration food)
- Failed crises reduce resources and NPC loyalty; successful ones boost growth

### 5. Aesthetic & Vibe

**Target mood: Bright cute Zelda-like top-down kingdom.**

- **Art style: Kenney Tiny Town + Tiny Dungeon** — 16x16 pixel art tiles at PPU=16 (1 tile = 1 world unit). Point-filtered for crisp pixels. Top-down orthographic view. Bright, colorful, inspired by Zelda: Echoes of Wisdom.
- **Characters:** Single 16x16 tiles per character (knight, viking, peasant, rogue, mage, etc.) from Tiny Dungeon. Scaled 1.3x for visibility. Flip horizontally for left/right movement. No animation frames.
- **Environment:** 30x30 grass tile grid with Tiny Town tiles — stone paths, orange-roofed houses, castle walls with towers, fences, crops, water with lily pads, bridges, flowers, trees (green + autumn), mushrooms. Perimeter trees ring the village.
- **Village layout:** Central castle with towers and flags, east market district (houses + shops), west barracks (stone buildings + training yard), north farm (fenced crops, scarecrow, animals). Pond with bridge south-west. Stone gate entrance to the south.
- **Dialogue box:** Warm parchment-toned panel at screen bottom. NPC portrait (Tiny Dungeon tile) on the left, name + message text on right. Quick command buttons below text, input field + send at bottom.
- **Color palette:** Bright greens (grass), warm orange (roofs), grey stone (castle), blue (water), autumn red/yellow (trees). Everything is cheerful and inviting — NO dark dungeon aesthetic.
- **Sound:** TTS voices (Google Cloud) give NPCs spoken responses when API key is configured. Bridges text and character personality.

---

## Art Direction Notes

### Asset Packs: Kenney Tiny Town + Tiny Dungeon (kenney.nl)
- **License:** CC0 (public domain) — free for commercial use
- **Resolution:** 16x16 per tile, PPU 16 = 1x1 world units
- **Tiny Town (132 tiles):** Grass, paths, trees (green/autumn), houses (orange roofs), castle walls, towers, water, bridges, fences, flowers, crops, animals, props (barrels, crates, well, cart, etc.)
- **Tiny Dungeon (132 tiles):** Characters (knights, mages, rogues, monsters), items (swords, shields, potions), dungeon floors/walls, furniture, doors, chests
- **Characters:** Single tile per character — no animation frames needed
- **Tilemap:** 12 columns x 11 rows, 1px spacing between tiles

### Why Tiny Town/Dungeon over Isometric or 3D?
- Bright, cute aesthetic that matches Zelda: Echoes of Wisdom target
- 16x16 pixel art — universally appealing retro charm
- Individual PNG per tile — no sheet slicing needed
- CC0 license — zero legal risk
- Consistent scale and style across both packs
- Top-down view is simpler than isometric (no stagger calculations)
- 264 total tiles cover all needs: village, characters, items, environment
- Point filtering gives crisp pixel-perfect rendering at any screen size

---

*Next milestone: M17 — NPC waypoint routines refined, time-of-day visual changes, additional Tiny Town building compositions.*
