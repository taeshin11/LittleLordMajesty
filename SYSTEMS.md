# LittleLordMajesty — Systems & Content Index

> **Project Manager Agent가 관리하는 파일.** 새 시스템/NPC/캐릭터 추가 시 반드시 업데이트.

---

## 📁 폴더 구조

```
Assets/Scripts/
├── Core/           핵심 게임 로직 (싱글톤 매니저)
├── AI/             Gemini AI + NPC 페르소나
├── NPC/            NPC 관리, 방문자, 승급
├── Civilization/   Anno+문명 시스템 (생산체인, 인구, 칙령, 연구)
├── GameSystems/    건물, 전투, 외교, 튜토리얼
├── World/          세계 지도, 3D 씬
├── Multiplayer/    LordNet PvP, 사절단, 수비대장, 국가
├── UI/             모든 UI 스크립트
├── Audio/          BGM, SFX, TTS
├── Firebase/       Firebase REST API
├── Localization/   7개 언어 현지화
├── Input/          터치/키보드 입력
├── Utils/          디버그 콘솔, 플레이스홀더 아트
├── Data/           ScriptableObject 데이터 클래스
└── Editor/         Unity 에디터 자동화 도구
```

---

## 🔧 시스템 목록

### Core (핵심)
| 파일 | 역할 | 주요 이벤트/API |
|------|------|----------------|
| `GameManager.cs` | 게임 상태 머신, 일/년 사이클, 영주 칭호 | `OnGameStateChanged`, `OnDayChanged`, `TogglePause()` |
| `ResourceManager.cs` | 목재/식량/금화/인구 관리 | `OnResourceChanged`, `TrySpend()`, `AddResource()` |
| `SaveSystem.cs` | JSON 저장/로드 (백업, 플랫폼별 경로) | `Save()`, `Load()` |
| `GameBootstrap.cs` | 게임 시작 시 매니저 초기화 순서 | - |
| `PlatformManager.cs` | 플랫폼 감지 (Mobile/PC), 저장 경로 | `SaveDirectory`, `IsMobile` |

### AI / NPC
| 파일 | 역할 | 주요 이벤트/API |
|------|------|----------------|
| `GeminiAPIClient.cs` | Gemini 1.5 Flash REST API (캐시, 재시도, 큐) | `SendMessage()` |
| `NPCPersonaSystem.cs` | NPC 페르소나 ScriptableObject | `GenerateSystemPrompt()` |
| `NPCManager.cs` | NPC 생성/관리, 기분/충성도, 3D 위치 | `OnNPCAdded`, `IssueCommandToNPC()` |
| `MysteriousVisitorSystem.cs` | 정체불명 방문자 (8가지 타입) | - |
| `NPCPromotion.cs` | NPC 5단계 승급 (XP, 직업별 능력) | - |

### Civilization (Anno + 문명 시스템)
| 파일 | 역할 | 주요 이벤트/API |
|------|------|----------------|
| `ProductionChainManager.cs` | Anno 스타일 생산 체인 (밀→밀가루→빵 등) | `OnGoodsProduced`, `OptimizeWorkforceFromNPC()` |
| `PopulationClassSystem.cs` | 인구 계급 (농노→농민→시민→귀족), 세금, 불만도 | `OnTierChanged`, `OnComplaint`, `OnTaxCollected` |
| `LordDecreeSystem.cs` | 영주 칙령 = 전체 NPC System Prompt 오버라이드 | `OnDecreeProclaimed`, `ProclaimDecree()` |
| `ResearchSystem.cs` | 학자 NPC 기반 테크트리 (자연어 연구 명령) | `OnResearchCompleted`, `IssueResearchOrder()` |

### GameSystems
| 파일 | 역할 |
|------|------|
| `BuildingManager.cs` | 건물 테크트리 16종 |
| `CombatSystem.cs` | 오크 습격 방어 |
| `DiplomacySystem.cs` | AI 영주와 텍스트 외교 |
| `TutorialSystem.cs` | 7단계 튜토리얼 |

### World / 3D Scene
| 파일 | 역할 |
|------|------|
| `WorldMapManager.cs` | 35개 영토, AI 영주 3명, 정찰/공성전 |
| `CastleScene3D.cs` | 3D 이소메트릭 씬 (지형, 건물, NPC 캐릭터) |
| `NPC3DClickHandler.cs` | 3D NPC 오브젝트 탭 감지 |

### Multiplayer (LordNet)
| 파일 | 역할 |
|------|------|
| `LordNetManager.cs` | 비동기 PvP 세계 (Firebase), 플레이어 등록/존재 확인 |
| `AmbassadorSystem.cs` | AI 사절단 파견 (Gemini가 발신자 외교관 연기) |
| `DefenseCommanderSystem.cs` | 오프라인 수비대장 AI (공격자와 협상 가능) |
| `NationConstitutionSystem.cs` | AI 재상 (헌법 기반 System Prompt로 운영) |
| `NationUI.cs` | 국가 창설/가입, 채팅, 외교 UI |

### Monetization
| 파일 | 역할 |
|------|------|
| `MonetizationManager.cs` | 지혜의 두루마리, 보상형 광고, 전설의 영웅 NPC, 월정액 |

### Audio / TTS
| 파일 | 역할 |
|------|------|
| `AudioManager.cs` | BGM 크로스페이드, SFX 풀 |
| `TTSManager.cs` | Google Cloud TTS + MD5 파일 캐시 |

---

## 👥 NPC 목록

### 시작 NPC (고정)
| ID | 이름 | 직업 | 성격 | 3D 위치 |
|----|------|------|------|---------|
| vassal_01 | Aldric | Vassal | Loyal | (0, 0, 2) |
| soldier_01 | Bram | Soldier | Brave | (3, 0, -1) |
| farmer_01 | Marta | Farmer | Hardworking | (-3, 0, -1) |
| merchant_01 | Sivaro | Merchant | Greedy | (2, 0, -3) |

### 전설 NPC (유료, MonetizationManager)
| ID | 이름 | 직함 | 아키타입 | 가격 |
|----|------|------|---------|------|
| zhuge_liang | Kongming | The Sleeping Dragon | Strategist | 300 gems |
| shadow_spy | The Whisperer | Master of Shadows | Spy | 250 gems |
| iron_general | Ironheart | The Unbroken General | Warrior | 280 gems |

### AI 영주 (World Map)
| ID | 이름 | 성격 |
|----|------|------|
| lord_ai_01 | Lord Aldric | Expansive |
| lord_ai_02 | Lady Seren | Diplomatic |
| lord_ai_03 | Warchief Krath | Aggressive |

---

## 🏗️ 생산 체인 (Production Chains)

```
Wheat Field → Windmill → Bakery → Bread (食 x3)
Hop Farm → Brewery → Ale (食 x2)
Sheep Farm → Weaving Mill → Cloth
Iron Smelter (Wood→Iron) → Weaponsmith → Weapons
```

---

## 🔬 테크트리 (Research System)

| 기술 | 카테고리 | 일수 | 선행 |
|------|----------|------|------|
| Iron Working | Military | 5 | - |
| Crossbow | Military | 8 | Iron Working |
| Siege Engineering | Military | 12 | Crossbow + Masonry |
| Cavalry Tactics | Military | 7 | Iron Working |
| Trade Routes | Economy | 4 | - |
| Banking | Economy | 8 | Trade Routes |
| Merchant Guilds | Economy | 6 | Banking |
| Masonry | Construction | 4 | - |
| Architecture | Construction | 7 | Masonry |
| Writing | Diplomacy | 3 | - |
| Code of Laws | Diplomacy | 8 | Writing |
| Herbalism | Nature | 4 | - |
| Astrology | Nature | 6 | Writing + Herbalism |

---

## 👤 Agent Team

| 에이전트 | 역할 | 담당 영역 |
|---------|------|---------|
| **Quality Tester** | 버그, null 참조, 경계값 오류 탐지 | Core/, AI/, GameSystems/ |
| **Backend Engineer** | 게임 로직, 저장/로드, API 연동 | 모든 시스템 |
| **UI Engineer** | SerializedField 와이어링, 모바일 UX | UI/, Editor/ |
| **Code Reviewer** | 성능, Unity 모범 사례, 메모리 누수 | 전체 |
| **Historian** | research_history/ 기록, PRD.md 업데이트 | 문서 전체 |
| **Project Manager** | 폴더 구조 유지, SYSTEMS.md 업데이트 | 파일 조직 |

### Project Manager 에이전트 사용법
새 파일/시스템 추가 후 Historian과 함께 실행:
```
역할: 새로 추가된 스크립트를 올바른 폴더에 배치하고,
      SYSTEMS.md의 시스템/NPC/캐릭터 목록을 최신 상태로 업데이트.
      새 폴더 필요 시 생성, 고아 파일 정리.
```

---

## 🔑 API 키 설정 위치
`Assets/Resources/Config/GameConfig.asset` (Unity 에디터에서 설정)
- `GeminiAPIKey` — Gemini 1.5 Flash
- `GoogleCloudAPIKey` — TTS
- `FirebaseAPIKey` — Firebase
- `FirebaseDatabaseURL` — Firebase Realtime DB URL
