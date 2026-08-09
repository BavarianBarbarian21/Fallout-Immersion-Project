from pathlib import Path

from PIL import Image, ImageFilter, ImageOps


WORKSPACE = Path(r"C:\Users\Matthias\Desktop\Fallout Immersion Project")
SOURCE = WORKSPACE / "Development" / "ImageGen" / "FalloutHorse-2026-07-31" / "Horse_v7_alpha.png"
TARGET = (
    WORKSPACE
    / "New-Mods"
    / "FIP-Arktos"
    / "LoadFolders"
    / "Base"
    / "Textures"
    / "FIP-Arktos"
    / "Animals"
    / "Horse"
    / "Arktos_Horse_v7.png"
)


source = Image.open(SOURCE).convert("RGBA")
alpha_box = source.getchannel("A").getbbox()
if alpha_box is None:
    raise ValueError("Horse_v7_alpha.png contains no opaque pixels")

trimmed = source.crop(alpha_box)

# Preserve the intentionally heavy RimWorld silhouette after the high-resolution
# source is reduced to 256 px. This expands only the exterior near-black stroke;
# the painted anatomy and antler shapes remain unchanged.
padded = ImageOps.expand(trimmed, border=12, fill=(0, 0, 0, 0))
stroke_alpha = padded.getchannel("A").filter(ImageFilter.MaxFilter(21))
stroke = Image.new("RGBA", padded.size, (5, 3, 3, 0))
stroke.putalpha(stroke_alpha)
trimmed = Image.alpha_composite(stroke, padded)
target_width = 244
target_height = round(trimmed.height * target_width / trimmed.width)
resized = trimmed.resize((target_width, target_height), Image.Resampling.LANCZOS)

canvas = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
canvas.alpha_composite(resized, ((256 - target_width) // 2, 8))
canvas.save(TARGET, optimize=True)
