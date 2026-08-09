# FIP 1.0 – Initial inventory

This report records the state of the copied release workspace before the structural refactor. It is intentionally not a description of the desired final state.

## Release workspace

- 14 playable modules, including the new Big MT placeholder.
- 4 downstream translation modules.
- 1,466 well-formed XML files at baseline.
- No copied `FIP-Sunset` directory.
- Big MT already owns package ID `FIP.Sunset` and Workshop ID `3760676309`.
- The copied source modules match the current `New-Mods` files byte-for-byte.

## Current hard dependencies requiring correction

| Module | Current hard dependencies | Required final state |
|---|---|---|
| Arktos | H&H Tools | none |
| Greenway | Ideology, H&H Tools | Ideology only |
| Hubris | Royalty | none |
| Lucky 38 | Harmony | none after safe Harmony isolation |
| Repconn | Odyssey | Odyssey |
| RobCo | Harmony, Biotech | Biotech only after safe Harmony isolation |
| WestTek | Harmony, Biotech, H&H Tools | Biotech only after safe Harmony isolation |
| Whitespring | Royalty | Royalty |

All other playable modules currently declare no hard dependencies.

## Current base-folder state

Twelve copied gameplay modules still use `LoadFolders/Base`. Big MT already uses `LoadFolders/BigMT`. Corvega has no unconditional folder at all. H&H Tools additionally uses two always-loaded equipment folders, `Equipment_Core` and `Equipment_FIP_HHTools`, which should be merged into its module folder.

Final module-folder names are defined in `REFACTOR_PROMPT.md`.

## Current texture roots

Most textures are already in each mod's old `Base` folder. Two exceptions need structural work:

- Lucky 38 has additional textures under `Plants_VBrewECandT`.
- Sunset reference content has textures in its former unconditional folder and will be imported into H&H Tools.

All final texture roots must live below the owning module's unconditional module folder.

## Current assemblies

- Arktos: 15 biome worker assemblies.
- Greenway: 1 assembly.
- H&H Tools: 1 assembly.
- Lucky 38: 1 Harmony-dependent assembly.
- RobCo: 1 mixed core/Harmony assembly.
- WestTek: 1 mixed core/Harmony assembly.

No private `0Harmony.dll` is present. Lucky 38 can likely make its complete DLL optional. RobCo and WestTek need source-level separation because their main assemblies contain both core Def classes and Harmony patches.

## Confirmed ownership/migration issues

### Sunset

Sunset has no direct saved Defs. It consists of language overrides, PatchOperations and 15 textures for Medieval 2, Settlers and Tribals. All of it must be imported into H&H Tools.

### Big MT

The release placeholder is safe without Anomaly. The older WIP Big MT reference contains Anomaly language overrides and WestTek combination Defs. These must load only through `Anomaly` and `Anomaly_WestTek` folders.

### Anomaly and Insanity

H&H Tools currently contains:

- Horax-cult faction integration,
- Anomaly equipment tags and retained faction pools,
- Insanity equipment tags.

These complete integrations must move to Big MT rather than remaining split across both modules.

### Empire and Donaustahl

Donaustahl currently overrides ten Empire language keys already owned by Whitespring: eight ThingDef descriptions and two permit strings. They are Saturnite variants of the Whitespring text and must become a mutually exclusive Whitespring/Donaustahl combination.

### Mechanoid Waiter

Lucky 38 currently has both a single-Mod `MechanoidWaiter` language folder and the correct RobCo combinations. The single-Mod language file must join the complete `MechanoidWaiter_RobCo` integration so no waiter replacement occurs without RobCo.

### Gravship and Skills

The current Skills patch changes only the three Gravship expertise labels and descriptions to Hellion terminology. It remains a complete Repconn-owned `Gravship_Skills` combination even though general Skills ownership belongs to Donaustahl.

### Frameworks

- Harmony is used as a patch engine but is not modified.
- Vanilla Expanded Framework types are consumed by content integrations, but no FIP module owns or patches the framework core merely for using those types.
- Vehicle Framework is assigned to Corvega as an optional transportation integration.
- Vanilla Base Generation Expanded is assigned to H&H Tools as an optional faction/base-generation integration.

## Baseline validation risks

- Generated H&H Tools folder/file names exceed traditional Windows path limits.
- Many optional folder names expose Package IDs instead of readable feature names.
- Some modules have multiple LoadFolder entries for the same exact condition and should merge them.
- Corvega can have no loaded content when no vehicle target is active.
- Optional FIP references currently live behind hard FIP dependencies in Arktos, Greenway and WestTek.
- Translation load-order metadata still treats `FIP.Sunset` as the old Sunset content rather than Big MT.

