#!/usr/bin/env python3
"""Remove English DefInjected entries that exactly mirror local Def values.

Run without arguments for a dry run. Pass --apply to update the files. The
rewrite is deliberately text based so comments, declarations, indentation, and
line endings outside the removed elements remain byte-for-byte unchanged.
"""

from __future__ import annotations

from collections import Counter, defaultdict
import os
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

from audit_content_identity import (
    MODULE_PREFIXES,
    NEW_MODS,
    extended_path,
    flatten_def_leaves,
    language_scope,
    parse_xml,
    relative,
    text_value,
)


def read_bytes(path: Path) -> bytes:
    with open(extended_path(path), "rb") as stream:
        return stream.read()


def write_bytes(path: Path, data: bytes) -> None:
    with open(extended_path(path), "wb") as stream:
        stream.write(data)


def remove_matching_element(
    source: str,
    tag: str,
    expected_value: str,
) -> tuple[str, int]:
    escaped_tag = re.escape(tag)
    pattern = re.compile(
        rf"(?ms)^[ \t]*"
        rf"(<{escaped_tag}(?:\s[^>]*)?>.*?</{escaped_tag}>)"
        rf"[ \t]*(?:\r?\n|$)"
    )
    removed = 0

    def replace(match: re.Match[str]) -> str:
        nonlocal removed
        try:
            element = ET.fromstring(match.group(1))
        except ET.ParseError:
            return match.group(0)
        if text_value(element) != expected_value:
            return match.group(0)
        removed += 1
        return ""

    return pattern.sub(replace, source), removed


def main() -> int:
    apply_changes = "--apply" in sys.argv[1:]
    unknown = [argument for argument in sys.argv[1:] if argument != "--apply"]
    if unknown:
        print(f"Unknown arguments: {', '.join(unknown)}", file=sys.stderr)
        return 2

    playable_mods = [
        NEW_MODS / name
        for name in MODULE_PREFIXES
        if (NEW_MODS / name).is_dir()
    ]
    parsed: dict[Path, ET.Element] = {}
    for path in sorted(NEW_MODS.rglob("*.xml")):
        parsed[path] = parse_xml(path)

    declared_values: dict[tuple[str, str, str], str] = {}
    for mod in playable_mods:
        for path, root in parsed.items():
            if mod not in path.parents or root.tag != "Defs":
                continue
            for definition in root:
                if not isinstance(definition.tag, str):
                    continue
                def_name = (definition.findtext("defName") or "").strip()
                if not def_name:
                    continue
                for leaf_path, value in flatten_def_leaves(definition).items():
                    declared_values[
                        (mod.name, definition.tag, f"{def_name}.{leaf_path}")
                    ] = value

    removals_by_file: dict[Path, list[tuple[str, str]]] = defaultdict(list)
    for mod in playable_mods:
        for path, root in parsed.items():
            if mod not in path.parents or root.tag != "LanguageData":
                continue
            scope = language_scope(path, mod)
            if scope is None or scope[0] != "English":
                continue
            def_type = scope[1]
            for element in root:
                if not isinstance(element.tag, str):
                    continue
                declared = declared_values.get(
                    (mod.name, def_type, element.tag)
                )
                if declared is not None and text_value(element) == declared:
                    removals_by_file[path].append((element.tag, declared))

    changed_files: list[Path] = []
    deleted_files: list[Path] = []
    removed_entries = 0
    removed_by_mod: Counter[str] = Counter()
    rendered: dict[Path, bytes | None] = {}

    for path, entries in sorted(removals_by_file.items()):
        original = read_bytes(path)
        has_bom = original.startswith(b"\xef\xbb\xbf")
        source = original.decode("utf-8-sig")
        updated = source
        file_removed = 0
        for tag, expected_value in entries:
            updated, count = remove_matching_element(
                updated,
                tag,
                expected_value,
            )
            if count != 1:
                raise RuntimeError(
                    f"Expected one removable {tag} in {relative(path)}, "
                    f"found {count}."
                )
            file_removed += count

        root = ET.fromstring(updated)
        remaining_elements = [
            child for child in root if isinstance(child.tag, str)
        ]
        if remaining_elements:
            encoded = updated.encode("utf-8")
            if has_bom:
                encoded = b"\xef\xbb\xbf" + encoded
            rendered[path] = encoded
            changed_files.append(path)
        else:
            rendered[path] = None
            deleted_files.append(path)

        removed_entries += file_removed
        mod_name = path.relative_to(NEW_MODS).parts[0]
        removed_by_mod[mod_name] += file_removed

    print(
        f"mode={'apply' if apply_changes else 'dry-run'} "
        f"entries={removed_entries} files={len(removals_by_file)} "
        f"changed={len(changed_files)} deleted={len(deleted_files)}"
    )
    for mod_name, count in sorted(removed_by_mod.items()):
        print(f"- {mod_name}: {count}")
    for path in deleted_files:
        print(f"- delete empty {relative(path)}")

    if not apply_changes:
        return 0

    for path, data in rendered.items():
        if data is None:
            os.unlink(extended_path(path))
        else:
            write_bytes(path, data)

    language_roots = [
        path
        for mod in playable_mods
        for path in mod.rglob("Languages")
        if path.is_dir()
    ]
    for language_root in language_roots:
        directories = sorted(
            (path for path in language_root.rglob("*") if path.is_dir()),
            key=lambda item: len(item.parts),
            reverse=True,
        )
        for directory in directories:
            try:
                directory.rmdir()
            except OSError:
                pass

    return 0


if __name__ == "__main__":
    sys.exit(main())
