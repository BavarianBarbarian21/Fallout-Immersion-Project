# FIP Translation Tools

This folder contains the shared updater tooling for the moved translation packs.

Launcher:
- Double-click [c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Start Translation Launcher.cmd](c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Start%20Translation%20Launcher.cmd) to open a clickable button panel for all updater actions.
- The launcher UI itself lives in [c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Tools/Start-FipTranslationLauncher.ps1](c:/Users/km-fei-mat/source/repos/FIP-Mods/FIP-Translation/Tools/Start-FipTranslationLauncher.ps1).
- The status line in the launcher reads updater status files under `Tools/state/launcher-status`. It now reports running, completed, and failed states for launched updater scripts.

Workflow:
- `English Sync` snapshots the current English pack for that part, refreshes the part source list, and rebuilds the English baseline only.
- `Language Translation` reuses the saved pre-sync English snapshot to detect changed entries and then updates the non-English locales without rewriting the English pack again.

Detailed sync steps:
- Read the category mod list and resolve each mod root.
- Gather `Keyed`, `Strings/Names`, existing `DefInjected`, and extracted translatable nodes from `Defs/**/*.xml`.
- Group extracted defs entries by def category such as `AbilityDef`, `ThingDef`, or `PawnKindDef`.
- Merge each def category into one generated file named `FIP-Translation_<DefType>.xml`.
- Write the English baseline first.
- Compare the new English baseline against the saved previous-English snapshot.
- Update each non-English locale by keeping still-valid translations and translating only changed or new English entries.
- Strip XML comments from generated locale files.
- Write progress details to `Reports/current-sync-progress.txt` and the per-mod count breakdown to `Reports/last-sync-report.txt`.

Current part behavior:
- Part 1 reads FIP mods from this repository.
- Part 2 refreshes the FCP source list and updates the local FCP cache during `English Sync` before rebuilding English.
- Part 3 still depends on compatibility lookup roots and package resolution from the shared config.
- Part 4 still depends on `paths.playsetModsRoot` in the local config.

One machine-specific question is still open: the extra local roots for Part 3 and the playset root for Part 4 on your other machine are not known in this workspace, so those two parts are scaffolded but not fully targeted yet.