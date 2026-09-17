#!/usr/bin/env python3
"""Install one or more English Argos packages into a project-local directory."""

from __future__ import annotations

import argparse
import os
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--packages-dir", required=True)
    parser.add_argument("--targets", nargs="+", default=["zh"])
    args = parser.parse_args()
    package_dir = Path(args.packages_dir).resolve()
    package_dir.mkdir(parents=True, exist_ok=True)
    os.environ["ARGOS_PACKAGES_DIR"] = str(package_dir)

    import argostranslate.package

    argostranslate.package.update_package_index()
    packages = argostranslate.package.get_available_packages()
    installed = argostranslate.package.get_installed_packages()
    for target in args.targets:
        if any(item.from_code == "en" and item.to_code == target for item in installed):
            print(f"Argos en->{target} is already installed in {package_dir}")
            continue
        package = next((item for item in packages if item.from_code == "en" and item.to_code == target), None)
        if package is None:
            raise RuntimeError(f"No Argos en->{target} package was found in the package index")
        downloaded = package.download()
        argostranslate.package.install_from_path(downloaded)
        print(f"Installed Argos en->{target} {package.package_version} in {package_dir}")
        installed = argostranslate.package.get_installed_packages()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
