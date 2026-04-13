# FIP Translation Part 1 Tooling

Run the generator from this folder:

```powershell
.\Generate-GermanLocale.ps1 -Clean
```

What it does:

- Copies existing English `Keyed` files into `Languages/German/Keyed` as first-pass placeholders.
- Copies existing English `Strings/Names` files into `Languages/German/Strings/Names`.
- Harvests `label` and `description` nodes from direct `Defs` XML.
- Resolves external patch targets against the current FCP repo, the RimWorld `Data` folder, and the local `FCP Mods` folder when those roots are available.
- Expands common patch selectors such as `li`, `li[key="..."]`, `li[@Class="..."]`, and `li[contains(text(),"...")]` into concrete DefInjected indices when the target def source exists locally.
- Harvests `label` and `description` replacements from supported `PatchOperation*` XML targets.
- Writes a coverage report to `Tools/Reports/generation-report.json`.

Current limitation:

- Wildcard patch targets for external mods that are not present in the lookup roots still cannot be mapped to a concrete DefInjected folder automatically.
- If an external target mod is not installed locally, the generator will keep those entries in the unsupported report instead of guessing the def type or list index.