#!/usr/bin/env python3
"""Repair high-confidence English leftovers in every FIP language pack.

The script only changes entries whose current target is byte-for-byte equal to
the English source after XML entity decoding. Name/settlement lists and the
length-sensitive hacking dictionary are intentionally excluded.
"""

from __future__ import annotations

import argparse
import csv
import html
import json
import re
import shutil
import sys
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Callable


LANGUAGES = {
    "German": ("FIP-German Language Pack", "German", "deu_Latn"),
    "Spanish": ("FIP-Spanish Language Pack", "Spanish", "spa_Latn"),
    "French": ("FIP-French Language Pack", "French", "fra_Latn"),
    "PortugueseBrazilian": ("FIP-Brazilian Portuguese Language Pack", "PortugueseBrazilian", "por_Latn"),
    "Korean": ("FIP-Korean Language Pack", "Korean", "kor_Hang"),
    "Japanese": ("FIP-Japanese Language Pack", "Japanese", "jpn_Jpan"),
    "Polish": ("FIP-Polish Language Pack", "Polish", "pol_Latn"),
    "Italian": ("FIP-Italian Language Pack", "Italian", "ita_Latn"),
    "ChineseSimplified": ("FIP-Simplified Chinese Language Pack", "ChineseSimplified", "zho_Hans"),
    "ChineseTraditional": ("FIP-Traditional Chinese Language Pack", "ChineseTraditional", "zho_Hant"),
    "Russian": ("FIP-Russian Language Pack", "Russian", "rus_Cyrl"),
    "Ukrainian": ("FIP-Ukrainian Language Pack", "Ukrainian", "ukr_Cyrl"),
    "Dutch": ("FIP-Dutch Language Pack", "Dutch", "nld_Latn"),
    "Czech": ("FIP-Czech Language Pack", "Czech", "ces_Latn"),
}

# RimWorld language XML is flat. Restricting the body to text (no child tag)
# deliberately selects leaf entries instead of the outer LanguageData element.
FLAT_XML = re.compile(r"<(?P<tag>[^!?/\s][^>\s]*)>(?P<text>[^<]*?)</(?P=tag)>", re.DOTALL)
STRUCTURAL = re.compile(r"\r\n|\n|\r|\t|<[^<>]+>|\[[^\]]+\]|\{[^{}\r\n]+\}|%[A-Za-z]|\\[nrt]")
WORD = re.compile(r"[A-Za-z]+(?:['’-][A-Za-z]+)*")
ACRONYM = re.compile(r"\b(?:[A-Z]\.){2,}[A-Z]?\.?|\b[A-Z]{2,}[A-Z0-9-]*\b")
MIXED_CASE = re.compile(r"\b(?=[A-Za-z0-9-]*[A-Z])(?=[A-Za-z0-9-]*[a-z])(?=[A-Za-z0-9-]*[A-Z][A-Za-z0-9-]*[A-Z])[A-Za-z0-9-]+\b")
PROPER_SEQUENCE = re.compile(r"\b[A-Z][A-Za-z0-9'’.-]+(?:\s+(?:of|the|and|&))?\s+[A-Z][A-Za-z0-9'’.-]+(?:\s+[A-Z][A-Za-z0-9'’.-]+)*\b")
CANONICAL = re.compile(r"\b(?:Assaultron|Protectron|Securitron|Robobrain|Eyebot|Mister Handy|Mr\. Handy|Miss Nanny|Vault-Tec|Pip-Boy|RobCo|Nuka-Cola|Nuka World|Poseidon Energy|West Tek|GECK|G\.E\.C\.K\.|VATS|V\.A\.T\.S\.|FEV)\b", re.IGNORECASE)
SKIP_TXT_PARTS = {"names", "settlement_names", "hackingminigame"}


@dataclass
class Change:
    kind: str
    path: Path
    key: str
    source: str
    set_value: Callable[[str], None]


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")


def xml_escape(value: str) -> str:
    return value.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def likely_prose(text: str) -> bool:
    stripped = text.strip()
    words = WORD.findall(stripped)
    if not words:
        return False
    if len(words) >= 4:
        # Long title-cased strings are normally people, places, quests or brands.
        if all(w[:1].isupper() for w in words if w.lower() not in {"of", "the", "and", "in", "at"}) and not re.search(r"[.!?;:]", stripped):
            return False
        return True
    if re.search(r"[.!?;:]", stripped):
        return True
    if len(words) >= 2:
        # Lower-case words strongly distinguish ordinary UI text from proper names.
        return any(w == w.lower() and w.lower() not in {"of", "the", "and", "de", "la"} for w in words)
    return False


def txt_is_translatable(relative: Path) -> bool:
    lowered_parts = {part.lower() for part in relative.parts}
    if lowered_parts & SKIP_TXT_PARTS:
        return False
    stem = relative.stem.lower()
    if "name" in stem or stem.startswith("place"):
        return False
    return True


def collect_xml(source_path: Path, target_path: Path) -> tuple[str, list[Change]]:
    source_raw = read_text(source_path)
    target_raw = read_text(target_path)
    source_matches = list(FLAT_XML.finditer(source_raw))
    target_matches = list(FLAT_XML.finditer(target_raw))
    replacements: dict[int, str] = {}
    changes: list[Change] = []
    target_indexes: dict[str, list[int]] = {}
    for index, match in enumerate(target_matches):
        target_indexes.setdefault(match.group("tag"), []).append(index)
    target_offsets: dict[str, int] = {}
    for source_match in source_matches:
        tag = source_match.group("tag")
        offset = target_offsets.get(tag, 0)
        indexes = target_indexes.get(tag, [])
        if offset >= len(indexes):
            # A valid pack may deliberately use a self-closing element.
            continue
        index = indexes[offset]
        target_offsets[tag] = offset + 1
        target_match = target_matches[index]
        source = html.unescape(source_match.group("text"))
        target = html.unescape(target_match.group("text"))
        if source != target or not likely_prose(source):
            continue
        def setter(value: str, i=index) -> None:
            replacements[i] = xml_escape(value)
        changes.append(Change("xml", target_path, tag, source, setter))

    def render() -> str:
        if not replacements:
            return target_raw
        chunks: list[str] = []
        cursor = 0
        for index, match in enumerate(target_matches):
            chunks.append(target_raw[cursor:match.start("text")])
            chunks.append(replacements.get(index, match.group("text")))
            cursor = match.end("text")
        chunks.append(target_raw[cursor:])
        return "".join(chunks)
    for change in changes:
        setattr(change, "render", render)
    return target_raw, changes


def collect_txt(source_path: Path, target_path: Path, relative: Path) -> tuple[str, list[Change]]:
    target_raw = read_text(target_path)
    if not txt_is_translatable(relative):
        return target_raw, []
    source_lines = read_text(source_path).splitlines()
    target_lines = target_raw.splitlines()
    if len(source_lines) != len(target_lines):
        raise ValueError(f"Text line count differs: {target_path}")
    replacements: dict[int, str] = {}
    changes: list[Change] = []
    for index, (source_line, target_line) in enumerate(zip(source_lines, target_lines)):
        source = source_line.strip()
        # Isolated word-list entries are too ambiguous for sentence models
        # (for example English "den" can become Spanish "breakfast"). Only
        # repair phrase-like leftovers automatically; single canonical terms
        # remain available for human glossary review.
        if not source or source != target_line.strip() or not likely_prose(source):
            continue
        def setter(value: str, i=index) -> None:
            leading = target_lines[i][:len(target_lines[i]) - len(target_lines[i].lstrip())]
            trailing = target_lines[i][len(target_lines[i].rstrip()):]
            replacements[i] = leading + value + trailing
        changes.append(Change("txt", target_path, str(index + 1), source, setter))

    newline = "\r\n" if "\r\n" in target_raw else "\n"
    terminal_newline = target_raw.endswith(("\n", "\r"))
    def render() -> str:
        lines = [replacements.get(i, line) for i, line in enumerate(target_lines)]
        return newline.join(lines) + (newline if terminal_newline else "")
    for change in changes:
        setattr(change, "render", render)
    return target_raw, changes


def load_glossary(path: Path, language: str) -> list[tuple[str, str]]:
    entries: list[tuple[str, str]] = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        for row in csv.DictReader(handle):
            source = (row.get("source") or "").strip()
            if not source:
                continue
            if (row.get("keep_original") or "").lower() == "true":
                target = source
            else:
                target = (row.get(language) or source).strip() or source
            entries.append((source, target))
    return sorted(entries, key=lambda item: len(item[0]), reverse=True)


def protect(text: str, glossary: list[tuple[str, str]]) -> tuple[str, dict[str, str]]:
    spans: list[tuple[int, int, str]] = []
    for pattern in (STRUCTURAL, ACRONYM, MIXED_CASE, PROPER_SEQUENCE, CANONICAL):
        spans.extend((m.start(), m.end(), m.group(0)) for m in pattern.finditer(text))
    for source, target in glossary:
        spans.extend((m.start(), m.end(), target) for m in re.finditer(re.escape(source), text, re.IGNORECASE))
    # Prefer the longest match at a position and discard overlaps.
    selected: list[tuple[int, int, str]] = []
    for start, end, value in sorted(spans, key=lambda item: (item[0], -(item[1] - item[0]))):
        if selected and start < selected[-1][1]:
            continue
        selected.append((start, end, value))
    mapping: dict[str, str] = {}
    chunks: list[str] = []
    cursor = 0
    for number, (start, end, value) in enumerate(selected):
        token = f"ZXQFIPTOKEN{number:04d}ZXQ"
        chunks.extend((text[cursor:start], token))
        mapping[token] = value
        cursor = end
    chunks.append(text[cursor:])
    return "".join(chunks), mapping


def restore(translated: str, mapping: dict[str, str]) -> str:
    result = translated
    for token, value in mapping.items():
        if result.count(token) != 1:
            raise ValueError(f"Protected token was changed or duplicated: {token}")
        result = result.replace(token, value)
    return result


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--english", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    parser.add_argument("--languages", nargs="*", choices=sorted(LANGUAGES), default=list(LANGUAGES))
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--batch-size", type=int, default=16)
    parser.add_argument("--report", type=Path, required=True)
    args = parser.parse_args()

    root = args.root.resolve()
    english = args.english.resolve()
    glossary_path = root / "FIP-Yes Man" / "Tools" / "translation-glossary.csv"
    report: dict[str, object] = {"started": time.strftime("%Y-%m-%dT%H:%M:%S"), "dryRun": args.dry_run, "languages": {}}

    tokenizer = model = torch = None
    if not args.dry_run:
        import torch as torch_module
        from transformers import AutoModelForSeq2SeqLM, AutoTokenizer
        torch = torch_module
        tokenizer = AutoTokenizer.from_pretrained(args.model, local_files_only=True)
        model = AutoModelForSeq2SeqLM.from_pretrained(args.model, local_files_only=True)
        model.eval()

    for language in args.languages:
        pack_name, folder_name, nllb_code = LANGUAGES[language]
        target_root = root / pack_name / "Languages" / folder_name
        if not target_root.is_dir():
            raise FileNotFoundError(target_root)
        changes: list[Change] = []
        originals: dict[Path, str] = {}
        for source_path in sorted(english.rglob("*")):
            if not source_path.is_file() or source_path.suffix.lower() not in {".xml", ".txt"}:
                continue
            relative = source_path.relative_to(english)
            target_path = target_root / relative
            if not target_path.is_file():
                raise FileNotFoundError(target_path)
            if source_path.suffix.lower() == ".xml":
                original, found = collect_xml(source_path, target_path)
            else:
                original, found = collect_txt(source_path, target_path, relative)
            if found:
                originals[target_path] = original
                changes.extend(found)

        info = {"candidates": len(changes), "xml": sum(c.kind == "xml" for c in changes), "txt": sum(c.kind == "txt" for c in changes), "changed": 0, "unchangedByModel": 0, "failures": []}
        report["languages"][language] = info
        print(f"[{language}] candidates={len(changes)} xml={info['xml']} txt={info['txt']}", flush=True)
        if args.dry_run or not changes:
            continue

        glossary = load_glossary(glossary_path, language)
        prepared: list[str] = []
        mappings: list[dict[str, str]] = []
        for change in changes:
            protected, mapping = protect(change.source, glossary)
            prepared.append(protected)
            mappings.append(mapping)

        tokenizer.src_lang = "eng_Latn"
        bos = tokenizer.convert_tokens_to_ids(nllb_code)
        for start in range(0, len(changes), args.batch_size):
            batch_texts = prepared[start:start + args.batch_size]
            encoded = tokenizer(batch_texts, return_tensors="pt", padding=True, truncation=True, max_length=512)
            with torch.inference_mode():
                generated = model.generate(**encoded, forced_bos_token_id=bos, max_new_tokens=512, num_beams=1)
            outputs = tokenizer.batch_decode(generated, skip_special_tokens=True)
            for offset, output in enumerate(outputs):
                index = start + offset
                change = changes[index]
                try:
                    translated = restore(output.strip(), mappings[index]).strip()
                    if not translated or translated == change.source:
                        info["unchangedByModel"] += 1
                        continue
                    if [m.group(0) for m in STRUCTURAL.finditer(translated)] != [m.group(0) for m in STRUCTURAL.finditer(change.source)]:
                        raise ValueError("structural placeholder sequence differs after restoration")
                    change.set_value(translated)
                    info["changed"] += 1
                except Exception as exc:
                    info["failures"].append({"path": str(change.path.relative_to(root)), "key": change.key, "error": str(exc)})
            print(f"[{language}] {min(start + len(batch_texts), len(changes))}/{len(changes)}", flush=True)

        for path in originals:
            path.write_text(getattr(next(c for c in changes if c.path == path), "render")(), encoding="utf-8", newline="")
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

    report["completed"] = time.strftime("%Y-%m-%dT%H:%M:%S")
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Report: {args.report}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
