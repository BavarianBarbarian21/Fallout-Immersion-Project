#!/usr/bin/env python3
"""Restore glossary terms left behind as FIPTERM markers by older Argos runs."""

from __future__ import annotations

import argparse
import csv
import html
import json
import re
from pathlib import Path


LANGUAGES = {
    "German": ("FIP-German Language Pack", "German"),
    "Spanish": ("FIP-Spanish Language Pack", "Spanish"),
    "French": ("FIP-French Language Pack", "French"),
    "PortugueseBrazilian": ("FIP-Brazilian Portuguese Language Pack", "PortugueseBrazilian"),
    "Korean": ("FIP-Korean Language Pack", "Korean"),
    "Japanese": ("FIP-Japanese Language Pack", "Japanese"),
    "Polish": ("FIP-Polish Language Pack", "Polish"),
    "Italian": ("FIP-Italian Language Pack", "Italian"),
    "ChineseSimplified": ("FIP-Simplified Chinese Language Pack", "ChineseSimplified"),
    "ChineseTraditional": ("FIP-Traditional Chinese Language Pack", "ChineseTraditional"),
    "Russian": ("FIP-Russian Language Pack", "Russian"),
    "Ukrainian": ("FIP-Ukrainian Language Pack", "Ukrainian"),
    "Dutch": ("FIP-Dutch Language Pack", "Dutch"),
    "Czech": ("FIP-Czech Language Pack", "Czech"),
}

LEAF_XML = re.compile(r"<(?P<tag>[^!?/\s][^>\s]*)>(?P<text>[^<]*?)</(?P=tag)>", re.DOTALL)
STRUCTURAL = re.compile(r"\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[A-Za-z]|\\[nrt]")
# The two unusual prefixes were emitted by the old Korean model. Ordinary
# punctuation such as '(' is not consumed because it belongs to the sentence.
BROKEN = re.compile(r"(?i)(?:[₢·]\s*킹?\s*)?FIP\s*TERM\s*(\d+)")


def glossary(path: Path, language: str) -> list[tuple[str, str]]:
    result: list[tuple[str, str]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            source = (row.get("source") or "").strip()
            if not source:
                continue
            target = source if (row.get("keep_original") or "").lower() == "true" else ((row.get(language) or source).strip() or source)
            result.append((source, target))
    return sorted(result, key=lambda pair: len(pair[0]), reverse=True)


def token_map(source: str, terms: list[tuple[str, str]]) -> dict[int, str]:
    protected = source
    values: dict[int, str] = {}
    number = 0
    for source_term, target_term in terms:
        pattern = re.compile(re.escape(source_term), re.IGNORECASE)
        def replace(_match: re.Match[str]) -> str:
            nonlocal number
            token = f"__FIP_TERM_{number}__"
            values[number] = target_term
            number += 1
            return token
        protected = pattern.sub(replace, protected)
    # Structural placeholders shared the same old counter. Their values are
    # irrelevant here, but incrementing reproduces the original term numbers.
    number += len(STRUCTURAL.findall(protected))
    return values


def repair_text(source: str, target: str, terms: list[tuple[str, str]]) -> tuple[str, int, list[int]]:
    mapping = token_map(source, terms)
    changed = 0
    unknown: list[int] = []
    def replace(match: re.Match[str]) -> str:
        nonlocal changed
        number = int(match.group(1))
        value = mapping.get(number)
        if value is None:
            unknown.append(number)
            return match.group(0)
        changed += 1
        return value
    return BROKEN.sub(replace, target), changed, unknown


def repair_xml(source_path: Path, target_path: Path, terms: list[tuple[str, str]]) -> tuple[int, list[dict[str, object]]]:
    source_raw = source_path.read_text(encoding="utf-8-sig")
    target_raw = target_path.read_text(encoding="utf-8-sig")
    sources: dict[str, list[str]] = {}
    for match in LEAF_XML.finditer(source_raw):
        sources.setdefault(match.group("tag"), []).append(html.unescape(match.group("text")))
    offsets: dict[str, int] = {}
    chunks: list[str] = []
    cursor = 0
    total = 0
    failures: list[dict[str, object]] = []
    for match in LEAF_XML.finditer(target_raw):
        tag = match.group("tag")
        offset = offsets.get(tag, 0)
        offsets[tag] = offset + 1
        source_values = sources.get(tag, [])
        target = match.group("text")
        repaired = target
        if BROKEN.search(target) and offset < len(source_values):
            repaired, count, unknown = repair_text(source_values[offset], target, terms)
            total += count
            if unknown:
                failures.append({"key": tag, "unknownTokenNumbers": unknown})
        chunks.append(target_raw[cursor:match.start("text")])
        chunks.append(repaired)
        cursor = match.end("text")
    chunks.append(target_raw[cursor:])
    if total:
        target_path.write_text("".join(chunks), encoding="utf-8", newline="")
    return total, failures


def repair_txt(source_path: Path, target_path: Path, terms: list[tuple[str, str]]) -> tuple[int, list[dict[str, object]]]:
    source_raw = source_path.read_text(encoding="utf-8-sig")
    target_raw = target_path.read_text(encoding="utf-8-sig")
    source_lines = source_raw.splitlines()
    target_lines = target_raw.splitlines()
    if len(source_lines) != len(target_lines):
        return 0, [{"path": str(target_path), "error": "line count differs"}]
    total = 0
    failures: list[dict[str, object]] = []
    for index, target in enumerate(target_lines):
        if not BROKEN.search(target):
            continue
        repaired, count, unknown = repair_text(source_lines[index], target, terms)
        target_lines[index] = repaired
        total += count
        if unknown:
            failures.append({"line": index + 1, "unknownTokenNumbers": unknown})
    if total:
        newline = "\r\n" if "\r\n" in target_raw else "\n"
        target_path.write_text(newline.join(target_lines) + (newline if target_raw.endswith(("\n", "\r")) else ""), encoding="utf-8", newline="")
    return total, failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--english", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    english = args.english.resolve()
    report: dict[str, object] = {"languages": {}}
    for language, (pack, folder) in LANGUAGES.items():
        terms = glossary(root / "FIP-Yes Man" / "Tools" / "translation-glossary.csv", language)
        target_root = root / pack / "Languages" / folder
        repaired = 0
        failures: list[dict[str, object]] = []
        for source_path in sorted(english.rglob("*")):
            if not source_path.is_file() or source_path.suffix.lower() not in {".xml", ".txt"}:
                continue
            relative = source_path.relative_to(english)
            target_path = target_root / relative
            if not target_path.is_file():
                continue
            if source_path.suffix.lower() == ".xml":
                count, found_failures = repair_xml(source_path, target_path, terms)
            else:
                count, found_failures = repair_txt(source_path, target_path, terms)
            repaired += count
            for failure in found_failures:
                failure["path"] = str(target_path.relative_to(root))
            failures.extend(found_failures)
        report["languages"][language] = {"repairedMarkers": repaired, "failures": failures}
        print(f"[{language}] repaired markers={repaired}, failures={len(failures)}")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
