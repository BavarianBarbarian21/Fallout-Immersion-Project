"""Resolve conflicts in optional legacy Translation Part work output."""

from __future__ import annotations

import argparse
from collections import defaultdict
from pathlib import Path
import os
import re
import xml.etree.ElementTree as ET
from xml.sax.saxutils import escape


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
LEGACY_OUTPUT_ROOT = REPOSITORY_ROOT / ".work" / "legacy-translation-parts"
TRANSLATION_PARTS = tuple(
    LEGACY_OUTPUT_ROOT / f"FIP-Translation Part {part}" for part in range(1, 5)
)


def read_xml_text(path: Path) -> tuple[str, bool]:
    data = path.read_bytes()
    has_bom = data.startswith(b"\xef\xbb\xbf")
    return data.decode("utf-8-sig"), has_bom


def encode_xml_text(text: str, has_bom: bool) -> bytes:
    data = text.encode("utf-8")
    return (b"\xef\xbb\xbf" + data) if has_bom else data


def remove_top_level_element(text: str, tag: str, path: Path) -> str:
    pattern = re.compile(
        rf"(?ms)^[ \t]+<{re.escape(tag)}(?:\s[^>]*)?>.*?</{re.escape(tag)}>"
        rf"[ \t]*(?:\r?\n|$)"
    )
    updated, count = pattern.subn("", text)
    if count != 1:
        raise RuntimeError(f"Expected one <{tag}> in {path}, found {count}.")
    return updated


def replace_top_level_text(text: str, tag: str, value: str, path: Path) -> str:
    pattern = re.compile(
        rf"(?ms)(^[ \t]+<{re.escape(tag)}(?:\s[^>]*)?>)"
        rf".*?(</{re.escape(tag)}>[ \t]*)(?=\r?$)"
    )
    updated, count = pattern.subn(
        lambda match: match.group(1) + escape(value) + match.group(2), text
    )
    if count != 1:
        raise RuntimeError(f"Expected one <{tag}> in {path}, found {count}.")
    return updated


def schedule_remove(
    changes: dict[Path, tuple[str, bool]], path: Path, tag: str
) -> None:
    text, has_bom = changes.get(path, read_xml_text(path))
    changes[path] = (remove_top_level_element(text, tag, path), has_bom)


def schedule_replace(
    changes: dict[Path, tuple[str, bool]], path: Path, tag: str, value: str
) -> None:
    text, has_bom = changes.get(path, read_xml_text(path))
    changes[path] = (replace_top_level_text(text, tag, value, path), has_bom)


def build_changes() -> tuple[dict[Path, tuple[str, bool]], int, int]:
    changes: dict[Path, tuple[str, bool]] = {}
    removed_elements = 0
    replaced_elements = 0

    part1 = LEGACY_OUTPUT_ROOT / "FIP-Translation Part 1" / "Languages"
    for language_dir in sorted(path for path in part1.iterdir() if path.is_dir()):
        keyed = language_dir / "Keyed"

        hhtools_transport = keyed / "FIP-H&HTools__HHTools_TransportOverrides.xml"
        schedule_remove(changes, hhtools_transport, "AsteroidLetterText")
        schedule_remove(changes, hhtools_transport, "CaravanShuttleFuel")

        whitespring_psycast = (
            keyed / "FIP-Whitespring__Whitespring_PsycastOverrides.xml"
        )
        whitespring_royalty = (
            keyed / "FIP-Whitespring__Whitespring_RoyaltyLanguageOverrides.xml"
        )
        schedule_remove(
            changes, whitespring_psycast, "LetterPsylinkLevelGained_First"
        )
        schedule_remove(
            changes, whitespring_royalty, "LetterPsylinkLevelGained_First"
        )
        removed_elements += 4

    part3 = LEGACY_OUTPUT_ROOT / "FIP-Translation Part 3" / "Languages"
    neutral_values = {
        "English": {
            "AlertNoRegisterExplanation": (
                "To open your business, you need at least one register. "
                "This can be built from the production tab."
            ),
            "TabRegisterOpenedTooltip": (
                "When checked, customers can visit or place orders while a "
                "shift is active."
            ),
            "TabRegisterStocked": "Stocked items:",
        },
        "German": {
            "AlertNoRegisterExplanation": (
                "Um deinen Betrieb zu öffnen, brauchst du mindestens eine Kasse. "
                "Diese kann über den Produktionsreiter gebaut werden."
            ),
            "TabRegisterOpenedTooltip": (
                "Wenn aktiviert, können Kunden während einer aktiven Schicht "
                "einkaufen oder Bestellungen aufgeben."
            ),
            "TabRegisterStocked": "Vorrätige Artikel:",
        },
    }
    for language, values in neutral_values.items():
        keyed = part3 / language / "Keyed"
        storefront = keyed / "2952321484__Storefront.xml"
        gastronomy = keyed / "3509488152__Gastronomy.xml"
        for tag, value in values.items():
            schedule_replace(changes, storefront, tag, value)
            schedule_remove(changes, gastronomy, tag)
            replaced_elements += 1
            removed_elements += 1

    hospitality = part3 / "English" / "Keyed" / "3509486825__Hospitality.xml"
    schedule_remove(changes, hospitality, "GuestBoughtItem")
    schedule_remove(changes, hospitality, "GuestTookFreeItem")
    removed_elements += 2

    part4_labels = (
        LEGACY_OUTPUT_ROOT
        / "FIP-Translation Part 4"
        / "Languages"
        / "English"
        / "Keyed"
        / "3014915404__Labels.xml"
    )
    schedule_remove(changes, part4_labels, "VF_JobLimitations")
    schedule_remove(changes, part4_labels, "VF_OutOfFuel")
    removed_elements += 2

    return changes, removed_elements, replaced_elements


def extended_path(path: Path) -> str:
    resolved = str(path.resolve())
    if os.name == "nt" and not resolved.startswith("\\\\?\\"):
        return "\\\\?\\" + resolved
    return resolved


def count_conflicting_groups() -> tuple[int, int]:
    groups: dict[tuple[str, str, str, str], list[str]] = defaultdict(list)
    parse_errors = 0

    for mod in TRANSLATION_PARTS:
        language_root = mod / "Languages"
        for directory, _, filenames in os.walk(language_root):
            for filename in filenames:
                if not filename.lower().endswith(".xml"):
                    continue
                path = Path(directory) / filename
                relative = path.relative_to(mod)
                parts = relative.parts
                if len(parts) < 4:
                    continue
                language = parts[1]
                if parts[2] == "Keyed":
                    namespace = "Keyed"
                elif parts[2] == "DefInjected" and len(parts) >= 5:
                    namespace = f"DefInjected/{parts[3]}"
                else:
                    continue

                try:
                    with open(extended_path(path), "rb") as stream:
                        root = ET.parse(stream).getroot()
                except (OSError, ET.ParseError):
                    parse_errors += 1
                    continue

                for element in list(root):
                    if list(element):
                        value = "".join(
                            ET.tostring(child, encoding="unicode")
                            for child in list(element)
                        )
                    else:
                        value = element.text or ""
                    key = (mod.name, language, namespace, element.tag)
                    groups[key].append(value.strip())

    conflicts = sum(
        1
        for values in groups.values()
        if len(values) > 1 and len(set(values)) > 1
    )
    return conflicts, parse_errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Apply the documented ownership decisions.",
    )
    args = parser.parse_args()

    before, parse_errors = count_conflicting_groups()
    print(f"conflicting_groups_before={before}")
    print(f"parse_errors_before={parse_errors}")
    if not args.apply:
        return 1 if parse_errors else 0

    if before != 97:
        raise RuntimeError(
            f"Expected the reviewed set of 97 conflicts, found {before}."
        )
    if parse_errors:
        raise RuntimeError("Translation XML contains parse errors.")

    changes, removed_elements, replaced_elements = build_changes()
    for path, (text, _) in changes.items():
        ET.fromstring(text)

    for path, (text, has_bom) in changes.items():
        path.write_bytes(encode_xml_text(text, has_bom))

    after, parse_errors = count_conflicting_groups()
    print(f"changed_files={len(changes)}")
    print(f"removed_elements={removed_elements}")
    print(f"replaced_elements={replaced_elements}")
    print(f"conflicting_groups_after={after}")
    print(f"parse_errors_after={parse_errors}")
    if after or parse_errors:
        raise RuntimeError("Conflict resolution did not produce a clean result.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
