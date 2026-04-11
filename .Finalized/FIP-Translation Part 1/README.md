# FIP - Translation Part 1

This mod is the generated non-English language pack for the Fallout Immersion Project translation workspace.

## Source of truth

- Source content lives under `.Translation/FIP-*/Languages`.
- English stays in the base mods and is not copied into this pack.
- Non-English locale folders are rebuilt into this mod by `.Translation/Tools/Build-FIPTranslationPart1.ps1`.

## Build notes

Run the generator from PowerShell:

```powershell
powershell -ExecutionPolicy Bypass -File .\.Translation\Tools\Build-FIPTranslationPart1.ps1
```

The build emits:

- `Languages/` with merged locale content
- `BuildManifest.txt` with the source mod list, emitted locales, and any merge conflicts

If the manifest reports conflicts, inspect them before publishing the pack.
