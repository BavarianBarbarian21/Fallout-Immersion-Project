#!/usr/bin/env python3
"""Apply only the current FIP-Repconn gravship localization delta to release packs."""

from __future__ import annotations

import base64
import html
import json
import os
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE_ROOT = ROOT / "FIP-Repconn" / "LoadFolders" / "Gravship2" / "Languages" / "English"
PROVIDER = ROOT / "FIP-Yes Man" / "Tools" / "OfflineTranslationProvider.py"
PYTHON = ROOT / "Tools" / "TranslationSync" / ".offline-venv" / "Scripts" / "python.exe"
ARGOS = ROOT / "Tools" / "TranslationSync" / "offline-models" / "argos"
CACHE_ROOT = ROOT / "Tools" / "TranslationSync" / "state" / "repconn-gravship2-delta"

PACKS = {
    "FIP-Brazilian Portuguese Language Pack": ("PortugueseBrazilian", "pt", False),
    "FIP-Czech Language Pack": ("Czech", "cs", False),
    "FIP-Dutch Language Pack": ("Dutch", "nl", False),
    "FIP-French Language Pack": ("French", "fr", False),
    "FIP-German Language Pack": ("German", "de", False),
    "FIP-Italian Language Pack": ("Italian", "it", False),
    "FIP-Japanese Language Pack": ("Japanese", "ja", False),
    "FIP-Korean Language Pack": ("Korean", "ko", False),
    "FIP-Polish Language Pack": ("Polish", "pl", False),
    "FIP-Russian Language Pack": ("Russian", "ru", False),
    "FIP-Simplified Chinese Language Pack": ("ChineseSimplified", "zh", False),
    "FIP-Spanish Language Pack": ("Spanish", "es", False),
    "FIP-Traditional Chinese Language Pack": ("ChineseTraditional", "zh", True),
    "FIP-Ukrainian Language Pack": ("Ukrainian", "uk", False),
}

PROTECTED_TERMS = (
    "Vanilla Gravships Expanded - Chapter 2",
    "US Automated Aerospace Defense",
    "Hellion Fighter",
    "Hellion Carrier",
    "REPCONN",
    "Hellion",
)
PLACEHOLDER = re.compile(
    r"\\n|\{[^{}]+\}|\[(?:discoveryMethod|threatDescription)\]|"
    r"quest(?:Description|Name)->|<[^<>]+>"
)


def xml_entries(path: Path) -> list[tuple[str, str]]:
    root = ET.parse(path).getroot()
    return [(node.tag, node.text or "") for node in root if isinstance(node.tag, str)]


def source_files() -> list[Path]:
    return sorted(path for path in SOURCE_ROOT.rglob("*.xml") if path.name.lower().endswith(".xml"))


def changed_part1_entries() -> dict[tuple[str, str], str]:
    result = subprocess.run(
        ["git", "diff", "--unified=0", "--", "FIP-Repconn"],
        cwd=ROOT,
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    current_file: Path | None = None
    keys_by_file: dict[Path, set[str]] = {}
    for line in result.stdout.splitlines():
        if line.startswith("+++ b/"):
            candidate = ROOT / line[6:]
            current_file = candidate if candidate.suffix.lower() == ".xml" else None
            continue
        if current_file is None or not line.startswith("+") or line.startswith("+++"):
            continue
        match = re.match(r"^\+\s*<([A-Za-z_][A-Za-z0-9_.-]*)>", line)
        if match and match.group(1) != "LanguageData":
            keys_by_file.setdefault(current_file, set()).add(match.group(1))

    entries: dict[tuple[str, str], str] = {}
    for path, changed_keys in keys_by_file.items():
        if not path.is_file() or "Languages" not in path.parts:
            continue
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        if root.tag != "LanguageData":
            continue
        kind = "Keyed" if "Keyed" in path.parts else path.parent.name
        for node in root:
            if isinstance(node.tag, str) and node.tag in changed_keys:
                entries[(kind, node.tag)] = node.text or ""
    return entries


def protect(text: str) -> tuple[str, dict[str, str]]:
    replacements: dict[str, str] = {}

    def store(value: str) -> str:
        marker = f"__FIP_TOKEN_{len(replacements)}__"
        replacements[marker] = value
        return marker

    for term in PROTECTED_TERMS:
        text = text.replace(term, store(term))
    text = PLACEHOLDER.sub(lambda match: store(match.group(0)), text)
    return text, replacements


def translate_values(values: list[str], code: str, traditional: bool) -> list[str]:
    protected: list[str] = []
    maps: list[dict[str, str]] = []
    for value in values:
        encoded, mapping = protect(value)
        protected.append(encoded)
        maps.append(mapping)

    args = [
        str(PYTHON), str(PROVIDER), "--provider", "argos", "--source", "en",
        "--target", code, "--argos-packages-dir", str(ARGOS),
    ]
    if traditional:
        args.extend(["--opencc-config", "s2t"])
    process = subprocess.Popen(
        args,
        cwd=ROOT,
        stdin=subprocess.PIPE,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        encoding="utf-8",
        bufsize=1,
    )
    assert process.stdin is not None and process.stdout is not None
    ready = json.loads(process.stdout.readline())
    if not ready.get("ready"):
        raise RuntimeError(f"Translation provider did not become ready: {ready}")
    payload = {
        "texts_b64": [base64.b64encode(value.encode("utf-8")).decode("ascii") for value in protected]
    }
    process.stdin.write(json.dumps(payload) + "\n")
    process.stdin.flush()
    response = json.loads(process.stdout.readline())
    process.stdin.close()
    process.wait(timeout=30)
    if "error" in response:
        stderr = process.stderr.read() if process.stderr else ""
        raise RuntimeError(f"Translation failed: {response['error']}\n{stderr}")
    translated = [base64.b64decode(value).decode("utf-8") for value in response["translations_b64"]]
    output: list[str] = []
    for value, mapping in zip(translated, maps):
        for marker, original in mapping.items():
            value = re.sub(re.escape(marker), lambda _match, item=original: item, value, flags=re.IGNORECASE)
        output.append(value)
    return output


def escape_xml_text(value: str) -> str:
    return html.escape(value, quote=False)


def atomic_write_text(path: Path, text: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".repconn-gravship2.tmp")
    temporary.write_text(text, encoding="utf-8", newline="\n")
    os.replace(temporary, path)


def replace_element_text(path: Path, key: str, value: str) -> bool:
    text = path.read_text(encoding="utf-8-sig")
    pattern = re.compile(rf"(<{re.escape(key)}>)(.*?)(</{re.escape(key)}>)", re.DOTALL)
    updated, count = pattern.subn(
        lambda match: match.group(1) + escape_xml_text(value) + match.group(3), text, count=1
    )
    if count:
        atomic_write_text(path, updated)
    return bool(count)


def index_locale(language_root: Path) -> dict[tuple[str, str], Path]:
    index: dict[tuple[str, str], Path] = {}
    for path in language_root.rglob("*.xml"):
        try:
            root = ET.parse(path).getroot()
        except ET.ParseError:
            continue
        if root.tag != "LanguageData":
            continue
        kind = "Keyed" if "Keyed" in path.parts else path.parent.name
        for node in root:
            if isinstance(node.tag, str):
                index[(kind, node.tag)] = path
    return index


def write_language_data(path: Path, entries: list[tuple[str, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    lines = ['<?xml version="1.0" encoding="utf-8"?>', "<LanguageData>"]
    lines.extend(f"  <{key}>{escape_xml_text(value)}</{key}>" for key, value in entries)
    lines.append("</LanguageData>")
    atomic_write_text(path, "\n".join(lines) + "\n")


def add_load_after(about_path: Path) -> None:
    text = about_path.read_text(encoding="utf-8-sig")
    if "<li>vanillaexpanded.gravship2</li>" in text:
        return
    anchor = "    <li>vanillaexpanded.gravship</li>"
    if anchor not in text:
        raise RuntimeError(f"Chapter 1 loadAfter anchor missing in {about_path}")
    text = text.replace(anchor, anchor + "\n    <li>vanillaexpanded.gravship2</li>", 1)
    atomic_write_text(about_path, text)


def main() -> int:
    chapter2_files = source_files()
    chapter2: list[tuple[Path, str, str, str]] = []
    for source in chapter2_files:
        relative = source.relative_to(SOURCE_ROOT)
        kind = "Keyed" if relative.parts[0] == "Keyed" else relative.parent.name
        for key, value in xml_entries(source):
            chapter2.append((relative, kind, key, value))

    changed = changed_part1_entries()
    all_values = list(dict.fromkeys([item[3] for item in chapter2] + list(changed.values())))
    print(f"Delta source: {len(chapter2)} Chapter-2 entries, {len(changed)} changed Chapter-1 entries, {len(all_values)} unique texts")

    for pack_name, (locale, code, traditional) in PACKS.items():
        pack_root = ROOT / pack_name
        language_root = pack_root / "Languages" / locale
        if not language_root.is_dir():
            raise RuntimeError(f"Language root missing: {language_root}")
        CACHE_ROOT.mkdir(parents=True, exist_ok=True)
        cache_path = CACHE_ROOT / f"{locale}.json"
        localized_values: list[str]
        if cache_path.is_file():
            cached = json.loads(cache_path.read_text(encoding="utf-8"))
            if cached.get("source") == all_values and isinstance(cached.get("localized"), list):
                localized_values = cached["localized"]
                print(f"Using cached {locale} delta...", flush=True)
            else:
                localized_values = []
        else:
            localized_values = []
        if not localized_values:
            print(f"Translating {locale} ({code})...", flush=True)
            localized_values = translate_values(all_values, code, traditional)
            atomic_write_text(
                cache_path,
                json.dumps({"source": all_values, "localized": localized_values}, ensure_ascii=False, indent=2) + "\n",
            )
        localized = dict(zip(all_values, localized_values))
        grouped: dict[Path, list[tuple[str, str]]] = {}
        for relative, kind, key, source_value in chapter2:
            grouped.setdefault(relative, []).append((key, localized[source_value]))
        for (kind, key), source_value in changed.items():
            if kind == "Keyed":
                relative = Path("Keyed") / "ZZZ_FIP-Repconn_Gravship2Delta.xml"
            else:
                relative = Path("DefInjected") / kind / "ZZZ_FIP-Repconn_Gravship2Delta.xml"
            grouped.setdefault(relative, []).append((key, localized[source_value]))
        for relative, entries in grouped.items():
            write_language_data(language_root / relative, entries)
        print(f"Completed {locale}: {len(chapter2) + len(changed)} delta entries", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
