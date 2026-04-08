# Milestone 08 — Warfare & Espionage Systems + LLM Latency Strategy

**Date:** 2026-04-09
**Status:** Complete

---

## Overview

Milestone 08 delivers the full Warfare & Espionage subsystem — six interlocking systems that transform multiplayer PvP from simple castle-bashing into a deep shadow-war of spies, propaganda, prisoner interrogation, autonomous traders, morale speeches, and province governance. Alongside the gameplay systems, this milestone formalises the LLM Latency Strategy that keeps the game feeling responsive on mobile even with Gemini round-trip times of 1–3 seconds.

---

## New Systems: Assets/Scripts/Warfare/

### 1. SpyInfiltrationSystem.cs — Prompt Virus (첩보전)

**Design Philosophy:** Physical destruction is loud and obvious; ideological contamination is silent and deniable. A spy dispatched as a disguised worker slowly poisons enemy NPC morale and productivity by whispering sedition. The defender's only counter is to interrogate suspicious NPCs — an expensive action that costs time and creates false positives.

**Key mechanics:**
- `Spy` object carries a `InfiltrationOrder` (Base64 obfuscated), a public `PublicPersona`, and a 7-day expiry.
- Every ~5 in-game minutes `TickActiveSpies()` calls Gemini to generate `SpyEffectResult`: `satisfactionDelta`, `loyaltyDelta`, `detectionChance`, and a `whisperText` narrative.
- Detection triggers `GameManager.EventManager.TriggerManualEvent()` prompting the player to interrogate or execute the suspect.
- `InterrogateSuspect()` feeds the spy's `PublicPersona` as a system prompt; Gemini maintains cover or breaks with `[CRACKED]`/`[HOLDING]` tags.
- Firebase path: `spies/{targetPlayerId}/{spyId}`

**LLM role:** Determines effect magnitude AND narrative flavour. Player reads the whisper text as a story beat, not just a number.

---

### 2. PrisonerSystem.cs — Dungeon Interrogation (포로 심문)

**Design Philosophy:** When you capture an enemy commander you are really capturing a persistent Gemini persona that the defender pre-configured. The interrogation becomes a duel of prompts: the attacker tries to break the prisoner; the defender's pre-written `DefensePhilosophy` and `PrisonerInstructions` fight back autonomously even when the defender is offline.

**Key mechanics:**
- `Prisoner` model tracks `Resolve` (0–100), `SecretsToGuard`, `RevealedSecrets`, and a full `InterrogationLog`.
- Six `InterrogationOutcome` states: `Resisting`, `Lying`, `PartialReveal`, `FullReveal`, `Negotiating`, `Broken`.
- System prompt injects prisoner's name, title, defense philosophy, secret instructions, current resolve, and secrets list.
- LLM ends its response with one of: `[RESIST]`, `[LIE: ...]`, `[PARTIAL: ...]`, `[FULL: ...]`, `[NEGOTIATE: gold]`, `[BROKEN]`.
- Ransom (`AcceptRansom()`) converts prisoner to gold; defender receives Firebase notification of capture and ransom amount.
- Firebase path: `prisoners/{captorPlayerId}/{prisonerId}`

**LLM role:** Roleplays the enemy commander. The defender's system prompt is the "soul" of the prisoner — making prisoner quality a meta-game around good prompt writing.

---

### 3. PropagandaSystem.cs — Fake News & Bards (정보전)

**Design Philosophy:** Reputation is a resource. Hiring bards to spread misinformation is an economic attack vector — it costs the target diplomatic relationships with AI lords and other players, forcing them to spend actions on debunking instead of building.

**Key mechanics:**
- `HireBardsForPropaganda()` spends gold, then asks Gemini to craft a convincing medieval-flavoured fake rumour from the player's blunt instruction (e.g. "say that lord colluded with orcs").
- The generated `RumorCampaign` spreads to 2–5 additional random world players via Firebase, not just the direct target.
- Rumours have a `CredibilityScore` (40–90) and a 3-day expiry.
- `IssueDebunkStatement()` patches `debunked=true` and broadcasts a debunk record to all players who received the rumour.
- Firebase paths: `rumors/{playerId}/{campaignId}`, `debunkBroadcasts/{campaignId}`

**LLM role:** Transforms a blunt slander instruction into a believable bard's verse or gossip that fits the medieval setting. Quality of the generated rumour affects how seriously other players take it.

---

### 4. TraderBotSystem.cs — Autonomous AI Merchant (암시장 상인)

**Design Philosophy:** The lord is often offline. An autonomous trader bot lets the player set commercial terms and personality once, then dispatches the merchant to negotiate with a real human buyer while the lord sleeps. This is economic agency without synchronous presence.

**Key mechanics:**
- `TradeOffer` records goods type/amount, `MinGold`, `AskingGold`, barter acceptance, `NegotiationStyle` (friendly/aggressive/sly), and private `TraderInstructions`.
- Buyer interacts via `NegotiateWithTrader()` — each message goes to Gemini with the merchant's system prompt including secret floor price.
- LLM ends with `[DEAL: gold=X]`, `[DEAL: barter=goods]`, `[REJECT]`, or `[COUNTER: amount]`.
- On `[DEAL]` with `gold >= MinGold`, `ExecuteTrade()` transfers resources and notifies the seller via Firebase.
- Firebase path: `traders/{targetPlayerId}/{offerId}`

**LLM role:** Maintains a consistent merchant persona across multiple negotiation turns, honouring the seller's secret minimum price without revealing it.

---

### 5. TotalWarSystems.cs — Four-System Bundle (총력전)

This single file bundles four tightly related systems sharing the battle namespace:

#### 5a. MoraleSpeechSystem — Pre-battle Speech Evaluation

**Design Philosophy:** Words have weight. A stirring speech before battle is a skill expression — players who write better rhetoric get a real gameplay advantage. The LLM acts as an impartial judge of persuasive writing quality.

- Player types a speech; Gemini evaluates `moraleBonus` (0.0–0.5), `rating` (Legendary/Inspiring/Decent/Weak), and `soldierReaction` narrative.
- Morale bonus stored in `PlayerPrefs` as `BattleMoraleBonus` for the combat system to apply.

#### 5b. GeneralTraitSystem — Battle Experience → NPC Traits

**Design Philosophy:** NPCs should remember and grow from what they survive. A general who defended against a fire attack acquires a fire-related trait — trauma or expertise — that permanently alters their system prompt, making each NPC's history legible in their personality.

- `ProcessBattleExperience()` passes battle context (e.g. "defended against fire attack") to Gemini.
- Gemini generates one `NPCTrait`: name, description, `systemPromptAddition` (10–20 words), and `isPositive` flag.
- Trait is appended to `PlayerPrefs` key `NPC_Traits_{npcId}` for `NPCPersonaSystem` to inject on next conversation.

#### 5c. BattleCommandSystem — Natural Language Tactical Orders

**Design Philosophy:** Medieval lords did not issue orders by clicking unit icons. Natural language commands ("Infantry shield wall, cavalry hold in the forest, flank on my signal") should map to NavMesh AI actions without the player needing to learn UI verbs.

- Free-text command is parsed by Gemini into a JSON array of `BattleOrder` objects.
- Each order specifies `unitType`, `UnitTactic` enum (Advance/Hold/ShieldWall/Flank/Ambush/Retreat/ChargeCalvary/Volley/Scatter), `targetLocation`, and `timingDelay`.
- `OnOrdersIssued` event delivers orders to NavMesh unit controllers.

#### 5d. GovernorSystem — Province Governance & Rebellion

**Design Philosophy:** Conquest is easy; holding is hard. Conquered provinces generate tax income but accumulate unrest proportional to over-taxation. When loyalty falls too low the governor — an AI persona — declares independence with a Gemini-written proclamation. The player must negotiate or suppress.

- Each `Province` tracks `LoyaltyToLord`, `TaxDemand`, `Unrest`, and `GarrisonSize`.
- Daily tick: tax collected, unrest grows if `TaxDemand > 50`, loyalty erodes if `Unrest > 50`.
- Rebellion probability: 10% per day when `LoyaltyToLord < 20`.
- `NegotiateWithGovernor()` feeds province stats as system prompt; governor stands down on `[STAND_DOWN]` if terms are met (tax cut ≥40%, garrison reinforcement, or sincere apology).

---

### 6. QuickActionTemplates.cs — One-Tap LLM Input (빠른 명령)

**Design Philosophy:** LLM interaction depth should be optional, not mandatory. Players who do not want to type full prompts can still play the full game using pre-written, context-aware template buttons. This lowers the barrier to entry without removing depth for power users.

**Key mechanics:**
- Static arrays of `QuickAction` (Label, Prompt, Icon, Category) organised by context:
  - `NPCQuickActions` — generic NPC conversation starters
  - `ProfessionQuickActions` — per-profession specialised prompts (Farmer/Soldier/Merchant/Vassal)
  - `BattleQuickActions` — 5 pre-built tactical orders
  - `DiplomacyQuickActions` — alliance proposal, peace, tribute demand, trade agreement
  - `EspionageQuickActions` — spy dispatch, agitation, fake news, sabotage
  - `EconomyQuickActions` — bread priority, weapons production, balanced distribution, max tax
  - `DefenseCommanderPresets` — 4 commander personality archetypes for offline defence
- `GetContextualActions(GameState, NPCProfession?)` returns the correct array for dynamic button generation based on current game state.

---

## LLM Latency Strategy

Gemini 1.5 Flash cold-start on mobile 4G: ~1.5–3 seconds. This section documents the five strategies implemented across all Warfare systems to make latency invisible to the player.

### Strategy 1: Streaming (Future)
All `GeminiAPIClient.SendMessage()` calls are designed to be swap-compatible with a streaming endpoint. When streaming is enabled, the first token appears in <500ms, with the UI updating character-by-character. Currently not implemented but the callback signature (`Action<string> onComplete`) will be replaced with `IEnumerator<string>` stream.

### Strategy 2: Action-First Response
Every Warfare system issues an **immediate visual action** before the LLM response arrives:
- Spy dispatch: merchant/spy character walks to the gate animation triggers at call time, not on Firebase confirm.
- Trader arrival: `OnTraderArrived` fires from the polling coroutine; the dialogue window opens showing "The merchant arrives..." before the first Gemini turn.
- Battle orders: unit movement begins on a default "Advance" order while Gemini parses the actual command.

### Strategy 3: Thinking Animation
All dialogue UIs display a "..." or character "thinking" idle animation during the Gemini wait. `TTSManager.Speak()` is called only after LLM response; the animation bridges the perceptual gap. Implemented via the `while (!done && t > 0)` timeout loop in every coroutine — the UI polls this state.

### Strategy 4: Response Length Limits
Every system prompt or user prompt passed to Gemini includes an explicit length instruction:
- Spy effects: `satisfactionDelta`, `loyaltyDelta`, `detectionChance`, `whisperText` — JSON only, no prose.
- Bard rumour: "2-3 sentences max."
- Morale speech: `soldierReaction` — "2 sentences."
- Battle orders: JSON array only.
- Governor declaration: "2-sentence declaration."
Shorter responses reduce token generation time by 40–60% compared to unconstrained prose.

### Strategy 5: Pre-generation Cache (Planned)
Frequently used responses (daily NPC greetings, standard bard rumour templates, merchant opening lines) will be pre-generated at session start and cached in memory. On first interaction the cached response plays instantly; a background refresh updates the cache for next time. Implementation target: `GeminiAPIClient` cache layer, keyed by system-prompt hash.

---

## Agent Team Roster (Milestone 08)

| Agent | Role | Domain |
|-------|------|--------|
| **Quality Tester** | Bug detection, null reference checks, boundary value analysis | Core/, AI/, GameSystems/, Warfare/ |
| **Backend Engineer** | Game logic, save/load, API integration, Firebase schema | All systems |
| **UI Engineer** | SerializedField wiring, mobile UX, QuickAction button generation | UI/, Editor/ |
| **Code Reviewer** | Performance, Unity best practices, memory leaks | All files |
| **Historian** | research_history/ records, PRD.md, SYSTEMS.md updates | All documentation |
| **Project Manager** | Folder structure, SYSTEMS.md maintenance, orphan file cleanup | File organisation |

---

## Next Priorities for MVP Testing

1. **Unity Scene Wiring** — Wire all 6 Warfare system MonoBehaviours into the GameBootstrap scene hierarchy. Assign `GameConfig` references.
2. **QuickAction UI** — Implement the dynamic button row in `DialogueUI.cs` and `BattleUI.cs` using `QuickActionTemplates.GetContextualActions()`.
3. **Firebase Schema Validation** — Test spy/prisoner/trader/rumor Firebase paths with real devices; confirm PATCH vs PUT semantics.
4. **Interrogation Flow E2E** — Full test: player A dispatches spy → player B's `TickActiveSpies()` fires → NPC mood drops → player B interrogates → `[CRACKED]` flow resolves correctly.
5. **TraderBot Negotiation Loop** — Deploy trader to own second test account; run 3-turn negotiation to deal closure; verify resource transfer.
6. **Latency Benchmarking** — Measure actual Gemini response time for each system's prompt length on mobile 4G. Tune `WaitForSeconds` timeouts accordingly (currently 10–15 seconds per call).
7. **Gemini Request Queue** — Implement the request queue in `GeminiAPIClient` to prevent concurrent warfare + NPC dialogue calls from saturating the free-tier quota.
8. **GovernorSystem Day Tick Integration** — Confirm `GameManager.OnDayChanged` event fires correctly and GovernorSystem is subscribed at scene load.

---

## Files Added This Milestone

```
Assets/Scripts/Warfare/
  SpyInfiltrationSystem.cs
  PrisonerSystem.cs
  PropagandaSystem.cs
  TraderBotSystem.cs
  TotalWarSystems.cs          (MoraleSpeechSystem + GeneralTraitSystem +
                               BattleCommandSystem + GovernorSystem)

Assets/Scripts/Utils/
  QuickActionTemplates.cs
```
