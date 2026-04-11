---
name: M14 session handoff (4090 migration)
description: Context for resuming the 4090 PC migration after restarting Claude Code as administrator. The previous (non-elevated) session got everything except Unity Editor install done; the editor install requires elevation that the current session can't trigger.
type: project
date: 2026-04-11
status: HANDOFF
---

# Session handoff — 4090 migration resume

The migration from the laptop to the 4090 PC is ~90% done. Everything that
doesn't need Windows admin rights is finished. The blocker is installing
Unity Editor 2022.3.62f1, which writes to `C:\Program Files\Unity\Hub\Editor\`
and therefore needs elevation. Multiple attempts to trigger UAC from the
non-elevated Claude Code session failed (UAC dialog never visible to user).

The decision was: close Claude Code, relaunch it as administrator, then
resume from this document. With an admin Claude Code, every child process
(including the Unity installer) inherits elevation, so no UAC prompts at all.

## What's already done (committed and pushed to origin/master)

Latest published commit: `2d6f819 m14: cut all Gemini image calls — LocalArtBank first-choice everywhere`

Done before the handoff and **already on origin/master**:

- 4090 dev environment up: torch 2.6.0+cu124, diffusers 0.37.1, transformers,
  accelerate, playwright, ollama (with `exaone3.5:7.8b` and `exaone3.5:32b`
  pulled), node, gh CLI authenticated as `taeshin11`.
- Repo cloned to `D:\MakingGames\LittleLordMajesty` (the C: path is a
  directory junction → D: so the legacy CI workflow's `/c/MakingGames/...`
  GameConfig injection still resolves).
- Self-hosted runner registered: `4090-desktop` on the 4090 PC.
- The old laptop runner `LittleLordMajesty-PC` is gone from the GitHub
  runner list (laptop process killed, GitHub idle-deregistered it).
- Gemini API key rotated. New key is in `Assets/Resources/Config/GameConfig.asset`
  (gitignored) and in the Dropbox copy at
  `C:\Users\taesh\Dropbox\012_업무상황기록\KTS\0.공유\새 폴더\GameConfig.asset`.
  Two prior leaked keys were redacted from `milestone_13_wasm_gear_resume.md`.
- Gemini image API calls cut at all 5 sites — `LocalArtBank` is now the
  first-choice path in `MainMenuUI`, `WorldMapUI`, `CastleViewUI` (background
  + NPC card portraits), and `NPCInteractionUI`. Gemini is fallback only.
  New `bg_main_menu.png` baked. New `LocalArtBank.GetMainMenuBackground()`
  helper. Net: zero Gemini API calls in the deployed WebGL build once Unity
  rebuilds and gh-pages redeploys.
- Local smoke tests passed (live playwright, dialogue 7.8B, image gen).
- CI runner pickup verified: commit `2d6f819` was picked up by `4090-desktop`,
  workflow ran end-to-end except the build step which failed with
  `Unity.exe: No such file or directory` — exactly as expected for
  the missing Editor.

## What's done locally but **not yet committed** (waiting for the handoff commit)

These are real, on-disk, validated changes that the next session should
commit + push:

1. `tools/image_gen/generate.py`
   - Switched the pipeline from `stabilityai/sdxl-turbo` to
     `stabilityai/stable-diffusion-xl-base-1.0` with `use_safetensors=True`.
   - Removed `enable_model_cpu_offload()`, just `pipe.to("cuda")` since the
     4090 has 24 GB.
   - `num_inference_steps` default 4 → 30, `guidance_scale` 0.0 → 7.0
     (the right values for SDXL base, not Turbo).
   - All 8 PNGs in `Assets/Resources/Art/Generated/` re-baked against the
     new pipeline at ~4 s/image. Files updated on disk:
     `bg_main_menu.png`, `bg_castle_courtyard.png`, `bg_world_map.png`,
     `bg_battle_field.png`, `portrait_vassal_01.png`, `portrait_soldier_01.png`,
     `portrait_farmer_01.png`, `portrait_merchant_01.png`.
   - Quality is noticeably better than Turbo (concept-art level vs.
     placeholder-grade). This is the "after-migration upgrade" the original
     plan called for.

2. `tools/dialogue_gen/generate.py`
   - `MODEL` switched from `exaone3.5:7.8b` to `exaone3.5:32b`.
   - `urlopen` timeout 300s → 900s because the 32B model's first call after
     load takes longer than 5 minutes (cold-start VRAM warmup).
   - Earlier session tried to regenerate all 1000 lines with this; killed
     mid-run because (a) it was slow (~3 min/slot, ~70 min total) and the
     session was about to end, and (b) we wanted a clean checkpoint commit.
     `Assets/Resources/Dialogue/dialogue_lines.json` was reverted via
     `git checkout` to the 7.8B baseline that's already on origin/master.
     The 32B regen is the **first thing the new session should kick off in
     the background** — it takes ~70 min and runs in parallel with the
     Unity install + smoke verification.

## What's still left, in order

### 0. Commit the handoff state and push

Before doing anything else, commit the staged changes from the previous
session and push so the new session has a clean starting point:

```
cd D:\MakingGames\LittleLordMajesty
git add tools/image_gen/generate.py tools/dialogue_gen/generate.py \
        Assets/Resources/Art/Generated/*.png \
        research_history/milestone_14_4090_migration.md \
        research_history/session_handoff_m14.md
git -c user.email=4090@local -c user.name=4090-desktop commit -m "ckpt: SDXL base 1.0 re-bakes + 32B model switch + handoff doc"
git push origin master
```

This will trigger the CI workflow on `4090-desktop`. It will fail the same
way (Unity.exe missing) until step 2 is done. The push itself is the smoke
test for the runner; the build failure is expected.

### 1. Restart the runner and dialogue regen background tasks

The previous session ran the actions runner and the dialogue 32B regen as
foreground-but-detached background bash tasks, both spawned by the old
Claude Code process. When Claude Code closes those processes go away.

Re-launch them in the new (admin) session as background tasks:

```
# 4090-desktop runner
cd C:\actions-runner
./run.cmd  &     # background

# EXAONE 32B dialogue regen (~70 min, GPU only)
python D:/MakingGames/LittleLordMajesty/tools/dialogue_gen/generate.py  &
```

Verify runner is online via `gh api repos/taeshin11/LittleLordMajesty/actions/runners`.

### 2. Install Unity Editor 2022.3.62f1

This is the whole reason the new session is admin. The installer is already
downloaded — no need to refetch:

- `C:\Users\taesh\Downloads\UnitySetup64-2022.3.62f1.exe` (3.7 GB, sha256 not
  recorded but file size is 3,696,796,104 bytes)
- `C:\Users\taesh\Downloads\UnitySetup-WebGL-Support-for-Editor-2022.3.62f1.exe` (587 MB)

Direct downloads from Unity's CDN, changeset `4af31df58517`, fetched via
`https://services.api.unity.com/unity/editor/release/v1/releases?version=2022.3.62f1`.

Run the editor installer first:

```
"C:\Users\taesh\Downloads\UnitySetup64-2022.3.62f1.exe"
```

Because Claude Code is running as admin, the NSIS installer inherits
elevation and **does not show a UAC prompt**. It proceeds straight to the
NSIS wizard. Accept the default install location
`C:\Program Files\Unity\Hub\Editor\2022.3.62f1\` so the CI workflow's
hardcoded path resolves. After the editor finishes, run the WebGL module
installer the same way:

```
"C:\Users\taesh\Downloads\UnitySetup-WebGL-Support-for-Editor-2022.3.62f1.exe"
```

It will auto-detect the editor and install the WebGL Build Support module
into it.

Verify:
```
ls "/c/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe"
ls "/c/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Data/PlaybackEngines/WebGLSupport"
```

Both must exist before pushing again.

### 3. Trigger CI build end-to-end

Once Unity is in place, an empty commit is enough to retrigger the
`build-local.yml` workflow on the now-equipped runner:

```
git commit --allow-empty -m "ci: trigger build now that Unity 2022.3.62f1 is installed"
git push
gh run watch
```

Expected outcome:
- `build-webgl` job picked up by `4090-desktop`
- `Inject GameConfig` step copies `GameConfig.asset` (with the new key) from
  `C:\MakingGames\LittleLordMajesty\Assets\Resources\Config\` (junction → D:)
- `Build WebGL` step runs Unity in batchmode, builds to `Builds/WebGL/`
- `Deploy to GitHub Pages` publishes to gh-pages
- The live site `https://taeshin11.github.io/LittleLordMajesty/` updates

### 4. Final live smoke test

```
node tools/playwright_test/live_test.js
```

Expected: `Page errors: 0`, `Console errors: 0` (no more Gemini 403 storm
because all image calls go through `LocalArtBank` first), `Unity dialogs: 0`.
If `Page errors` is 0 and `Console errors` is 0, the migration is **done**.

### 5. After dialogue 32B regen finishes

The 32B background task (started in step 1) takes ~70 min. When it finishes,
`Assets/Resources/Dialogue/dialogue_lines.json` will be 1000 lines of
sharper, more in-character dialogue. Commit + push it:

```
git add Assets/Resources/Dialogue/dialogue_lines.json
git -c user.email=4090@local -c user.name=4090-desktop commit -m "content: regenerate all 1000 dialogue lines via EXAONE 3.5 32B"
git push
```

This retriggers CI; same `gh run watch` to verify.

## Things the next session should NOT touch

- **`.github/workflows/build-local.yml`** — pushing this requires the
  `workflow` OAuth scope which the gh CLI token currently does not have.
  An attempt to refresh the scope (`gh auth refresh -h github.com -s workflow`)
  was started in the previous session but timed out because the user couldn't
  reach the device authorization page in time. The C: junction makes the
  legacy `/c/MakingGames/...` path resolve to the real D: location, so the
  workflow doesn't need to change. Leave it alone.

- The C: junction at `C:\MakingGames\LittleLordMajesty` — also leave it alone.
  It's the only thing keeping the workflow's hardcoded GameConfig path
  resolvable without a workflow file edit.

- **PillScan inference server processes** (`D:\PillScan\inference_server\`)
  — these are unrelated user work running in other python venvs. Saw 4
  python processes during cleanup that were NOT migration-related. Don't
  kill them.

## Quick state-of-the-system snapshot

```
# Branches and remotes
master ──→ origin/master (commit 2d6f819, m14 Gemini cuts published)

# Working tree (uncommitted)
M  tools/image_gen/generate.py             (SDXL Turbo → SDXL base 1.0)
M  tools/dialogue_gen/generate.py          (7.8B → 32B, timeout 300→900)
M  Assets/Resources/Art/Generated/*.png    (8 PNGs, all SDXL base 1.0)

# Background processes that need restarting after Claude restart
- C:\actions-runner\run.cmd (4090-desktop runner)
- python D:/MakingGames/LittleLordMajesty/tools/dialogue_gen/generate.py
  (EXAONE 3.5 32B regen of all 1000 lines, ~70 min)

# Background processes that survive Claude restart
- ollama serve (Windows service, persistent)
- Unity Hub (multiple processes from previous attempts; can be ignored or killed)

# Already-downloaded installers in C:\Users\taesh\Downloads\
- UnitySetup64-2022.3.62f1.exe (3,696,796,104 B)
- UnitySetup-WebGL-Support-for-Editor-2022.3.62f1.exe (587,745,272 B)
- UnityHubSetup.exe (152,341,256 B; Hub is already installed, this is leftover)

# Self-hosted runner state on github.com side
- 4090-desktop: registered, may show offline immediately after Claude
  restart until run.cmd is re-launched
```

## Resume prompt for the new admin session

> "D:\MakingGames\LittleLordMajesty 4090 PC 마이그레이션 이어서 진행해.
> research_history/session_handoff_m14.md 그대로 따라가. 너는 이번엔
> Claude Code가 admin으로 떠 있어서 UAC 안 떠도 됨. 순서: (0) 위 문서
> 'Step 0' 의 commit + push, (1) runner + dialogue 32B regen 백그라운드
> 기동, (2) Unity Editor + WebGL installer 둘 다 실행, (3) empty commit
> push해서 CI 풀 검증, (4) live_test.js 로 console errors 0 확인,
> (5) 32B regen 끝나면 commit + push. 막히면 알려줘."
