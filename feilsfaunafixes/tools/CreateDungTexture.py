from pathlib import Path
import sys

from PIL import Image, ImageEnhance


def main() -> None:
    if len(sys.argv) != 3:
        raise SystemExit("usage: CreateDungTexture.py INPUT OUTPUT")

    source = Image.open(sys.argv[1]).convert("RGB")
    # The central patch is entirely on the generated dry, fibrous material and
    # deliberately excludes the transparent presentation background.
    left = source.width * 39 // 100
    top = source.height * 24 // 100
    size = min(source.width * 24 // 100, source.height * 36 // 100)
    tile = source.crop((left, top, left + size, top + size))
    tile = ImageEnhance.Contrast(tile).enhance(1.12)
    tile = ImageEnhance.Color(tile).enhance(0.82)
    tile = tile.resize((32, 32), Image.Resampling.LANCZOS)
    tile = tile.quantize(colors=24, method=Image.Quantize.MEDIANCUT).convert("RGBA")
    tile.putalpha(255)

    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    tile.save(output, optimize=True)


if __name__ == "__main__":
    main()
