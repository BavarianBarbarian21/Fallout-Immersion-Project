#!/usr/bin/env python3
"""Repair encoding, invented tags, placeholders, and line structure in a generated FIP pack."""

from __future__ import annotations

import argparse
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

from ftfy import fix_text


TOKEN = re.compile(r"\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[A-Za-z]|\\[nrt]")
ASS_TAG = re.compile(r"\{\\fn[^{}]*(?:\\[A-Za-z0-9]+[^{}]*)?\}")
MOJIBAKE_HINT = re.compile(r"[ÃÂ]|â€|â€™|â€œ|â€|ï¿½")
NONLINGUISTIC_VALUE = re.compile(r"\s*[\d.,+\-–—/%:() ]+\s*")
MANUAL_ENCODING_REPAIRS = {
    "Ã¥": "å",
    "Ã3": "ó",
    "à ̈": "è",
    "piÃ1": "più",
}
MANUAL_VALUE_REPAIRS = {
    "Przepisy zapewnią Państwu inwentaryzację przyczepy kempingowej co 10 dni:\n\n- 6NCRKlatka żywnościowa\n- � 1NCRPole Medic Crate":
        "Zapasy będą dodawać do ekwipunku karawany co 10 dni:\n\n- 6 skrzyń z żywnością NCR\n- 1 skrzynia medyka polowego NCR",
}


def clean_generated_text(text: str, source_text: str = "") -> tuple[str, int, bool]:
    repaired = fix_text(text)
    for broken, corrected in MANUAL_ENCODING_REPAIRS.items():
        repaired = repaired.replace(broken, corrected)
    repaired = MANUAL_VALUE_REPAIRS.get(repaired, repaired)
    encoding_changed = repaired != text
    removed = 0
    if not ASS_TAG.search(source_text):
        repaired, removed = ASS_TAG.subn("", repaired)
    return repaired, removed, encoding_changed


def replace_token_sequence(target: str, source_tokens: list[str]) -> str:
    iterator = iter(source_tokens)
    return TOKEN.sub(lambda _match: next(iterator), target)


def remove_unexpected_tokens(target: str, source_tokens: list[str]) -> tuple[str, bool]:
    target_tokens = TOKEN.findall(target)
    source_position = 0
    keep: list[bool] = []
    for token in target_tokens:
        matches_next = source_position < len(source_tokens) and token == source_tokens[source_position]
        keep.append(matches_next)
        if matches_next:
            source_position += 1
    if source_position != len(source_tokens):
        return target, False
    keep_iterator = iter(keep)
    return TOKEN.sub(lambda match: match.group(0) if next(keep_iterator) else "", target), True


def repair_xml(source_path: Path, target_path: Path, apply: bool) -> dict[str, int]:
    source_tree = ET.parse(source_path)
    target_tree = ET.parse(target_path)
    source_by_tag = {child.tag: child for child in source_tree.getroot() if isinstance(child.tag, str)}
    stats = {"encoding_files": 0, "ass_tags_removed": 0, "literal_values_restored": 0, "token_sequences_fixed": 0, "unresolved": 0, "unresolved_items": []}

    raw_target = target_path.read_text(encoding="utf-8-sig")
    if MOJIBAKE_HINT.search(raw_target):
        stats["encoding_files"] = 1

    for target_element in target_tree.getroot():
        if not isinstance(target_element.tag, str) or target_element.tag not in source_by_tag:
            continue
        source_element = source_by_tag[target_element.tag]
        if len(target_element) or len(source_element):
            continue
        source_text = source_element.text or ""
        target_text = target_element.text or ""
        cleaned, removed, _changed = clean_generated_text(target_text, source_text)
        stats["ass_tags_removed"] += removed
        if NONLINGUISTIC_VALUE.fullmatch(source_text) and cleaned != source_text:
            cleaned = source_text
            stats["literal_values_restored"] += 1
        source_tokens = TOKEN.findall(source_text)
        target_tokens = TOKEN.findall(cleaned)
        if source_tokens != target_tokens:
            if len(target_tokens) > len(source_tokens):
                cleaned, removed_extras = remove_unexpected_tokens(cleaned, source_tokens)
                if removed_extras:
                    target_tokens = TOKEN.findall(cleaned)
            if len(source_tokens) == len(target_tokens):
                cleaned = replace_token_sequence(cleaned, source_tokens)
                stats["token_sequences_fixed"] += 1
            else:
                stats["unresolved"] += 1
                stats["unresolved_items"].append(
                    {
                        "file": str(source_path),
                        "key": str(target_element.tag),
                        "source_tokens": source_tokens,
                        "target_tokens": target_tokens,
                    }
                )
        target_element.text = cleaned

    if apply:
        temporary = target_path.with_suffix(target_path.suffix + ".repairing")
        ET.indent(target_tree, space="  ")
        target_tree.write(temporary, encoding="utf-8", xml_declaration=True)
        temporary.replace(target_path)
    return stats


def repair_text(source_path: Path, target_path: Path, apply: bool, language: str) -> dict[str, int]:
    source = source_path.read_text(encoding="utf-8-sig")
    target = target_path.read_text(encoding="utf-8-sig")
    cleaned, removed, encoding_changed = clean_generated_text(target, source)
    source_lines = source.splitlines()
    target_lines = cleaned.splitlines()
    manual_terms_by_language = {
        "ChineseSimplified": {"zine": "小志"},
        "German": {"zine": "Fanzine"},
        "French": {"zine": "fanzine"},
        "Spanish": {"zine": "fanzine"},
        "PortugueseBrazilian": {"zine": "fanzine"},
        "Polish": {"zine": "zin"},
        "Italian": {"zine": "fanzine"},
        "Ukrainian": {"zine": "зін"},
        "Dutch": {"zine": "fanzine"},
        "Czech": {"zine": "fanzin"},
        "Japanese": {"zine": "ジン"},
        "Korean": {"zine": "진"},
        "Russian": {"zine": "зин"},
    }
    manual_terms = manual_terms_by_language.get(language, {})
    restored_lines = 0
    while len(target_lines) < len(source_lines):
        source_value = source_lines[len(target_lines)]
        target_lines.append(manual_terms.get(source_value.strip().lower(), source_value))
        restored_lines += 1
    cleaned = "\n".join(target_lines)
    source_has_final_newline = source.endswith(("\n", "\r"))
    cleaned = cleaned.rstrip("\r\n") + ("\n" if source_has_final_newline else "")
    if apply:
        temporary = target_path.with_suffix(target_path.suffix + ".repairing")
        temporary.write_text(cleaned, encoding="utf-8", newline="")
        temporary.replace(target_path)
    return {
        "encoding_files": int(encoding_changed),
        "ass_tags_removed": removed,
        "token_sequences_fixed": 0,
        "line_entries_restored": restored_lines,
        "unresolved": 0,
        "unresolved_items": [],
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-root", required=True)
    parser.add_argument("--target-root", required=True)
    parser.add_argument("--report", required=True)
    parser.add_argument("--language", default="ChineseSimplified")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()

    source_root = Path(args.source_root).resolve()
    target_root = Path(args.target_root).resolve()
    totals = {
        "files_checked": 0,
        "encoding_files": 0,
        "ass_tags_removed": 0,
        "literal_values_restored": 0,
        "token_sequences_fixed": 0,
        "line_entries_restored": 0,
        "unresolved": 0,
        "unresolved_items": [],
        "applied": args.apply,
    }
    for source_path in sorted(source_root.rglob("*")):
        if not source_path.is_file() or source_path.suffix.lower() not in {".xml", ".txt"}:
            continue
        target_path = target_root / source_path.relative_to(source_root)
        if not target_path.exists():
            totals["unresolved"] += 1
            continue
        result = (
            repair_xml(source_path, target_path, args.apply)
            if source_path.suffix.lower() == ".xml"
            else repair_text(source_path, target_path, args.apply, args.language)
        )
        totals["files_checked"] += 1
        for key, value in result.items():
            if key == "unresolved_items":
                totals[key].extend(value)
            else:
                totals[key] += value

    report_path = Path(args.report).resolve()
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(totals, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(totals, ensure_ascii=True, indent=2))
    return 1 if totals["unresolved"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
