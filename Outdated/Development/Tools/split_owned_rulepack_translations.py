#!/usr/bin/env python3
"""Split the combined H&HTools English RulePackDef translations by owner."""

from __future__ import annotations

import argparse
from pathlib import Path
import xml.etree.ElementTree as ET


EXPECTED_COUNTS = {
    "hhtools": 299,
    "arktos": 955,
    "greenway": 461,
}


def document(lines: list[str]) -> str:
    return "\n".join(
        [
            '<?xml version="1.0" encoding="utf-8"?>',
            "<LanguageData>",
            *lines,
            "</LanguageData>",
            "",
        ]
    )


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--apply",
        action="store_true",
        help="Write the three ownership-specific files after validating the split.",
    )
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    source = (
        repository
        / "New-Mods"
        / "FIP-H&HTools"
        / "LoadFolders"
        / "Base"
        / "Languages"
        / "English"
        / "DefInjected"
        / "RulePackDef"
        / "HHTools_Arktos_World_RulePackHelpers.xml"
    )
    targets = {
        "hhtools": source,
        "arktos": (
            repository
            / "New-Mods"
            / "FIP-Arktos"
            / "LoadFolders"
            / "Base"
            / "Languages"
            / "English"
            / "DefInjected"
            / "RulePackDef"
            / "Arktos_World_RulePacks.xml"
        ),
        "greenway": (
            repository
            / "New-Mods"
            / "FIP-Greenway"
            / "LoadFolders"
            / "Base"
            / "Languages"
            / "English"
            / "DefInjected"
            / "RulePackDef"
            / "Greenway_Religion_NameMakers.xml"
        ),
    }

    root = ET.parse(source).getroot()
    if root.tag != "LanguageData":
        raise RuntimeError(f"Unexpected root element in {source}: {root.tag}")

    groups: dict[str, list[str]] = {key: [] for key in targets}
    for line in source.read_text(encoding="utf-8-sig").splitlines():
        stripped = line.lstrip()
        if not stripped.startswith("<HHTools_"):
            continue
        if stripped.startswith("<HHTools_Arktos_"):
            owner = "arktos"
        elif stripped.startswith("<HHTools_Greenway_"):
            owner = "greenway"
        else:
            owner = "hhtools"
        groups[owner].append(line)

    counts = {owner: len(lines) for owner, lines in groups.items()}
    if counts != EXPECTED_COUNTS:
        raise RuntimeError(f"Unexpected ownership counts: {counts}")

    rendered = {owner: document(lines) for owner, lines in groups.items()}
    for owner, xml in rendered.items():
        parsed = ET.fromstring(xml)
        if len(parsed) != EXPECTED_COUNTS[owner]:
            raise RuntimeError(f"Rendered {owner} count changed unexpectedly.")

    print(
        "Validated RulePackDef split: "
        + ", ".join(f"{owner}={counts[owner]}" for owner in targets)
    )
    if not args.apply:
        print("Dry run only; pass --apply to write the split files.")
        return 0

    for owner, target in targets.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(rendered[owner], encoding="utf-8", newline="\n")
        print(f"Wrote {target.relative_to(repository)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
