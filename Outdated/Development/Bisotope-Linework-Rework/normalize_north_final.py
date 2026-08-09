from __future__ import annotations

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
ALPHA_DIR = ROOT / "Development" / "Bisotope-Linework-Rework" / "alpha"
TEXTURE_DIR = (
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


JOBS = (
    (
        "NorthFinal_Female_alpha.png",
        "Arktos_Bisotope_Female_v10_north.png",
        "Arktos_Bisotope_Female_v10_north.png",
    ),
    (
        "NorthFinal_Male_alpha.png",
        "Arktos_Bisotope_Male_v9_north.png",
        "Arktos_Bisotope_Male_v10_north.png",
    ),
    (
        "NorthFinal_Calf_alpha.png",
        "Arktos_Bisotope_Calf_v11_north.png",
        "Arktos_Bisotope_Calf_v11_north.png",
    ),
)


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int]:
    bbox = image.getchannel("A").getbbox()
    if bbox is None:
        raise ValueError("Sprite has no visible pixels")
    return bbox


def clean_chroma_residue(image: Image.Image) -> None:
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            red, green, blue, alpha = pixels[x, y]
            if alpha <= 2 or (green > red + 20 and green > blue + 20):
                pixels[x, y] = (0, 0, 0, 0)


def main() -> None:
    # Read every footprint before overwriting the active North files.
    footprints: dict[str, tuple[int, int, int, int]] = {}
    for _, footprint_name, _ in JOBS:
        with Image.open(TEXTURE_DIR / footprint_name) as image:
            footprints[footprint_name] = alpha_bbox(image.convert("RGBA"))

    for source_name, footprint_name, destination_name in JOBS:
        source = Image.open(ALPHA_DIR / source_name).convert("RGBA")
        crop = source.crop(alpha_bbox(source))
        left, top, right, bottom = footprints[footprint_name]
        target_width = right - left
        target_height = bottom - top
        scale = min(target_width / crop.width, target_height / crop.height)
        width = max(1, round(crop.width * scale))
        height = max(1, round(crop.height * scale))
        resized = crop.resize((width, height), Image.Resampling.LANCZOS)

        center_x = (left + right) / 2
        center_y = (top + bottom) / 2
        paste_x = round(center_x - width / 2)
        paste_y = round(center_y - height / 2)
        canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
        canvas.alpha_composite(resized, (paste_x, paste_y))
        clean_chroma_residue(canvas)

        destination = TEXTURE_DIR / destination_name
        canvas.save(destination, optimize=True)
        print(f"{destination.name}: bbox={alpha_bbox(canvas)}")


if __name__ == "__main__":
    main()
