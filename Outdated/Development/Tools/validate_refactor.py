#!/usr/bin/env python3
"""Run the static completion checks for the isolated FIP refactor."""

from __future__ import annotations

from collections import Counter, defaultdict
import hashlib
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from lxml import etree
from PIL import Image

from audit_content_identity import (
    APPROVED_SAME_MOD_DUPLICATE_ASSET_GROUPS,
    MODULE_PREFIXES,
    PATCH_DEF_RE,
    TRANSLATABLE_LEAVES,
    flatten_def_leaves,
    normalize_xpath_path,
    text_value,
)


REPOSITORY = Path(__file__).resolve().parents[2]
NEW_MODS = REPOSITORY / "New-Mods"
SOURCE = REPOSITORY / "Development" / "Source"
TEXT_SUFFIXES = {".xml", ".txt", ".md", ".json", ".yaml", ".yml"}
CE_SCAN_SUFFIXES = TEXT_SUFFIXES | {".cs", ".dll"}
CE_CONTENT_MARKERS = (
    b"combat extended",
    b"combatextended",
    b"ceteam.combatextended",
    b"deserters_nocombatextended",
)
CE_PATH_MARKERS = ("combatextended", "ceteam", "nocombatextended")
IMAGE_SUFFIXES = {".png", ".jpg", ".jpeg"}
AUDIO_SUFFIXES = {".wav", ".ogg"}
PLACEHOLDER = re.compile(
    r"\[[A-Za-z_][A-Za-z0-9_.-]*\]|\{[^{}\r\n]+\}"
)
KNOWN_PLACEHOLDER_VALUES = {
    "todo",
    "text.todo",
    "text_todo",
    "текст todo",
    "텍스트 todo",
    ".todo에",
    "テキスト.todo",
    "テキスト・テキスト・テキスト",
    "文本. 待办事宜",
    "文本待办事宜( t)",
    "文字. 待辦事宜",
    "文字待辦事宜( t)",
}
CURRENT_TRANSLATION_LANGUAGES = {
    "FIP-Translation Chinese": {"ChineseSimplified", "ChineseTraditional"},
    "FIP-Translation Japanese": {"Japanese"},
    "FIP-Translation Korean": {"Korean"},
    "FIP-Translation Russian": {"Russian"},
}
DLC_PREFIX = "ludeon.rimworld."
FIP_PARENT_PREFIXES = (
    "Arktos",
    "Corvega",
    "Donaustahl",
    "FIP",
    "FutureTec",
    "Greenway",
    "HHTools",
    "Hubris",
    "Lucky",
    "Poseidon",
    "Repconn",
    "RobCo",
    "Sunset",
    "WestTek",
    "Whitespring",
)
def extended_path(path: Path) -> str:
    resolved = str(path.resolve())
    if os.name == "nt" and not resolved.startswith("\\\\?\\"):
        return "\\\\?\\" + resolved
    return resolved


def parse_xml(path: Path) -> ET.ElementTree:
    with open(extended_path(path), "rb") as stream:
        return ET.parse(stream)


def read_text(path: Path) -> str:
    with open(extended_path(path), "r", encoding="utf-8-sig") as stream:
        return stream.read()


def file_size(path: Path) -> int:
    return os.stat(extended_path(path)).st_size


def language_scope(path: Path, mod: Path) -> tuple[str, str] | None:
    parts = path.relative_to(mod).parts
    try:
        language_index = parts.index("Languages")
    except ValueError:
        return None
    if len(parts) <= language_index + 3:
        return None
    language = parts[language_index + 1]
    area = parts[language_index + 2]
    if area == "Keyed":
        return language, "Keyed"
    if area == "DefInjected" and len(parts) > language_index + 4:
        return language, f"DefInjected/{parts[language_index + 3]}"
    return None


def find_cycle(graph: dict[str, set[str]]) -> list[str] | None:
    visiting: set[str] = set()
    visited: set[str] = set()
    route: list[str] = []

    def visit(node: str) -> list[str] | None:
        if node in visiting:
            index = route.index(node)
            return route[index:] + [node]
        if node in visited:
            return None
        visiting.add(node)
        route.append(node)
        for successor in sorted(graph.get(node, ())):
            cycle = visit(successor)
            if cycle:
                return cycle
        route.pop()
        visiting.remove(node)
        visited.add(node)
        return None

    for node in sorted(graph):
        cycle = visit(node)
        if cycle:
            return cycle
    return None


def main() -> int:
    failures: list[str] = []
    mod_dirs = sorted(
        path
        for path in NEW_MODS.iterdir()
        if path.is_dir() and (path / "About" / "About.xml").is_file()
    )
    all_files = sorted(
        (Path(directory) / filename)
        for directory, _, filenames in os.walk(NEW_MODS)
        for filename in filenames
    )
    xml_files = [path for path in all_files if path.suffix.lower() == ".xml"]

    ce_compatibility_hits: list[str] = []
    for path in all_files:
        relative_path = path.relative_to(NEW_MODS).as_posix()
        compact_path = relative_path.lower().replace(" ", "").replace("_", "")
        if any(marker in compact_path for marker in CE_PATH_MARKERS):
            ce_compatibility_hits.append(f"path: {relative_path}")
        if path.suffix.lower() not in CE_SCAN_SUFFIXES:
            continue
        with open(extended_path(path), "rb") as stream:
            content = stream.read().lower()
        matched_markers = [
            marker.decode("ascii")
            for marker in CE_CONTENT_MARKERS
            if marker in content
        ]
        if matched_markers:
            ce_compatibility_hits.append(
                f"content: {relative_path} ({', '.join(matched_markers)})"
            )
    failures.extend(
        f"Forbidden Combat Extended compatibility: {entry}"
        for entry in ce_compatibility_hits
    )

    parsed: dict[Path, ET.ElementTree] = {}
    for path in xml_files:
        try:
            parsed[path] = parse_xml(path)
        except (OSError, ET.ParseError) as error:
            failures.append(
                f"XML parse error: {path.relative_to(REPOSITORY)}: {error}"
            )

    package_to_mod: dict[str, Path] = {}
    dependencies: dict[str, set[str]] = {}
    load_after: dict[str, set[str]] = {}
    for mod in mod_dirs:
        about = mod / "About" / "About.xml"
        if about not in parsed:
            continue
        root = parsed[about].getroot()
        package_id = (root.findtext("packageId") or "").strip()
        if not package_id:
            failures.append(f"Missing packageId: {mod.name}")
            continue
        key = package_id.lower()
        if key in package_to_mod:
            failures.append(
                f"Duplicate packageId {package_id}: "
                f"{package_to_mod[key].name}, {mod.name}"
            )
        package_to_mod[key] = mod
        dependencies[key] = {
            (element.text or "").strip().lower()
            for element in root.findall("./modDependencies/li/packageId")
            if (element.text or "").strip()
        }
        load_after[key] = {
            (element.text or "").strip().lower()
            for element in root.findall("./loadAfter/li")
            if (element.text or "").strip()
        }

    missing_loadfolders: list[str] = []
    missing_optional_order: list[str] = []
    loadfolder_entries = 0
    for package_id, mod in package_to_mod.items():
        definition = mod / "LoadFolders.xml"
        if not definition.is_file() or definition not in parsed:
            continue
        for element in parsed[definition].findall("./v1.6/li"):
            relative = (element.text or "").strip().replace("/", os.sep)
            loadfolder_entries += 1
            if not relative or not (mod / relative).is_dir():
                missing_loadfolders.append(f"{mod.name}: {relative or '<empty>'}")
            conditions: list[str] = []
            for attribute in ("IfModActive", "IfModActiveAll"):
                conditions.extend(
                    item.strip().lower()
                    for item in element.attrib.get(attribute, "").split(",")
                    if item.strip()
                )
            for condition in conditions:
                if condition.startswith(DLC_PREFIX):
                    continue
                if (
                    condition not in dependencies.get(package_id, set())
                    and condition not in load_after.get(package_id, set())
                ):
                    missing_optional_order.append(
                        f"{mod.name}: {condition} ({relative})"
                    )
    failures.extend(
        f"Missing LoadFolder target: {entry}" for entry in missing_loadfolders
    )
    failures.extend(
        f"Conditional package missing dependency/loadAfter: {entry}"
        for entry in missing_optional_order
    )

    graph: dict[str, set[str]] = defaultdict(set)
    internal_ids = set(package_to_mod)
    for package_id in internal_ids:
        for target in dependencies.get(package_id, set()) | load_after.get(
            package_id, set()
        ):
            if target in internal_ids:
                graph[target].add(package_id)
        graph.setdefault(package_id, set())
    cycle = find_cycle(graph)
    if cycle:
        failures.append("Internal load cycle: " + " -> ".join(cycle))

    def_count = 0
    abstract_def_count = 0
    duplicate_defs: list[str] = []
    duplicate_global_defs: list[str] = []
    missing_concrete_names: list[str] = []
    mismatched_def_prefixes: list[str] = []
    owned_def_counts = Counter()
    owner_rulepack_counts = Counter()
    global_seen_defs: dict[tuple[str, str], tuple[str, Path]] = {}
    declared_values: dict[tuple[str, str, str], tuple[str, Path]] = {}
    named_parent_defs: set[str] = set()
    parent_references: list[tuple[str, Path]] = []
    for mod in mod_dirs:
        seen: set[tuple[str, str]] = set()
        for path in xml_files:
            if mod not in path.parents or path not in parsed:
                continue
            root = parsed[path].getroot()
            if root.tag != "Defs":
                continue
            for definition in list(root):
                if not isinstance(definition.tag, str):
                    continue
                if definition.attrib.get("Name"):
                    parent_name = definition.attrib["Name"]
                    named_parent_defs.add(parent_name)
                    expected_prefix = MODULE_PREFIXES.get(mod.name)
                    if (
                        expected_prefix
                        and not parent_name.lower().startswith(
                            expected_prefix.lower()
                        )
                    ):
                        mismatched_def_prefixes.append(
                            f"{mod.name}: {definition.tag}@Name/{parent_name}: "
                            f"{path.relative_to(REPOSITORY)}"
                        )
                if definition.attrib.get("ParentName"):
                    parent_references.append(
                        (definition.attrib["ParentName"], path)
                    )
                name = (definition.findtext("defName") or "").strip()
                is_abstract = (
                    definition.attrib.get("Abstract", "").strip().lower() == "true"
                )
                if is_abstract:
                    abstract_def_count += 1
                if not name:
                    if not is_abstract:
                        missing_concrete_names.append(
                            f"{mod.name}: {path.name}: {definition.tag}"
                        )
                    continue
                def_count += 1
                identity = (definition.tag, name)
                if identity in seen:
                    duplicate_defs.append(f"{mod.name}: {definition.tag}/{name}")
                seen.add(identity)
                if identity in global_seen_defs:
                    other_mod, other_path = global_seen_defs[identity]
                    duplicate_global_defs.append(
                        f"{definition.tag}/{name}: "
                        f"{other_mod}:{other_path.relative_to(REPOSITORY)}, "
                        f"{mod.name}:{path.relative_to(REPOSITORY)}"
                    )
                else:
                    global_seen_defs[identity] = (mod.name, path)
                expected_prefix = MODULE_PREFIXES.get(mod.name)
                if (
                    expected_prefix
                    and not name.lower().startswith(expected_prefix.lower())
                ):
                    mismatched_def_prefixes.append(
                        f"{mod.name}: {definition.tag}/{name}: "
                        f"{path.relative_to(REPOSITORY)}"
                    )
                for leaf_path, value in flatten_def_leaves(definition).items():
                    declared_values[
                        (mod.name, definition.tag, f"{name}.{leaf_path}")
                    ] = (value, path)
                if name.startswith("HHTools_Arktos_"):
                    owned_def_counts[(mod.name, "Arktos")] += 1
                if name.startswith("HHTools_Greenway_"):
                    owned_def_counts[(mod.name, "Greenway")] += 1
                if definition.tag == "RulePackDef":
                    if mod.name == "FIP-Arktos" and name.startswith("Arktos_"):
                        owner_rulepack_counts["FIP-Arktos"] += 1
                    if mod.name == "FIP-Greenway" and name.startswith(
                        "Greenway_"
                    ):
                        owner_rulepack_counts["FIP-Greenway"] += 1
    failures.extend(f"Duplicate concrete Def: {entry}" for entry in duplicate_defs)
    failures.extend(
        f"Duplicate global concrete Def: {entry}"
        for entry in duplicate_global_defs
    )
    failures.extend(
        f"Concrete Def without defName: {entry}" for entry in missing_concrete_names
    )
    failures.extend(
        f"Mismatched owner prefix: {entry}" for entry in mismatched_def_prefixes
    )
    missing_internal_parents = [
        (name, path)
        for name, path in parent_references
        if name not in named_parent_defs
        and name.startswith(FIP_PARENT_PREFIXES)
    ]
    failures.extend(
        "Missing internal ParentName: "
        f"{path.relative_to(REPOSITORY)}: {name}"
        for name, path in missing_internal_parents
    )
    if owned_def_counts[("FIP-H&HTools", "Arktos")]:
        failures.append("Arktos-owned Defs remain in FIP-H&HTools.")
    if owned_def_counts[("FIP-H&HTools", "Greenway")] != 1:
        failures.append(
            "Expected the single shared HHTools_Greenway_IdeoBase schema Def "
            "in FIP-H&HTools."
        )
    if owner_rulepack_counts["FIP-Arktos"] != 143:
        failures.append(
            "Expected 143 Arktos-owned RulePackDefs in FIP-Arktos, found "
            f"{owner_rulepack_counts['FIP-Arktos']}."
        )
    if owner_rulepack_counts["FIP-Greenway"] != 24:
        failures.append(
            "Expected 24 Greenway-owned RulePackDefs in FIP-Greenway, found "
            f"{owner_rulepack_counts['FIP-Greenway']}."
        )

    xpath_count = 0
    xpath_errors: list[str] = []
    animal_compat_anchor_removals: list[str] = []
    translatable_patch_replaces: list[str] = []
    patch_files = 0
    for path, document in parsed.items():
        xpath_elements = document.findall(".//xpath")
        if xpath_elements:
            patch_files += 1
        for element in xpath_elements:
            expression = (element.text or "").strip()
            xpath_count += 1
            try:
                etree.XPath(expression)
            except etree.XPathSyntaxError as error:
                xpath_errors.append(
                    f"{path.relative_to(REPOSITORY)}: {expression}: {error}"
                )
        if document.getroot().tag == "Patch":
            for operation in document.getroot().iter():
                if (
                    path.name.endswith("AnimalRemovalPatch.xml")
                    and operation.attrib.get("Class") == "PatchOperationRemove"
                ):
                    expression = (operation.findtext("xpath") or "").strip()
                    if re.search(r"Defs/(?:ThingDef|PawnKindDef)\[", expression):
                        animal_compat_anchor_removals.append(
                            f"{path.relative_to(REPOSITORY)}: {expression}"
                        )
                if operation.attrib.get("Class") != "PatchOperationReplace":
                    continue
                expression = (operation.findtext("xpath") or "").strip()
                match = PATCH_DEF_RE.search(expression)
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
                children = [
                    child for child in value if isinstance(child.tag, str)
                ]
                if len(children) != 1 or children[0].tag.lower() != leaf:
                    continue
                translatable_patch_replaces.append(
                    f"{path.relative_to(REPOSITORY)}: "
                    f"{match.group('type')}/{match.group('name')}."
                    f"{normalized_path}"
                )
    failures.extend(f"Invalid XPath: {entry}" for entry in xpath_errors)
    failures.extend(
        "Animal removal patch deletes a compatibility Def anchor: " + entry
        for entry in animal_compat_anchor_removals
    )
    failures.extend(
        f"Translatable PatchOperationReplace should be DefInjected: {entry}"
        for entry in translatable_patch_replaces
    )

    language_groups: dict[
        tuple[str, str, str, str], list[str]
    ] = defaultdict(list)
    empty_language_values: list[str] = []
    redundant_english_injections: list[str] = []
    language_entries: dict[
        tuple[str, str, str, str], list[tuple[str, Path]]
    ] = defaultdict(list)
    known_placeholder_values: list[str] = []
    for mod in mod_dirs:
        for path in xml_files:
            if mod not in path.parents or path not in parsed:
                continue
            root = parsed[path].getroot()
            if root.tag != "LanguageData":
                continue
            scope = language_scope(path, mod)
            if not scope:
                continue
            language, namespace = scope
            for element in list(root):
                value = "".join(element.itertext()).strip()
                key = (mod.name, language, namespace, element.tag)
                language_groups[key].append(value)
                language_entries[key].append((value, path))
                if not value:
                    empty_language_values.append(
                        f"{path.relative_to(REPOSITORY)}: {element.tag}"
                    )
                if value.lower() in KNOWN_PLACEHOLDER_VALUES:
                    known_placeholder_values.append(
                        f"{path.relative_to(REPOSITORY)}: "
                        f"{element.tag}={value!r}"
                    )
                if language == "English" and namespace.startswith(
                    "DefInjected/"
                ):
                    def_type = namespace.removeprefix("DefInjected/")
                    declared = declared_values.get(
                        (mod.name, def_type, element.tag)
                    )
                    if declared is not None and value == declared[0]:
                        redundant_english_injections.append(
                            f"{path.relative_to(REPOSITORY)}: "
                            f"{def_type}/{element.tag}"
                        )
    duplicate_language_groups = [
        key for key, values in language_groups.items() if len(values) > 1
    ]
    conflicting_language_groups = [
        key
        for key, values in language_groups.items()
        if key[0].startswith("FIP-Translation")
        and len(values) > 1
        and len(set(values)) > 1
    ]
    failures.extend(
        "Conflicting translation key: " + "/".join(key)
        for key in conflicting_language_groups
    )
    failures.extend(
        "Duplicate DefInjected key: " + "/".join(key)
        for key in duplicate_language_groups
    )
    failures.extend(
        f"Redundant English DefInjected mirror: {entry}"
        for entry in redundant_english_injections
    )
    failures.extend(
        f"Empty translation value: {entry}" for entry in empty_language_values
    )
    failures.extend(
        f"Generated placeholder text: {entry}"
        for entry in known_placeholder_values
    )

    actual_translation_languages: dict[str, set[str]] = defaultdict(set)
    for mod, language, _, _ in language_groups:
        if mod in CURRENT_TRANSLATION_LANGUAGES:
            actual_translation_languages[mod].add(language)
    for mod, expected_languages in CURRENT_TRANSLATION_LANGUAGES.items():
        actual_languages = actual_translation_languages.get(mod, set())
        if actual_languages != expected_languages:
            failures.append(
                f"Unexpected translation languages in {mod}: "
                f"{sorted(actual_languages)} != {sorted(expected_languages)}"
            )

    malformed_placeholder_values: list[str] = []
    for (_, _, _, tag), entries in language_entries.items():
        for value, path in entries:
            if value.count("{") != value.count("}"):
                malformed_placeholder_values.append(
                    f"{path.relative_to(REPOSITORY)}: {tag}"
                )
    failures.extend(
        f"Malformed translation placeholder: {entry}"
        for entry in malformed_placeholder_values
    )

    placeholder_comparisons = 0
    placeholder_errors: list[str] = []
    translation_key_sets: dict[tuple[str, str], set[tuple[str, str]]] = {}
    for mod, languages in CURRENT_TRANSLATION_LANGUAGES.items():
        for language in languages:
            translation_key_sets[(mod, language)] = {
                (namespace, tag)
                for group_mod, group_language, namespace, tag in language_groups
                if group_mod == mod and group_language == language
            }
    translation_key_union = (
        set().union(*translation_key_sets.values())
        if translation_key_sets
        else set()
    )
    translation_key_intersection = (
        set.intersection(*translation_key_sets.values())
        if translation_key_sets
        else set()
    )
    translation_fallback_keys = (
        translation_key_union - translation_key_intersection
    )
    for namespace, tag in sorted(translation_key_union):
        reference: Counter[str] | None = None
        reference_name = ""
        for mod, languages in CURRENT_TRANSLATION_LANGUAGES.items():
            for language in languages:
                values = language_groups.get((mod, language, namespace, tag))
                if not values:
                    continue
                placeholders = Counter(PLACEHOLDER.findall(values[0]))
                if reference is None:
                    reference = placeholders
                    reference_name = language
                    continue
                placeholder_comparisons += 1
                if placeholders != reference:
                    placeholder_errors.append(
                        f"{mod}/{language}/{namespace}/{tag}: "
                        f"{reference_name}={dict(reference)} != "
                        f"{language}={dict(placeholders)}"
                    )
    failures.extend(
        f"Translation placeholder mismatch: {entry}"
        for entry in placeholder_errors
    )

    texture_failures: list[str] = []
    stack_count_texture_failures: list[str] = []
    sound_failures: list[str] = []
    for mod in mod_dirs:
        texture_paths: set[str] = set()
        sound_folders: set[str] = set()
        for directory, _, filenames in os.walk(mod):
            current = Path(directory)
            if current.name == "Textures":
                for texture_dir, _, texture_files in os.walk(current):
                    for filename in texture_files:
                        file = Path(texture_dir) / filename
                        if file.suffix.lower() in IMAGE_SUFFIXES:
                            texture_paths.add(
                                file.relative_to(current)
                                .with_suffix("")
                                .as_posix()
                            )
            if current.name == "Sounds":
                for sound_dir, _, sound_files in os.walk(current):
                    if any(
                        Path(filename).suffix.lower() in AUDIO_SUFFIXES
                        for filename in sound_files
                    ):
                        sound_folders.add(Path(sound_dir).relative_to(current).as_posix())
        if not texture_paths and not sound_folders:
            continue
        for path in xml_files:
            if mod not in path.parents or path not in parsed:
                continue
            for graphic_data in parsed[path].iter("graphicData"):
                graphic_class = graphic_data.findtext("graphicClass", "").strip()
                tex_path = (
                    graphic_data.findtext("texPath", "")
                    .strip()
                    .replace("\\", "/")
                )
                if (
                    graphic_class == "Graphic_StackCount"
                    and tex_path.startswith("FIP-")
                    and not any(
                        candidate.startswith(tex_path + "/")
                        for candidate in texture_paths
                    )
                ):
                    stack_count_texture_failures.append(
                        f"{path.relative_to(REPOSITORY)}: {tex_path}"
                    )
            for element in parsed[path].iter():
                value = (element.text or "").strip().replace("\\", "/")
                if not value:
                    continue
                tag = element.tag.lower() if isinstance(element.tag, str) else ""
                if tag.endswith("texpath") or tag in {
                    "iconpath",
                    "uiiconpath",
                    "texturepath",
                }:
                    if value.startswith("FIP-"):
                        exact = value in texture_paths
                        variant = any(
                            candidate.startswith(value + "_")
                            or candidate.startswith(value + "/")
                            for candidate in texture_paths
                        )
                        if not exact and not variant:
                            texture_failures.append(
                                f"{path.relative_to(REPOSITORY)}: {value}"
                            )
                if tag == "clipfolderpath" and value.startswith("FIP-"):
                    if value not in sound_folders:
                        sound_failures.append(
                            f"{path.relative_to(REPOSITORY)}: {value}"
                        )
    failures.extend(
        f"Missing local texture reference: {entry}" for entry in texture_failures
    )
    failures.extend(
        f"Graphic_StackCount path is not a texture folder: {entry}"
        for entry in stack_count_texture_failures
    )
    failures.extend(
        f"Missing local sound folder reference: {entry}" for entry in sound_failures
    )

    image_files = [
        path for path in all_files if path.suffix.lower() in IMAGE_SUFFIXES
    ]
    owner_prefix_file_errors: list[str] = []
    for mod in mod_dirs:
        expected_prefix = MODULE_PREFIXES.get(mod.name)
        if not expected_prefix:
            continue
        expected_lower = expected_prefix.lower()
        for path in all_files:
            if mod not in path.parents:
                continue
            relative_parts = path.relative_to(mod).parts
            if (
                path.suffix.lower() in IMAGE_SUFFIXES | AUDIO_SUFFIXES
                and "About" not in relative_parts
                and not path.stem.lower().startswith(expected_lower)
            ):
                owner_prefix_file_errors.append(
                    f"{path.relative_to(REPOSITORY)}"
                )
            if (
                path.suffix.lower() == ".xml"
                and path.name not in {"About.xml", "LoadFolders.xml"}
                and not path.stem.lower().startswith(expected_lower)
            ):
                owner_prefix_file_errors.append(
                    f"{path.relative_to(REPOSITORY)}"
                )
    failures.extend(
        f"Runtime file missing owner prefix: {entry}"
        for entry in owner_prefix_file_errors
    )

    image_errors: list[str] = []
    for path in image_files:
        try:
            with Image.open(extended_path(path)) as image:
                image.verify()
            with Image.open(extended_path(path)) as image:
                image.load()
        except Exception as error:  # Pillow has several format-specific errors.
            image_errors.append(f"{path.relative_to(REPOSITORY)}: {error}")
    failures.extend(f"Unreadable image: {entry}" for entry in image_errors)

    runtime_assets = [
        path
        for path in all_files
        if path.suffix.lower() in IMAGE_SUFFIXES | AUDIO_SUFFIXES
    ]
    asset_hashes: dict[str, list[Path]] = defaultdict(list)
    for path in runtime_assets:
        digest = hashlib.sha256()
        with open(extended_path(path), "rb") as stream:
            for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                digest.update(chunk)
        asset_hashes[digest.hexdigest()].append(path)
    approved_same_mod_asset_duplicate_groups: list[str] = []
    unexpected_same_mod_asset_duplicate_groups: list[str] = []
    cross_mod_asset_duplicate_groups = 0
    for paths in asset_hashes.values():
        by_mod: dict[str, list[Path]] = defaultdict(list)
        for path in paths:
            relative_parts = path.relative_to(NEW_MODS).parts
            by_mod[relative_parts[0]].append(path)
        if len(by_mod) > 1:
            cross_mod_asset_duplicate_groups += 1
        for mod_name, duplicates in by_mod.items():
            if len(duplicates) <= 1:
                continue
            relative_duplicates = frozenset(
                path.relative_to(NEW_MODS).as_posix() for path in duplicates
            )
            reason = APPROVED_SAME_MOD_DUPLICATE_ASSET_GROUPS.get(
                relative_duplicates
            )
            entry = (
                f"{mod_name}: {', '.join(sorted(relative_duplicates))}"
            )
            if reason is None:
                unexpected_same_mod_asset_duplicate_groups.append(entry)
            else:
                approved_same_mod_asset_duplicate_groups.append(
                    f"{entry} ({reason})"
                )
    failures.extend(
        f"Unexpected byte-identical same-mod asset copies: {entry}"
        for entry in unexpected_same_mod_asset_duplicate_groups
    )

    empty_files = [path for path in all_files if file_size(path) == 0]
    failures.extend(
        f"Empty file: {path.relative_to(REPOSITORY)}" for path in empty_files
    )
    empty_directories = [
        Path(directory)
        for directory, directories, filenames in os.walk(NEW_MODS)
        if not directories and not filenames
    ]
    failures.extend(
        f"Empty directory: {path.relative_to(REPOSITORY)}"
        for path in empty_directories
    )

    big_mt_hits: list[str] = []
    for path in all_files:
        relative = path.relative_to(NEW_MODS).as_posix()
        if "BigMT" in relative or "Big-MT" in relative:
            big_mt_hits.append(relative)
            continue
        if path.suffix.lower() in TEXT_SUFFIXES:
            text = read_text(path)
            if "FIP.BigMT" in text or "BigMT_" in text:
                big_mt_hits.append(relative)
    failures.extend(f"Big MT remains active: {entry}" for entry in big_mt_hits)

    dll_files = [
        path for path in all_files if path.suffix.lower() == ".dll"
    ]
    private_harmony = [
        path for path in dll_files if path.name.lower() == "0harmony.dll"
    ]
    failures.extend(
        f"Private Harmony runtime remains: {path.relative_to(REPOSITORY)}"
        for path in private_harmony
    )
    debug_binaries = [
        path for path in all_files if path.suffix.lower() in {".pdb", ".mdb"}
    ]
    failures.extend(
        f"Debug symbol remains in runtime mod: {path.relative_to(REPOSITORY)}"
        for path in debug_binaries
    )
    projects = sorted(SOURCE.rglob("*.csproj"))
    source_files = sorted(SOURCE.rglob("*.cs"))
    source_types: set[str] = set()
    for path in source_files:
        text = read_text(path)
        namespace = re.search(
            r"\bnamespace\s+([A-Za-z_][A-Za-z0-9_.]*)", text
        )
        if not namespace:
            continue
        for _, name in re.findall(
            r"\b(class|struct|enum)\s+([A-Za-z_][A-Za-z0-9_]*)", text
        ):
            source_types.add(f"{namespace.group(1)}.{name}")
    xml_type_references: list[tuple[str, Path]] = []
    type_pattern = re.compile(r"FIP\.[A-Za-z_][A-Za-z0-9_.]+")
    for path, document in parsed.items():
        for element in document.iter():
            if not isinstance(element.tag, str):
                continue
            for attribute, value in element.attrib.items():
                if attribute.lower() == "class" and value.startswith("FIP."):
                    xml_type_references.append((value, path))
            if element.tag.lower().endswith("class"):
                value = (element.text or "").strip()
                if value.startswith("FIP."):
                    xml_type_references.append((value, path))
            if element.tag == "xpath":
                for value in type_pattern.findall(element.text or ""):
                    xml_type_references.append((value, path))
    missing_xml_types = [
        (name, path)
        for name, path in xml_type_references
        if name not in source_types
    ]
    failures.extend(
        f"Missing FIP C# type: {path.relative_to(REPOSITORY)}: {name}"
        for name, path in missing_xml_types
    )
    generated_source_dirs = [
        path
        for name in ("bin", "obj")
        for path in SOURCE.rglob(name)
        if path.is_dir()
    ]
    failures.extend(
        f"Generated source directory remains: {path.relative_to(REPOSITORY)}"
        for path in generated_source_dirs
    )

    obsolete_tokens = {
        "maxCountAtGameStart",
        "canMakeRandomly",
        "Log.Message",
    }
    source_and_mod_text = [
        path
        for base in (NEW_MODS, SOURCE)
        for path in base.rglob("*")
        if path.is_file() and path.suffix.lower() in {".xml", ".cs"}
    ]
    for token in obsolete_tokens:
        hits = [
            path
            for path in source_and_mod_text
            if token in read_text(path)
        ]
        failures.extend(
            f"Obsolete token {token}: {path.relative_to(REPOSITORY)}"
            for path in hits
        )

    playable_mods = [
        package
        for package in package_to_mod
        if not package.startswith("fip.translation.")
    ]
    translation_mods = [
        package
        for package in package_to_mod
        if package.startswith("fip.translation.")
    ]
    audio_files = [
        path for path in all_files if path.suffix.lower() in AUDIO_SUFFIXES
    ]
    total_bytes = sum(file_size(path) for path in all_files)

    print(f"modules={len(mod_dirs)} (playable={len(playable_mods)}, translations={len(translation_mods)})")
    print(
        f"files={len(all_files)}, bytes={total_bytes}, "
        f"ce_compatibility_hits={len(ce_compatibility_hits)}"
    )
    print(
        f"xml={len(xml_files)}, patch_files={patch_files}, "
        f"xpath={xpath_count}, parse_errors={len(xml_files) - len(parsed)}"
    )
    print(
        f"defs={def_count} (abstract={abstract_def_count}), "
        f"duplicate_defs={len(duplicate_defs)}, "
        f"global_duplicate_defs={len(duplicate_global_defs)}, "
        f"prefix_errors={len(mismatched_def_prefixes)}"
    )
    print(
        f"loadfolder_entries={loadfolder_entries}, "
        f"missing_targets={len(missing_loadfolders)}, "
        f"missing_optional_order={len(missing_optional_order)}, "
        f"internal_cycle={'yes' if cycle else 'no'}"
    )
    print(
        f"translation_conflicts={len(conflicting_language_groups)}, "
        f"duplicate_language_keys={len(duplicate_language_groups)}, "
        f"redundant_english_injections={len(redundant_english_injections)}, "
        f"empty_values={len(empty_language_values)}, "
        f"generated_placeholders={len(known_placeholder_values)}, "
        f"fallback_keys={len(translation_fallback_keys)}, "
        f"placeholder_comparisons={placeholder_comparisons}, "
        f"placeholder_errors={len(placeholder_errors)}, "
        f"malformed_placeholders={len(malformed_placeholder_values)}"
    )
    print(
        f"images={len(image_files)}, image_errors={len(image_errors)}, "
        f"audio={len(audio_files)}, sound_reference_errors={len(sound_failures)}"
    )
    print(
        f"owner_prefix_file_errors={len(owner_prefix_file_errors)}, "
        "approved_same_mod_asset_duplicate_groups="
        f"{len(approved_same_mod_asset_duplicate_groups)}, "
        "unexpected_same_mod_asset_duplicate_groups="
        f"{len(unexpected_same_mod_asset_duplicate_groups)}, "
        f"cross_mod_asset_duplicate_groups={cross_mod_asset_duplicate_groups}, "
        f"translatable_patch_replaces={len(translatable_patch_replaces)}"
    )
    print(
        f"dll={len(dll_files)}, private_harmony={len(private_harmony)}, "
        f"debug_symbols={len(debug_binaries)}, "
        f"projects={len(projects)}, source_types={len(source_types)}, "
        f"xml_type_refs={len(set(name for name, _ in xml_type_references))}, "
        f"missing_xml_types={len(missing_xml_types)}, "
        f"generated_source_dirs={len(generated_source_dirs)}"
    )
    print(
        f"empty_files={len(empty_files)}, empty_directories={len(empty_directories)}, "
        f"big_mt_hits={len(big_mt_hits)}"
    )

    if failures:
        print(f"FAILED ({len(failures)} findings)")
        for failure in failures:
            print(f"- {failure}")
        return 1
    print("PASS: all static refactor checks completed without findings.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
