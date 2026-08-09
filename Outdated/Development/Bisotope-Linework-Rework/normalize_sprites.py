from __future__ import annotations

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
ALPHA_DIR = ROOT / "Development" / "Bisotope-Linework-Rework" / "alpha"
FINAL_DIR = (
    ROOT
    / "New-Mods"
    / "FIP-Arktos"
    / "LoadFolders"
    / "Base"
    / "Textures"
    / "FIP-Arktos"
    / "Animals"
    / "Bisotope"
)


# Each generated sprite is fitted, without distortion, into the on-map footprint of
# the currently active counterpart.  This keeps all three Graphic_Multi directions
# aligned with the established Bisotope draw scale while changing the anatomy.
JOBS = {
    "Arktos_Bisotope_Female_v10_east": "Arktos_Bisotope_Female_v9_east.png",
    "Arktos_Bisotope_Female_v10_north": "Arktos_Bisotope_Female_v9_north.png",
    "Arktos_Bisotope_Female_v10_south": "Arktos_Bisotope_Female_v9_south.png",
    "Arktos_Bisotope_Male_v10_east": "Arktos_Bisotope_Male_v9_east.png",
    "Arktos_Bisotope_Male_v10_north": "Arktos_Bisotope_Male_v9_north.png",
    "Arktos_Bisotope_Male_v10_south": "Arktos_Bisotope_Male_v9_south.png",
    "Arktos_Bisotope_Calf_v11_east": "Arktos_Bisotope_Calf_v10_east.png",
    "Arktos_Bisotope_Calf_v11_north": "Arktos_Bisotope_Calf_v10_north.png",
    "Arktos_Bisotope_Calf_v11_south": "Arktos_Bisotope_Calf_v10_south.png",
}


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Sprite has no visible pixels")
    return bbox


def fit_sprite(name: str, footprint_name: str) -> None:
    source = Image.open(ALPHA_DIR / f"{name}_alpha.png").convert("RGBA")
    footprint = Image.open(FINAL_DIR / footprint_name).convert("RGBA")

    source_crop = source.crop(alpha_bbox(source))
    left, top, right, bottom = alpha_bbox(footprint)
    target_width = right - left
    target_height = bottom - top

    scale = min(target_width / source_crop.width, target_height / source_crop.height)
    resized_width = max(1, round(source_crop.width * scale))
    resized_height = max(1, round(source_crop.height * scale))
    resized = source_crop.resize((resized_width, resized_height), Image.Resampling.LANCZOS)

    center_x = (left + right) / 2
    center_y = (top + bottom) / 2
    paste_x = round(center_x - resized_width / 2)
    paste_y = round(center_y - resized_height / 2)

    canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    canvas.alpha_composite(resized, (paste_x, paste_y))

    # Lanczos can blend extremely faint chroma pixels back into the edge during
    # downscaling.  Bisotopes contain no green, so remove only green-dominant
    # residuals and leave the antialiased near-black outline untouched.
    pixels = canvas.load()
    for y in range(canvas.height):
        for x in range(canvas.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha <= 2 or (green > red + 20 and green > blue + 20):
                pixels[x, y] = (0, 0, 0, 0)

    destination = FINAL_DIR / f"{name}.png"
    canvas.save(destination, optimize=True)
    print(f"{destination.name}: source={source_crop.size}, final={alpha_bbox(canvas)}")


def main() -> None:
    FINAL_DIR.mkdir(parents=True, exist_ok=True)
    for name, footprint_name in JOBS.items():
        fit_sprite(name, footprint_name)


if __name__ == "__main__":
    main()
