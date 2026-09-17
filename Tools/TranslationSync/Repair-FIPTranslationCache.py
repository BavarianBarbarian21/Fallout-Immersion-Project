#!/usr/bin/env python3
"""Repair mojibake and invented subtitle formatting in the persistent FIP cache."""

from __future__ import annotations

import argparse
import json
import re
from pathlib import Path

from ftfy import fix_text


ASS_TAG = re.compile(r"\{\\fn[^{}]*(?:\\[A-Za-z0-9]+[^{}]*)?\}")
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


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--cache", required=True)
    args = parser.parse_args()
    cache_path = Path(args.cache).resolve()
    cache = json.loads(cache_path.read_text(encoding="utf-8-sig"))
    changed = 0
    for key, value in list(cache.items()):
        if not isinstance(value, str):
            continue
        repaired = ASS_TAG.sub("", fix_text(value))
        for broken, corrected in MANUAL_ENCODING_REPAIRS.items():
            repaired = repaired.replace(broken, corrected)
        repaired = MANUAL_VALUE_REPAIRS.get(repaired, repaired)
        if repaired != value:
            cache[key] = repaired
            changed += 1
    backup = cache_path.with_suffix(cache_path.suffix + ".pre-unicode-repair.bak")
    if not backup.exists():
        backup.write_bytes(cache_path.read_bytes())
    temporary = cache_path.with_suffix(cache_path.suffix + ".repairing")
    temporary.write_text(json.dumps(cache, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    temporary.replace(cache_path)
    print(json.dumps({"entries": len(cache), "changed": changed, "backup": str(backup)}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
