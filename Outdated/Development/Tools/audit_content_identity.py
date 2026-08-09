#!/usr/bin/env python3
"""Audit FIP module ownership, duplicate assets, and English DefInjected data.

This complements validate_refactor.py with checks that need semantic review:

* exact duplicate runtime assets,
* module prefixes on runtime assets, XML files, and declared Defs,
* duplicate LanguageData keys (including identical duplicates),
* English DefInjected values that merely repeat a value already in a local Def,
* English DefInjected values in playable FIP modules that exactly mirror an
  installed Vanilla, DLC, FCP, VE, or other upstream source value,
* label/description-only PatchOperationReplace operations that can become
  DefInjected entries.

The script is intentionally read-only. It reports candidates and leaves changes
to a reviewed refactor because identical directional sprites and cross-module
copies can be required by RimWorld's loaders or by module independence.
"""

from __future__ import annotations

from collections import defaultdict
import hashlib
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET


REPOSITORY = Path(__file__).resolve().parents[2]
NEW_MODS = REPOSITORY / "New-Mods"
RIMWORLD_VERSION = "1.6"
RIMWORLD_ROOT_ENV = "FIP_RIMWORLD_ROOT"
VERSION_DIRECTORY = re.compile(r"^1\.\d+$")

MODULE_PREFIXES = {
    "FIP-Arktos": "Arktos_",
    "FIP-Corvega": "Corvega_",
    "FIP-Donaustahl": "Donaustahl_",
    "FIP-FutureTec": "FutureTec_",
    "FIP-Greenway": "Greenway_",
    "FIP-H&HTools": "HHTools_",
    "FIP-Hubris": "Hubris_",
    "FIP-Lucky 38": "Lucky38_",
    "FIP-Poseidon": "Poseidon_",
    "FIP-Repconn": "Repconn_",
    "FIP-RobCo": "RobCo_",
    "FIP-Sunset": "Sunset_",
    "FIP-WestTek": "WestTek_",
    "FIP-Whitespring": "Whitespring_",
}

ASSET_SUFFIXES = {".png", ".jpg", ".jpeg", ".wav", ".ogg"}
GENERIC_XML_FILES = {"About.xml", "LoadFolders.xml"}
TRANSLATABLE_LEAVES = {
    "arrivedletterlabel",
    "arrivedlettertext",
    "basedesc",
    "baseinspectline",
    "basetitle",
    "basetitlefemale",
    "beginletterlabel",
    "beginlettertext",
    "calledoffmessage",
    "chargenoun",
    "customlabel",
    "customletterlabel",
    "customlettertext",
    "customsummary",
    "description",
    "descriptionextra",
    "descriptionshort",
    "failmessage",
    "failtext",
    "failtriggertext",
    "finishedmessage",
    "fixedname",
    "gerund",
    "gerundlabel",
    "header",
    "headertip",
    "helptext",
    "inspectstring",
    "jobstring",
    "jointext",
    "label",
    "labelfemale",
    "labelfemaleplural",
    "labelmale",
    "labelmaleplural",
    "labelnoun",
    "labelnounpretty",
    "labelplural",
    "labelshort",
    "leaderpawnsingular",
    "leadertitle",
    "leadertitlefemale",
    "letterdesc",
    "letterlabel",
    "lettertext",
    "lockedreason",
    "name",
    "namenoun",
    "nameprefix",
    "namesuffix",
    "note",
    "notworkingkey",
    "pawncannotequipreason",
    "pawnlabel",
    "pawnplural",
    "pawnsarrivalmessage",
    "pawnsplural",
    "pawnsingular",
    "rejectinputmessage",
    "reportstring",
    "skilldescription",
    "skilllabel",
    "successmessage",
    "successtext",
    "summary",
    "text",
    "tip",
    "title",
    "titlefemale",
    "titleshort",
    "titleshortfemale",
    "tooltip",
    "verb",
}
PATCH_DEF_RE = re.compile(
    r"(?:^|/)Defs/(?P<type>[A-Za-z_][A-Za-z0-9_.]*)"
    r'\[defName=(?P<quote>["\'])(?P<name>[^"\']+)(?P=quote)\]'
    r"/(?P<path>.+)$"
)
APPROVED_SAME_MOD_DUPLICATE_ASSET_GROUPS = {
    frozenset(
        {
            "FIP-Lucky 38/LoadFolders/Plants_VBrewECandT/Textures/FIP-Lucky 38/Buildings/Lucky38_CoffeeWorkbench_east.png",
            "FIP-Lucky 38/LoadFolders/Plants_VBrewECandT/Textures/FIP-Lucky 38/Buildings/Lucky38_CoffeeWorkbench_south.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/MechGestator/RobCo_MechGestatorGlass_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/MechGestator/RobCo_MechGestatorGlass_south.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/LargeMechGestator/RobCo_LargeMechGestatorGlass_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/LargeMechGestator/RobCo_LargeMechGestatorGlass_south.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/BasicRecharger/RobCo_BasicRecharger_west.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/LargeMechGestator/RobCo_LargeMechGestator_west.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/SubcoreRipscanner/RobCo_SubcoreRipscanner_west.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/SubcoreSoftscanner/RobCo_SubcoreSoftscanner_west.png",
        }
    ): "west-view slots currently using shared placeholder art",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/LibertyPrime/LibertyPrimeMK1/RobCo_body_south.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/LibertyPrime/LibertyPrimeMK2/RobCo_body_south.png",
        }
    ): "required variant texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Pacificator/RobCo_Pacificator_east.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Pacificator/RobCo_Pacificator_west.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Scurrybot/RobCo_Scurrybot_east.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Scurrybot/RobCo_Scurrybot_west.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Roboscorpion/RobCo_Roboscorpion_east.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/Roboscorpion/RobCo_Roboscorpion_west.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/SubcoreEncoder/RobCo_SubcoreEncoder_east.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Buildings/SubcoreEncoder/RobCo_SubcoreEncoder_west.png",
        }
    ): "required directional texture slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Sounds/FIP-RobCo/Robots/Robobrain/Angry/RobCo_Robobrain_Angry_2.ogg",
            "FIP-RobCo/LoadFolders/Base/Sounds/FIP-RobCo/Robots/Robobrain/Angry/RobCo_Robobrain_Angry_3.ogg",
        }
    ): "deliberate weighted sound slots",
    frozenset(
        {
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesA_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesB_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesC_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesD_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesE_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesF_north.png",
            "FIP-RobCo/LoadFolders/Base/Textures/FIP-RobCo/Robots/ThinkTank/Eyes/RobCo_Thinktank_eyesJ_north.png",
        }
    ): "required variant texture slots",
}


def relative(path: Path) -> str:
    return path.relative_to(REPOSITORY).as_posix()


def extended_path(path: Path) -> str:
    resolved = str(path.resolve())
    if os.name == "nt" and not resolved.startswith("\\\\?\\"):
        return "\\\\?\\" + resolved
    return resolved


def parse_xml(path: Path) -> ET.Element:
    with open(extended_path(path), "rb") as stream:
        return ET.parse(stream).getroot()


def text_value(element: ET.Element) -> str:
    return "".join(element.itertext()).strip()


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with open(extended_path(path), "rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def language_scope(path: Path, mod: Path) -> tuple[str, str] | None:
    parts = path.relative_to(mod).parts
    try:
        index = parts.index("Languages")
    except ValueError:
        return None
    if len(parts) <= index + 4 or parts[index + 2] != "DefInjected":
        return None
    return parts[index + 1], parts[index + 3]


def flatten_def_leaves(definition: ET.Element) -> dict[str, str]:
    """Return DefInjected-style relative keys for explicitly declared leaves."""

    leaves: dict[str, str] = {}

    def walk(element: ET.Element, parts: list[str]) -> None:
        children = [child for child in element if isinstance(child.tag, str)]
        if not children:
            if parts:
                leaves[".".join(parts)] = text_value(element)
            return

        li_index = 0
        for child in children:
            if child.tag == "defName" and not parts:
                continue
            if child.tag == "li":
                child_part = str(li_index)
                li_index += 1
            else:
                child_part = child.tag
            walk(child, parts + [child_part])

    walk(definition, [])
    return leaves


def discover_rimworld_root() -> Path | None:
    """Find the local RimWorld root without baking one machine into the audit."""

    candidates: list[Path] = []
    configured = os.environ.get(RIMWORLD_ROOT_ENV, "").strip()
    if configured:
        candidates.append(Path(configured))
    candidates.extend(
        [
            Path("D:/Steam/steamapps/common/RimWorld"),
            Path("C:/Program Files (x86)/Steam/steamapps/common/RimWorld"),
            Path("C:/Program Files/Steam/steamapps/common/RimWorld"),
        ]
    )
    for candidate in candidates:
        if (candidate / "Data").is_dir():
            return candidate.resolve()
    return None


def is_current_version_path(path: Path, source_root: Path) -> bool:
    """Ignore definitions from explicitly versioned pre-1.6 directories."""

    for part in path.relative_to(source_root).parts:
        if VERSION_DIRECTORY.fullmatch(part) and part != RIMWORLD_VERSION:
            return False
    return True


def is_installed_fip_path(path: Path, source_root: Path) -> bool:
    """Do not let the installed copy of FIP validate the working copy itself."""

    parts = path.relative_to(source_root).parts
    return source_root.name == "Mods" and bool(parts) and parts[0].startswith("FIP-")


def external_language_scope(path: Path, source_root: Path) -> tuple[str, str] | None:
    parts = path.relative_to(source_root).parts
    try:
        index = parts.index("Languages")
    except ValueError:
        return None
    if (
        len(parts) <= index + 4
        or parts[index + 1] != "English"
        or parts[index + 2] != "DefInjected"
    ):
        return None
    return parts[index + 1], parts[index + 3]


def build_upstream_value_catalog(
    rimworld_root: Path | None,
) -> tuple[
    dict[tuple[str, str], list[tuple[str, Path]]],
    int,
    list[str],
    list[Path],
]:
    """Collect explicit English Def values from the installed upstream stack."""

    catalog: dict[tuple[str, str], list[tuple[str, Path]]] = defaultdict(list)
    parse_errors: list[str] = []
    scanned_files = 0
    if rimworld_root is None:
        return catalog, scanned_files, parse_errors, []

    steamapps = rimworld_root.parent.parent
    candidate_roots = [
        rimworld_root / "Data",
        rimworld_root / "Mods",
        steamapps / "workshop" / "content" / "294100",
    ]
    source_roots = [root for root in candidate_roots if root.is_dir()]

    for source_root in source_roots:
        for path in source_root.rglob("*.xml"):
            relative_parts = path.relative_to(source_root).parts
            is_defs_file = "Defs" in relative_parts
            is_english_injection = (
                external_language_scope(path, source_root) is not None
            )
            if not is_defs_file and not is_english_injection:
                continue
            if not is_current_version_path(path, source_root):
                continue
            if is_installed_fip_path(path, source_root):
                continue
            scanned_files += 1
            try:
                root = parse_xml(path)
            except (OSError, ET.ParseError) as error:
                parse_errors.append(f"{path.as_posix()}: {error}")
                continue

            if root.tag == "Defs":
                for definition in root:
                    if not isinstance(definition.tag, str):
                        continue
                    def_name = (definition.findtext("defName") or "").strip()
                    if not def_name:
                        continue
                    for leaf_path, value in flatten_def_leaves(definition).items():
                        catalog[(definition.tag, f"{def_name}.{leaf_path}")].append(
                            (value, path)
                        )
                continue

            scope = external_language_scope(path, source_root)
            if root.tag != "LanguageData" or scope is None:
                continue
            _, def_type = scope
            for element in root:
                if isinstance(element.tag, str):
                    catalog[(def_type, element.tag)].append(
                        (text_value(element), path)
                    )

    return catalog, scanned_files, parse_errors, source_roots


def normalize_xpath_path(raw_path: str) -> str | None:
    parts: list[str] = []
    for segment in raw_path.split("/"):
        segment = segment.strip()
        if not segment:
            continue
        match = re.fullmatch(r"li\[(\d+)\]", segment)
        if match:
            parts.append(str(int(match.group(1)) - 1))
            continue
        if "[" in segment or "]" in segment:
            return None
        parts.append(segment)
    return ".".join(parts) if parts else None


def print_group(title: str, entries: list[str], limit: int = 200) -> None:
    print(f"\n{title}: {len(entries)}")
    for entry in entries[:limit]:
        print(f"- {entry}")
    if len(entries) > limit:
        print(f"- ... {len(entries) - limit} weitere")


def main() -> int:
    mods = sorted(
        path
        for path in NEW_MODS.iterdir()
        if path.is_dir() and (path / "About" / "About.xml").is_file()
    )
    playable_mods = [mod for mod in mods if mod.name in MODULE_PREFIXES]
    rimworld_root = discover_rimworld_root()
    (
        upstream_values,
        upstream_xml_scanned,
        upstream_parse_errors,
        upstream_source_roots,
    ) = build_upstream_value_catalog(rimworld_root)

    parsed: dict[Path, ET.Element] = {}
    parse_errors: list[str] = []
    for path in sorted(NEW_MODS.rglob("*.xml")):
        try:
            parsed[path] = parse_xml(path)
        except (OSError, ET.ParseError) as error:
            parse_errors.append(f"{relative(path)}: {error}")

    asset_prefix_mismatches: list[str] = []
    xml_prefix_mismatches: list[str] = []
    def_prefix_mismatches: list[str] = []
    declared_values: dict[tuple[str, str, str], tuple[str, Path]] = {}
    global_defs: dict[tuple[str, str], list[tuple[str, Path]]] = defaultdict(list)

    for mod in playable_mods:
        prefix = MODULE_PREFIXES[mod.name]
        prefix_lower = prefix.lower()
        for path in sorted(mod.rglob("*")):
            if not path.is_file():
                continue
            suffix = path.suffix.lower()
            if (
                suffix in ASSET_SUFFIXES
                and "About" not in path.relative_to(mod).parts
                and not path.stem.lower().startswith(prefix_lower)
            ):
                asset_prefix_mismatches.append(
                    f"{mod.name}: {path.relative_to(mod).as_posix()}"
                )
            if (
                suffix == ".xml"
                and path.name not in GENERIC_XML_FILES
                and not path.stem.lower().startswith(prefix_lower)
            ):
                xml_prefix_mismatches.append(
                    f"{mod.name}: {path.relative_to(mod).as_posix()}"
                )

        for path, root in parsed.items():
            if mod not in path.parents or root.tag != "Defs":
                continue
            for definition in root:
                if not isinstance(definition.tag, str):
                    continue
                def_name = (definition.findtext("defName") or "").strip()
                if def_name:
                    global_defs[(definition.tag, def_name)].append((mod.name, path))
                    if not def_name.lower().startswith(prefix_lower):
                        def_prefix_mismatches.append(
                            f"{mod.name}: {definition.tag}/{def_name} "
                            f"({path.relative_to(mod).as_posix()})"
                        )
                    for leaf_path, value in flatten_def_leaves(definition).items():
                        declared_values[
                            (mod.name, definition.tag, f"{def_name}.{leaf_path}")
                        ] = (value, path)
                    continue
                parent_name = definition.attrib.get("Name", "").strip()
                if (
                    parent_name
                    and not parent_name.lower().startswith(prefix_lower)
                ):
                    def_prefix_mismatches.append(
                        f"{mod.name}: {definition.tag}@Name/{parent_name} "
                        f"({path.relative_to(mod).as_posix()})"
                    )

    duplicate_defs: list[str] = []
    for (def_type, def_name), entries in sorted(global_defs.items()):
        if len(entries) <= 1:
            continue
        locations = ", ".join(
            f"{mod}:{relative(path)}" for mod, path in entries
        )
        duplicate_defs.append(f"{def_type}/{def_name}: {locations}")

    language_entries: dict[
        tuple[str, str, str, str], list[tuple[str, Path]]
    ] = defaultdict(list)
    for mod in mods:
        for path, root in parsed.items():
            if mod not in path.parents or root.tag != "LanguageData":
                continue
            scope = language_scope(path, mod)
            if scope is None:
                continue
            language, def_type = scope
            for element in root:
                if isinstance(element.tag, str):
                    language_entries[
                        (mod.name, language, def_type, element.tag)
                    ].append((text_value(element), path))

    duplicate_language_keys: list[str] = []
    for key, entries in sorted(language_entries.items()):
        if len(entries) <= 1:
            continue
        values = {value for value, _ in entries}
        state = "identisch" if len(values) == 1 else "widersprüchlich"
        locations = ", ".join(relative(path) for _, path in entries)
        duplicate_language_keys.append(
            f"{'/'.join(key)} ({state}, {len(entries)}x): {locations}"
        )

    redundant_english_injections: list[str] = []
    redundant_upstream_english_injections: list[str] = []
    for (mod_name, language, def_type, key), entries in sorted(
        language_entries.items()
    ):
        if language != "English":
            continue
        declared = declared_values.get((mod_name, def_type, key))
        if declared is None:
            continue
        declared_value, def_path = declared
        for value, language_path in entries:
            if value == declared_value:
                redundant_english_injections.append(
                    f"{mod_name}: {def_type}/{key}={value!r} "
                    f"({relative(language_path)} mirrors {relative(def_path)})"
                )
        if mod_name not in MODULE_PREFIXES:
            continue
        upstream_entries = upstream_values.get((def_type, key), [])
        for value, language_path in entries:
            matching_sources = sorted(
                {
                    source
                    for upstream_value, source in upstream_entries
                    if value == upstream_value
                },
                key=lambda source: source.as_posix().lower(),
            )
            if not matching_sources:
                continue
            displayed_sources = ", ".join(
                source.as_posix() for source in matching_sources[:3]
            )
            if len(matching_sources) > 3:
                displayed_sources += f", +{len(matching_sources) - 3} more"
            redundant_upstream_english_injections.append(
                f"{mod_name}: {def_type}/{key}={value!r} "
                f"({relative(language_path)} mirrors {displayed_sources})"
            )

    patch_replace_candidates: list[str] = []
    for path, root in parsed.items():
        if root.tag != "Patch":
            continue
        for operation in root.iter():
            if operation.attrib.get("Class") != "PatchOperationReplace":
                continue
            xpath = operation.findtext("xpath", "").strip()
            match = PATCH_DEF_RE.search(xpath)
            if match is None:
                continue
            normalized_path = normalize_xpath_path(match.group("path"))
            if not normalized_path:
                continue
            leaf = normalized_path.rsplit(".", 1)[-1].lower()
            if leaf not in TRANSLATABLE_LEAVES:
                continue
            value = operation.find("value")
            if value is None:
                continue
            children = [child for child in value if isinstance(child.tag, str)]
            if len(children) != 1 or children[0].tag.lower() != leaf:
                continue
            def_type = match.group("type")
            def_name = match.group("name")
            key = f"{def_name}.{normalized_path}"
            patch_replace_candidates.append(
                f"{relative(path)}: {def_type}/{key}={text_value(children[0])!r}"
            )

    assets = sorted(
        path
        for mod in playable_mods
        for path in mod.rglob("*")
        if path.is_file() and path.suffix.lower() in ASSET_SUFFIXES
    )
    hashes: dict[str, list[Path]] = defaultdict(list)
    for path in assets:
        hashes[sha256(path)].append(path)

    same_mod_duplicate_assets: list[str] = []
    approved_same_mod_duplicate_assets: list[str] = []
    unexpected_same_mod_duplicate_assets: list[str] = []
    cross_mod_duplicate_assets: list[str] = []
    same_name_duplicate_assets: list[str] = []
    for digest, paths in sorted(hashes.items()):
        if len(paths) <= 1:
            continue
        relative_paths = [relative(path) for path in paths]
        by_mod: dict[str, list[Path]] = defaultdict(list)
        for path in paths:
            by_mod[path.relative_to(NEW_MODS).parts[0]].append(path)
        if len(by_mod) > 1:
            line = f"{digest[:12]} ({len(paths)}x): " + ", ".join(relative_paths)
            cross_mod_duplicate_assets.append(line)
        for mod_name, mod_paths in sorted(by_mod.items()):
            if len(mod_paths) <= 1:
                continue
            relative_to_new_mods = frozenset(
                path.relative_to(NEW_MODS).as_posix() for path in mod_paths
            )
            line = (
                f"{digest[:12]} ({mod_name}, {len(mod_paths)}x): "
                + ", ".join(relative(path) for path in mod_paths)
            )
            same_mod_duplicate_assets.append(line)
            reason = APPROVED_SAME_MOD_DUPLICATE_ASSET_GROUPS.get(
                relative_to_new_mods
            )
            if reason is None:
                unexpected_same_mod_duplicate_assets.append(line)
            else:
                approved_same_mod_duplicate_assets.append(
                    f"{line} ({reason})"
                )
        names: dict[str, list[Path]] = defaultdict(list)
        for path in paths:
            names[path.name.lower()].append(path)
        for name_paths in names.values():
            if len(name_paths) > 1:
                same_name_duplicate_assets.append(
                    f"{digest[:12]} ({len(name_paths)}x gleicher Dateiname): "
                    + ", ".join(relative(path) for path in name_paths)
                )

    print(
        "modules="
        f"{len(mods)} (playable={len(playable_mods)}, "
        f"translations={len(mods) - len(playable_mods)})"
    )
    print(
        f"xml={len(parsed)}, parse_errors={len(parse_errors)}, "
        f"assets={len(assets)}"
    )
    print(
        "upstream_scan="
        f"{'enabled' if rimworld_root else 'skipped'}, "
        f"roots={len(upstream_source_roots)}, "
        f"xml={upstream_xml_scanned}, "
        f"parse_errors={len(upstream_parse_errors)}, "
        "redundant_playable_english_injections="
        f"{len(redundant_upstream_english_injections)}"
    )
    print(
        "findings="
        f"asset_prefix={len(asset_prefix_mismatches)}, "
        f"xml_prefix={len(xml_prefix_mismatches)}, "
        f"def_prefix={len(def_prefix_mismatches)}, "
        f"duplicate_defs={len(duplicate_defs)}, "
        f"duplicate_language_keys={len(duplicate_language_keys)}, "
        f"redundant_english_injections={len(redundant_english_injections)}, "
        "redundant_upstream_english_injections="
        f"{len(redundant_upstream_english_injections)}, "
        f"translatable_patch_replaces={len(patch_replace_candidates)}, "
        f"same_mod_duplicate_assets={len(same_mod_duplicate_assets)}, "
        "approved_same_mod_duplicate_assets="
        f"{len(approved_same_mod_duplicate_assets)}, "
        "unexpected_same_mod_duplicate_assets="
        f"{len(unexpected_same_mod_duplicate_assets)}, "
        f"cross_mod_duplicate_assets={len(cross_mod_duplicate_assets)}, "
        f"same_name_duplicate_assets={len(same_name_duplicate_assets)}"
    )

    print_group("XML-Parsefehler", parse_errors)
    print_group("Asset-Dateien ohne Modulpräfix", asset_prefix_mismatches)
    print_group("XML-Dateien ohne Modulpräfix", xml_prefix_mismatches)
    print_group("Deklarierte Defs ohne Modulpräfix", def_prefix_mismatches)
    print_group("Global doppelte direkte Defs", duplicate_defs)
    print_group("Doppelte DefInjected-Schlüssel", duplicate_language_keys)
    print_group(
        "Redundante englische DefInjected-Spiegel",
        redundant_english_injections,
    )
    print_group(
        "Redundante englische DefInjected-Spiegel gegen installierte Quellen",
        redundant_upstream_english_injections,
    )
    print_group(
        "Nicht lesbare externe Upstream-XML-Dateien (informativ)",
        upstream_parse_errors,
    )
    print_group(
        "In DefInjected konvertierbare Text-Patch-Replaces",
        patch_replace_candidates,
    )
    print_group(
        "Freigegebene bytegleiche Assets innerhalb eines Mods",
        approved_same_mod_duplicate_assets,
    )
    print_group(
        "Unerwartete bytegleiche Assets innerhalb eines Mods",
        unexpected_same_mod_duplicate_assets,
    )
    print_group(
        "Bytegleiche Assets über Modgrenzen",
        cross_mod_duplicate_assets,
    )
    print_group(
        "Bytegleiche Assets mit gleichem Dateinamen",
        same_name_duplicate_assets,
    )

    blocking_findings = (
        parse_errors
        or asset_prefix_mismatches
        or xml_prefix_mismatches
        or def_prefix_mismatches
        or duplicate_defs
        or duplicate_language_keys
        or redundant_english_injections
        or redundant_upstream_english_injections
        or patch_replace_candidates
        or unexpected_same_mod_duplicate_assets
    )
    return 1 if blocking_findings else 0


if __name__ == "__main__":
    sys.exit(main())
