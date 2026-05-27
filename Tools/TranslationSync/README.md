# TranslationSync

`Invoke-TranslationSync.ps1` is a part-driven translation sync tool for the four translation mods you described:

- `part1` / `fip`: all `FIP-*` mods in this repository, excluding non-mod folders and translation folders.
- `part2` / `fcp`: all matching repositories from the `FalloutCollaborationProject` GitHub organization.
- `part3` / `compatible`: mods referenced from FIP `About.xml`, `LoadFolders.xml`, and XML patch metadata such as `MayRequire` and `IfModActive`, excluding DLC, FIP, and FCP package IDs.
- `part4` / `playset-other`: mods in a user-provided playset folder that are not already included in the first three parts.

## What it does

- Refreshes plain text mod-name lists under `mod-lists/`.
- Optionally fast-forwards the local FIP repository when you pass `-RefreshFipSource`.
- Optionally clones or updates FCP repositories into the configured cache root when you pass `-RefreshFcpSources`.
- Scans each source mod for:
  - `Languages/English/Keyed/*.xml`
  - `Languages/English/Strings/Names/*.txt`
  - translatable leaf nodes inside any `Defs/**/*.xml`, emitted as `DefInjected`
- Builds the XML leaf-name matcher from both the built-in fallback list and RimWorld source members marked with `MustTranslate` when `RimWorldRoot/Source` is available.
- Writes an English dummy baseline for each output translation mod.
- Updates every configured non-English locale so that:
  - unchanged English source keeps the existing translation,
  - unchanged English fallback entries that are still identical to English are machine-translated,
  - new English entries are inserted and machine-translated,
  - changed English entries are retranscribed from the new English source.

## Step By Step

1. The tool resolves the category mod list from `config.json` and the matching `mod-lists/*.txt` file.
2. It resolves each listed mod folder from the configured search roots for that category.
3. For each resolved mod it scans `Languages/English/Keyed/*.xml` and keeps those as `Keyed` outputs.
4. It scans `Languages/English/Strings/Names/*.txt` and keeps those as `Names` outputs.
5. It scans any existing `Languages/English/DefInjected/<DefType>/*.xml` files and reads their `<LanguageData>` nodes into translation entries.
6. It scans every `Defs/**/*.xml` file under the mod.
7. Each defs file is parsed as RimWorld def XML.
8. Each non-abstract def node with a `defName` is inspected recursively for translatable leaf nodes.
9. The extraction logic builds translation keys as `defName.path.to.node`, for example a label under a protectron def becomes a keyed entry under that def name.
10. Extracted entries are grouped by def type such as `AbilityDef`, `ThingDef`, or `PawnKindDef`.
11. Existing English `DefInjected` entries and extracted defs entries are merged into one generated file per def category.
12. The generated def category files are now named `FIP-Translation_<DefType>.xml`, for example `DefInjected/AbilityDef/FIP-Translation_AbilityDef.xml`.
13. New keys are appended in encounter order as the mod scan proceeds; if the same key appears again later, the later text replaces the earlier text without creating a duplicate node.
14. The tool writes the English baseline first.
15. The tool then updates each non-English locale by comparing current English against the previous English snapshot and the existing locale file.
16. Locale XML comments are intentionally stripped from generated output.
17. Progress is written to `Reports/current-sync-progress.txt`, while the per-mod counts are written to `Reports/last-sync-report.txt`.

## Runtime behavior

- Def XML parsing is incremental. The tool caches parsed `Defs` files using file length plus UTC write ticks.
- English `Keyed` files are normalized through the XML writer so generated output does not preserve source comments.
- English `Names` files are copied directly from source mods.
- Existing English `DefInjected` files are normalized into generated category files before extracted `Defs` fallback is merged in.
- Generated locale XML no longer preserves comment nodes from source or previous locale files.
- When `translation.provider` is enabled, non-English locale text is machine-translated through the configured backend with a small cache under `state/translation-sync-state.json`.
- If the configured provider changes, existing non-English locale files are refreshed on the next sync so stale output from the previous backend is not kept.
- Local FIP source refresh is optional and only happens when you pass `-RefreshFipSource`.
- FCP source refresh is optional and only happens when you pass `-RefreshFcpSources`.
- Part 3 currently means: external mods your FIP mods patch, depend on, or conditionally load against.

## Configuration

Edit `config.json` before first real use.

- `paths.languageTemplateRoot`: folder whose subdirectories define the languages to maintain.
- `paths.playsetModsRoot`: leave empty for now; set this later when you want the fourth category populated automatically.
- `paths.fcpCacheRoot`: local folder where FCP repositories are cloned or updated.
- `compatibility.additionalLookupRoots`: add extra mod library folders if compatibility targets are not under RimWorld or the FCP cache.
- `translation.provider`: set to `none` to disable machine translation, `google-gtx` to use the current default backend, or `mymemory` if you explicitly want the older public backend.
- `translation.timeoutSeconds`: per-request timeout for machine translation.

## Usage

Refresh the text lists only:

```powershell
.\Invoke-TranslationSync.ps1 -Command refresh-lists
```

Refresh lists and update the local FCP cache:

```powershell
.\Invoke-TranslationSync.ps1 -Command refresh-lists -RefreshFcpSources
```

Refresh lists and also fast-forward the local FIP repository:

```powershell
.\Invoke-TranslationSync.ps1 -Command refresh-lists -RefreshFipSource -RefreshFcpSources
```

Sync one category:

```powershell
.\Invoke-TranslationSync.ps1 -Command sync -Category part1 -RefreshLists
```

Sync one category for a specific mod only:

```powershell
.\Invoke-TranslationSync.ps1 -Command sync -Category part1 -IncludeMods FIP-Hubris -Languages English,German,ChineseSimplified
```

Sync everything:

```powershell
.\Invoke-TranslationSync.ps1 -Command sync-all -RefreshLists
```

Limit sync to specific locales plus English:

```powershell
.\Invoke-TranslationSync.ps1 -Command sync-all -Languages German,French
```

## Notes

- The generated output mod folders default to `Translations/Generated/*` and are created on first sync.
- `compatible-unresolved-packageIds.txt` is written when Part 3 can detect a referenced package ID but cannot map it to a local folder name.
- Comma-separated package-id lists from `LoadFolders.xml` and patch metadata are split into individual package IDs.
- Part 1 is configured to `loadAfter` all package IDs discovered across all category lists when you use `sync-all`, plus Parts 2-4.
- Some shells on this machine block `.ps1` execution by policy. If direct script invocation is denied, run the script through `powershell.exe -ExecutionPolicy Bypass` for that session or process.
- The current public machine-translation backends are network-dependent and should be treated as a best-effort first pass, not as a polished final localization pass.
- `google-gtx` is now the default because it preserves RimWorld placeholders far more reliably than the older MyMemory path and avoids the very small daily quota that caused partial output.
