#!/usr/bin/env python3
"""
FIP Translation Next

Builds four language-specific RimWorld translation mods from the current FIP
workspace, installed FCP repositories, compatibility targets, and the user's
mod list. Translation state is committed incrementally to SQLite so a long run
can be resumed safely.
"""

from __future__ import annotations

import argparse
import concurrent.futures
import datetime as dt
import hashlib
import html
import json
import os
import re
import shutil
import sqlite3
import sys
import threading
import time
import traceback
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET
from collections import OrderedDict, defaultdict
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Iterator


TOOL_ROOT = Path(__file__).resolve().parent
CONFIG_PATH = TOOL_ROOT / "config.json"
GLOSSARY_PATH = TOOL_ROOT / "glossary.json"
STATE_ROOT = TOOL_ROOT / "state"
LOG_ROOT = TOOL_ROOT / "logs"
DB_PATH = STATE_ROOT / "translation.sqlite3"
PROGRESS_PATH = LOG_ROOT / "progress.txt"
MANIFEST_PATH = LOG_ROOT / "source-manifest.json"
EXTRACTION_REPORT_PATH = LOG_ROOT / "extraction-report.txt"
VALIDATION_REPORT_PATH = LOG_ROOT / "validation-report.txt"
VENDOR_ROOT = TOOL_ROOT / "vendor"
MODEL_ROOT = TOOL_ROOT / "models"

TARGET_LANGUAGES = (
    "Russian",
    "ChineseSimplified",
    "ChineseTraditional",
    "Japanese",
    "Korean",
)

TRANSLATABLE_LEAF_NAMES = {
    name.casefold()
    for name in (
        "label",
        "labelShort",
        "labelMale",
        "labelFemale",
        "labelMalePlural",
        "labelFemalePlural",
        "labelPlural",
        "description",
        "descriptionShort",
        "baseDesc",
        "title",
        "titleShort",
        "titleFemale",
        "titleShortFemale",
        "leaderTitle",
        "leaderTitleFemale",
        "jobString",
        "reportString",
        "inspectString",
        "baseInspectLine",
        "verb",
        "chargeNoun",
        "letterLabel",
        "letterText",
        "letterDesc",
        "beginLetterLabel",
        "beginLetterText",
        "arrivedLetterLabel",
        "arrivedLetterText",
        "pawnsArrivalMessage",
        "joinText",
        "successMessage",
        "successText",
        "failMessage",
        "failText",
        "rejectInputMessage",
        "customLetterLabel",
        "customLetterText",
        "gerundLabel",
        "customLabel",
        "pawnLabel",
        "pawnSingular",
        "pawnPlural",
        "skillLabel",
        "skillDescription",
        "headerTip",
        "tip",
        "helpText",
        "summary",
        "text",
        "note",
        "nameNoun",
        "nameSuffix",
        "namePrefix",
        "failTriggerText",
        "pawnCannotEquipReason",
        "descriptionExtra",
        "labelNounPretty",
        "customSummary",
        "extraTooltip",
        "header",
        "baseTitle",
        "baseTitleFemale",
        "pawnsPlural",
        "leaderPawnSingular",
        "name",
        "message",
        "tooltip",
        "explanation",
        "prompt",
        "reason",
        "confirmationText",
        "invalidReason",
        "floatMenuLabel",
        "commandLabel",
        "commandDescription",
    )
}
STRING_LIST_PARENTS = {"rulesstrings"}
VERSION_DIR_RE = re.compile(r"^\d+\.\d+(?:\.\d+)?$")
VALID_XML_NAME_RE = re.compile(r"^[A-Za-z_][A-Za-z0-9_.-]*$")
BARE_AMP_RE = re.compile(
    r"&(?!amp;|lt;|gt;|quot;|apos;|#\d+;|#x[0-9A-Fa-f]+;|\w+;)"
)
XPATH_DEF_RE = re.compile(
    r"(?:^|/)Defs/(?P<def_type>[A-Za-z0-9_.]+)\[(?P<condition>[^\]]+)\]"
)
XPATH_DEFNAME_RE = re.compile(r"""defName\s*=\s*["']([^"']+)["']""")
PLACEHOLDER_RE = re.compile(
    r"""
    \[[^\]\r\n]+\]
    |\{[^{}\r\n]+\}
    |<[^<>\r\n]+>
    |\\[nrt]
    |%\d*\$?[sdif]
    """,
    re.VERBOSE,
)
ASS_STYLE_RE = re.compile(r"\{\\[^{}\r\n]*\}")
TOKEN_RE = re.compile(
    r"https?\s*:\s*/\s*/\s*qzx\s*\.\s*invalid\s*/\s*"
    r"(?P<kind>ph|term)\s*/\s*0*(?P<index>\d+)",
    re.IGNORECASE,
)

_OFFLINE_INIT_LOCK = threading.Lock()
_OFFLINE_TRANSLATIONS: dict[str, object] = {}
_OFFLINE_OPENCC = None
_OFFLINE_POST_VARIANTS: set[str] = set()
MOJIBAKE_MARKERS = ("Ã", "Â", "â€", "Ð", "Ñ", "å", "æ", "ç", "�")


@dataclass(frozen=True)
class SourceMod:
    root: Path
    folder_name: str
    package_id: str
    display_name: str
    category: str
    precedence: int


def now_text() -> str:
    return dt.datetime.now().astimezone().strftime("%Y-%m-%d %H:%M:%S %z")


def log(message: str) -> None:
    print(f"[{dt.datetime.now().strftime('%H:%M:%S')}] {message}", flush=True)


def read_json(path: Path) -> dict:
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def resolve_path(value: str | None, base: Path = TOOL_ROOT) -> Path | None:
    if not value:
        return None
    path = Path(value)
    if not path.is_absolute():
        path = base / path
    return path.resolve()


def ensure_roots() -> None:
    STATE_ROOT.mkdir(parents=True, exist_ok=True)
    LOG_ROOT.mkdir(parents=True, exist_ok=True)


def write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8", newline="\n")


def local_name(tag: str) -> str:
    return tag.rsplit("}", 1)[-1]


def safe_stem(value: str) -> str:
    value = re.sub(r"[<>:\"/\\|?*\x00-\x1f]", "_", value)
    value = re.sub(r"\s+", "_", value.strip())
    return value[:120] or "Mod"


def read_about(folder: Path) -> tuple[str, str] | None:
    candidates = (
        folder / "About" / "About.xml",
        folder / "about" / "About.xml",
        folder / "About.xml",
    )
    for path in candidates:
        if not path.is_file():
            continue
        try:
            root = ET.parse(path).getroot()
            package = root.findtext("packageId") or root.findtext("packageIdPlayerFacing")
            name = root.findtext("name") or folder.name
            if package:
                return package.strip(), name.strip()
        except Exception:
            continue
    return None


def build_mod_index(roots: Iterable[Path]) -> tuple[dict[str, Path], dict[str, Path]]:
    by_package: dict[str, Path] = {}
    by_folder: dict[str, Path] = {}
    for library in roots:
        if not library or not library.is_dir():
            continue
        try:
            children = sorted((p for p in library.iterdir() if p.is_dir()), key=lambda p: p.name)
        except OSError:
            continue
        for child in children:
            metadata = read_about(child)
            if not metadata:
                continue
            package, _ = metadata
            by_package.setdefault(package.casefold(), child.resolve())
            by_folder.setdefault(child.name.casefold(), child.resolve())
    return by_package, by_folder


def read_nonempty_lines(path: Path) -> list[str]:
    if not path or not path.is_file():
        return []
    return [
        line.strip()
        for line in path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    ]


def read_active_package_ids(path: Path | None) -> tuple[list[str], str | None]:
    if not path:
        return [], f"ModsConfig not found: {path}"
    try:
        if not path.is_file():
            return [], f"ModsConfig not found: {path}"
        root = ET.parse(path).getroot()
        values = [
            (node.text or "").strip()
            for node in root.findall(".//activeMods/li")
            if (node.text or "").strip()
        ]
        return values, None
    except Exception as exc:
        return [], f"Unable to read ModsConfig {path}: {exc}"


def discover_sources(config: dict) -> tuple[list[SourceMod], dict]:
    workspace = resolve_path(config["workspaceRoot"])
    local_mods = resolve_path(config.get("localModsRoot"))
    workshop = resolve_path(config.get("workshopRoot"))
    fcp_cache = resolve_path(config.get("fcpCacheRoot"))
    mods_config = resolve_path(config.get("modsConfigPath"))
    assert workspace is not None

    index_roots = [p for p in (local_mods, workshop, fcp_cache) if p]
    by_package, by_folder = build_mod_index(index_roots)
    sources: dict[str, SourceMod] = {}
    missing: list[dict] = []

    def add_folder(folder: Path, category: str, precedence: int, reason: str) -> None:
        metadata = read_about(folder)
        if not metadata:
            missing.append({"entry": str(folder), "reason": reason, "problem": "missing About metadata"})
            return
        package, name = metadata
        package_key = package.casefold()
        candidate = SourceMod(
            root=folder.resolve(),
            folder_name=folder.name,
            package_id=package,
            display_name=name,
            category=category,
            precedence=precedence,
        )
        previous = sources.get(package_key)
        if previous is None or candidate.precedence >= previous.precedence:
            sources[package_key] = candidate

    # Every real FIP mod in the workspace. Translation outputs and support folders
    # do not qualify because they lack normal mod metadata or are explicitly skipped.
    for child in sorted(workspace.glob("FIP-*"), key=lambda p: p.name.casefold()):
        if not child.is_dir():
            continue
        if child.name.casefold() in {"fip-translation", "fip-translation-next"}:
            continue
        if read_about(child):
            add_folder(child, "FIP", 400, "workspace FIP discovery")

    # Every installed/cached FCP repository, with installed copies preferred.
    for root, precedence in ((fcp_cache, 280), (local_mods, 300)):
        if not root or not root.is_dir():
            continue
        for child in sorted(root.iterdir(), key=lambda p: p.name.casefold()):
            if child.is_dir() and (
                child.name.casefold().startswith("fcp-")
                or child.name.casefold() == "radio-talk-show"
            ):
                if read_about(child):
                    add_folder(child, "FCP", precedence, "FCP discovery")

    requested_other: OrderedDict[str, str] = OrderedDict()
    for list_value in config.get("legacySourceLists", []):
        list_path = resolve_path(list_value)
        if list_path:
            for entry in read_nonempty_lines(list_path):
                requested_other.setdefault(entry.casefold(), entry)

    active_ids, active_error = read_active_package_ids(mods_config)
    for package in active_ids:
        requested_other.setdefault(package.casefold(), package)

    excluded_prefixes = tuple(
        str(prefix).casefold() for prefix in config.get("excludePackagePrefixes", [])
    )
    active_missing: list[str] = []
    for entry in requested_other.values():
        entry_key = entry.casefold()
        if entry_key.startswith(excluded_prefixes):
            continue
        if entry_key.startswith("fip.") or entry_key.startswith("rick.fcp."):
            continue
        folder: Path | None = None
        if entry.isdigit() and workshop:
            candidate = workshop / entry
            if candidate.is_dir():
                folder = candidate
        if folder is None:
            folder = by_package.get(entry_key) or by_folder.get(entry_key)
        if folder is None:
            record = {
                "entry": entry,
                "reason": "active/configured compatibility or playset source",
                "problem": "not installed/resolvable",
            }
            missing.append(record)
            if entry_key in {item.casefold() for item in active_ids}:
                active_missing.append(entry)
            continue
        metadata = read_about(folder)
        if metadata and (
            metadata[0].casefold().startswith("fip.")
            or metadata[0].casefold().startswith("rick.fcp.")
        ):
            continue
        add_folder(folder, "Other", 200, "configured source")

    ordered = sorted(
        sources.values(),
        key=lambda source: (
            source.precedence,
            source.category.casefold(),
            source.folder_name.casefold(),
        ),
    )
    manifest = {
        "generatedAt": now_text(),
        "workspace": str(workspace),
        "sourceCount": len(ordered),
        "countsByCategory": {
            category: sum(1 for source in ordered if source.category == category)
            for category in ("Other", "FCP", "FIP")
        },
        "sources": [
            {
                "category": source.category,
                "folder": source.folder_name,
                "name": source.display_name,
                "packageId": source.package_id,
                "root": str(source.root),
                "precedence": source.precedence,
            }
            for source in ordered
        ],
        "missingConfiguredSources": missing,
        "activeMissingSources": active_missing,
        "modsConfigWarning": active_error,
    }
    write_text(MANIFEST_PATH, json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
    return ordered, manifest


def content_roots(source: SourceMod) -> list[Path]:
    root = source.root
    roots: list[Path] = []
    version_root = root / "1.6"
    if version_root.is_dir():
        roots.append(version_root)
        for common_name in ("Defs", "Languages", "Patches"):
            common = root / common_name
            if common.is_dir():
                roots.append(common)
    else:
        roots.append(root)
    return roots


def iter_unique_files(source: SourceMod, suffix: str) -> Iterator[Path]:
    seen: set[Path] = set()
    for root in content_roots(source):
        try:
            files = root.rglob(f"*{suffix}")
        except OSError:
            continue
        for path in files:
            if not path.is_file():
                continue
            resolved = path.resolve()
            if resolved in seen:
                continue
            seen.add(resolved)
            yield path


def is_in_named_directory(path: Path, name: str) -> bool:
    return any(part.casefold() == name.casefold() for part in path.parts)


def english_language_relative(path: Path) -> Path | None:
    parts = list(path.parts)
    for index in range(len(parts) - 1):
        if parts[index].casefold() == "languages" and parts[index + 1].casefold() == "english":
            return Path(*parts[index + 2 :])
    return None


def parse_xml_recover(path: Path) -> ET.Element | None:
    try:
        raw = path.read_text(encoding="utf-8-sig", errors="replace")
    except OSError:
        return None
    raw = BARE_AMP_RE.sub("&amp;", raw)
    try:
        return ET.fromstring(raw)
    except ET.ParseError:
        without_header = re.sub(r"<\?xml[^>]*\?>", "", raw, flags=re.IGNORECASE)
        try:
            return ET.fromstring(f"<SyntheticRoot>{without_header}</SyntheticRoot>")
        except ET.ParseError:
            return None


def element_text(element: ET.Element) -> str:
    return "".join(element.itertext()).strip()


def walk_translatable(
    element: ET.Element,
    prefix: str,
    string_list_parent: bool = False,
) -> Iterator[tuple[str, str]]:
    li_index = 0
    for child in list(element):
        child_name = local_name(child.tag)
        is_li = child_name == "li"
        if is_li:
            child_path = f"{prefix}.{li_index}"
            li_index += 1
        else:
            child_path = f"{prefix}.{child_name}" if prefix else child_name
        child_elements = [item for item in list(child) if isinstance(item.tag, str)]
        if string_list_parent and is_li:
            text = element_text(child)
            if text:
                yield child_path, text
            continue
        if not child_elements:
            if child_name.casefold() in TRANSLATABLE_LEAF_NAMES:
                text = element_text(child)
                if text:
                    yield child_path, text
            continue
        yield from walk_translatable(
            child,
            child_path,
            child_name.casefold() in STRING_LIST_PARENTS,
        )


def extract_defs(path: Path) -> Iterator[tuple[str, str, str]]:
    root = parse_xml_recover(path)
    if root is None:
        return
    roots = [root] if local_name(root.tag) == "Defs" else [
        node for node in root.iter() if local_name(node.tag) == "Defs"
    ]
    for defs_root in roots:
        for definition in list(defs_root):
            if not isinstance(definition.tag, str):
                continue
            if definition.attrib.get("Abstract", "").casefold() == "true":
                continue
            def_name_node = next(
                (child for child in list(definition) if local_name(child.tag) == "defName"),
                None,
            )
            if def_name_node is None or not element_text(def_name_node):
                continue
            def_name = element_text(def_name_node)
            def_type = local_name(definition.tag)
            for key, text in walk_translatable(definition, def_name):
                yield def_type, key, text


def extract_patch_values(path: Path) -> Iterator[tuple[str, str, str]]:
    root = parse_xml_recover(path)
    if root is None:
        return
    for operation in root.iter():
        if local_name(operation.tag) not in {"Operation", "li"}:
            continue
        xpath_node = next(
            (child for child in list(operation) if local_name(child.tag) == "xpath"),
            None,
        )
        value_node = next(
            (child for child in list(operation) if local_name(child.tag) == "value"),
            None,
        )
        if xpath_node is None or value_node is None:
            continue
        xpath = element_text(xpath_node)
        match = XPATH_DEF_RE.search(xpath)
        if not match:
            continue
        def_type = match.group("def_type")
        def_names = XPATH_DEFNAME_RE.findall(match.group("condition"))
        if not def_names:
            continue
        for def_name in def_names:
            for key, text in walk_translatable(value_node, def_name):
                yield def_type, key, text


def read_language_data(path: Path) -> list[tuple[str, str]]:
    root = parse_xml_recover(path)
    if root is None:
        return []
    language_root = root
    if local_name(root.tag).casefold() not in {"languagedata", "languagdata"}:
        language_root = next(
            (
                node
                for node in root.iter()
                if local_name(node.tag).casefold() in {"languagedata", "languagdata"}
            ),
            root,
        )
    entries: list[tuple[str, str]] = []
    for child in list(language_root):
        if not isinstance(child.tag, str):
            continue
        key = local_name(child.tag)
        text = element_text(child)
        if key and text:
            entries.append((key, text))
    return entries


def open_database() -> sqlite3.Connection:
    ensure_roots()
    connection = sqlite3.connect(DB_PATH)
    connection.execute("PRAGMA journal_mode=WAL")
    connection.execute("PRAGMA synchronous=NORMAL")
    connection.executescript(
        """
        CREATE TABLE IF NOT EXISTS catalog_entries (
            kind TEXT NOT NULL,
            output_path TEXT NOT NULL,
            entry_key TEXT NOT NULL,
            source_text TEXT NOT NULL,
            source_mod TEXT NOT NULL,
            source_file TEXT NOT NULL,
            sort_order INTEGER NOT NULL,
            PRIMARY KEY (kind, output_path, entry_key)
        );
        CREATE TABLE IF NOT EXISTS string_lines (
            output_path TEXT NOT NULL,
            line_index INTEGER NOT NULL,
            source_text TEXT NOT NULL,
            translate INTEGER NOT NULL,
            source_mod TEXT NOT NULL,
            source_file TEXT NOT NULL,
            PRIMARY KEY (output_path, line_index)
        );
        CREATE TABLE IF NOT EXISTS translations (
            language TEXT NOT NULL,
            source_hash TEXT NOT NULL,
            source_text TEXT NOT NULL,
            translated_text TEXT NOT NULL,
            origin TEXT NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (language, source_hash)
        );
        CREATE TABLE IF NOT EXISTS translation_failures (
            language TEXT NOT NULL,
            source_hash TEXT NOT NULL,
            source_text TEXT NOT NULL,
            error TEXT NOT NULL,
            attempts INTEGER NOT NULL,
            updated_at TEXT NOT NULL,
            PRIMARY KEY (language, source_hash)
        );
        CREATE TABLE IF NOT EXISTS metadata (
            key TEXT PRIMARY KEY,
            value TEXT NOT NULL
        );
        """
    )
    connection.commit()
    return connection


def upsert_catalog_entry(
    connection: sqlite3.Connection,
    kind: str,
    output_path: str,
    key: str,
    text: str,
    source: SourceMod,
    source_file: Path,
    sort_order: int,
) -> None:
    if not VALID_XML_NAME_RE.match(key):
        return
    connection.execute(
        """
        INSERT INTO catalog_entries
            (kind, output_path, entry_key, source_text, source_mod, source_file, sort_order)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(kind, output_path, entry_key) DO UPDATE SET
            source_text=excluded.source_text,
            source_mod=excluded.source_mod,
            source_file=excluded.source_file,
            sort_order=excluded.sort_order
        """,
        (
            kind,
            output_path.replace("\\", "/"),
            key,
            text,
            source.folder_name,
            str(source_file),
            sort_order,
        ),
    )


def extract_catalog(
    connection: sqlite3.Connection,
    sources: list[SourceMod],
    manifest: dict,
) -> dict:
    connection.execute("DELETE FROM catalog_entries")
    connection.execute("DELETE FROM string_lines")
    connection.commit()
    parse_errors: list[str] = []
    source_stats: list[dict] = []
    order = 0

    for source_index, source in enumerate(sources, 1):
        stats = {
            "mod": source.folder_name,
            "packageId": source.package_id,
            "defsFiles": 0,
            "patchFiles": 0,
            "englishXmlFiles": 0,
            "englishStringFiles": 0,
            "entriesSeen": 0,
        }
        log(f"Extracting [{source_index}/{len(sources)}] {source.folder_name}")

        xml_files = sorted(iter_unique_files(source, ".xml"), key=lambda p: str(p).casefold())
        # Raw Defs first.
        for path in xml_files:
            relative_english = english_language_relative(path)
            if relative_english is not None:
                continue
            if not is_in_named_directory(path, "Defs"):
                continue
            stats["defsFiles"] += 1
            root = parse_xml_recover(path)
            if root is None:
                parse_errors.append(str(path))
                continue
            for def_type, key, text in extract_defs(path):
                order += 1
                upsert_catalog_entry(
                    connection,
                    "DefInjected",
                    f"DefInjected/{def_type}/Entries.xml",
                    key,
                    text,
                    source,
                    path,
                    order,
                )
                stats["entriesSeen"] += 1

        # Patch values second; explicit English language data remains authoritative.
        for path in xml_files:
            if english_language_relative(path) is not None:
                continue
            if not is_in_named_directory(path, "Patches"):
                continue
            stats["patchFiles"] += 1
            for def_type, key, text in extract_patch_values(path):
                order += 1
                upsert_catalog_entry(
                    connection,
                    "DefInjected",
                    f"DefInjected/{def_type}/Entries.xml",
                    key,
                    text,
                    source,
                    path,
                    order,
                )
                stats["entriesSeen"] += 1

        # Existing English Keyed and DefInjected override raw extraction.
        for path in xml_files:
            relative = english_language_relative(path)
            if relative is None or not relative.parts:
                continue
            category = relative.parts[0].casefold()
            if category not in {"keyed", "definjected"}:
                continue
            stats["englishXmlFiles"] += 1
            entries = read_language_data(path)
            if not entries and parse_xml_recover(path) is None:
                parse_errors.append(str(path))
                continue
            if category == "keyed":
                kind = "Keyed"
                output = "Keyed/FIP_Translation.xml"
            else:
                if len(relative.parts) < 2:
                    continue
                def_type = relative.parts[1]
                kind = "DefInjected"
                output = f"DefInjected/{def_type}/Entries.xml"
            for key, text in entries:
                order += 1
                upsert_catalog_entry(
                    connection,
                    kind,
                    output,
                    key,
                    text,
                    source,
                    path,
                    order,
                )
                stats["entriesSeen"] += 1

        # Every English Strings text file is preserved under a source-prefixed path.
        for path in sorted(iter_unique_files(source, ".txt"), key=lambda p: str(p).casefold()):
            relative = english_language_relative(path)
            if relative is None or not relative.parts or relative.parts[0].casefold() != "strings":
                continue
            stats["englishStringFiles"] += 1
            relative_tail = Path(*relative.parts[1:])
            output = Path("Strings") / safe_stem(source.folder_name) / relative_tail
            # Personal-name pools are language data that must be shipped, but machine
            # translating them corrupts names and creates tens of thousands of bogus
            # "translations". Preserve those files verbatim in every output pack.
            preserve_as_names = any(
                part.casefold() == "names" for part in relative_tail.parts
            )
            try:
                lines = path.read_text(encoding="utf-8-sig", errors="replace").splitlines()
            except OSError as exc:
                parse_errors.append(f"{path}: {exc}")
                continue
            for line_index, line in enumerate(lines):
                stripped = line.strip()
                translate = bool(
                    not preserve_as_names
                    and stripped
                    and not stripped.startswith(("#", "//", ";"))
                )
                connection.execute(
                    """
                    INSERT OR REPLACE INTO string_lines
                        (output_path, line_index, source_text, translate, source_mod, source_file)
                    VALUES (?, ?, ?, ?, ?, ?)
                    """,
                    (
                        output.as_posix(),
                        line_index,
                        line,
                        1 if translate else 0,
                        source.folder_name,
                        str(path),
                    ),
                )
                if translate:
                    stats["entriesSeen"] += 1
        source_stats.append(stats)
        connection.commit()

    catalog_count = connection.execute("SELECT COUNT(*) FROM catalog_entries").fetchone()[0]
    string_count = connection.execute(
        "SELECT COUNT(*) FROM string_lines WHERE translate=1"
    ).fetchone()[0]
    unique_count = connection.execute(
        """
        SELECT COUNT(*) FROM (
            SELECT source_text FROM catalog_entries
            UNION
            SELECT source_text FROM string_lines WHERE translate=1
        )
        """
    ).fetchone()[0]
    output_files = connection.execute(
        "SELECT COUNT(DISTINCT output_path) FROM catalog_entries"
    ).fetchone()[0] + connection.execute(
        "SELECT COUNT(DISTINCT output_path) FROM string_lines"
    ).fetchone()[0]

    report = {
        "generatedAt": now_text(),
        "sources": len(sources),
        "catalogEntries": catalog_count,
        "translatableStringLines": string_count,
        "uniqueEnglishTexts": unique_count,
        "outputFilesPerLanguage": output_files,
        "xmlParseErrors": parse_errors,
        "sourceStats": source_stats,
        "missingConfiguredSources": manifest.get("missingConfiguredSources", []),
        "activeMissingSources": manifest.get("activeMissingSources", []),
    }
    lines = [
        "FIP TRANSLATION NEXT - EXTRACTION REPORT",
        "========================================",
        "",
        f"Generated: {report['generatedAt']}",
        f"Resolved source mods: {report['sources']}",
        f"Catalog XML entries: {catalog_count}",
        f"Translatable string lines: {string_count}",
        f"Unique English texts: {unique_count}",
        f"Output files per language: {output_files}",
        f"XML parse errors: {len(parse_errors)}",
        f"Missing configured but non-active sources: {len(manifest.get('missingConfiguredSources', []))}",
        f"Missing active sources: {len(manifest.get('activeMissingSources', []))}",
        "",
    ]
    if parse_errors:
        lines.append("Parse errors:")
        lines.extend(f"- {item}" for item in parse_errors)
        lines.append("")
    if manifest.get("missingConfiguredSources"):
        lines.append("Unresolved configured sources:")
        for item in manifest["missingConfiguredSources"]:
            lines.append(f"- {item['entry']}: {item['problem']}")
        lines.append("")
    write_text(EXTRACTION_REPORT_PATH, "\n".join(lines))
    connection.execute(
        "INSERT OR REPLACE INTO metadata(key,value) VALUES('last_extraction',?)",
        (json.dumps(report, ensure_ascii=False),),
    )
    connection.commit()
    return report


def source_hash(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def placeholders(text: str) -> tuple[str, ...]:
    return tuple(text[start:end] for start, end in placeholder_spans(text))


def placeholder_spans(text: str) -> list[tuple[int, int]]:
    """Find RimWorld placeholders, including nested conditional brace syntax."""
    spans: list[tuple[int, int]] = []
    simple_without_braces = re.compile(
        r"\[[^\]\r\n]+\]|<[^<>\r\n]+>|\\[nrt]|%\d*\$?[sdif]"
    )
    spans.extend(match.span() for match in simple_without_braces.finditer(text))

    depth = 0
    start = -1
    for index, character in enumerate(text):
        if character == "{":
            if depth == 0:
                start = index
            depth += 1
        elif character == "}" and depth > 0:
            depth -= 1
            if depth == 0 and start >= 0:
                spans.append((start, index + 1))
                start = -1

    spans.sort()
    non_overlapping: list[tuple[int, int]] = []
    for candidate in spans:
        if non_overlapping and candidate[0] < non_overlapping[-1][1]:
            if candidate[1] <= non_overlapping[-1][1]:
                continue
            non_overlapping[-1] = (
                non_overlapping[-1][0],
                candidate[1],
            )
        else:
            non_overlapping.append(candidate)
    return non_overlapping


def looks_mojibake(text: str) -> bool:
    marker_count = sum(text.count(marker) for marker in MOJIBAKE_MARKERS)
    return marker_count >= 3


def usable_legacy_translation(language: str, source: str, translated: str) -> bool:
    if not source or not translated:
        return False
    if "QUERY LENGTH LIMIT EXCEEDED" in translated.upper():
        return False
    if "FIP_TOKEN" in translated or "FIP_TERM" in translated:
        return False
    if "\ufffd" in translated or looks_mojibake(translated):
        return False
    if placeholders(source) != placeholders(translated):
        return False
    if source == translated and len(source) > 12 and re.search(r"[A-Za-z]{4}", source):
        return False
    return language in TARGET_LANGUAGES


def import_legacy_translation_memory(
    connection: sqlite3.Connection,
    config: dict,
    disabled: bool,
) -> int:
    if disabled:
        log("Legacy translation-memory import disabled.")
        return 0
    already = connection.execute(
        "SELECT value FROM metadata WHERE key='legacy_import_complete'"
    ).fetchone()
    if already:
        log(f"Legacy translation memory already imported ({already[0]} entries).")
        return int(already[0])
    legacy_path = resolve_path(config.get("legacyTranslationState"))
    if not legacy_path or not legacy_path.is_file():
        log(f"Legacy translation memory not found: {legacy_path}")
        return 0
    log(f"Importing matching translation memory from {legacy_path} ...")
    try:
        with legacy_path.open("r", encoding="utf-8-sig") as handle:
            state = json.load(handle)
    except Exception as exc:
        log(f"Legacy translation-memory import failed: {exc}")
        return 0
    cache = state.get("translationCache", {})
    imported = 0
    for record in cache.values():
        if not isinstance(record, dict):
            continue
        language = str(record.get("language", ""))
        source = str(record.get("source", ""))
        translated = str(record.get("translated", ""))
        if not usable_legacy_translation(language, source, translated):
            continue
        cursor = connection.execute(
            """
            INSERT OR IGNORE INTO translations
                (language, source_hash, source_text, translated_text, origin, updated_at)
            VALUES (?, ?, ?, ?, 'legacy-memory', ?)
            """,
            (language, source_hash(source), source, translated, now_text()),
        )
        imported += max(cursor.rowcount, 0)
        if imported and imported % 1000 == 0:
            connection.commit()
            log(f"Imported {imported} legacy translations ...")
    # total_changes also includes older connection changes, so derive the actual count.
    connection.commit()
    actual = connection.execute(
        "SELECT COUNT(*) FROM translations WHERE origin='legacy-memory'"
    ).fetchone()[0]
    connection.execute(
        "INSERT OR REPLACE INTO metadata(key,value) VALUES('legacy_import_complete',?)",
        (str(actual),),
    )
    connection.commit()
    del state
    log(f"Imported {actual} usable legacy translations.")
    return actual


def get_unique_source_texts(connection: sqlite3.Connection) -> list[str]:
    return [
        row[0]
        for row in connection.execute(
            """
            SELECT source_text FROM catalog_entries
            UNION
            SELECT source_text FROM string_lines WHERE translate=1
            ORDER BY source_text
            """
        )
    ]


def glossary_for_language(glossary: dict, language: str) -> tuple[dict[str, str], dict[str, str], str]:
    language_config = glossary["languages"][language]
    terms: dict[str, str] = {}
    terms.update(glossary.get("globalTerms", {}))
    terms.update(language_config.get("terms", {}))
    post = dict(language_config.get("postReplacements", {}))
    return terms, post, language_config["code"]


def apply_post_replacements(text: str, post: dict[str, str]) -> str:
    if not post:
        return text
    replacements: dict[str, str] = {}
    for old, new in post.items():
        if old:
            replacements.setdefault(old.casefold(), new)
    ordered = sorted(replacements, key=len, reverse=True)
    pattern = re.compile("|".join(re.escape(old) for old in ordered), re.IGNORECASE)

    def replace_plain(value: str) -> str:
        return pattern.sub(
            lambda match: replacements[match.group(0).casefold()],
            value,
        )

    # Glossary normalization must never alter tokens such as
    # [Enclave_Settlements] or nested RimWorld conditional expressions.
    spans = placeholder_spans(text)
    if not spans:
        return replace_plain(text)
    pieces: list[str] = []
    cursor = 0
    for start, end in spans:
        pieces.append(replace_plain(text[cursor:start]))
        pieces.append(text[start:end])
        cursor = end
    pieces.append(replace_plain(text[cursor:]))
    return "".join(pieces)


def protect_text(text: str, terms: dict[str, str]) -> tuple[str, list[tuple[str, str]]]:
    replacements: list[tuple[str, str]] = []

    def add_token(kind: str, value: str) -> str:
        token = f"https://qzx.invalid/{kind.casefold()}/{len(replacements):05d}"
        replacements.append((token, value))
        return token

    spans = placeholder_spans(text)
    # RulePack prefixes such as subject_story-> are identifiers, not prose.
    structured = re.match(r"^([^\s]+(?:->|&gt;))", text)
    if structured:
        spans.append(structured.span(1))
        spans.sort()

    pieces: list[str] = []
    cursor = 0
    for start, end in spans:
        if start < cursor:
            continue
        pieces.append(text[cursor:start])
        pieces.append(add_token("PH", text[start:end]))
        cursor = end
    pieces.append(text[cursor:])
    protected = "".join(pieces)
    # Do not replace every glossary term with a sentinel. Several neural models
    # drop or duplicate sentences containing many sentinels. Fallout terminology
    # is normalized after translation using curated and model-derived variants.
    return protected, replacements


def normalize_tokens(text: str) -> str:
    def replace(match: re.Match) -> str:
        return (
            f"https://qzx.invalid/{match.group('kind').casefold()}/"
            f"{int(match.group('index')):05d}"
        )

    return TOKEN_RE.sub(replace, text)


def restore_tokens(text: str, replacements: list[tuple[str, str]]) -> str:
    restored = normalize_tokens(text)
    for token, value in sorted(replacements, key=lambda item: len(item[0]), reverse=True):
        restored = restored.replace(token, value)
    leftovers = TOKEN_RE.findall(restored)
    if leftovers:
        raise ValueError(f"unrestored protection tokens: {leftovers[:5]}")
    return restored


def split_translation_chunks(text: str, max_chars: int = 2800) -> list[str]:
    if len(text) <= max_chars:
        return [text]
    chunks: list[str] = []
    remaining = text
    while len(remaining) > max_chars:
        cut = max(
            remaining.rfind(". ", 0, max_chars),
            remaining.rfind("! ", 0, max_chars),
            remaining.rfind("? ", 0, max_chars),
            remaining.rfind("; ", 0, max_chars),
            remaining.rfind(" ", 0, max_chars),
        )
        if cut < max_chars // 2:
            cut = max_chars
        else:
            cut += 1
        chunks.append(remaining[:cut].strip())
        remaining = remaining[cut:].lstrip()
    if remaining:
        chunks.append(remaining)
    return chunks


def google_translate_request(
    text: str,
    target_code: str,
    timeout: int,
    max_attempts: int,
    delay_ms: int,
) -> str:
    last_error: Exception | None = None
    for attempt in range(1, max_attempts + 1):
        try:
            query = urllib.parse.urlencode(
                {
                    "client": "gtx",
                    "sl": "en",
                    "tl": target_code,
                    "dt": "t",
                    "q": text,
                }
            )
            request = urllib.request.Request(
                f"https://translate.googleapis.com/translate_a/single?{query}",
                headers={
                    "User-Agent": "Mozilla/5.0 FIP-Translation-Next/1.0",
                    "Accept": "application/json",
                },
            )
            with urllib.request.urlopen(request, timeout=timeout) as response:
                payload = json.loads(response.read().decode("utf-8"))
            translated = "".join(
                str(segment[0])
                for segment in payload[0]
                if isinstance(segment, list) and segment and segment[0] is not None
            )
            if not translated:
                raise ValueError("empty Google translation response")
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)
            return translated
        except Exception as exc:
            last_error = exc
            if attempt >= max_attempts:
                break
            backoff = min(45.0, 1.5 * (2 ** (attempt - 1)))
            time.sleep(backoff)
    raise RuntimeError(f"Google translation failed after {max_attempts} attempts: {last_error}")


def offline_argos_translation(target_code: str, workers: int):
    """Return a warmed, thread-safe CTranslate2-backed Argos translation."""
    model_code = {
        "ru": "ru",
        "zh": "zh",
        "zh-CN": "zh",
        "zh-TW": "zh",
        "ja": "ja",
        "ko": "ko",
    }.get(target_code, target_code)
    with _OFFLINE_INIT_LOCK:
        existing = _OFFLINE_TRANSLATIONS.get(model_code)
        if existing is not None:
            return existing
        if not VENDOR_ROOT.is_dir():
            raise RuntimeError(
                f"Offline translation runtime not installed: {VENDOR_ROOT}"
            )
        vendor_text = str(VENDOR_ROOT)
        if vendor_text not in sys.path:
            sys.path.insert(0, vendor_text)
        os.environ.setdefault("XDG_DATA_HOME", str(MODEL_ROOT / "data"))
        os.environ.setdefault("XDG_CONFIG_HOME", str(MODEL_ROOT / "config"))
        os.environ.setdefault("XDG_CACHE_HOME", str(MODEL_ROOT / "cache"))
        os.environ.setdefault(
            "ARGOS_PACKAGES_DIR",
            str(MODEL_ROOT / "data" / "argos-translate" / "packages"),
        )
        os.environ.setdefault("ARGOS_DEVICE_TYPE", "cpu")
        os.environ.setdefault("ARGOS_COMPUTE_TYPE", "int8")
        os.environ.setdefault("ARGOS_CHUNK_TYPE", "SPACY")
        os.environ.setdefault("ARGOS_INTER_THREADS", str(max(1, workers)))
        os.environ.setdefault("ARGOS_INTRA_THREADS", "0")
        os.environ.setdefault("ARGOS_BEAM_SIZE", "4")

        import argostranslate.translate as argos_translate

        translation = argos_translate.get_translation_from_codes("en", model_code)
        if translation is None:
            raise RuntimeError(
                f"Offline Argos model English -> {model_code} is not installed. "
                f"Run {TOOL_ROOT / 'download_offline_models.py'} first."
            )
        # Warm the model while holding the initialization lock. Without this,
        # several worker threads could try to construct the same translator.
        translation.translate("Translation model ready.")
        _OFFLINE_TRANSLATIONS[model_code] = translation
        log(f"Offline Argos model loaded: English -> {model_code}.")
        return translation


def offline_translate_request(
    text: str,
    target_code: str,
    workers: int,
) -> str:
    global _OFFLINE_OPENCC
    translation = offline_argos_translation(target_code, workers)
    translated = translation.translate(text)
    if target_code == "zh-TW":
        with _OFFLINE_INIT_LOCK:
            if _OFFLINE_OPENCC is None:
                from opencc import OpenCC

                _OFFLINE_OPENCC = OpenCC("s2twp")
            converter = _OFFLINE_OPENCC
        translated = converter.convert(translated)
    return translated


def offline_translate_batch_request(
    texts: list[str],
    target_code: str,
    workers: int,
) -> list[str]:
    """Translate independent text chunks in one native CTranslate2 batch."""
    if not texts:
        return []
    translation = offline_argos_translation(target_code, workers)
    package_translation = translation
    while hasattr(package_translation, "underlying"):
        package_translation = package_translation.underlying
    package = package_translation.pkg
    tokenized = [package.tokenizer.encode(text) for text in texts]
    target_prefix = None
    if package.target_prefix:
        target_prefix = [[package.target_prefix]] * len(tokenized)
    try:
        results = package_translation.translator.translate_batch(
            tokenized,
            target_prefix=target_prefix,
            replace_unknowns=True,
            max_batch_size=4096,
            batch_type="tokens",
            beam_size=1,
            num_hypotheses=1,
            length_penalty=0.2,
            return_scores=False,
        )
        translated: list[str] = []
        for result in results:
            value = package.tokenizer.decode(result.hypotheses[0])
            if package.target_prefix and value.startswith(package.target_prefix):
                value = value[len(package.target_prefix) :]
            translated.append(value[1:] if value.startswith(" ") else value)
    except Exception:
        # Isolate an unusual overlong or malformed entry without sacrificing the
        # rest of the batch.
        if len(texts) == 1:
            translated = [offline_translate_request(texts[0], target_code, workers)]
        else:
            middle = len(texts) // 2
            translated = offline_translate_batch_request(
                texts[:middle], target_code, workers
            ) + offline_translate_batch_request(texts[middle:], target_code, workers)
    if target_code == "zh-TW":
        global _OFFLINE_OPENCC
        with _OFFLINE_INIT_LOCK:
            if _OFFLINE_OPENCC is None:
                from opencc import OpenCC

                _OFFLINE_OPENCC = OpenCC("s2twp")
            converter = _OFFLINE_OPENCC
        translated = [converter.convert(value) for value in translated]
    return translated


def translate_offline_source_group(
    sources: list[str],
    language: str,
    glossary: dict,
    translation_config: dict,
    workers: int,
) -> list[tuple[str, str | None, str | None]]:
    """Translate a group of catalog texts while batching ordinary entries."""
    _, post, target_code = glossary_for_language(glossary, language)
    records: list[dict] = []
    chunks_to_translate: list[str] = []
    direct_results: dict[int, str] = {}
    fallback_indices: list[int] = []

    for index, source in enumerate(sources):
        prepared, token_values = protect_text(source, {})
        if len(token_values) > 1:
            fallback_indices.append(index)
            records.append(
                {"source": source, "tokens": token_values, "chunk_indices": []}
            )
            continue
        unprotected = TOKEN_RE.sub("", prepared)
        if not re.search(r"[A-Za-z]", unprotected):
            direct_results[index] = prepared
            records.append(
                {"source": source, "tokens": token_values, "chunk_indices": []}
            )
            continue
        chunk_indices: list[int] = []
        for chunk in split_translation_chunks(prepared):
            chunk_indices.append(len(chunks_to_translate))
            chunks_to_translate.append(chunk)
        records.append(
            {
                "source": source,
                "tokens": token_values,
                "chunk_indices": chunk_indices,
            }
        )

    translated_chunks = offline_translate_batch_request(
        chunks_to_translate,
        target_code,
        workers,
    )
    results: list[tuple[str, str | None, str | None]] = []
    fallback_set = set(fallback_indices)
    for index, record in enumerate(records):
        source = record["source"]
        try:
            if index in fallback_set:
                translated = offline_translate_with_fixed_segments(
                    source,
                    target_code,
                    workers,
                )
            elif index in direct_results:
                translated = direct_results[index]
            else:
                translated = " ".join(
                    translated_chunks[chunk_index]
                    for chunk_index in record["chunk_indices"]
                )
                translated = html.unescape(translated)
                try:
                    translated = restore_tokens(translated, record["tokens"])
                except ValueError:
                    translated = offline_translate_with_fixed_segments(
                        source,
                        target_code,
                        workers,
                    )
            translated = ASS_STYLE_RE.sub("", translated)
            translated = apply_post_replacements(translated, post)
            if placeholders(source) != placeholders(translated):
                translated = apply_post_replacements(
                    ASS_STYLE_RE.sub(
                        "",
                        offline_translate_with_fixed_segments(
                            source,
                            target_code,
                            workers,
                        ),
                    ),
                    post,
                )
            if placeholders(source) != placeholders(translated):
                raise ValueError(
                    f"placeholder mismatch: source={placeholders(source)} "
                    f"translated={placeholders(translated)}"
                )
            results.append((source, translated, None))
        except Exception as exc:
            results.append((source, None, str(exc)))
    return results


def iter_offline_translations(
    sources: list[str],
    language: str,
    glossary: dict,
    translation_config: dict,
    workers: int,
    source_batch_size: int = 512,
) -> Iterator[tuple[str, str | None, str | None]]:
    for start in range(0, len(sources), source_batch_size):
        group = sources[start : start + source_batch_size]
        yield from translate_offline_source_group(
            group,
            language,
            glossary,
            translation_config,
            workers,
        )


def derive_traditional_chinese(
    connection: sqlite3.Connection,
    sources: list[str],
    glossary: dict,
) -> int:
    """Create Traditional Chinese locally from completed Simplified Chinese."""
    global _OFFLINE_OPENCC
    with _OFFLINE_INIT_LOCK:
        if _OFFLINE_OPENCC is None:
            vendor_text = str(VENDOR_ROOT)
            if vendor_text not in sys.path:
                sys.path.insert(0, vendor_text)
            from opencc import OpenCC

            _OFFLINE_OPENCC = OpenCC("s2twp")
        converter = _OFFLINE_OPENCC
    _, post, _ = glossary_for_language(glossary, "ChineseTraditional")
    existing = {
        row[0]
        for row in connection.execute(
            "SELECT source_hash FROM translations WHERE language='ChineseTraditional'"
        )
    }
    simplified = {
        row[0]: row[1]
        for row in connection.execute(
            """
            SELECT source_hash, translated_text
            FROM translations
            WHERE language='ChineseSimplified'
            """
        )
    }
    derived = 0
    for source in sources:
        text_hash = source_hash(source)
        if text_hash in existing or text_hash not in simplified:
            continue
        translated = apply_post_replacements(
            converter.convert(simplified[text_hash]),
            post,
        )
        if placeholders(source) != placeholders(translated):
            continue
        connection.execute(
            """
            INSERT OR IGNORE INTO translations
                (language, source_hash, source_text, translated_text, origin, updated_at)
            VALUES ('ChineseTraditional', ?, ?, ?, 'offline-opencc', ?)
            """,
            (text_hash, source, translated, now_text()),
        )
        derived += 1
        if derived % 1000 == 0:
            connection.commit()
    connection.commit()
    log(
        f"ChineseTraditional: derived {derived} entries locally from "
        "ChineseSimplified with OpenCC."
    )
    return derived


def offline_translate_with_fixed_segments(
    source: str,
    target_code: str,
    workers: int,
) -> str:
    """Fallback that never exposes RimWorld placeholders to the NMT model."""
    def translate_prose(prose: str) -> str:
        leading = re.match(r"^\s*", prose).group(0)
        trailing = re.search(r"\s*$", prose).group(0)
        core_end = len(prose) - len(trailing) if trailing else len(prose)
        core = prose[len(leading) : core_end]
        if not core or not re.search(r"[A-Za-z]", core):
            return prose
        translated_core = " ".join(
            offline_translate_request(chunk, target_code, workers)
            for chunk in split_translation_chunks(core)
        )
        return leading + translated_core + trailing

    spans: list[tuple[int, int]] = []
    structured = re.match(r"^([^\s]+(?:->|&gt;))", source)
    if structured:
        spans.append(structured.span(1))
    spans.extend(placeholder_spans(source))
    spans = sorted(set(spans))

    pieces: list[str] = []
    cursor = 0
    for start, end in spans:
        if start < cursor:
            continue
        prose = source[cursor:start]
        if prose:
            pieces.append(translate_prose(prose))
        pieces.append(source[start:end])
        cursor = end
    tail = source[cursor:]
    if tail:
        pieces.append(translate_prose(tail))
    return "".join(pieces)


def augment_offline_post_replacements(
    glossary: dict,
    language: str,
    workers: int,
) -> None:
    """Learn each glossary term's standalone model output, then normalize it."""
    if language in _OFFLINE_POST_VARIANTS:
        return
    terms, post, target_code = glossary_for_language(glossary, language)
    added = 0
    for source_term, target_term in terms.items():
        if not source_term:
            continue
        if source_term.casefold() not in {key.casefold() for key in post}:
            post[source_term] = target_term
            added += 1
        try:
            model_variant = offline_translate_request(
                source_term,
                target_code,
                workers,
            ).strip()
        except Exception as exc:
            log(
                f"{language}: could not derive glossary variant for "
                f"{source_term!r}: {exc}"
            )
            continue
        if (
            model_variant
            and model_variant.casefold() not in {key.casefold() for key in post}
        ):
            post[model_variant] = target_term
            added += 1
    glossary["languages"][language]["postReplacements"] = post
    _OFFLINE_POST_VARIANTS.add(language)
    log(f"{language}: added {added} offline glossary-normalization variants.")


def translate_one(
    source: str,
    language: str,
    glossary: dict,
    translation_config: dict,
    workers: int,
) -> str:
    terms, post, target_code = glossary_for_language(glossary, language)
    prepared, token_values = protect_text(source, terms)
    unprotected_text = TOKEN_RE.sub("", prepared)
    provider = str(translation_config.get("provider", "offline-argos"))
    fixed_segments = provider == "offline-argos" and len(token_values) > 1
    if fixed_segments:
        translated = offline_translate_with_fixed_segments(
            source,
            target_code,
            workers,
        )
    elif not re.search(r"[A-Za-z]", unprotected_text):
        translated = prepared
    else:
        chunks = split_translation_chunks(prepared)
        if provider == "offline-argos":
            translated_chunks = [
                offline_translate_request(chunk, target_code, workers)
                for chunk in chunks
            ]
        elif provider == "google-gtx":
            translated_chunks = [
                google_translate_request(
                    chunk,
                    target_code,
                    int(translation_config.get("timeoutSeconds", 45)),
                    int(translation_config.get("maxAttempts", 6)),
                    int(translation_config.get("requestDelayMs", 75)),
                )
                for chunk in chunks
            ]
        else:
            raise ValueError(f"Unsupported translation provider: {provider}")
        translated = " ".join(translated_chunks)
    translated = ASS_STYLE_RE.sub("", html.unescape(translated))
    if not fixed_segments:
        try:
            translated = restore_tokens(translated, token_values)
        except ValueError:
            if provider != "offline-argos":
                raise
            translated = offline_translate_with_fixed_segments(
                source,
                target_code,
                workers,
            )
    translated = apply_post_replacements(translated, post)
    if placeholders(source) != placeholders(translated):
        if provider == "offline-argos":
            translated = apply_post_replacements(
                ASS_STYLE_RE.sub(
                    "",
                    offline_translate_with_fixed_segments(
                        source,
                        target_code,
                        workers,
                    ),
                ),
                post,
            )
        if placeholders(source) != placeholders(translated):
            raise ValueError(
                f"placeholder mismatch: source={placeholders(source)} "
                f"translated={placeholders(translated)}"
            )
    return translated


def write_progress(
    status: str,
    language: str = "",
    completed: int = 0,
    total: int = 0,
    cache_hits: int = 0,
    failures: int = 0,
    started: float | None = None,
) -> None:
    elapsed = time.time() - started if started else 0
    rate = completed / elapsed if elapsed > 0 else 0
    remaining = max(0, total - completed)
    eta = remaining / rate if rate > 0 else 0
    lines = [
        "FIP Translation Next",
        f"Status: {status}",
        f"Updated: {now_text()}",
        f"Language: {language or '-'}",
        f"Completed in current language: {completed}/{total}",
        f"Existing translation-memory hits: {cache_hits}",
        f"Failures: {failures}",
        f"Elapsed seconds: {elapsed:.1f}",
        f"Estimated remaining seconds: {eta:.1f}",
    ]
    write_text(PROGRESS_PATH, "\n".join(lines) + "\n")


def invalidate_cached_placeholder_mismatches(
    connection: sqlite3.Connection,
) -> int:
    removed = 0
    by_language: dict[str, int] = defaultdict(int)
    rows = list(
        connection.execute(
            """
            SELECT language, source_hash, source_text, translated_text
            FROM translations
            """
        )
    )
    for language, text_hash, source, translated in rows:
        clean = ASS_STYLE_RE.sub("", translated)
        if placeholders(source) != placeholders(clean):
            connection.execute(
                "DELETE FROM translations WHERE language=? AND source_hash=?",
                (language, text_hash),
            )
            removed += 1
            by_language[language] += 1
        elif clean != translated:
            connection.execute(
                """
                UPDATE translations
                SET translated_text=?, updated_at=?
                WHERE language=? AND source_hash=?
                """,
                (clean, now_text(), language, text_hash),
            )
    connection.commit()
    if removed:
        details = ", ".join(
            f"{language}={count}"
            for language, count in sorted(by_language.items())
        )
        log(f"Invalidated {removed} cached placeholder mismatches ({details}).")
    return removed


def translate_catalog(
    connection: sqlite3.Connection,
    config: dict,
    glossary: dict,
    workers: int,
) -> int:
    sources = get_unique_source_texts(connection)
    translation_config = dict(config["translation"])
    passes = int(translation_config.get("passes", 3))
    total_failures = 0
    log(f"Translation catalog contains {len(sources)} unique English texts.")
    invalidate_cached_placeholder_mismatches(connection)

    for language in TARGET_LANGUAGES:
        if str(translation_config.get("provider", "offline-argos")) == "offline-argos":
            augment_offline_post_replacements(glossary, language, workers)
            if language == "ChineseTraditional":
                derive_traditional_chinese(connection, sources, glossary)
        existing_rows = {
            row[0]
            for row in connection.execute(
                "SELECT source_hash FROM translations WHERE language=?",
                (language,),
            )
        }
        pending = [text for text in sources if source_hash(text) not in existing_rows]
        cache_hits = len(sources) - len(pending)
        log(
            f"{language}: {cache_hits} translation-memory hits, "
            f"{len(pending)} texts require translation."
        )
        language_failures: list[tuple[str, str]] = []
        language_started = time.time()

        for pass_number in range(1, passes + 1):
            if not pending:
                break
            log(
                f"{language}: pass {pass_number}/{passes}, "
                f"translating {len(pending)} texts with {workers} workers."
            )
            failures_this_pass: list[tuple[str, str]] = []
            completed = 0
            provider = str(translation_config.get("provider", "offline-argos"))

            def work(text: str) -> tuple[str, str | None, str | None]:
                try:
                    translated = translate_one(
                        text,
                        language,
                        glossary,
                        translation_config,
                        workers,
                    )
                    return text, translated, None
                except Exception as exc:
                    return text, None, str(exc)

            def consume(
                result_iterator: Iterable[tuple[str, str | None, str | None]]
            ) -> None:
                nonlocal completed
                for text, translated, error in result_iterator:
                    completed += 1
                    text_hash = source_hash(text)
                    if error is None and translated is not None:
                        connection.execute(
                            """
                            INSERT OR REPLACE INTO translations
                                (language, source_hash, source_text, translated_text, origin, updated_at)
                            VALUES (?, ?, ?, ?, ?, ?)
                            """,
                            (
                                language,
                                text_hash,
                                text,
                                translated,
                                provider,
                                now_text(),
                            ),
                        )
                        connection.execute(
                            "DELETE FROM translation_failures WHERE language=? AND source_hash=?",
                            (language, text_hash),
                        )
                    else:
                        failures_this_pass.append((text, error or "unknown error"))
                    if completed % 20 == 0 or completed == len(pending):
                        connection.commit()
                        write_progress(
                            "TRANSLATING",
                            language,
                            completed,
                            len(pending),
                            cache_hits,
                            len(failures_this_pass),
                            language_started,
                        )
                    if completed % 100 == 0 or completed == len(pending):
                        log(
                            f"{language}: {completed}/{len(pending)} in pass "
                            f"{pass_number}; failures={len(failures_this_pass)}"
                        )

            if provider == "offline-argos":
                consume(
                    iter_offline_translations(
                        pending,
                        language,
                        glossary,
                        translation_config,
                        workers,
                    )
                )
            else:
                with concurrent.futures.ThreadPoolExecutor(
                    max_workers=workers
                ) as executor:
                    future_map = {
                        executor.submit(work, text): text for text in pending
                    }
                    consume(
                        future.result()
                        for future in concurrent.futures.as_completed(future_map)
                    )
            connection.commit()
            pending = [text for text, _ in failures_this_pass]
            language_failures = failures_this_pass
            if pending and pass_number < passes:
                log(f"{language}: retrying {len(pending)} failed texts after cooldown.")
                time.sleep(min(30, 5 * pass_number))

        for text, error in language_failures:
            text_hash = source_hash(text)
            if connection.execute(
                "SELECT 1 FROM translations WHERE language=? AND source_hash=?",
                (language, text_hash),
            ).fetchone():
                continue
            connection.execute(
                """
                INSERT OR REPLACE INTO translation_failures
                    (language, source_hash, source_text, error, attempts, updated_at)
                VALUES (?, ?, ?, ?, ?, ?)
                """,
                (
                    language,
                    text_hash,
                    text,
                    error,
                    int(translation_config.get("maxAttempts", 6)) * passes,
                    now_text(),
                ),
            )
        connection.commit()
        unresolved = connection.execute(
            """
            SELECT COUNT(*) FROM translation_failures f
            WHERE f.language=?
              AND NOT EXISTS (
                  SELECT 1 FROM translations t
                  WHERE t.language=f.language AND t.source_hash=f.source_hash
              )
            """,
            (language,),
        ).fetchone()[0]
        total_failures += unresolved
        log(f"{language}: translation phase complete; unresolved failures={unresolved}.")

    write_progress(
        "TRANSLATION PHASE COMPLETE" if total_failures == 0 else "TRANSLATION PHASE INCOMPLETE",
        failures=total_failures,
    )
    return total_failures


def lookup_translation(
    connection: sqlite3.Connection,
    language: str,
    source: str,
    post: dict[str, str],
) -> str:
    row = connection.execute(
        "SELECT translated_text FROM translations WHERE language=? AND source_hash=?",
        (language, source_hash(source)),
    ).fetchone()
    translated = row[0] if row else source
    return apply_post_replacements(translated, post)


def xml_escape(value: str) -> str:
    return (
        value.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
        .replace('"', "&quot;")
    )


def write_language_xml(path: Path, entries: Iterable[tuple[str, str]]) -> None:
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    for key, value in entries:
        if VALID_XML_NAME_RE.match(key):
            lines.append(f"  <{key}>{xml_escape(value)}</{key}>")
    lines.append("</LanguageData>")
    write_text(path, "\n".join(lines) + "\n")


def build_about_xml(config: dict, output: dict, package_ids: list[str]) -> str:
    lines = [
        '<?xml version="1.0" encoding="utf-8"?>',
        "<ModMetaData>",
        f"  <name>{xml_escape(output['name'])}</name>",
        f"  <packageId>{xml_escape(output['packageId'])}</packageId>",
        "  <author>Feil</author>",
        "  <supportedVersions>",
        "    <li>1.6</li>",
        "  </supportedVersions>",
        "  <description>Automatically generated Fallout Immersion Project translations built from the current FIP, FCP, compatibility, and playset sources. Includes protected RimWorld placeholders and a curated Fallout terminology glossary.</description>",
        "  <loadAfter>",
    ]
    lines.extend(f"    <li>{xml_escape(package)}</li>" for package in package_ids)
    lines.extend(["  </loadAfter>", "</ModMetaData>"])
    return "\n".join(lines) + "\n"


def safe_remove_languages(output_root: Path, translation_root: Path) -> None:
    languages = (output_root / "Languages").resolve()
    allowed = translation_root.resolve()
    if allowed not in languages.parents:
        raise RuntimeError(f"Unsafe output cleanup target: {languages}")
    if languages.is_dir():
        shutil.rmtree(languages)


def sanitize_cached_translations(connection: sqlite3.Connection) -> int:
    cleaned = 0
    rows = list(
        connection.execute(
            """
            SELECT language, source_hash, translated_text
            FROM translations
            WHERE instr(translated_text, '\\fn') > 0
               OR instr(translated_text, '\\fs') > 0
               OR instr(translated_text, '\\bord') > 0
            """
        )
    )
    for language, text_hash, translated in rows:
        clean = ASS_STYLE_RE.sub("", translated)
        if clean == translated:
            continue
        connection.execute(
            """
            UPDATE translations
            SET translated_text=?, updated_at=?
            WHERE language=? AND source_hash=?
            """,
            (clean, now_text(), language, text_hash),
        )
        cleaned += 1
    connection.commit()
    if cleaned:
        log(f"Removed hallucinated subtitle-style tags from {cleaned} cached translations.")
    return cleaned


def render_outputs(
    connection: sqlite3.Connection,
    config: dict,
    glossary: dict,
    sources: list[SourceMod],
) -> dict:
    sanitize_cached_translations(connection)
    translation_root = TOOL_ROOT.parent
    package_ids = sorted({source.package_id for source in sources}, key=str.casefold)
    entry_paths = [
        row[0]
        for row in connection.execute(
            "SELECT DISTINCT output_path FROM catalog_entries ORDER BY output_path"
        )
    ]
    string_paths = [
        row[0]
        for row in connection.execute(
            "SELECT DISTINCT output_path FROM string_lines ORDER BY output_path"
        )
    ]
    output_stats: dict[str, dict] = {}

    for output_key, output_config in config["outputs"].items():
        output_root = resolve_path(output_config["folder"])
        assert output_root is not None
        if translation_root.resolve() not in output_root.resolve().parents:
            raise RuntimeError(f"Output mod is outside FIP-Translation-Next: {output_root}")
        output_root.mkdir(parents=True, exist_ok=True)
        safe_remove_languages(output_root, translation_root)
        write_text(
            output_root / "About" / "About.xml",
            build_about_xml(config, output_config, package_ids),
        )
        output_stats[output_key] = {"languages": {}, "root": str(output_root)}

        for language in output_config["languages"]:
            _, post, _ = glossary_for_language(glossary, language)
            language_root = output_root / "Languages" / language
            xml_written = 0
            string_written = 0
            entry_count = 0
            for relative in entry_paths:
                rows = list(
                    connection.execute(
                        """
                        SELECT entry_key, source_text
                        FROM catalog_entries
                        WHERE output_path=?
                        ORDER BY sort_order, entry_key
                        """,
                        (relative,),
                    )
                )
                translated_entries = [
                    (
                        key,
                        lookup_translation(connection, language, source_text, post),
                    )
                    for key, source_text in rows
                ]
                write_language_xml(language_root / Path(relative), translated_entries)
                xml_written += 1
                entry_count += len(translated_entries)

            for relative in string_paths:
                rows = list(
                    connection.execute(
                        """
                        SELECT source_text, translate
                        FROM string_lines
                        WHERE output_path=?
                        ORDER BY line_index
                        """,
                        (relative,),
                    )
                )
                lines = [
                    lookup_translation(connection, language, source_text, post)
                    if should_translate
                    else source_text
                    for source_text, should_translate in rows
                ]
                write_text(language_root / Path(relative), "\n".join(lines) + "\n")
                string_written += 1
                entry_count += sum(1 for _, should_translate in rows if should_translate)

            output_stats[output_key]["languages"][language] = {
                "xmlFiles": xml_written,
                "stringFiles": string_written,
                "entries": entry_count,
            }
            log(
                f"Rendered {output_config['name']} / {language}: "
                f"{xml_written} XML + {string_written} string files, {entry_count} entries."
            )
    return output_stats


def validate_outputs(
    connection: sqlite3.Connection,
    config: dict,
    glossary: dict,
    sources: list[SourceMod],
    manifest: dict,
) -> tuple[bool, dict]:
    expected_xml_entries = connection.execute(
        "SELECT COUNT(*) FROM catalog_entries"
    ).fetchone()[0]
    expected_xml_files = connection.execute(
        "SELECT COUNT(DISTINCT output_path) FROM catalog_entries"
    ).fetchone()[0]
    expected_string_files = connection.execute(
        "SELECT COUNT(DISTINCT output_path) FROM string_lines"
    ).fetchone()[0]
    expected_string_lines = connection.execute(
        "SELECT COUNT(*) FROM string_lines WHERE translate=1"
    ).fetchone()[0]
    unresolved = connection.execute(
        """
        SELECT COUNT(*) FROM translation_failures f
        WHERE NOT EXISTS (
            SELECT 1 FROM translations t
            WHERE t.language=f.language AND t.source_hash=f.source_hash
        )
        """
    ).fetchone()[0]
    parse_errors: list[str] = []
    missing_files: list[str] = []
    count_mismatches: list[str] = []
    placeholder_errors: list[str] = []
    identical_warnings: dict[str, int] = {}

    for output_config in config["outputs"].values():
        output_root = resolve_path(output_config["folder"])
        assert output_root is not None
        about_path = output_root / "About" / "About.xml"
        try:
            ET.parse(about_path)
        except Exception as exc:
            parse_errors.append(f"{about_path}: {exc}")
        for language in output_config["languages"]:
            language_root = output_root / "Languages" / language
            xml_files = list(language_root.rglob("*.xml")) if language_root.is_dir() else []
            txt_files = list(language_root.rglob("*.txt")) if language_root.is_dir() else []
            if len(xml_files) != expected_xml_files:
                count_mismatches.append(
                    f"{language}: XML files {len(xml_files)} != {expected_xml_files}"
                )
            if len(txt_files) != expected_string_files:
                count_mismatches.append(
                    f"{language}: string files {len(txt_files)} != {expected_string_files}"
                )
            actual_entries = 0
            for path in xml_files:
                try:
                    root = ET.parse(path).getroot()
                    actual_entries += len(list(root))
                except Exception as exc:
                    parse_errors.append(f"{path}: {exc}")
            if actual_entries != expected_xml_entries:
                count_mismatches.append(
                    f"{language}: XML entries {actual_entries} != {expected_xml_entries}"
                )

            cached = {
                row[0]: row[1]
                for row in connection.execute(
                    "SELECT source_text, translated_text FROM translations WHERE language=?",
                    (language,),
                )
            }
            identical = 0
            for source, translated in cached.items():
                if placeholders(source) != placeholders(translated):
                    placeholder_errors.append(
                        f"{language}: {source[:120]!r} -> {translated[:120]!r}"
                    )
                if (
                    source == translated
                    and len(source) > 12
                    and re.search(r"[A-Za-z]{4}", source)
                ):
                    identical += 1
            identical_warnings[language] = identical

    fatal = bool(
        unresolved
        or parse_errors
        or missing_files
        or count_mismatches
        or placeholder_errors
        or manifest.get("activeMissingSources")
    )
    report = {
        "generatedAt": now_text(),
        "result": "FAIL" if fatal else "PASS",
        "resolvedSourceMods": len(sources),
        "expectedXmlFilesPerLanguage": expected_xml_files,
        "expectedXmlEntriesPerLanguage": expected_xml_entries,
        "expectedStringFilesPerLanguage": expected_string_files,
        "expectedTranslatableStringLinesPerLanguage": expected_string_lines,
        "unresolvedTranslations": unresolved,
        "activeMissingSources": manifest.get("activeMissingSources", []),
        "configuredMissingSourceWarnings": len(manifest.get("missingConfiguredSources", [])),
        "xmlParseErrors": parse_errors,
        "missingFiles": missing_files,
        "countMismatches": count_mismatches,
        "placeholderErrors": placeholder_errors,
        "identicalEnglishWarnings": identical_warnings,
    }
    lines = [
        "FIP TRANSLATION NEXT - VALIDATION REPORT",
        "========================================",
        "",
        f"Result: {report['result']}",
        f"Generated: {report['generatedAt']}",
        f"Resolved source mods: {len(sources)}",
        f"XML files per language: {expected_xml_files}",
        f"XML entries per language: {expected_xml_entries}",
        f"String files per language: {expected_string_files}",
        f"Translatable string lines per language: {expected_string_lines}",
        f"Unresolved translations: {unresolved}",
        f"Missing active sources: {len(manifest.get('activeMissingSources', []))}",
        f"Unresolved non-active configured sources (warnings): {len(manifest.get('missingConfiguredSources', []))}",
        f"XML parse errors: {len(parse_errors)}",
        f"Count mismatches: {len(count_mismatches)}",
        f"Placeholder errors: {len(placeholder_errors)}",
        "",
        "Identical-English warnings (proper names and technical tokens may be intentional):",
    ]
    lines.extend(f"- {language}: {count}" for language, count in identical_warnings.items())
    for title, values in (
        ("Parse errors", parse_errors),
        ("Count mismatches", count_mismatches),
        ("Placeholder errors", placeholder_errors),
        ("Missing active sources", manifest.get("activeMissingSources", [])),
    ):
        if values:
            lines.extend(["", f"{title}:"])
            lines.extend(f"- {value}" for value in values)
    write_text(VALIDATION_REPORT_PATH, "\n".join(lines) + "\n")
    return not fatal, report


def write_completion_marker(success: bool, report: dict, output_stats: dict | None = None) -> None:
    root = TOOL_ROOT.parent
    complete_path = root / "TRANSLATION_COMPLETE.txt"
    incomplete_path = root / "TRANSLATION_INCOMPLETE.txt"
    for path in (complete_path, incomplete_path):
        if path.exists():
            path.unlink()
    target = complete_path if success else incomplete_path
    lines = [
        "FIP TRANSLATION NEXT",
        "====================",
        "",
        f"STATUS: {'COMPLETE' if success else 'INCOMPLETE'}",
        f"Finished: {now_text()}",
        f"Validation: {report.get('result', 'UNKNOWN')}",
        f"Resolved source mods: {report.get('resolvedSourceMods', 0)}",
        f"XML entries per language: {report.get('expectedXmlEntriesPerLanguage', 0)}",
        f"String lines per language: {report.get('expectedTranslatableStringLinesPerLanguage', 0)}",
        f"Unresolved translations: {report.get('unresolvedTranslations', 0)}",
        f"XML parse errors: {len(report.get('xmlParseErrors', []))}",
        f"Placeholder errors: {len(report.get('placeholderErrors', []))}",
        "",
        f"Progress log: {PROGRESS_PATH}",
        f"Validation report: {VALIDATION_REPORT_PATH}",
        f"Source manifest: {MANIFEST_PATH}",
    ]
    write_text(target, "\n".join(lines) + "\n")


def load_last_manifest() -> tuple[list[SourceMod], dict]:
    config = read_json(CONFIG_PATH)
    return discover_sources(config)


def main() -> int:
    parser = argparse.ArgumentParser(description="Build and translate FIP language packs.")
    parser.add_argument(
        "command",
        choices=("discover", "extract", "translate", "render", "validate", "all"),
    )
    parser.add_argument("--workers", type=int, default=None)
    parser.add_argument("--no-legacy-import", action="store_true")
    args = parser.parse_args()

    ensure_roots()
    config = read_json(CONFIG_PATH)
    glossary = read_json(GLOSSARY_PATH)
    workers = max(1, args.workers or int(config["translation"].get("workers", 4)))
    started = time.time()
    write_progress("STARTING", started=started)
    log(f"FIP Translation Next started: command={args.command}, workers={workers}")

    connection = open_database()
    output_stats: dict | None = None
    try:
        sources, manifest = discover_sources(config)
        log(
            f"Discovered {len(sources)} source mods: "
            f"{manifest['countsByCategory'].get('FIP', 0)} FIP, "
            f"{manifest['countsByCategory'].get('FCP', 0)} FCP, "
            f"{manifest['countsByCategory'].get('Other', 0)} other."
        )
        if args.command == "discover":
            return 0

        if args.command in {"extract", "all"}:
            extraction = extract_catalog(connection, sources, manifest)
            log(
                f"Extraction complete: {extraction['catalogEntries']} XML entries, "
                f"{extraction['translatableStringLines']} string lines, "
                f"{extraction['uniqueEnglishTexts']} unique English texts."
            )
        if args.command == "extract":
            return 0

        if args.command in {"translate", "all"}:
            import_legacy_translation_memory(connection, config, args.no_legacy_import)
            unresolved = translate_catalog(connection, config, glossary, workers)
            if unresolved:
                log(f"Translation phase left {unresolved} unresolved entries.")
        if args.command == "translate":
            return 0 if unresolved == 0 else 2

        if args.command in {"render", "all"}:
            output_stats = render_outputs(connection, config, glossary, sources)
        if args.command == "render":
            return 0

        success, report = validate_outputs(
            connection, config, glossary, sources, manifest
        )
        write_completion_marker(success, report, output_stats)
        write_progress(
            "COMPLETE" if success else "INCOMPLETE",
            failures=report["unresolvedTranslations"],
            started=started,
        )
        log(
            f"Validation {report['result']}. Total runtime: "
            f"{dt.timedelta(seconds=int(time.time() - started))}."
        )
        return 0 if success else 2
    except KeyboardInterrupt:
        write_progress("INTERRUPTED - SAFE TO RESUME", started=started)
        log("Interrupted. Completed translations are saved; rerun to resume.")
        return 130
    except Exception:
        error = traceback.format_exc()
        write_text(LOG_ROOT / "fatal-error.txt", error)
        write_progress("FAILED - SAFE TO RESUME", failures=1, started=started)
        log(error)
        return 1
    finally:
        connection.close()


if __name__ == "__main__":
    raise SystemExit(main())
