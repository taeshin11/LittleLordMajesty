# M12 세션 인계사항 (2026-04-09)

## 이번 세션에서 완료한 것

### M12 Phase A — Steam + 빌드 + 에러핸들링
- `SteamManager.cs` — Facepunch.Steamworks, 10개 업적, `#if USE_STEAM` 가드
- `ErrorHandler.cs` — Application.logMessageReceived, crash_log.txt, 복구 UI
- `.github/workflows/build-local.yml` — self-hosted runner, WebGL+Windows 빌드
- `SceneAutoBuilder.cs` — `BuildWebGL()`, `BuildWindows()` CI 진입점 추가
- WebGL Build Support 모듈 설치 완료

### M12 Phase B — 튜토리얼
- `TutorialUI.cs` — 새 파일. 오버레이, 타이프라이터(maxVisibleCharacters), 하이라이트, 스킵
- `TutorialSystem.cs` — DontDestroyOnLoad 추가, SkipTutorial→OnTutorialComplete, ResetTutorial(), PlayerPrefs.Save()
- `GameManager.cs` — NewGame()에서 ResetTutorial→StartTutorial
- `UIManager.cs` — OpenDialogue에서 "talk_to_aldric" 완료, SendCommand에서 "issue_command" 완료
- `BuildingManager.cs` — TryBuild에서 "build_farm" 완료

### M12 Phase C — LLM 최적화
- `GeminiAPIClient.cs` — gemini-2.0-flash-lite, system_instruction 네이티브, SendMessageStreaming(SSE), NullValueHandling.Ignore
- `NPCPersonaSystem.cs` — ConversationSummary, CompressHistory(), GetContextualHistory()
- `NPCManager.cs` — GetContextualHistory() 사용으로 변경

### 인프라
- Self-hosted runner: `C:\actions-runner\` (Startup 바로가기로 자동실행)
- WebGL Build Support 설치됨 (7-zip도 설치됨)
- 32개 버그 수정 (에이전트 QA/CodeReview/Backend 리뷰 포함)

## 빌드 상태
- **마지막 빌드:** run 24170875761 ✅ (WebGL 5m57s)
- **컴파일:** 오류 없음
- **artifacts:** GitHub Actions에서 WebGL-Build-Local 다운로드 가능

## 다음 세션에서 해야 할 것

### P0 — Alpha 플레이테스트
1. Unity Editor 열기 → Bootstrap.unity → Play
2. Main Menu → New Game → Castle 뷰 확인
3. 튜토리얼 7단계 진행 확인
4. NPC 클릭 → Gemini 응답 확인 (API 키 필요)
5. 건설, 월드맵 전환 확인
6. 10회 반복 무오류 달성

### P1 — Steam 배포 준비
- Steamworks 파트너 계정 설정
- 스토어 페이지 메타데이터 (설명, 스크린샷, 태그)
- Steamworks SDK 빌드 업로드 스크립트

### P2 — 비주얼 개선
- Kenney.nl low-poly 에셋으로 프리미티브 교체
- NPC 애니메이션 (idle/walk)
- 파티클 효과 (건설, 이벤트)

## 주의사항
- UAC 필요한 작업: `Start-Process -Verb RunAs` → 유저에게 "예" 클릭 요청
- Gemini API 키: GameConfig.asset에 이미 설정됨 (gitignored)
- Runner가 이미 실행 중인지 확인 후 워크플로우 트리거 할 것
- `SaveSystem.cs`에 TutorialCompleted 필드 아직 없음 (PlayerPrefs만 사용중)
