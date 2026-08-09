from pathlib import Path
from shutil import copyfile

from PIL import Image


WORKSPACE = Path(r"C:\Users\Matthias\Desktop\Fallout Immersion Project")
SOURCE_DIR = WORKSPACE / "Development" / "ImageGen" / "Bisotope-NorthSouth-2026-07-31"
TARGET_DIR = (
    WORKSPACE
    / "New-Mods"
    / "FIP-Arktos"
    / "LoadFolders"
    / "Base"
    / "Textures"
    / "FIP-Arktos"
    / "Animals"
    / "Bisotope"
)


# Directional silhouettes retain the established in-game footprint of the
# previous set. Width is normally derived from the generated aspect ratio;
# explicit widths are used only where the male horns need the same visual span
# as the east-facing design.
SPRITES = {
    "Calf_v10_north": {"height": 206, "width": None, "top": 25},
    "Calf_v10_south": {"height": 208, "width": None, "top": 21},
    "Female_v9_north": {"height": 228, "width": None, "top": 15},
    "Female_v9_south": {"height": 217, "width": None, "top": 18},
    "Male_v9_north": {"height": 233, "width": 150, "top": 15},
    "Male_v9_south": {"height": 225, "width": 204, "top": 13},
}


def prepare_sprite(name: str, height: int, width: int | None, top: int) -> None:
    source = Image.open(SOURCE_DIR / f"{name}_alpha.png").convert("RGBA")
    alpha_box = source.getchannel("A").getbbox()
    if alpha_box is None:
        raise ValueError(f"{name}: no opaque pixels")

    trimmed = source.crop(alpha_box)
    if width is None:
        width = round(trimmed.width * height / trimmed.height)

    resized = trimmed.resize((width, height), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    left = (256 - width) // 2
    if left < 0 or top < 0 or left + width > 256 or top + height > 256:
        raise ValueError(f"{name}: target bounds exceed 256x256")

    canvas.alpha_composite(resized, (left, top))
    output_name = f"Arktos_Bisotope_{name}.png"
    canvas.save(TARGET_DIR / output_name, optimize=True)


for sprite_name, settings in SPRITES.items():
    prepare_sprite(sprite_name, **settings)


# Graphic_Multi requires every direction to share one basename. Preserve the
# approved east art byte-for-byte under the new versioned basename.
copyfile(
    TARGET_DIR / "Arktos_Bisotope_Calf_v9_east.png",
    TARGET_DIR / "Arktos_Bisotope_Calf_v10_east.png",
)
copyfile(
    TARGET_DIR / "Arktos_Bisotope_Female_v8_east.png",
    TARGET_DIR / "Arktos_Bisotope_Female_v9_east.png",
)
copyfile(
    TARGET_DIR / "Arktos_Bisotope_Male_v8_east.png",
    TARGET_DIR / "Arktos_Bisotope_Male_v9_east.png",
)
