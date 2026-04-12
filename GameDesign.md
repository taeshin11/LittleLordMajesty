# LittleLordMajesty — Game Design Document

> **Purpose:** A design-focused summary for discussing game direction.
> Updated each milestone with player-experience details, not code internals.
> Last updated: M16 (2026-04-12)

---

## M16: 2D Roaming Foundation

### 1. World & Lore

- **Setting:** The player inherits a tiny pixel-art castle surrounded by a courtyard of pastel greens. The world is small and intimate — a single screen's worth of walking in any direction. This is intentional: you are a *Little* Lord, not a king.
- **Narrative beat:** The game opens in medias res. No cutscene, no exposition. The player simply appears in their courtyard, surrounded by their four subjects. The smallness of the domain is the story — you must grow it through conversation and command.
- **Time:** Each real-world 5 minutes equals one in-game day. A subtle day-night cycle (via NPC schedule changes) gives the feeling of time passing. The HUD shows "Year 1, Day 1" and ticks forward.

### 2. NPC Roster & Persona

Four starting NPCs, each with a distinct personality that shapes their AI dialogue:

| Name | Profession | Personality | Starting Loyalty | Dialogue Character |
|------|-----------|-------------|-----------------|-------------------|
| **Aldric** | Vassal | Loyal | 80/100 | Formal, deferential. 20 years of service. Calls the player "my lord" without irony. Pragmatic — pushes for safe, conservative decisions. Will warn against reckless orders but ultimately obeys. |
| **Bram** | Soldier | Brave | 65/100 | Blunt, eager. Young and hungry for glory. Speaks in short, punchy sentences. Will enthusiastically agree to fight-related orders and drag his feet on farming duties. |
| **Marta** | Farmer | Hardworking | 70/100 | Warm but plain-spoken. Knows the land better than anyone. Will push back on orders that waste food or neglect the fields. Gives practical advice rooted in seasons and soil. |
| **Sivaro** | Merchant | Greedy | 40/100 | Slippery, always calculating. Low loyalty because he's here for profit, not patriotism. Will happily trade, negotiate, and deal — but anything that costs him money gets resistance. Can be won over with gold-related incentives. |

**AI Persona System:** Each NPC's Gemini system prompt encodes their personality, loyalty level, mood, background story, current task, and current location. The prompt instructs the AI to stay in character, use period-appropriate language, and keep responses to 1-3 sentences. The NPC's personality directly affects how they respond to orders (e.g., ordering Sivaro to "give food to the poor" → resistance and mood drop; ordering Bram to "patrol the walls" → enthusiasm and loyalty boost).

### 3. Core Fun & Interactions

- **Walk up → Talk:** The player physically walks to an NPC. When close enough (within ~2 tile lengths), a prompt appears: "E - Talk to Aldric". This is the core loop — proximity triggers conversation.
- **Free-text commands:** In the dialogue box, the player types anything. "Go farm the wheat field." "What do you think of Sivaro?" "I'm doubling your pay." The Gemini AI interprets the command in character.
- **Resource feedback:** Certain keywords in commands trigger resource changes:
  - "farm" / "harvest" → assigns Farming task
  - "build" / "construct" → assigns Construction task
  - "patrol" / "guard" → assigns Patrol task
  - "reward" / "praise" → mood +5, loyalty +2
  - "punish" / "threaten" → mood -10, loyalty -5
- **Quick commands:** Four profession-specific shortcut buttons appear in the dialogue box (e.g., Farmer gets "Check Crops", "Water Fields", "Harvest", "Store Grain"). These provide guidance for players who don't know what to type.
- **NPC daily routines:** NPCs aren't static — they walk between locations on a schedule (e.g., Marta walks from the wheat field at 6am → granary at 10am → town square at noon → vegetable plot at 1pm). Finding them at different locations yields different conversational context.

### 4. Events & Crises

*Not yet implemented in M16 (planned for M19).* The systems exist in code (EventManager) but aren't wired to in-world markers yet. When implemented:
- Events will spawn as visible markers (e.g., orc sprite at the gate, fire particle at a building)
- The player must walk to the event marker to trigger the crisis dialogue
- Resolution happens through conversation with the relevant NPC (e.g., ordering Bram to fight the orcs)

### 5. Aesthetic & Vibe

**Target mood: Cozy storybook kingdom.** Think Zelda: Echoes of Wisdom, not Lord of the Rings.

- **Pixel sprites (Anokolisa):** Tiny 64×64 characters with bouncy idle animations (4 frames) and smooth walk cycles (6 frames). The small scale makes the world feel toylike and approachable. Three visual NPC types — Knight (Aldric, Bram), Rogue (Marta), Wizard (Sivaro) — differentiated by subtle color tints.
- **SDXL portraits:** When talking to an NPC, a painted portrait appears in the dialogue box. These are richer and more detailed than the pixel sprites — they're the "close-up" moment, like a visual novel. The contrast between tiny pixel overworld and detailed portrait creates a sense of intimacy in conversation.
- **Pastel ground:** The courtyard is a soft green with the SDXL-painted castle courtyard as ground texture. This will transition to proper pixel art tilemap in M18.
- **UI tone:** Cream/parchment dialogue box with warm brown text. No harsh colors. The UI should feel like reading a letter from your steward, not operating a computer.
- **Sound:** TTS voices (Google Cloud) give NPCs spoken responses. This bridges the gap between text and character — hearing Aldric's formal tone vs. Bram's excited delivery adds personality beyond text.

---

*Next milestone: M17 — NPC walk/idle Anokolisa animations wired with directional facing + waypoint routines refined.*
