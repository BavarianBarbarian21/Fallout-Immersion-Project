#!/usr/bin/env python3
"""Download and install the four Argos Translate models used by this tool."""

from __future__ import annotations

import os
import sys
from pathlib import Path


TOOL_ROOT = Path(__file__).resolve().parent
VENDOR_ROOT = TOOL_ROOT / "vendor"
MODEL_ROOT = TOOL_ROOT / "models"

sys.path.insert(0, str(VENDOR_ROOT))
os.environ.setdefault("XDG_DATA_HOME", str(MODEL_ROOT / "data"))
os.environ.setdefault("XDG_CONFIG_HOME", str(MODEL_ROOT / "config"))
os.environ.setdefault("XDG_CACHE_HOME", str(MODEL_ROOT / "cache"))
os.environ.setdefault(
    "ARGOS_PACKAGES_DIR", str(MODEL_ROOT / "data" / "argos-translate" / "packages")
)
os.environ.setdefault("ARGOS_DEVICE_TYPE", "cpu")

import argostranslate.package  # noqa: E402


TARGETS = {
    "ru": "Russian",
    "zh": "Chinese (Simplified; Traditional is derived locally with OpenCC)",
    "ja": "Japanese",
    "ko": "Korean",
}


def main() -> int:
    print("Updating the Argos Translate package index ...", flush=True)
    argostranslate.package.update_package_index()
    available = argostranslate.package.get_available_packages()
    installed = {
        (package.from_code, package.to_code)
        for package in argostranslate.package.get_installed_packages()
    }

    for target_code, target_name in TARGETS.items():
        if ("en", target_code) in installed:
            print(f"Already installed: English -> {target_name}", flush=True)
            continue
        candidates = [
            package
            for package in available
            if package.from_code == "en"
            and package.to_code == target_code
            and getattr(package, "type", "translate") == "translate"
        ]
        if not candidates:
            print(f"ERROR: no Argos model found for English -> {target_name}", flush=True)
            return 1
        package = candidates[0]
        print(
            f"Downloading English -> {target_name} "
            f"(version {package.package_version}) ...",
            flush=True,
        )
        download_path = package.download()
        print(f"Installing {download_path.name} ...", flush=True)
        argostranslate.package.install_from_path(download_path)
        print(f"Installed English -> {target_name}", flush=True)

    installed = {
        (package.from_code, package.to_code)
        for package in argostranslate.package.get_installed_packages()
    }
    missing = [code for code in TARGETS if ("en", code) not in installed]
    if missing:
        print("ERROR: missing models after installation: " + ", ".join(missing))
        return 1
    print("All required offline translation models are installed.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
