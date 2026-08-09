from __future__ import annotations

import hashlib
import xml.etree.ElementTree as ET
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
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
PATCH_FILE = (
    ROOT
    / "New-Mods"
    / "FIP-Arktos"
    / "LoadFolders"
    / "Base"
    / "Patches"
    / "FIP-Arktos"
    / "Nature"
    / "Arktos_AnimalTexturePatch.xml"
)

EXPECTED_BASES = {
    "Arktos_Bisotope_Female_v10",
    "Arktos_Bisotope_Male_v10",
    "Arktos_Bisotope_Calf_v11",
}


def validate_png(path: Path) -> tuple[str, tuple[int, int, int, int], int]:
    with Image.open(path) as image:
        rgba = image.convert("RGBA")
        assert rgba.size == (256, 256), f"wrong size: {path} {rgba.size}"
        assert image.mode == "RGBA", f"wrong mode: {path} {image.mode}"
        assert rgba.getpixel((0, 0))[3] == 0, f"opaque corner: {path}"
        bbox = rgba.getchannel("A").getbbox()
        assert bbox is not None, f"empty sprite: {path}"
        fringe = sum(
            1
            for red, green, blue, alpha in rgba.getdata()
            if alpha > 0 and green > red + 20 and green > blue + 20
        )
        assert fringe == 0, f"green fringe pixels: {path} ({fringe})"
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    return digest, bbox, fringe


def main() -> None:
    tree = ET.parse(PATCH_FILE)
    paths = {
        element.text.rsplit("/", 1)[-1]
        for element in tree.findall(".//texPath")
        if element.text and "/Bisotope/" in element.text
    }
    assert paths == EXPECTED_BASES, f"unexpected active Bisotope paths: {paths}"

    hashes: dict[str, str] = {}
    for base in sorted(EXPECTED_BASES):
        for direction in ("east", "north", "south"):
            path = TEXTURE_DIR / f"{base}_{direction}.png"
            assert path.exists(), f"missing: {path}"
            digest, bbox, fringe = validate_png(path)
            assert digest not in hashes, f"duplicate PNG: {path.name} == {hashes[digest]}"
            hashes[digest] = path.name
            print(f"OK {path.name}: bbox={bbox}, green_fringe={fringe}, sha256={digest[:12]}")


if __name__ == "__main__":
    main()
