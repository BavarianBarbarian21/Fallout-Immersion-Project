from pathlib import Path
from shutil import copyfile

from PIL import Image


WORKSPACE = Path(r"C:\Users\Matthias\Desktop\Fallout Immersion Project")
SOURCE_DIR = WORKSPACE / "Development" / "ImageGen" / "Snakes-NorthSouth-2026-07-31"
SNAKE_DIR = (
    WORKSPACE
    / "New-Mods"
    / "FIP-Arktos"
    / "LoadFolders"
    / "Base"
    / "Textures"
    / "FIP-Arktos"
    / "Animals"
    / "Snakes"
)


SPRITES = {
    "Copperhead_v7_north": ("Copperhead", "Arktos_Copperhead_v7_north.png", 220, 18),
    "Copperhead_v7_south": ("Copperhead", "Arktos_Copperhead_v7_south.png", 210, 23),
    "WastelandIndigo_v7_north": (
        "WastelandIndigo",
        "Arktos_WastelandIndigo_v7_north.png",
        215,
        20,
    ),
    "WastelandIndigo_v7_south": (
        "WastelandIndigo",
        "Arktos_WastelandIndigo_v7_south.png",
        205,
        26,
    ),
    "CoralSnake_v7_north": ("CoralSnake", "Arktos_CoralSnake_v7_north.png", 220, 18),
    "CoralSnake_v7_south": ("CoralSnake", "Arktos_CoralSnake_v7_south.png", 220, 18),
    "Cottonmouth_v6_north": ("Cottonmouth", "Arktos_Cottonmouth_v6_north.png", 215, 20),
    "Cottonmouth_v6_south": ("Cottonmouth", "Arktos_Cottonmouth_v6_south.png", 210, 23),
}


def prepare_sprite(source_name: str, subdir: str, output_name: str, height: int, top: int) -> None:
    source = Image.open(SOURCE_DIR / f"{source_name}_alpha.png").convert("RGBA")
    alpha_box = source.getchannel("A").getbbox()
    if alpha_box is None:
        raise ValueError(f"{source_name}: no opaque pixels")

    trimmed = source.crop(alpha_box)
    width = round(trimmed.width * height / trimmed.height)
    resized = trimmed.resize((width, height), Image.Resampling.LANCZOS)

    left = (256 - width) // 2
    if left < 0 or top < 0 or left + width > 256 or top + height > 256:
        raise ValueError(f"{source_name}: target bounds exceed 256x256")

    canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    canvas.alpha_composite(resized, (left, top))
    canvas.save(SNAKE_DIR / subdir / output_name, optimize=True)


for source_name, settings in SPRITES.items():
    prepare_sprite(source_name, *settings)


# Graphic_Multi requires an east file sharing the same basename. Preserve each
# approved active east sprite byte-for-byte under the new versioned name.
EAST_COPIES = {
    ("Copperhead", "Arktos_Copperhead_v6.png"): "Arktos_Copperhead_v7_east.png",
    ("WastelandIndigo", "Arktos_WastelandIndigo_v6.png"): "Arktos_WastelandIndigo_v7_east.png",
    ("CoralSnake", "Arktos_CoralSnake_v6.png"): "Arktos_CoralSnake_v7_east.png",
    ("Cottonmouth", "Arktos_Cottonmouth_v5.png"): "Arktos_Cottonmouth_v6_east.png",
}

for (subdir, old_name), new_name in EAST_COPIES.items():
    copyfile(SNAKE_DIR / subdir / old_name, SNAKE_DIR / subdir / new_name)
