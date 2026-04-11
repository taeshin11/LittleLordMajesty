#!/usr/bin/env python3
"""
Build-time sprite sheet generator for the roaming pivot (M16).

Generates 4-direction × 2-frame walk cycle sprites for the player avatar
and all NPCs, using the same local SDXL base 1.0 pipeline as generate.py.
Output PNGs go straight into Assets/Resources/Art/Sprites/ and are loaded
at runtime via Resources.Load<Sprite>("Art/Sprites/<id>_<dir>_<frame>").

Style target: Zelda Echoes of Wisdom — cute chibi, big head small body,
pastel colors, thick outlines, Nintendo soft palette. Plain pastel
background so Unity's alpha rim / background-removal at import time
stays clean.

Output layout:
  Assets/Resources/Art/Sprites/player_n_0.png     player facing north, walk frame 0
  Assets/Resources/Art/Sprites/player_n_1.png     player facing north, walk frame 1
  Assets/Resources/Art/Sprites/player_s_0.png     ... south / east / west
  ...
  Assets/Resources/Art/Sprites/vassal_01_n_0.png
  ...

Usage:
  python generate_sprites.py                        # all characters, all directions
  python generate_sprites.py --only player          # just the player
  python generate_sprites.py --only vassal_01       # one NPC
  python generate_sprites.py --no-offload           # full VRAM, ~2x faster
"""
from __future__ import annotations
import argparse, sys, time
from pathlib import Path

# Force UTF-8 on stdout/stderr (Windows cp949 console chokes on non-ASCII
# from diffusers warnings).
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass

OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Art" / "Sprites"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# Style anchor — kept SHORT so that after the per-character vibe string the
# total prompt still fits inside CLIP's 77-token budget. Figurine / sheet /
# multi-view keywords live in NEGATIVE, not in positive.
STYLE = (
    "solo chibi character, one figure, big head round eyes, pastel colors, "
    "thick outlines, cel-shaded, Nintendo style"
)

# Per-character "vibe" — one line that disambiguates this character from the
# others. Reused verbatim from the portrait prompts so the sprite and the
# dialogue-box portrait look like the same person.
CHARACTERS = [
    # (id, display_name, profession_or_role, vibe)
    ("player",      "Young Lord", "noble boy",
     "small crown, royal blue cape, cream tunic, brown boots, friendly smile"),
    ("vassal_01",   "Aldric",   "elderly steward",
     "kind grandpa, white beard, soft purple robes, round glasses, gentle smile"),
    ("soldier_01",  "Bram",     "young guard",
     "brave kid, short brown hair, freckles, light blue tunic, tiny wooden shield"),
    ("farmer_01",   "Marta",    "cheerful farmer",
     "round friendly woman, braided brown hair, pink apron, small fruit basket"),
    ("merchant_01", "Sivaro",   "traveling merchant",
     "skinny smiling man, small goatee, teal coat, floppy feather hat"),
]

DIRECTIONS = [
    # (suffix, prompt_phrase)
    ("s", "facing forward toward the viewer, front view"),
    ("n", "facing away from the viewer, back view, seen from behind"),
    ("e", "facing right in profile, side view from the left"),
    ("w", "facing left in profile, side view from the right"),
]

# Walk frames: neutral idle pose (frame 0) and mid-step walking pose (frame 1).
# In-game we flip between them every ~0.25 s while the avatar is moving.
FRAMES = [
    (0, "standing still, both feet on the ground"),
    (1, "mid walk step, one foot forward, one foot lifted"),
]

NEGATIVE = (
    "character sheet, turnaround, model sheet, reference sheet, sticker sheet, "
    "multiple views, multiple characters, two characters, three characters, "
    "grid layout, tiled, duplicate, twins, clone, "
    "photorealistic, oil painting, dark, gritty, scary, dramatic shadows, "
    "cinematic, concept art, painterly, ugly, deformed, watermark, signature, "
    "text, letters, blurry, low quality, extra limbs, frame, border, figurine, "
    "background clutter, scenery, props"
)

# SDXL natively wants 1024-multiple-of-8 dims. 512×768 gives a portrait aspect
# that suits a full-body chibi. Unity's sprite import will scale this down to
# whatever pixels-per-unit we pick.
SPRITE_W, SPRITE_H = 512, 768


def load_pipe(use_offload=True):
    print("[sprite] Loading SDXL base 1.0...", flush=True)
    import torch
    from diffusers import AutoPipelineForText2Image
    pipe = AutoPipelineForText2Image.from_pretrained(
        "stabilityai/stable-diffusion-xl-base-1.0",
        torch_dtype=torch.float16,
        variant="fp16",
        use_safetensors=True,
    )
    if torch.cuda.is_available():
        free_b, _ = torch.cuda.mem_get_info()
        print(f"[sprite] CUDA: {torch.cuda.get_device_name(0)} - free {free_b/(1024**3):.1f} GB",
              flush=True)
        if use_offload:
            print("[sprite] model_cpu_offload (shared GPU)", flush=True)
            pipe.enable_model_cpu_offload()
        else:
            print("[sprite] GPU-resident (no offload)", flush=True)
            pipe.to("cuda")
    else:
        print("[sprite] WARNING: no CUDA, running on CPU (very slow)")
    return pipe


def gen_one(pipe, prompt, out_path, seed, steps):
    import torch
    g = torch.Generator(device="cpu").manual_seed(seed)
    t0 = time.time()
    img = pipe(
        prompt=prompt,
        negative_prompt=NEGATIVE,
        num_inference_steps=steps,
        guidance_scale=7.0,
        width=SPRITE_W, height=SPRITE_H,
        generator=g,
    ).images[0]
    img.save(out_path)
    print(f"[sprite] {out_path.name} in {time.time()-t0:.1f}s", flush=True)


def build_prompt(vibe, direction_phrase, frame_phrase):
    # Order: per-character vibe first (early CLIP tokens are weighted more),
    # then direction, then frame pose, then style anchor, then background
    # spec. Keep under ~70 tokens so CLIP truncation doesn't drop the
    # direction / frame keywords.
    return (
        f"{vibe}, {direction_phrase}, {frame_phrase}, {STYLE}, "
        f"plain pastel mint background, centered, no shadow"
    )


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="all",
                    help="Generate only this character id (e.g. 'player', 'vassal_01') or 'all'")
    ap.add_argument("--steps", type=int, default=40,
                    help="Denoising steps — 40 is the sweet spot for consistency")
    ap.add_argument("--no-offload", action="store_true",
                    help="Disable cpu_offload (full 12 GB VRAM, ~2x faster)")
    args = ap.parse_args()

    # Join the shared GPU mutex. Shared-mode schema (v2) allows packing with
    # SPINAI training / PillScan inference as long as sum(vram_mb) <= capacity.
    sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
    from gpu_lock import acquire as gpu_acquire
    vram_budget = 12000 if args.no_offload else 4000
    if not gpu_acquire("LittleLordMajesty_sprite_gen",
                       vram_mb=vram_budget, on_busy="wait"):
        print("[sprite] GPU busy, exiting")
        sys.exit(0)

    pipe = load_pipe(use_offload=not args.no_offload)

    targets = CHARACTERS if args.only == "all" else \
        [c for c in CHARACTERS if c[0] == args.only]
    if not targets:
        print(f"[sprite] No character matches --only={args.only}")
        print(f"[sprite] Known ids: {[c[0] for c in CHARACTERS]}")
        sys.exit(1)

    for char_id, name, role, vibe in targets:
        print(f"\n[sprite] === {char_id} ({name}, {role}) ===", flush=True)
        # Deterministic base seed per character so re-runs are stable (and so
        # all 8 sprites of one character share a visual identity).
        base_seed = hash(char_id) & 0x7fffffff
        for dir_idx, (dir_suffix, dir_phrase) in enumerate(DIRECTIONS):
            for frame_idx, frame_phrase in FRAMES:
                # Seed offset per (direction, frame) — keeps the character
                # coherent but lets SDXL actually produce different poses.
                seed = (base_seed
                        + dir_idx * 100_003
                        + frame_idx * 10_007) & 0x7fffffff
                out_path = OUT_DIR / f"{char_id}_{dir_suffix}_{frame_idx}.png"
                prompt = build_prompt(vibe, dir_phrase, frame_phrase)
                gen_one(pipe, prompt, out_path, seed, args.steps)

    print(f"\n[sprite] DONE -> {OUT_DIR}")


if __name__ == "__main__":
    sys.exit(main())
