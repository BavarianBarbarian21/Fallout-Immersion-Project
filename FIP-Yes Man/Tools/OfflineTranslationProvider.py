#!/usr/bin/env python3
"""Persistent JSON-lines bridge for local FIP machine-translation models."""

from __future__ import annotations

import argparse
import base64
import json
import os
import re
import sys
from pathlib import Path

from ftfy import fix_text


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--provider", choices=("argos", "opus"), required=True)
    parser.add_argument("--source", default="en")
    parser.add_argument("--target", default="zh")
    parser.add_argument("--argos-packages-dir", default="")
    parser.add_argument("--opus-model", default="Helsinki-NLP/opus-mt-en-zh")
    parser.add_argument("--model-cache-dir", default="")
    parser.add_argument("--opencc-config", default="")
    return parser.parse_args()


def load_argos(args: argparse.Namespace):
    if args.argos_packages_dir:
        os.environ["ARGOS_PACKAGES_DIR"] = str(Path(args.argos_packages_dir).resolve())
    os.environ.setdefault("ARGOS_DEVICE_TYPE", "cpu")
    os.environ.setdefault("ARGOS_INTER_THREADS", "1")
    os.environ.setdefault("ARGOS_INTRA_THREADS", str(max(1, (os.cpu_count() or 2) - 1)))
    os.environ.setdefault("ARGOS_BEAM_SIZE", "4")
    import ctranslate2
    from argostranslate import settings, translate

    installed = translate.get_installed_languages()
    source = next((language for language in installed if language.code == args.source), None)
    target = next((language for language in installed if language.code == args.target), None)
    if source is None or target is None:
        raise RuntimeError(f"Argos language package {args.source}->{args.target} is not installed")
    translation = source.get_translation(target)
    if translation is None:
        raise RuntimeError(f"Argos translation {args.source}->{args.target} is unavailable")
    package_translation = translation
    while hasattr(package_translation, "underlying"):
        package_translation = package_translation.underlying
    if not hasattr(package_translation, "pkg"):
        raise RuntimeError("Argos package translation could not be resolved")

    if package_translation.translator is None:
        package_translation.translator = ctranslate2.Translator(
            str(package_translation.pkg.package_path / "model"),
            device=settings.device,
            inter_threads=settings.inter_threads,
            intra_threads=settings.intra_threads,
            compute_type=settings.compute_type,
        )

    def translate_plain(texts: list[str]) -> list[str]:
        tokenized: list[list[str]] = []
        text_layouts: list[list[tuple[int, int]]] = []
        for text in texts:
            paragraph_layout: list[tuple[int, int]] = []
            for paragraph in text.split("\n"):
                start = len(tokenized)
                sentences = package_translation.sentencizer.split_sentences(paragraph) if paragraph else []
                tokenized.extend(package_translation.pkg.tokenizer.encode(sentence) for sentence in sentences)
                paragraph_layout.append((start, len(tokenized)))
            text_layouts.append(paragraph_layout)

        target_prefix = None
        if package_translation.pkg.target_prefix:
            target_prefix = [[package_translation.pkg.target_prefix]] * len(tokenized)
        translated = package_translation.translator.translate_batch(
            tokenized,
            target_prefix=target_prefix,
            replace_unknowns=True,
            max_batch_size=settings.batch_size,
            batch_type="tokens",
            beam_size=max(1, settings.beam_size),
            num_hypotheses=1,
            length_penalty=0.2,
            return_scores=True,
        ) if tokenized else []

        results: list[str] = []
        for paragraph_layout in text_layouts:
            paragraphs: list[str] = []
            for start, end in paragraph_layout:
                pieces: list[str] = []
                for item in translated[start:end]:
                    pieces.extend(item.hypotheses[0])
                value = package_translation.pkg.tokenizer.decode(pieces) if pieces else ""
                if package_translation.pkg.target_prefix and value.startswith(package_translation.pkg.target_prefix):
                    value = value[len(package_translation.pkg.target_prefix) :]
                paragraphs.append(value[1:] if value.startswith(" ") else value)
            results.append("\n".join(paragraphs))
        ass_tag = re.compile(r"\{\\fn[^{}]*(?:\\[A-Za-z0-9]+[^{}]*)?\}")
        return [
            ass_tag.sub("", fix_text(target)) if not ass_tag.search(source) else fix_text(target)
            for source, target in zip(texts, results)
        ]

    marker_pattern = re.compile(r"(__FIP_(?:TOKEN|TERM)_\d+__)", re.IGNORECASE)

    def run(texts: list[str]) -> list[str]:
        plain_segments: list[str] = []
        layouts: list[list[tuple[str, str | int]]] = []
        for text in texts:
            layout: list[tuple[str, str | int]] = []
            for part in marker_pattern.split(text):
                if not part:
                    continue
                if marker_pattern.fullmatch(part):
                    layout.append(("literal", part))
                elif part.strip():
                    layout.append(("translated", len(plain_segments)))
                    plain_segments.append(part)
                else:
                    layout.append(("literal", part))
            layouts.append(layout)

        translated_segments = translate_plain(plain_segments) if plain_segments else []
        return [
            "".join(
                str(value) if kind == "literal" else translated_segments[int(value)]
                for kind, value in layout
            )
            for layout in layouts
        ]

    if args.opencc_config:
        from opencc import OpenCC

        converter = OpenCC(args.opencc_config)

        def run_with_conversion(texts: list[str]) -> list[str]:
            return [converter.convert(text) for text in run(texts)]

        return run_with_conversion
    return run


def load_opus(args: argparse.Namespace):
    import torch
    from transformers import AutoModelForSeq2SeqLM, AutoTokenizer

    cache_dir = args.model_cache_dir or None
    tokenizer = AutoTokenizer.from_pretrained(args.opus_model, cache_dir=cache_dir)
    model = AutoModelForSeq2SeqLM.from_pretrained(args.opus_model, cache_dir=cache_dir)
    model.eval()

    def run(texts: list[str]) -> list[str]:
        results: list[str] = []
        for start in range(0, len(texts), 16):
            batch = texts[start : start + 16]
            encoded = tokenizer(batch, return_tensors="pt", padding=True, truncation=True, max_length=512)
            with torch.inference_mode():
                generated = model.generate(**encoded, max_length=512, num_beams=4, renormalize_logits=True)
            results.extend(tokenizer.batch_decode(generated, skip_special_tokens=True))
        return results

    return run


def main() -> int:
    if hasattr(sys.stdin, "reconfigure"):
        sys.stdin.reconfigure(encoding="utf-8")
        sys.stdout.reconfigure(encoding="utf-8")
    args = parse_args()
    translate_batch = load_argos(args) if args.provider == "argos" else load_opus(args)
    print(json.dumps({"ready": True, "provider": args.provider}), flush=True)
    for line in sys.stdin:
        try:
            request = json.loads(line)
            encoded_texts = request.get("texts_b64")
            if not isinstance(encoded_texts, list) or not all(isinstance(text, str) for text in encoded_texts):
                raise ValueError("request.texts_b64 must be a base64 string array")
            texts = [base64.b64decode(text).decode("utf-8") for text in encoded_texts]
            translations = translate_batch(texts)
            encoded_translations = [base64.b64encode(text.encode("utf-8")).decode("ascii") for text in translations]
            print(json.dumps({"translations_b64": encoded_translations}), flush=True)
        except Exception as error:
            print(json.dumps({"error": str(error)}, ensure_ascii=False), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
