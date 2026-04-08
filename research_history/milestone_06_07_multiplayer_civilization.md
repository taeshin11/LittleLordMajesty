# Milestone 06+07 — LordNet 멀티플레이어 + Anno/문명 시스템

**Date:** 2026-04-09
**Status:** Complete

---

## Milestone 06: LordNet 멀티플레이어 + Freemium 수익화

### 핵심 설계 원칙
- **비동기 PvP** (Clash of Clans 방식) — 실시간 서버 불필요, Firebase만으로 구현
- **AI 대리인** — 오프라인 플레이어를 Gemini AI가 대신 응대
- **신원 모호성** — AI 영주와 실제 플레이어가 겉으로 구분 불가능

### 구현된 시스템

#### AmbassadorSystem.cs
- 영주가 외교관 NPC에게 임무 지시 → Firebase에 파견
- 수신자 접속 시 성문 앞에서 Gemini가 발신자 외교관 연기
- 협상/수락/거절 결과 발신자에게 전달

#### DefenseCommanderSystem.cs
- 오프라인 수비 AI: 영주가 직접 수비 철학 작성 (System Prompt)
- 공격자가 수비대장과 실시간 협상 가능 (금화 뇌물, 군사 위협)
- 결과 태그: [ONGOING], [NEGOTIATED: 금액], [BREACHED], [REPELLED], [RETREATED]

#### NationConstitutionSystem.cs
- 국가 창설 시 왕이 헌법(System Prompt) 작성
- AI 재상이 헌법에 따라 자원 배분, 퀘스트 발행, 국정 운영
- [GRANT: gold=200], [QUEST: description] 태그로 실제 게임 효과 발동

#### MonetizationManager.cs
- **지혜의 두루마리**: AI 대화 횟수 제한 (무료 30/일, 광고로 충전)
- **보상형 광고**: 이국 상인, 파랑새 정찰 힌트, 신령의 가호, 건설 가속
- **전설의 영웅 NPC**: 프리미엄 System Prompt = 게임 컨텐츠 (Kongming, The Whisperer, Ironheart)
- **영주의 축복 월정액**: 무제한 두루마리 + 광고 제거

---

## Milestone 07: Anno 1404 + 문명 5 시스템

### 핵심 설계 원칙
- LLM을 UI 대신 사용: 복잡한 메뉴 클릭 대신 NPC에게 자연어 명령
- Anno의 생산 체인 + 인구 욕구 시스템
- 문명 5의 사회 정책(칙령) + 테크트리(학자 NPC)

### 구현된 시스템

#### ProductionChainManager.cs
- **9개 생산 체인**: 밀→밀가루→빵, 홉→맥주, 울→천, 철→무기 등
- 내정관 NPC에게 "밀가루가 넘치니 빵집 돌려" → Gemini가 최적 일꾼 배분
- 완성품은 ResourceManager에 자동 반영 (빵 = 식량 x3)

#### PopulationClassSystem.cs
- **5계급**: 농노→농민→시민→부르주아→귀족
- 각 계급의 수요 충족 시 자동 승격 (빵, 맥주, 천, 무기 안전, 도서관 등)
- 불만도 < 30% → 시민 대표 NPC가 찾아와 Gemini로 항의 (TTS 재생)
- 매일 세금 자동 징수 (귀족 1인당 20금화/일)

#### LordDecreeSystem.cs
- 영주가 칙령 자연어 선포 → 키워드 분석으로 아키타입 감지
- 5개 아키타입: military / commerce / peace / culture / expansion
- 칙령이 ResourceManager 생산 배율에 즉시 적용
- 전체 NPC의 말투가 칙령에 따라 변경 (PlayerPrefs로 전파)

#### ResearchSystem.cs
- **13개 기술**: 군사(철제무기/석궁/공성), 경제(무역/은행), 건설, 외교, 자연
- 학자 NPC에게 "오크 가죽 뚫는 무기 연구해" → Gemini가 최적 기술 선택
- 인게임 일수로 연구 진행, 완료 시 학자가 보고 + ToastNotification

---

## 신규 파일 전체

```
Assets/Scripts/Multiplayer/
  LordNetManager.cs
  AmbassadorSystem.cs
  DefenseCommanderSystem.cs
  NationConstitutionSystem.cs
  NationUI.cs
  MonetizationManager.cs

Assets/Scripts/Civilization/
  ProductionChainManager.cs
  PopulationClassSystem.cs
  LordDecreeSystem.cs
  ResearchSystem.cs

SYSTEMS.md  ← 시스템/NPC/테크 마스터 인덱스 (신규)
```

---

## Agent Team 현황

| 에이전트 | 역할 |
|---------|------|
| Quality Tester | 버그, null 참조 탐지 |
| Backend Engineer | 게임 로직, 저장/로드, API |
| UI Engineer | SerializedField 와이어링, UX |
| Code Reviewer | 성능, 메모리, Unity 모범 사례 |
| Historian | research_history, PRD.md 업데이트 |
| Project Manager | 폴더 구조, SYSTEMS.md 유지 |

---

## 다음 마일스톤 (Milestone 08)

Priority:
1. SceneAutoBuilder SettingsUI/Toast 와이어링 수정 (UI Engineer 발견)
2. 건물/외교 상태 Save/Load 추가 (Backend 발견)
3. LordDecreeSystem을 NPCPersonaSystem.GenerateSystemPrompt()에 통합
4. ProductionChainManager를 BuildingManager 완공 이벤트에 연결
5. PC 화면 Landscape 레이아웃 (UIManager 분기)
6. Gemini API 요청 큐 구현
