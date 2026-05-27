# FIP Translation Tools

This folder contains the shared updater tooling for the moved translation packs.

Launcher:
- Double-click [c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Start Translation Launcher.cmd](c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Start%20Translation%20Launcher.cmd) to open a clickable button panel for all updater actions.
- The launcher UI itself lives in [c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Tools/Start-FipTranslationLauncher.ps1](c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Tools/Start-FipTranslationLauncher.ps1).

Workflow:
- `English Sync` snapshots the current English pack for that part, refreshes the part source list, and rebuilds the English baseline only.
- `Language Translation` reuses the saved pre-sync English snapshot to detect changed entries and then updates the non-English locales without rewriting the English pack again.

Current part behavior:
- Part 1 reads FIP mods from this repository.
- Part 2 refreshes the FCP source list and updates the local FCP cache during `English Sync` before rebuilding English.
- Part 3 still depends on compatibility lookup roots and package resolution from the shared config.
- Part 4 still depends on `paths.playsetModsRoot` in the local config.

One machine-specific question is still open: the extra local roots for Part 3 and the playset root for Part 4 on your other machine are not known in this workspace, so those two parts are scaffolded but not fully targeted yet.