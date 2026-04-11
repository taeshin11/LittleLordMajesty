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

# Force UTF-8 on stdout/stderr so non-ASCII prints (arrows, em-dashes) from
# diffusers/transformers warnings don't crash on Windows cp949 consoles.
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")
except Exception:
    pass
import faulthandler
faulthandler.enable()

OUT_DIR = Path(__file__).resolve().parents[2] / "Assets" / "Resources" / "Art" / "Generated"
OUT_DIR.mkdir(parents=True, exist_ok=True)

# (filename_stem, prompt, width, height)
#
# Style target: Zelda Echoes of Wisdom — bright pastel toy diorama, isometric
# 3-quarter view, soft Nintendo-style lighting, chibi proportions, no
# painterly/oil/cinematic vocabulary. Anchors duplicated up front because
# SDXL base 1.0 weights early tokens more heavily.
STYLE_BG = (
    "cute isometric diorama, miniature toy world, pastel storybook colors, "
    "soft Nintendo style, bright sky blue and pastel green, thick clean outlines, "
    "low-poly stylized shapes, soft cel-shaded lighting, no harsh shadows, "
    "Zelda Echoes of Wisdom inspired, charming cozy mood"
)
BACKGROUNDS = [
    ("bg_main_menu",
     f"{STYLE_BG}, tiny medieval castle on a small grassy hill floating like a "
     f"toy island, fluffy round clouds, pastel sunset gradient sky, distant "
     f"miniature village rooftops, friendly cozy fantasy kingdom, "
     f"3-quarter top-down isometric view, no text",
     1024, 576),
    ("bg_castle_courtyard",
     f"{STYLE_BG}, tiny stylized medieval castle courtyard, smooth pastel cobblestones, "
     f"small wooden market stalls, round potted plants, tiny banners, soft daylight, "
     f"no characters, 3-quarter top-down isometric view, no text",
     1024, 576),
    ("bg_world_map",
     f"{STYLE_BG}, tiny stylized fantasy world map seen as a 3D toy diorama, "
     f"pastel green rolling hills, mint forests, small puffy mountains, tiny "
     f"castle and village icons sitting on the terrain, soft pastel ocean, "
     f"3-quarter top-down isometric view, no text or labels",
     1024, 576),
    ("bg_battle_field",
     f"{STYLE_BG}, tiny stylized open meadow with soft pastel grass, scattered "
     f"colorful flags planted in the ground, round bushes, distant rolling hills, "
     f"friendly and adventurous mood (NOT scary), no characters, "
     f"3-quarter top-down isometric view, no text",
     1024, 576),
]

# (npc_id, npc_name, profession_word, vibe_words)
PORTRAITS = [
    ("vassal_01",   "Aldric", "elderly steward",
     "kind grandpa, white beard, rosy cheeks, soft purple robes, gentle smile"),
    ("soldier_01",  "Bram",   "young guard",
     "brave kid, short brown hair, freckles, light blue tunic, tiny round wooden shield"),
    ("farmer_01",   "Marta",  "cheerful farmer",
     "round friendly woman, braided brown hair, pink apron, holding a tiny basket of fruit"),
    ("merchant_01", "Sivaro", "traveling merchant",
     "skinny smiling man, small goatee, bright teal traveling coat, big floppy hat with a feather"),
]

STYLE_PORTRAIT = (
    "cute chibi character, big head small body, oversized round eyes, soft pastel colors, "
    "thick clean outlines, soft cel-shaded toon shading, friendly expression, "
    "Zelda Echoes of Wisdom inspired, Nintendo soft palette, charming storybook style, "
    "low-poly stylized figurine look"
)
PORTRAIT_PROMPT_TEMPLATE = (
    f"{STYLE_PORTRAIT}, full body chibi portrait of {{name}}, a {{profession}} in a small "
    f"medieval kingdom. {{vibe}}. Standing on a tiny round pastel grass tile, plain soft "
    f"pastel mint background, centered, no text"
)

NEGATIVE = (
    "photorealistic, oil painting, dark, gritty, scary, dramatic shadows, "
    "cinematic, concept art, painterly, ugly, deformed, watermark, signature, "
    "text, letters, blurry, low quality, extra limbs, frame, border"
)


def load_pipe(use_offload=True):
    print("[img] Loading SDXL base 1.0 (this takes 1-2 min on first run)...")
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
        free_gb = free_b / (1024**3)
        print(f"[img] CUDA: {torch.cuda.get_device_name(0)} - free {free_gb:.1f} GB",
              flush=True)
        if use_offload:
            # Default: use model_cpu_offload — keeps peak VRAM under ~4 GB at
            # a few seconds per image cost. Safer when sharing the GPU with
            # other jobs (xray training, inference) that spike unpredictably.
            print("[img] Enabling model_cpu_offload (shared GPU)", flush=True)
            pipe.enable_model_cpu_offload()
        else:
            # --no-offload: user explicitly told us the 4090 is ours. Full
            # GPU residency runs ~2x faster per image and produces identical
            # output for the same seed/steps. Peak VRAM ~12 GB for SDXL fp16.
            print("[img] GPU-resident (no offload, full VRAM)", flush=True)
            pipe.to("cuda")
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
    ap.add_argument("--no-offload", action="store_true",
                    help="Disable cpu_offload (use when the 4090 is fully ours; "
                         "runs ~2x faster, needs ~12 GB VRAM)")
    ap.add_argument("--seed-variants", type=int, default=1,
                    help="Generate N additional seed variations per asset "
                         "saved to --variants-out (does NOT overwrite originals). "
                         "Default 1 = originals only.")
    ap.add_argument("--variants-out", default=None,
                    help="Directory for variant PNGs (default tools/image_gen/variants/)")
    args = ap.parse_args()

    sys.path.insert(0, str(Path(__file__).resolve().parents[2] / "scripts"))
    from gpu_lock import acquire as gpu_acquire
    # Two VRAM modes:
    #   default (cpu_offload): ~4 GB peak, for shared-GPU use
    #   --no-offload:          ~12 GB peak, full residency, ~2x faster
    # We declare the right one so the shared lock packs jobs correctly.
    vram_budget = 12000 if args.no_offload else 4000
    if not gpu_acquire("LittleLordMajesty_image_gen", vram_mb=vram_budget, on_busy="wait"):
        print("GPU busy, exiting")
        sys.exit(0)

    pipe = load_pipe(use_offload=not args.no_offload)

    variants_dir = Path(args.variants_out) if args.variants_out else \
        Path(__file__).parent / "variants"
    if args.seed_variants > 1:
        variants_dir.mkdir(parents=True, exist_ok=True)

    def _run(stem, prompt, w, h, base_seed):
        # Always produce the canonical original at base_seed.
        out = OUT_DIR / f"{stem}.png"
        gen_image(pipe, prompt, w, h, out, seed=base_seed, steps=args.steps)
        # Optional alternate seeds — saved outside Resources/ so they don't
        # land in the Unity build. User promotes one by hand if preferred.
        for i in range(1, args.seed_variants):
            alt_seed = (base_seed + i * 1_000_003) & 0x7fffffff
            vout = variants_dir / f"{stem}_v{i}_seed{alt_seed}.png"
            gen_image(pipe, prompt, w, h, vout, seed=alt_seed, steps=args.steps)

    if args.only in ("all", "bg"):
        for stem, prompt, w, h in BACKGROUNDS:
            _run(stem, prompt, w, h, hash(stem) & 0x7fffffff)

    if args.only in ("all", "portraits"):
        for npc_id, name, profession, vibe in PORTRAITS:
            prompt = PORTRAIT_PROMPT_TEMPLATE.format(
                name=name, profession=profession, vibe=vibe)
            _run(f"portrait_{npc_id}", prompt, 512, 512,
                 hash(npc_id) & 0x7fffffff)

    print(f"[img] DONE -> {OUT_DIR}")
    if args.seed_variants > 1:
        print(f"[img] Variants -> {variants_dir}")


if __name__ == "__main__":
    sys.exit(main())
