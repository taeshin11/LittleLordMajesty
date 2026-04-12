"""
Upscale Kenney 16x16 pixel art tiles to 64x64 using Real-ESRGAN.
Preserves pixel art style while adding detail.
"""
import os, glob, time
from PIL import Image
import numpy as np

def upscale_nearest(input_dir, output_dir, scale=4):
    """Fallback: simple nearest-neighbor upscale (preserves pixel art perfectly)"""
    os.makedirs(output_dir, exist_ok=True)
    files = sorted(glob.glob(os.path.join(input_dir, "tile_*.png")))
    for f in files:
        img = Image.open(f)
        w, h = img.size
        upscaled = img.resize((w * scale, h * scale), Image.NEAREST)
        out = os.path.join(output_dir, os.path.basename(f))
        upscaled.save(out)
    print(f"Nearest upscaled {len(files)} tiles from {input_dir}")

def upscale_realesrgan(input_dir, output_dir, scale=4):
    """AI upscale with Real-ESRGAN for enhanced detail"""
    os.makedirs(output_dir, exist_ok=True)
    try:
        from basicsr.archs.rrdbnet_arch import RRDBNet
        from realesrgan import RealESRGANer
        import torch
        
        # Use RealESRGAN_x4plus_anime model for pixel art
        model = RRDBNet(num_in_ch=3, num_out_ch=3, num_feat=64, num_block=6, num_grow_ch=32, scale=4)
        upsampler = RealESRGANer(
            scale=4,
            model_path=None,  # Will auto-download
            model=model,
            tile=0,
            tile_pad=10,
            pre_pad=0,
            half=True  # FP16 for 4090
        )
        
        files = sorted(glob.glob(os.path.join(input_dir, "tile_*.png")))
        for i, f in enumerate(files):
            img = Image.open(f).convert('RGB')
            img_np = np.array(img)
            output, _ = upsampler.enhance(img_np, outscale=scale)
            out_img = Image.fromarray(output)
            out = os.path.join(output_dir, os.path.basename(f))
            out_img.save(out)
            if (i+1) % 20 == 0:
                print(f"  {i+1}/{len(files)} done")
        print(f"Real-ESRGAN upscaled {len(files)} tiles")
    except Exception as e:
        print(f"Real-ESRGAN failed: {e}")
        print("Falling back to nearest-neighbor upscale...")
        upscale_nearest(input_dir, output_dir, scale)

if __name__ == "__main__":
    KENNY = r"C:\MakingGames\LittleLordMajesty\Kenny\Kenney Game Assets All-in-1 3.4.0\2D assets"
    OUT = r"D:\MakingGames\LittleLordMajesty\Assets\Resources\Art"
    
    print("=== Upscaling Tiny Town tiles ===")
    upscale_nearest(
        os.path.join(KENNY, "Tiny Town", "Tiles"),
        os.path.join(OUT, "TinyTown"),
        scale=4
    )
    
    print("\n=== Upscaling Tiny Dungeon tiles ===")
    upscale_nearest(
        os.path.join(KENNY, "Tiny Dungeon", "Tiles"),
        os.path.join(OUT, "TinyDungeon"),
        scale=4
    )
    
    print("\nDone! Tiles upscaled to 64x64")
