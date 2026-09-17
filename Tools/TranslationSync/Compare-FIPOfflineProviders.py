#!/usr/bin/env python3
"""Compare Argos and OPUS-MT on a deterministic sample of the FIP corpus."""

from __future__ import annotations

import argparse
import csv
import json
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from types import SimpleNamespace

PLACEHOLDER = re.compile(r"\[[^\]]+\]|\{[^{}\r\n]+\}|%[A-Za-z]|\\[nrt]")
HAN = re.compile(r"[\u3400-\u4dbf\u4e00-\u9fff]")


def collect_units(english_root: Path) -> list[dict[str, str]]:
    units: list[dict[str, str]] = []
    for path in sorted(english_root.rglob("*")):
        if not path.is_file() or path.suffix.lower() not in {".xml", ".txt"}:
            continue
        relative = path.relative_to(english_root).as_posix()
        if path.suffix.lower() == ".xml":
            root = ET.parse(path).getroot()
            for child in root:
                for node in child.iter():
                    if node.text and node.text.strip():
                        units.append({"id": f"{relative}|{child.tag}", "text": node.text.strip()})
        else:
            for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
                if line.strip():
                    units.append({"id": f"{relative}|{number}", "text": line.strip()})
    return units


def sample_units(units: list[dict[str, str]], count: int) -> list[dict[str, str]]:
    eligible = [unit for unit in units if 4 <= len(unit["text"]) <= 450]
    protected = [unit for unit in eligible if PLACEHOLDER.search(unit["text"])]
    ordinary = [unit for unit in eligible if not PLACEHOLDER.search(unit["text"])]
    chosen = protected[: min(25, len(protected))]
    remaining = count - len(chosen)
    if remaining > 0 and ordinary:
        for index in range(remaining):
            position = round(index * (len(ordinary) - 1) / max(1, remaining - 1))
            chosen.append(ordinary[position])
    return chosen[:count]


def tokens(text: str) -> list[str]:
    return PLACEHOLDER.findall(text)


def metrics(source: str, translated: str) -> dict[str, object]:
    return {
        "identical": source == translated,
        "placeholder_ok": tokens(source) == tokens(translated),
        "han_characters": len(HAN.findall(translated)),
        "length_ratio": round(len(translated) / max(1, len(source)), 3),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--english-root", required=True)
    parser.add_argument("--python-tools", required=True)
    parser.add_argument("--argos-packages-dir", required=True)
    parser.add_argument("--hf-home", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--count", type=int, default=100)
    args = parser.parse_args()

    os.environ["ARGOS_PACKAGES_DIR"] = str(Path(args.argos_packages_dir).resolve())
    os.environ["HF_HOME"] = str(Path(args.hf_home).resolve())
    os.environ["ARGOS_DEVICE_TYPE"] = "cpu"
    os.environ["ARGOS_INTER_THREADS"] = "1"
    os.environ["ARGOS_INTRA_THREADS"] = str(max(1, (os.cpu_count() or 2) - 1))
    sys.path.insert(0, str(Path(args.python_tools).resolve()))
    from OfflineTranslationProvider import load_argos, load_opus

    sample = sample_units(collect_units(Path(args.english_root)), args.count)
    texts = [unit["text"] for unit in sample]
    argos = load_argos(SimpleNamespace(argos_packages_dir=args.argos_packages_dir, source="en", target="zh"))
    opus = load_opus(SimpleNamespace(model_cache_dir="", opus_model="Helsinki-NLP/opus-mt-en-zh"))
    argos_results = argos(texts)
    opus_results = opus(texts)

    rows: list[dict[str, object]] = []
    for unit, argos_text, opus_text in zip(sample, argos_results, opus_results):
        row: dict[str, object] = {
            "id": unit["id"],
            "source": unit["text"],
            "argos": argos_text,
            "opus": opus_text,
        }
        row.update({f"argos_{key}": value for key, value in metrics(unit["text"], argos_text).items()})
        row.update({f"opus_{key}": value for key, value in metrics(unit["text"], opus_text).items()})
        rows.append(row)

    output = Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)
    summary = {
        "sample_count": len(rows),
        "argos_identical": sum(bool(row["argos_identical"]) for row in rows),
        "opus_identical": sum(bool(row["opus_identical"]) for row in rows),
        "argos_placeholder_failures": sum(not bool(row["argos_placeholder_ok"]) for row in rows),
        "opus_placeholder_failures": sum(not bool(row["opus_placeholder_ok"]) for row in rows),
        "argos_without_han": sum(int(row["argos_han_characters"]) == 0 for row in rows),
        "opus_without_han": sum(int(row["opus_han_characters"]) == 0 for row in rows),
        "csv": str(output),
    }
    output.with_suffix(".json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(summary, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
