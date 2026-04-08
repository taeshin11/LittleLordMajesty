# 📜 PRD: LittleLordMajesty (LLM)

> **Note:** This document serves as the core compass for the project. It is a flexible, living document that can be modified and expanded at any time to accommodate changes in development progress, new features, or shifting requirements.

## 1. Project Overview
* **Product Name:** LittleLordMajesty (LLM)
* **Platforms:** Mobile (Android/iOS) & Tablet (iPad/Android Tablets)
* **Genre:** 2D Retro Strategy Simulation + AI NPC Interaction + Territory Conquest
* **Core Concept:** * "Rule the realm and overcome crises with a single word."
  * The player starts as a "Little Lord" and commands NPCs using prompts (LLM) on a journey to become "Majesty" of the continent.

    ## 2. Core Game Loop
      1. **Instruction & Communication:** The Lord (Player) issues work orders and mediates internal conflicts via text prompts to NPCs.
        2. **Internal Affairs & Prosperity:** Strengthen the castle's economic and military power through resource gathering and building construction.
          3. **Conquest & Expansion (Main Goal):** Train an army, deploy them to the World Map, occupy other AI lords' castles, and expand the territory.
            4. **Crisis Management:** Defend against unpredictable Orc raids and resolve unexpected internal events (e.g., civilian complaints, accidents) within the castle.

              ## 3. Key Feature Requirements

                ### 3.1. AI NPC & Interaction System
                  * **Gemini 1.5 Flash API:** Generates all NPC dialogues, interprets player commands, and evaluates unexpected situations (utilizing the most cost-effective model).
                    * **Persona System:** Assigns unique personalities, backgrounds, and speech styles to different professions (e.g., Vassal, Soldier, Merchant, Worker).
                      * **Voice Output (TTS):** Integration of Google Cloud TTS API.
                          * **Cost Optimization (Local Caching):** Implement a local caching system. Once an audio file is generated, it is saved to the device storage. If the same text is generated again, the game plays the cached file to minimize API calls (aiming for near-zero cost).

                              ### 3.2. Territory Management & World Map Conquest
                                  * **Internal Affairs:** Assign workers, manage resources (Wood, Food, Gold), and upgrade the building tech tree.
                                      * **Conquest System:** Scout hostile lords on the World Map, dispatch armies, and execute sieges. Victory grants territory expansion and loot.
                                          * **Defense:** Defend the castle from random, unannounced Orc raids (not a traditional wave-based tower defense, but dynamic events).

                                              ### 3.3. Unexpected Internal Events (LLM-based)
                                                  * **Conflict Resolution:** When disputes arise between NPCs (e.g., resource allocation, personality clashes), the Lord must mediate through prompt-based conversations.
                                                      * **Suspicious Visitors:** Unidentified NPCs will visit the castle. The Lord must converse with them to gather information and decide whether to hire, trade with, or banish them.
                                                          * **Disaster Management:** The Lord's direct text instructions determine the outcome of crises such as food shortages or fires.

                                                              ### 3.4. Localization & UI/UX (Device Compatibility)
                                                                  * **Dynamic Localization:** **NO in-game text should be hardcoded in a single language.** The game architecture must support dynamic localization, translating dialogues, UI elements, and API prompts according to the user's system language or in-game settings.
                                                                      * **Tablet Support:** The UI/UX must be fully responsive and optimized to scale perfectly on Tablet devices (iPad, Android Tablets) as well as standard Mobile screens.

                                                                          ## 4. Tech Stack & Development Strategy (CLI & API Centric)
                                                                              * **Engine:** Unity (C#)
                                                                                  * **Art Style:** 2D Retro Pixel Art (Cute/Chibi aesthetic)
                                                                                      * **Backend:** Firebase Spark Plan (Free tier for data storage and user authentication)
                                                                                          * **AI API:** Gemini 1.5 Flash (Prioritizing free/low-cost tiers)
                                                                                              * **Development Approach:** Minimal use of heavy third-party SDKs; rely on pure script-based API calls.
                                                                                                  * **Version Control:** Lightweight configuration management using Git CLI.

                                                                                                      ## 5. Phased Development Roadmap (MVP)
                                                                                                          1. **Phase 1 (Foundation):** Set up Unity project, implement UI responsiveness for Mobile/Tablet, and integrate Gemini API (Basic text interaction).
                                                                                                              2. **Phase 2 (Voice & Localization):** Build the dynamic localization manager, integrate Google Cloud TTS, and implement the local audio file caching logic.
                                                                                                                  3. **Phase 3 (Internal Affairs):** Establish the resource system, place 2D pixel NPCs, and implement simple prompt-based task execution (e.g., move, gather).
                                                                                                                      4. **Phase 4 (War & Events):** Design the World Map conquest logic and create the LLM prompt structures for unexpected internal events.
                                                                                                                          5. **Phase 5 (Optimization):** Final polish of UI/UX, testing on diverse screen sizes, and balancing gameplay mechanics.

                                                                                                                              make ui ux comfortable and modern

                                                                                                                                  중요 마일스톤 달성시 git push를 하도록 꼭 해줘. create the GitHub repo using gh CLI 꼭 지시해.

                                                                                                                                      research_history 폴더만들어서 폴더내에 진행상황을 마일스톤마다 기록할것.

                                                                                                                                          막힐때 CLI로 해결이 가능하면 무조건 자동화로 해. CLI 이용해서 해