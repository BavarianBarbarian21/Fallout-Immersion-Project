#!/usr/bin/env python3
"""Apply canonical owner prefixes to legacy FIP Def identifiers.

The default mode is a dry run. Pass --apply to rewrite references throughout
New-Mods and Development/Source and to rename the five owner-mismatched
NameMaker XML files.

The generic vanilla identifiers Highmate and SPECIAL are handled narrowly so
that WestTek's new Defs no longer replace the vanilla Defs while references to
the actual vanilla Defs remain unchanged.
"""

from __future__ import annotations

from collections import Counter
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from audit_content_identity import (
    MODULE_PREFIXES,
    NEW_MODS,
    REPOSITORY,
    extended_path,
    parse_xml,
    relative,
)


SOURCE = REPOSITORY / "Development" / "Source"
TEXT_SUFFIXES = {".xml", ".cs", ".json", ".txt", ".md"}
GENERIC_IDS = {"Highmate", "SPECIAL", "Pungaling"}


def canonical_name(mod_name: str, old_name: str) -> str | None:
    if mod_name == "FIP-Arktos":
        if old_name.startswith("HHTools_Arktos_"):
            return old_name.removeprefix("HHTools_")
        if old_name == "ArktosUrban":
            return "Arktos_Urban"
    elif mod_name == "FIP-Greenway":
        if old_name.startswith("HHTools_Greenway_"):
            return old_name.removeprefix("HHTools_")
        if old_name == "Dryad_Pungaling":
            return "Greenway_Dryad_Pungaling"
        if old_name == "Pungaling":
            return "Greenway_Pungaling"
    elif mod_name == "FIP-H&HTools":
        if old_name.startswith("FIPD_"):
            return "HHTools_" + old_name.removeprefix("FIPD_")
        if old_name in {
            "Cascadia_Seattle",
            "Cascadia_Vancouver",
            "FalloutTribalClan",
            "Texico_Chihuahua",
            "Texico_RioGrande",
            "Texico_Sinaloa",
            "Wasteland_CityRuins",
            "Wasteland_Desert",
            "Wasteland_Forest",
        }:
            return "HHTools_" + old_name
    elif mod_name == "FIP-RobCo":
        if old_name.startswith("WestTek_Gene_"):
            return "RobCo_Gene_" + old_name.removeprefix("WestTek_Gene_")
        if old_name in {
            "PowerClaw",
            "ReloadAbilityFromMap",
            "ReloadMechAbility",
            "Shot_ChargeBlasterCannon",
        }:
            return "RobCo_" + old_name
    elif mod_name == "FIP-WestTek":
        if old_name == "SPECIAL":
            return "WestTek_SPECIAL"
        if old_name == "Highmate":
            return "WestTek_Xenotype_SNuffy"
    return None


def read_bytes(path: Path) -> bytes:
    with open(extended_path(path), "rb") as stream:
        return stream.read()


def write_bytes(path: Path, data: bytes) -> None:
    with open(extended_path(path), "wb") as stream:
        stream.write(data)


def replace_generic_ids(source: str) -> str:
    source = source.replace(
        "<defName>Highmate</defName>",
        "<defName>WestTek_Xenotype_SNuffy</defName>",
    )
    source = source.replace(
        "<Highmate.",
        "<WestTek_Xenotype_SNuffy.",
    ).replace(
        "</Highmate.",
        "</WestTek_Xenotype_SNuffy.",
    )
    source = source.replace(
        "XenotypeDef Highmate",
        "XenotypeDef WestTek_Xenotype_SNuffy",
    ).replace(
        "WestTekDefOf.Highmate",
        "WestTekDefOf.WestTek_Xenotype_SNuffy",
    )

    source = source.replace(
        "<defName>SPECIAL</defName>",
        "<defName>WestTek_SPECIAL</defName>",
    ).replace(
        "<displayCategory>SPECIAL</displayCategory>",
        "<displayCategory>WestTek_SPECIAL</displayCategory>",
    )
    source = source.replace(
        "<SPECIAL.",
        "<WestTek_SPECIAL.",
    ).replace(
        "</SPECIAL.",
        "</WestTek_SPECIAL.",
    )

    source = source.replace(
        "<defName>Pungaling</defName>",
        "<defName>Greenway_Pungaling</defName>",
    )
    for tag in ("pawnKindDef", "ThingDef", "GauranlenTreeModeDef"):
        source = source.replace(
            f"<{tag}>Pungaling</{tag}>",
            f"<{tag}>Greenway_Pungaling</{tag}>",
        )
    source = source.replace(
        "<Pungaling.",
        "<Greenway_Pungaling.",
    ).replace(
        "</Pungaling.",
        "</Greenway_Pungaling.",
    )
    return source


def main() -> int:
    apply_changes = "--apply" in sys.argv[1:]
    unknown = [argument for argument in sys.argv[1:] if argument != "--apply"]
    if unknown:
        print(f"Unknown arguments: {', '.join(unknown)}", file=sys.stderr)
        return 2

    mappings: dict[str, str] = {}
    declared: list[tuple[str, str, str, Path]] = []
    named_parents: list[tuple[str, str, str, Path]] = []
    for mod_name, prefix in MODULE_PREFIXES.items():
        mod = NEW_MODS / mod_name
        for path in sorted(mod.rglob("*.xml")):
            root = parse_xml(path)
            if root.tag != "Defs":
                continue
            for definition in root:
                if not isinstance(definition.tag, str):
                    continue
                def_name = (definition.findtext("defName") or "").strip()
                if def_name:
                    declared.append((mod_name, definition.tag, def_name, path))
                    if not def_name.lower().startswith(prefix.lower()):
                        target = canonical_name(mod_name, def_name)
                        if target is None:
                            raise RuntimeError(
                                f"No canonical mapping for {mod_name} "
                                f"{definition.tag}/{def_name} in {relative(path)}"
                            )
                        previous = mappings.setdefault(def_name, target)
                        if previous != target:
                            raise RuntimeError(
                                f"Ambiguous mapping for {def_name}: "
                                f"{previous} vs {target}"
                            )
                parent_name = definition.attrib.get("Name", "").strip()
                if parent_name:
                    named_parents.append(
                        (mod_name, definition.tag, parent_name, path)
                    )
                    if not parent_name.lower().startswith(prefix.lower()):
                        target = canonical_name(mod_name, parent_name)
                        if target is None:
                            raise RuntimeError(
                                f"No canonical mapping for {mod_name} "
                                f"{definition.tag}@Name/{parent_name} "
                                f"in {relative(path)}"
                            )
                        mappings[parent_name] = target

    existing_identities = {
        (def_type, def_name) for _, def_type, def_name, _ in declared
    }
    collisions: list[str] = []
    for mod_name, def_type, old_name, path in declared:
        new_name = mappings.get(old_name)
        if new_name and (def_type, new_name) in existing_identities:
            collisions.append(
                f"{def_type}/{old_name} -> {new_name} ({relative(path)})"
            )
    if collisions:
        raise RuntimeError("Target Def collisions:\n" + "\n".join(collisions))

    text_files = sorted(
        path
        for base in (NEW_MODS, SOURCE)
        for path in base.rglob("*")
        if path.is_file() and path.suffix.lower() in TEXT_SUFFIXES
    )
    rewritten: dict[Path, bytes] = {}
    replacements_by_file: Counter[str] = Counter()
    safe_mappings = {
        old_name: new_name
        for old_name, new_name in mappings.items()
        if old_name not in GENERIC_IDS
    }
    safe_pattern = (
        re.compile(
            r"(?<![A-Za-z0-9_])("
            + "|".join(
                re.escape(old_name)
                for old_name in sorted(
                    safe_mappings,
                    key=len,
                    reverse=True,
                )
            )
            + r")(?![A-Za-z0-9_])"
        )
        if safe_mappings
        else None
    )

    for path in text_files:
        original = read_bytes(path)
        has_bom = original.startswith(b"\xef\xbb\xbf")
        try:
            source = original.decode("utf-8-sig")
        except UnicodeDecodeError:
            continue
        updated = (
            safe_pattern.sub(
                lambda match: safe_mappings[match.group(1)],
                source,
            )
            if safe_pattern is not None
            else source
        )
        updated = replace_generic_ids(updated)
        if updated == source:
            continue
        encoded = updated.encode("utf-8")
        if has_bom:
            encoded = b"\xef\xbb\xbf" + encoded
        rewritten[path] = encoded
        replacements_by_file[path.relative_to(REPOSITORY).parts[0]] += 1

    file_renames = {
        NEW_MODS
        / "FIP-Arktos"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Arktos"
        / "NameMaker"
        / "HHTools_Arktos_World_RulePackHelpers.xml":
        NEW_MODS
        / "FIP-Arktos"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Arktos"
        / "NameMaker"
        / "Arktos_World_RulePackHelpers.xml",
        NEW_MODS
        / "FIP-Arktos"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Arktos"
        / "NameMaker"
        / "HHTools_Arktos_World_RulePacks.xml":
        NEW_MODS
        / "FIP-Arktos"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Arktos"
        / "NameMaker"
        / "Arktos_World_RulePacks.xml",
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "HHTools_Greenway_Religion_NewWorldChurch_NameMakers.xml":
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "Greenway_Religion_NewWorldChurch_NameMakers.xml",
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "HHTools_Greenway_Religion_OldWorldChurch_NameMakers.xml":
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "Greenway_Religion_OldWorldChurch_NameMakers.xml",
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "HHTools_Greenway_Religion_Spiritual_NameMakers.xml":
        NEW_MODS
        / "FIP-Greenway"
        / "LoadFolders"
        / "Base"
        / "Defs"
        / "FIP-Greenway"
        / "NameMaker"
        / "Greenway_Religion_Spiritual_NameMakers.xml",
    }
    pending_file_renames: dict[Path, Path] = {}
    for old_path, new_path in file_renames.items():
        if not old_path.is_file() and new_path.is_file():
            continue
        if not old_path.is_file():
            raise RuntimeError(f"Missing rename source: {relative(old_path)}")
        if new_path.exists():
            raise RuntimeError(f"Rename target exists: {relative(new_path)}")
        if REPOSITORY not in old_path.resolve().parents:
            raise RuntimeError(f"Unsafe rename source: {old_path}")
        if REPOSITORY not in new_path.resolve().parents:
            raise RuntimeError(f"Unsafe rename target: {new_path}")
        pending_file_renames[old_path] = new_path

    print(
        f"mode={'apply' if apply_changes else 'dry-run'} "
        f"def_mappings={len(mappings)} declarations="
        f"{sum(1 for _, _, name, _ in declared if name in mappings)} "
        f"named_parents="
        f"{sum(1 for _, _, name, _ in named_parents if name in mappings)} "
        f"rewritten_files={len(rewritten)} "
        f"renamed_files={len(pending_file_renames)}"
    )
    for top_level, count in sorted(replacements_by_file.items()):
        print(f"- {top_level}: {count} files")
    for old_name, new_name in sorted(mappings.items()):
        print(f"- {old_name} -> {new_name}")

    if not apply_changes:
        return 0

    for path, data in rewritten.items():
        write_bytes(path, data)
    for old_path, new_path in pending_file_renames.items():
        os.replace(extended_path(old_path), extended_path(new_path))

    return 0


if __name__ == "__main__":
    sys.exit(main())
