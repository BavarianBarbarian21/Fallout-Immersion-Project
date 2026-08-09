#!/usr/bin/env python3
"""Prefix runtime asset filenames with the owning FIP module name.

Run without arguments for a dry run. Pass --apply to rewrite logical asset
references and rename the files. Directional and underscore-suffixed variant
families are updated through their shared RimWorld texture path.
"""

from __future__ import annotations

from collections import Counter, defaultdict
import os
from pathlib import Path
import re
import sys

from audit_content_identity import (
    ASSET_SUFFIXES,
    MODULE_PREFIXES,
    NEW_MODS,
    REPOSITORY,
    extended_path,
    relative,
)


SOURCE = REPOSITORY / "Development" / "Source"
TEXT_SUFFIXES = {".xml", ".cs", ".json", ".txt", ".md"}
DIRECTION_SUFFIX = re.compile(
    r"_(?:north|south|east|west)(?:m)?$",
    re.IGNORECASE,
)
VARIANT_SUFFIX = re.compile(r"_[a-z0-9]+$", re.IGNORECASE)


def read_bytes(path: Path) -> bytes:
    with open(extended_path(path), "rb") as stream:
        return stream.read()


def write_bytes(path: Path, data: bytes) -> None:
    with open(extended_path(path), "wb") as stream:
        stream.write(data)


def asset_root(path: Path, mod: Path) -> tuple[Path, str] | None:
    relative_parts = path.relative_to(mod).parts
    for marker in ("Textures", "Sounds"):
        if marker in relative_parts:
            index = relative_parts.index(marker)
            root = mod.joinpath(*relative_parts[: index + 1])
            return root, marker
    return None


def logical_path(path: Path, root: Path) -> str:
    return path.relative_to(root).with_suffix("").as_posix()


def main() -> int:
    apply_changes = "--apply" in sys.argv[1:]
    unknown = [argument for argument in sys.argv[1:] if argument != "--apply"]
    if unknown:
        print(f"Unknown arguments: {', '.join(unknown)}", file=sys.stderr)
        return 2

    renames: dict[Path, Path] = {}
    logical_replacements_by_mod: dict[str, dict[str, str]] = defaultdict(dict)
    counts_by_mod: Counter[str] = Counter()

    for mod_name, prefix in MODULE_PREFIXES.items():
        mod = NEW_MODS / mod_name
        prefix_lower = prefix.lower()
        candidates: list[tuple[Path, Path, Path, str]] = []
        family_members: dict[tuple[Path, str], list[Path]] = defaultdict(list)

        for path in sorted(mod.rglob("*")):
            if (
                not path.is_file()
                or path.suffix.lower() not in ASSET_SUFFIXES
                or "About" in path.relative_to(mod).parts
                or path.stem.lower().startswith(prefix_lower)
            ):
                continue
            root_info = asset_root(path, mod)
            if root_info is None:
                continue
            root, marker = root_info
            target = path.with_name(prefix + path.name)
            if target.exists() and target != path:
                raise RuntimeError(
                    f"Asset rename target exists: {relative(target)}"
                )
            renames[path] = target
            candidates.append((path, target, root, marker))
            counts_by_mod[mod_name] += 1

            stem = path.stem
            family = DIRECTION_SUFFIX.sub("", stem)
            if family == stem:
                variant = VARIANT_SUFFIX.sub("", stem)
                if variant != stem:
                    family = variant
            family_members[(path.parent, family)].append(path)

        replacements = logical_replacements_by_mod[mod_name]
        for old_path, new_path, root, marker in candidates:
            old_logical = logical_path(old_path, root)
            new_logical = logical_path(new_path, root)
            replacements[old_logical] = new_logical
            replacements[old_logical + old_path.suffix] = (
                new_logical + new_path.suffix
            )

            stem = old_path.stem
            direction_family = DIRECTION_SUFFIX.sub("", stem)
            if direction_family != stem:
                old_family = (
                    old_path.parent.relative_to(root) / direction_family
                ).as_posix()
                new_family = (
                    old_path.parent.relative_to(root)
                    / (prefix + direction_family)
                ).as_posix()
                replacements[old_family] = new_family
                continue

            variant_family = VARIANT_SUFFIX.sub("", stem)
            if (
                variant_family != stem
                and len(family_members[(old_path.parent, variant_family)]) > 1
            ):
                old_family = (
                    old_path.parent.relative_to(root) / variant_family
                ).as_posix()
                new_family = (
                    old_path.parent.relative_to(root)
                    / (prefix + variant_family)
                ).as_posix()
                replacements[old_family] = new_family

    rewritten: dict[Path, bytes] = {}
    rewritten_by_mod: Counter[str] = Counter()
    for mod_name, replacements in logical_replacements_by_mod.items():
        roots = [NEW_MODS / mod_name]
        source_mod = SOURCE / mod_name
        if source_mod.is_dir():
            roots.append(source_mod)
        text_files = sorted(
            path
            for root in roots
            for path in root.rglob("*")
            if path.is_file() and path.suffix.lower() in TEXT_SUFFIXES
        )
        ordered = sorted(
            replacements.items(),
            key=lambda item: len(item[0]),
            reverse=True,
        )
        for path in text_files:
            original = read_bytes(path)
            has_bom = original.startswith(b"\xef\xbb\xbf")
            try:
                source = original.decode("utf-8-sig")
            except UnicodeDecodeError:
                continue
            updated = source
            for old_value, new_value in ordered:
                updated = updated.replace(old_value, new_value)
                updated = updated.replace(
                    old_value.replace("/", "\\"),
                    new_value.replace("/", "\\"),
                )
            if updated == source:
                continue
            encoded = updated.encode("utf-8")
            if has_bom:
                encoded = b"\xef\xbb\xbf" + encoded
            rewritten[path] = encoded
            rewritten_by_mod[mod_name] += 1

    workspace = REPOSITORY.resolve()
    for old_path, new_path in renames.items():
        if workspace not in old_path.resolve().parents:
            raise RuntimeError(f"Unsafe asset rename source: {old_path}")
        if workspace not in new_path.resolve().parents:
            raise RuntimeError(f"Unsafe asset rename target: {new_path}")

    print(
        f"mode={'apply' if apply_changes else 'dry-run'} "
        f"assets={len(renames)} rewritten_files={len(rewritten)}"
    )
    for mod_name in sorted(counts_by_mod):
        print(
            f"- {mod_name}: assets={counts_by_mod[mod_name]}, "
            f"references={rewritten_by_mod[mod_name]} files"
        )

    if not apply_changes:
        return 0

    for path, data in rewritten.items():
        write_bytes(path, data)
    for old_path, new_path in renames.items():
        os.replace(extended_path(old_path), extended_path(new_path))

    return 0


if __name__ == "__main__":
    sys.exit(main())
