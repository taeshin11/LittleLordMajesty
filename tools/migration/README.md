# 4090 PC Migration — Quick Reference

Moving the LittleLordMajesty dev environment from the laptop to the 4090 PC.
24 GB VRAM unlocks SDXL base, EXAONE 32B, and faster Unity builds.

## Why move

| Capability | Laptop (RTX 4050 6 GB) | 4090 (24 GB) |
|---|---|---|
| Image gen model | SDXL Turbo (4-step) | SDXL base / FLUX.1-dev / SD3 |
| Image gen wall time | ~6 s/img @ 1024×576 | ~4 s/img @ 1024×576 *50-step* |
| LLM dialogue model | EXAONE 3.5 7.8B | EXAONE 3.5 32B |
| Unity WebGL build | ~7 min | ~3 min |
| Local code LLM | none | DeepSeek-Coder-V2 16B |

## One-shot setup on the 4090

```powershell
# 1. Clone
mkdir C:\MakingGames
cd C:\MakingGames
git clone https://github.com/taeshin11/LittleLordMajesty.git
cd LittleLordMajesty

# 2. Run the auto-installer (PowerShell, NOT admin — UAC prompts come up where needed)
Set-ExecutionPolicy -Scope Process Bypass
.\tools\migration\setup_4090.ps1
```

The script installs: git, gh, Python 3.11, Node.js, Ollama, torch+CUDA12.4,
diffusers, transformers, Playwright + Chromium, and pulls EXAONE 3.5 7.8B
and 32B models. Idempotent — safe to re-run.

## What you still have to do manually

The script can't automate these because they need either UI interaction
or per-machine secrets. The script ends with this checklist printed.

### 1. Unity Editor 2022.3.62f1

Open Unity Hub, install **2022.3.62f1** with the **WebGL Build Support**
module checked. The CI workflow at `.github/workflows/build-local.yml`
hardcodes this path:

```
C:/Program Files/Unity/Hub/Editor/2022.3.62f1/Editor/Unity.exe
```

If you install elsewhere, edit that line in the workflow.

### 2. Copy `GameConfig.asset` (gitignored)

Has the Gemini API key — never committed. Copy from laptop:

```
Assets/Resources/Config/GameConfig.asset
Assets/Resources/Config/GameConfig.asset.meta
```

Use OneDrive, USB, scp, anything but git. The CI workflow copies it from
this path during the build step (`Inject GameConfig` step).

### 3. Register the new self-hosted runner

Open: <https://github.com/taeshin11/LittleLordMajesty/settings/actions/runners/new>

Pick **Windows x64**. The page shows download + config commands with a
**1-hour token** — copy them straight into PowerShell on the 4090. Default
install at `C:\actions-runner`. After config:

```powershell
cd C:\actions-runner
.\svc.cmd install     # register as Windows service
.\svc.cmd start
```

The workflow uses `runs-on: self-hosted` so any registered runner picks
up jobs. No workflow change needed.

### 4. Deregister the laptop runner

Same settings page. Find the laptop runner row, click **...** → Remove.
You can keep both registered if you want a fallback, but it gets confusing
when you're not sure which one is going to grab a build.

### 5. Smoke test

```powershell
cd C:\MakingGames\LittleLordMajesty

# Live build still serves the latest deploy from gh-pages — confirms remote works
node tools/playwright_test/live_test.js          # → Page errors: 0

# Local toolchain — confirms ollama + diffusers work
python tools/dialogue_gen/generate.py --only-role vassal
python tools/image_gen/generate.py --only portraits

# Push a tiny change to test the new CI runner end-to-end
git commit --allow-empty -m "test: 4090 runner smoke"
git push
gh run watch
```

## After-migration upgrades worth doing

These are now possible with 24 GB VRAM. None are required.

- **SDXL base instead of Turbo** in `tools/image_gen/generate.py`:
  swap `stabilityai/sdxl-turbo` → `stabilityai/stable-diffusion-xl-base-1.0`,
  bump `num_inference_steps` from 4 to 30, set `guidance_scale=7.0`.
- **FLUX.1-dev** for top-tier quality: `black-forest-labs/FLUX.1-dev`
  (~24 GB VRAM, just barely fits with cpu offload).
- **EXAONE 32B for dialogue**: in `tools/dialogue_gen/generate.py` change
  `MODEL = "exaone3.5:7.8b"` → `MODEL = "exaone3.5:32b"`. Slower per call
  but smarter, more in-character lines.
- **Static-baked NotoSansKR fallback** so Korean dialogue actually renders
  on WebGL without crashing (currently gated off in `LocalDialogueBank`).
  Modify `Assets/Editor/CJKFontSetup.cs` to use `AtlasPopulationMode.Static`
  with a pre-baked Hangul character set, run from CI.
- **Local code assistant**: `ollama pull deepseek-coder-v2:16b` — runs on
  the 4090 alongside EXAONE if you have RAM headroom.

## Rollback

If something breaks, you can always run the laptop in parallel until the
4090 setup is solid. Both can be registered as runners simultaneously —
GitHub picks whichever is idle. Just don't `gh secret` anywhere.
