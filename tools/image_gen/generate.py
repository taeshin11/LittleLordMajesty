#!/usr/bin/env python3
"""
Build-time art generator using SDXL Turbo locally (no API cost).

Replaces the Gemini Image API calls in CastleViewUI (castle courtyard
background) and NPCInteractionUI (NPC portraits) with offline-generated
PNGs committed to Assets/Resources/Art/Generated/.

Why SDXL Turbo:
- 1-4 step generation instead of 30+ for SDXL base
- ~6 GB VRAM in fp16 with model_cpu_offload (fits RTX 4050)
- Single distilled model, no separate refiner

Outputs:
  Assets/Resources/Art/Generated/bg_castle_courtyard.png   (1024x576, 16:9)
  Assets/Resources/Art/Generated/portrait_<npc_id>.png      (512x512)

Usage: python generate.py [--only bg|portraits|all]
"""
from __future__ import annotations
import argparse, os, sys, time
from pathlib import Path

OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Art" / "Generated"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# (filename_stem, prompt, width, height)
BACKGROUNDS = [
    ("bg_main_menu",
     "Dramatic medieval fantasy kingdom painting, a young lord standing on a high "
     "castle balcony at dusk overlooking a vast kingdom with rolling hills, distant "
     "villages, glowing sunset sky, painterly oil-painting style, cinematic lighting, "
     "warm golden tones, atmospheric haze, highly detailed, epic mood, "
     "wide establishing shot",
     1024, 576),
    ("bg_castle_courtyard",
     "Medieval fantasy castle courtyard at golden hour, painterly oil-painting style, "
     "warm torchlight, stone walls, wooden scaffolding, distant towers, dramatic sky, "
     "atmospheric depth, no characters, cinematic wide establishing shot, "
     "detailed background art, fantasy concept art",
     1024, 576),
    ("bg_world_map",
     "Medieval fantasy hand-drawn world map on weathered parchment, rolling hills, "
     "dark forests, distant mountains, small castle icons, faded ink lines, "
     "subtle compass rose in the corner, sepia tones, cartographer's style, "
     "no text or labels",
     1024, 576),
    ("bg_battle_field",
     "Medieval fantasy battlefield at dusk, churned mud, tattered banners, "
     "smoke drifting across, distant siege towers, dramatic stormy sky, "
     "painterly oil-painting style, no characters foreground, cinematic wide shot",
     1024, 576),
]

# (npc_id, npc_name, profession_word, vibe_words)
PORTRAITS = [
    ("vassal_01",   "Aldric", "elderly steward",
     "wise, pragmatic, gray beard, kind eyes, simple noble robes"),
    ("soldier_01",  "Bram",   "young soldier",
     "eager, brave, short brown hair, scarred chin, leather and chainmail"),
    ("farmer_01",   "Marta",  "middle-aged farmer woman",
     "sturdy, weathered face, tied-back brown hair, sun-tanned skin, simple linen dress"),
    ("merchant_01", "Sivaro", "traveling merchant",
     "shrewd, slim, dark goatee, calculating eyes, colorful traveling cloak with coin pouches"),
]

PORTRAIT_PROMPT_TEMPLATE = (
    "Medieval fantasy character portrait of {name}, a {profession} in a small lord's castle. "
    "{vibe}. Head-and-shoulders view, looking slightly off-camera, painterly oil-painting style, "
    "warm torchlight, earthy medieval colors, detailed face, subtle background of castle stone, "
    "concept art quality"
)

NEGATIVE = (
    "modern clothes, cars, neon, anime, cartoon, watermark, signature, text, "
    "letters, blurry, low quality, deformed face, extra limbs, frame, border"
)


def load_pipe():
    print("[img] Loading SDXL base 1.0 (this takes 1-2 min on first run)...")
    import torch
    from diffusers import AutoPipelineForText2Image
    pipe = AutoPipelineForText2Image.from_pretrained(
        "stabilityai/stable-diffusion-xl-base-1.0",
        torch_dtype=torch.float16,
        variant="fp16",
        use_safetensors=True,
    )
    # 24 GB VRAM on the 4090 — keep the whole pipe on GPU instead of cpu_offload.
    if torch.cuda.is_available():
        pipe.to("cuda")
        print(f"[img] CUDA: {torch.cuda.get_device_name(0)} "
              f"({torch.cuda.get_device_properties(0).total_memory // (1024**3)} GB)")
    else:
        print("[img] WARNING: no CUDA, running on CPU (very slow)")
    return pipe


def gen_image(pipe, prompt, w, h, out_path, seed=42, steps=30):
    import torch
    g = torch.Generator(device="cpu").manual_seed(seed)
    t0 = time.time()
    img = pipe(
        prompt=prompt,
        negative_prompt=NEGATIVE,
        num_inference_steps=steps,
        guidance_scale=7.0,    # SDXL base 1.0: ~7 is the sweet spot
        width=w, height=h,
        generator=g,
    ).images[0]
    img.save(out_path)
    print(f"[img] {out_path.name} ({w}x{h}) in {time.time()-t0:.1f}s")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="all", choices=["all", "bg", "portraits"])
    ap.add_argument("--steps", type=int, default=30)
    args = ap.parse_args()

    pipe = load_pipe()

    if args.only in ("all", "bg"):
        for stem, prompt, w, h in BACKGROUNDS:
            out = OUT_DIR / f"{stem}.png"
            gen_image(pipe, prompt, w, h, out,
                      seed=hash(stem) & 0x7fffffff, steps=args.steps)

    if args.only in ("all", "portraits"):
        for npc_id, name, profession, vibe in PORTRAITS:
            prompt = PORTRAIT_PROMPT_TEMPLATE.format(
                name=name, profession=profession, vibe=vibe)
            out = OUT_DIR / f"portrait_{npc_id}.png"
            gen_image(pipe, prompt, 512, 512, out,
                      seed=hash(npc_id) & 0x7fffffff, steps=args.steps)

    print(f"[img] DONE → {OUT_DIR}")


if __name__ == "__main__":
    sys.exit(main())
