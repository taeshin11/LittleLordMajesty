---
name: M14 4090 PC migration
description: Migrating LittleLordMajesty dev environment from RTX 4050 6 GB laptop to RTX 4090 24 GB desktop. Unlocks SDXL base / EXAONE 32B / faster Unity builds. Smoke tests of dialogue + image gen + live WebGL passing on 4090.
type: project
date: 2026-04-11
status: IN_PROGRESS
---

# M14 — 4090 PC migration

## Goal
Move dev box from laptop (RTX 4050 6 GB) to 4090 PC (24 GB VRAM) so that:
- Image gen can run SDXL base / FLUX.1-dev instead of SDXL-Turbo 4-step
- Dialogue gen can run EXAONE 3.5 32B instead of 7.8B
- Unity WebGL builds run on the local self-hosted CI runner faster
- Future "open world" work has GPU headroom for procedural / tooling

## Done so far on 4090
- `git clone` → `C:\MakingGames\LittleLordMajesty`
- Copied gitignored `Assets/Resources/Config/GameConfig.asset(.meta)` from
  `C:\Users\taesh\Dropbox\012_업무상황기록\KTS\0.공유\새 폴더\`
- Python ML stack into Anaconda Python 3.12:
  torch 2.6.0+cu124, diffusers 0.37.1, transformers 5.5.3, accelerate,
  safetensors, playwright, huggingface_hub
- `python -m playwright install chromium` and `npx playwright install chromium`
- Ollama models: `exaone3.5:7.8b` (4.8 GB), `exaone3.5:32b` (19 GB)
- Smoke tests:
  - `node tools/playwright_test/live_test.js` against the live gh-pages build:
    `Page errors: 0`, `Unity dialogs: 0` ✓ — wasm crash fix from M13 confirmed
  - `python tools/dialogue_gen/generate.py --only-role vassal`:
    250 lines regenerated via EXAONE 7.8B in ~6 min, JSON intact (1000 lines total)
  - `python tools/image_gen/generate.py --only portraits`:
    All 4 NPC portraits regenerated on RTX 4090 (23 GB), first image 23.9s
    (model load + cuda warmup), subsequent ~4s

## Fixes made during migration
1. **`tools/migration/setup_4090.ps1`** died at `playwright install chromium` —
   `playwright.exe` from the Python 3.11 store install isn't on PATH and
   `$ErrorActionPreference = "Stop"` killed the script. Manually completed
   the remaining steps. Fix for next time: use `python -m playwright install`
   so it doesn't depend on PATH.

2. **Anaconda env had `scikit-learn 1.4.2` and `pyarrow 14.0.2`** built against
   numpy<2, which broke the diffusers→transformers→sklearn import chain on
   `numpy 2.2.6`. Upgraded to `scikit-learn 1.8.0` and `pyarrow 23.0.1`. Note
   that the pip resolver warned about other env packages (tptbox, streamlit)
   that still pin numpy<2 — those are unrelated to LLM and not used by the
   LittleLordMajesty toolchain, so left as-is.

3. **`tools/playwright_test/live_test.js`** used `waitUntil: 'networkidle'`
   which never fires on Unity WebGL (the runtime keeps making subresource
   requests). On the new chromium build the 60 s timeout was hit before
   load completed. Switched to `waitUntil: 'load'`. The rest of the script
   already does explicit `waitForFunction` for `unityInstance` and
   `waitForTimeout` for scene transitions, so dropping the idle wait
   doesn't lose any verification.

4. **`tools/dialogue_gen/generate.py`** final `print` had an em-dash that
   `cp949`-codepage Windows consoles can't encode → `UnicodeEncodeError`
   crash AFTER all 250 lines were already saved. Replaced em-dash and
   arrow with ASCII equivalents in the two trailing print statements.
   No data impact — checkpoint saves happen after every slot.

## Open issues / next actions
- **Gemini API key in `GameConfig.asset` is leaked.** The deployed gh-pages
  build's console shows 10 Gemini 403 PERMISSION_DENIED errors with
  `"Your API key was reported as leaked. Please use another API key."`
  Need to rotate the key in Google Cloud Console, paste new key into
  `Assets/Resources/Config/GameConfig.asset` (gitignored), and re-deploy
  the WebGL build so the live courtyard background / portrait API calls
  recover. (Local image_gen workflow doesn't depend on this — it bakes
  PNGs into Resources at build time.)
- **Unity Editor 2022.3.62f1 install** — Unity Hub installed but the
  Editor is still downloading at the time of writing. After download,
  verify `C:\Program Files\Unity\Hub\Editor\2022.3.62f1\Editor\Unity.exe`
  exists with WebGL Build Support module.
- **Self-hosted runner registration** — pending the user copying a fresh
  1-hour token from the GitHub repo settings page. Plan: install at
  `C:\actions-runner`, register as Windows service, then `git push` an
  empty commit and `gh run watch` to verify the new runner picks up the
  build. After that, deregister the laptop runner from the same page.
- **Optional upgrades (post-migration)**:
  - Switch `tools/dialogue_gen/generate.py` MODEL to `exaone3.5:32b` and
    regenerate all 1000 lines for richer in-character dialogue.
  - Switch `tools/image_gen/generate.py` to
    `stabilityai/stable-diffusion-xl-base-1.0` with
    `num_inference_steps=30`, `guidance_scale=7.0` for concept-art
    quality portraits + backgrounds.

## Why this matters for next steps
The 4090 unblocks the next major feature ("open world" — moving from
UI-panel-clicking to a character walking around a world). Procedural
world gen, larger LLM-driven NPC behavior, and higher-res concept art
all become tractable here.
