---
name: M14 follow-up — GPU lock shared-mode extension
description: Design + handoff prompt for extending gpu_lock.py from exclusive lock to shared lock with vram_mb capacity accounting. Change happens in SPINAI repo first (source of truth), then re-copied byte-identical to LittleLordMajesty and any other cooperating projects.
type: project
date: 2026-04-12
status: DONE
---

# GPU lock shared-mode extension — plan + handoff

## Why
After wiring LittleLordMajesty into the global GPU mutex (see
`session_handoff_m14.md` tail), we hit the design limit of the current
exclusive lock: SPINAI xray training holds the lock with 18 GB reserved but
6 GB of the 4090's 24 GB VRAM sits idle. Any other job — even a 4 GB
`image_gen` run with cpu_offload — blocks until training finishes.

Decision: extend `gpu_lock.py` to a **shared lock with VRAM partitioning**.
`acquire(vram_mb)` succeeds if `sum(active_holders.vram) + vram_mb <= capacity_mb`,
otherwise wait/skip/error per `on_busy`.

## Rollout order (IMPORTANT)
1. **SPINAI Claude** updates `D:\ImageLabelAPI_SPINAI\scripts\gpu_lock.py`
   (the source of truth) and reports new SHA-256.
2. **LittleLordMajesty Claude** re-copies the updated file to
   `D:\MakingGames\LittleLordMajesty\scripts\gpu_lock.py`, verifies hash
   equality, and commits.
3. Any future cooperating project joins by copying the same file.

The LittleLordMajesty copy must **never** diverge from SPINAI's — always
re-copy, never hand-edit.

## Handoff prompt for SPINAI Claude Code

> D:\ImageLabelAPI_SPINAI\scripts\gpu_lock.py 를 shared-mode로 확장해줘.
> LittleLordMajesty 쪽에서 현재 버전을 byte-identical로 복사해서 쓰고 있고
> (D:\MakingGames\LittleLordMajesty\scripts\gpu_lock.py), 앞으로 합류할 다른
> 프로젝트들도 이 파일을 공유할 거야. 변경은 SPINAI 쪽에서 먼저 하고, 끝나면
> 나한테 알려줘. 내가 각 프로젝트로 가서 재복사할게.
>
> ## 목표
> 지금은 한 번에 한 프로젝트만 GPU 를 잡을 수 있음 (exclusive). SPINAI xray
> training 이 18 GB 만 쓰는데 나머지 6 GB 가 놀고 있음. vram_mb 합산이
> capacity 미만이면 병행 허용하게 바꿔줘.
>
> ## 스키마 변경 (하위호환 필수)
> - 현재: `{holder_pid, holder_name, vram_estimate_mb, started_at, expires_at, ...}`
> - 신규: `{schema_version: 2, capacity_mb: 24000, holders: [ {pid, name, vram_mb, started_at, expires_at}, ... ]}`
> - `_read_state()` 는 파일이 없으면 빈 신규 스키마를 반환, 옛날 단일-holder
>   스키마를 읽으면 `holders` 리스트로 마이그레이션해서 반환. 디스크 파일은
>   다음 쓰기 때 자연스럽게 신규 스키마로 덮이게.
> - `capacity_mb` 는 환경변수 `GPU_CAPACITY_MB` 가 있으면 그걸 쓰고,
>   없으면 pynvml 로 device 0 total memory 를 조회, 실패하면 fallback 24000.
>
> ## 동작 변경
> - `acquire(name, vram_mb, on_busy="wait")`:
>   1. 파일 락 획득 (msvcrt.locking 또는 portalocker — Windows 우선)
>   2. state 읽고, expired/dead holder 청소
>   3. `sum(h.vram_mb for h in holders) + vram_mb <= capacity_mb` 면 내 holder
>      엔트리 append 하고 write + 락 해제 후 True 리턴
>   4. 안 맞으면 on_busy 분기. wait 는 poll_interval 초 대기 후 재시도.
>      로그는 "waiting for {vram_mb}MB; currently used {used}/{capacity},
>      holders: [...]"
> - `release()`:
>   1. 파일 락 획득
>   2. 내 pid 의 holder 엔트리 제거
>   3. holders 가 비면 파일 삭제, 아니면 write
> - `_is_holder_active(holder_dict)`: 엔트리 단위로 체크
> - `status()`: 전체 holders 리스트 + used_mb / capacity_mb / free_mb 출력
> - `--status` CLI 를 표 형태로:
>   ```
>   GPU lock: 18000/24000 MB used (6000 free)
>     [1] SPINAI_xray_34cls  pid=65308  vram~18000 MB  expires 12:55
>   ```
>   대기 중인 프로세스는 holders 에 안 들어감 — 로그에만.
>
> ## 동시성 (중요)
> - Windows: `msvcrt.locking(fd, msvcrt.LK_LOCK, 1)` 로 `gpu_lock.json.lock`
>   sentinel 파일 락
> - 크로스플랫폼 원하면 portalocker 의존성 추가 (pip 추가를 최소화하려면
>   Windows msvcrt + posix fcntl 분기)
> - RMW 는 전부 락 안에서. acquire 의 wait 루프는 매 iteration 마다 락을
>   새로 잡고 풀기.
>
> ## 금지
> - `LOCK_FILE` 경로 (`C:\Users\taesh\gpu_lock.json`) 변경 금지
> - CLI 인터페이스 (`--status`, `--force-release`) 유지. 출력 형식만 확장
> - `acquire` 의 기존 파라미터 (name, vram_mb, on_busy, poll_interval,
>   max_wait, ttl_hours) 제거 금지. 추가는 OK
> - `atexit` 자동 release 유지
> - `gpu_lock_context` 컨텍스트 매니저 유지
>
> ## 테스트
> 1. 단일 holder 로 acquire → status → release 라운드트립
> 2. 두 프로세스 acquire (vram 합이 capacity 미만) → 둘 다 성공
> 3. 세 번째 acquire 가 합 초과 → on_busy=wait 이면 블록, skip 이면 False
> 4. 홀더 프로세스 kill → 다른 acquire 가 dead 감지 후 성공
> 5. 옛날 스키마 json 파일 수동으로 만든 뒤 acquire 호출 → 마이그레이션 동작
> 6. `--status` 출력 포맷 확인
>
> ## 끝나면
> - 변경된 파일의 SHA-256 알려줘. 내가 LittleLordMajesty 에 재복사할 때 비교용
> - 테스트 결과 요약 (6 개 케이스 pass/fail)
> - 스키마 버전 번호 확정 (2 로 맞는지)

## Rollout 완료 (2026-04-12)
- SPINAI Claude 가 `D:\ImageLabelAPI_SPINAI\scripts\gpu_lock.py` 를 shared-mode
  로 확장 (SHA-256 `ee90f18e24d8fd362446b67188d12334aa8f75edc89a54b530d451a071787b66`,
  288→576 lines). 6/6 test cases PASS: roundtrip, 2-holders-fit, over-capacity
  wait/skip/error, dead-holder purge, v1 legacy migration, CLI status format.
- LittleLordMajesty 쪽 `scripts/gpu_lock.py` 재복사 byte-identical, py_compile OK.
- `python scripts/gpu_lock.py --status` 실행 시 pynvml 로 실제 capacity 자동
  감지: 4090 = 24564 MB (fallback 24000 대신). 새 CLI format 동작 확인.

## LittleLordMajesty 쪽 반영 (이미 완료)
- `scripts/gpu_lock.py` 재복사 후 byte-identical.
- 호출 사이트는 그대로 두면 됨 — 시그니처는 유지되므로:
  - `tools/image_gen/generate.py`: `acquire("LittleLordMajesty_image_gen", vram_mb=12000, on_busy="wait")`
  - `tools/dialogue_gen/generate.py`: `acquire("LittleLordMajesty_dialogue_gen", vram_mb=8000, on_busy="wait")`
- 이 vram_mb 값들은 shared-mode 에서도 정확함:
  - SDXL base 1.0 at fp16: ~12 GB peak (cpu_offload 시 ~4 GB — 하지만 공유
    모드에서는 cpu_offload 안 써도 될 수 있으니 12000 유지)
  - EXAONE 7.8B via ollama: ~8 GB

## 기대 효과 (shared-mode 도입 후)
- SPINAI training (18 GB) 돌 때, image_gen (cpu_offload 로 ~4 GB)을
  declare vram_mb=4000 으로 낮춰서 병행 가능
- dialogue_gen (~8 GB) 는 여전히 SPINAI 와 병행 불가 (합 26 > 24) — 대기
- SPINAI training 배치를 줄이거나 MPS 로 더 쪼개면 더 많은 병행 가능
