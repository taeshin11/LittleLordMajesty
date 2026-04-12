"""
Stable Diffusion img2img — upgrade Kenney tiles to Zelda-style.
Takes 16x16 pixel art, upscales, then runs through SD for style enhancement.
Only processes key tiles (grass, houses, characters, trees).
"""
import os, torch
from PIL import Image
from diffusers import StableDiffusionImg2ImgPipeline

KENNY = r"C:\MakingGames\LittleLordMajesty\Kenny\Kenney Game Assets All-in-1 3.4.0\2D assets"
OUT = r"D:\MakingGames\LittleLordMajesty\Assets\Resources\Art"

# Key tiles to upgrade (index, pack, description)
KEY_TILES = {
    # Tiny Town grass/nature
    "TinyTown": {
        0: "bright green grass tile, game texture, top-down view",
        1: "bright green grass with small flowers, game texture, top-down",
        13: "green grass with yellow path, game texture, top-down",
        24: "green round tree, game sprite, top-down RPG",
        25: "green round tree, game sprite, top-down RPG",
        26: "autumn orange tree, game sprite, top-down RPG",
        36: "cute orange roof house, game building, top-down RPG, medieval cottage",
        37: "cute orange roof house with door, game building, top-down",
        48: "stone castle wall, game texture, medieval, top-down",
        49: "stone castle tower, game building, medieval, top-down",
        72: "blue water tile, game texture, top-down",
        84: "wooden fence, game prop, top-down RPG",
        96: "stone path tile, game texture, medieval, top-down",
    },
    # Tiny Dungeon characters
    "TinyDungeon": {
        84: "cute pixel RPG knight character, chibi, front view, game sprite",
        85: "cute pixel RPG knight character, chibi, back view, game sprite",
        86: "cute pixel RPG mage character, chibi, front view, game sprite",
        87: "cute pixel RPG mage character, chibi, back view, game sprite",
        96: "cute pixel RPG rogue character, chibi, front view, game sprite",
        97: "cute pixel RPG female warrior, chibi, front view, game sprite",
        108: "cute pixel RPG elf character, chibi, front view, game sprite",
        109: "cute pixel RPG dwarf character, chibi, front view, game sprite",
    }
}

def run():
    print("Loading Stable Diffusion pipeline...")
    pipe = StableDiffusionImg2ImgPipeline.from_pretrained(
        "runwayml/stable-diffusion-v1-5",
        torch_dtype=torch.float16,
        safety_checker=None,
    ).to("cuda")
    pipe.enable_attention_slicing()
    
    style_prefix = "zelda echoes of wisdom style, cute, bright colors, high quality, detailed pixel art, "
    negative = "dark, gritty, realistic, photo, blurry, low quality, ugly, deformed"
    
    for pack, tiles in KEY_TILES.items():
        pack_dir = os.path.join(KENNY, "Tiny Town" if pack == "TinyTown" else "Tiny Dungeon", "Tiles")
        out_dir = os.path.join(OUT, pack)
        os.makedirs(out_dir, exist_ok=True)
        
        for idx, desc in tiles.items():
            src = os.path.join(pack_dir, f"tile_{idx:04d}.png")
            if not os.path.exists(src):
                print(f"  Skip {src} (not found)")
                continue
            
            # Upscale to 256x256 for SD input
            img = Image.open(src).convert("RGB")
            img_up = img.resize((256, 256), Image.NEAREST)
            
            prompt = style_prefix + desc
            
            try:
                result = pipe(
                    prompt=prompt,
                    image=img_up,
                    strength=0.55,  # Keep original structure, enhance style
                    guidance_scale=7.5,
                    num_inference_steps=30,
                ).images[0]
                
                # Resize back to 64x64 for game use
                result_small = result.resize((64, 64), Image.LANCZOS)
                out_path = os.path.join(out_dir, f"tile_{idx:04d}.png")
                result_small.save(out_path)
                print(f"  ✓ {pack}/tile_{idx:04d} — {desc[:40]}...")
            except Exception as e:
                print(f"  ✗ {pack}/tile_{idx:04d} — {e}")
    
    print("\nStyle upgrade complete!")

if __name__ == "__main__":
    run()
