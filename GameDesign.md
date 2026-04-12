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

Four starting NPCs, each with a distinct Kenney character sprite and AI personality:

| Name | Profession | Personality | Loyalty | Visual | Dialogue Character |
|------|-----------|-------------|---------|--------|-------------------|
| **Aldric** | Vassal | Loyal | 80 | Blue-haired knight type | Formal, deferential. 20 years of service. Calls the player "my lord" without irony. Pragmatic — pushes for safe decisions. |
| **Bram** | Soldier | Brave | 65 | Brown-haired adventurer | Blunt, eager. Young and hungry for glory. Short punchy sentences. Enthusiastic about combat, drags feet on farming. |
| **Marta** | Farmer | Hardworking | 70 | Green-shirted villager | Warm, plain-spoken. Knows the land. Pushes back on orders that waste food. Gives practical seasonal advice. |
| **Sivaro** | Merchant | Greedy | 40 | Dark-haired trader | Slippery, calculating. Low loyalty — here for profit. Resists charity, responds to gold incentives. |

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

**Target mood: Cozy pixel storybook kingdom.**

- **Art style: Kenney RPG Urban Pack** — Clean, colorful 16x16 pixel art. Characters are small and charming with 4 directional sprites. The art is deliberately simple and readable — no visual clutter. Each character is immediately distinguishable by hair/outfit color.
- **Walk animation:** Characters bob gently when moving (sine wave on Y axis), giving life without requiring animation frames. When idle, perfectly still.
- **Environment:** Green grass courtyard with trees forming a natural perimeter. Bushes scattered inside for visual texture. The courtyard feels like a protected garden — safe, intimate, yours.
- **Dialogue box:** Dark semi-transparent panel at screen bottom. NPC portrait (their front-facing sprite, scaled up) on the left, name + message text on right. Modern, clean look — not medieval parchment. Quick command buttons below text, input field + send at bottom.
- **Color palette:** Earthy greens (grass), warm browns (UI accents), soft whites (text). The overall feel is warm daylight in a small garden.
- **Sound:** TTS voices (Google Cloud) give NPCs spoken responses when API key is configured. Bridges text and character personality.

---

## Art Direction Notes

### Asset Pack: Kenney RPG Urban Pack (kenney.nl)
- **License:** CC0 (public domain) — free for commercial use
- **Resolution:** 16x16 per tile
- **Characters:** 18 unique character types, 4 directions each (front/back/left/right)
- **Environment:** Ground tiles, trees, bushes, buildings, furniture, props
- **Expansion:** Kenney has 100+ compatible packs in the same style

### Why Kenney over Anokolisa?
- Consistent tile sizes (always 16x16, no mixed 32x32/64x64 frame confusion)
- Individual PNG per sprite (no complex sheet slicing needed)
- Huge variety in one pack (486 tiles in RPG Urban alone)
- CC0 license — zero legal risk
- Clean, modern pixel art style — "cozy" not "dark fantasy"

---

*Next milestone: M17 — NPC walk animations, waypoint routines refined, time-of-day visual changes.*
