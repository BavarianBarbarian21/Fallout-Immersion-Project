# FIP 1.0 - final validation

Generated: 2026-08-12 18:36:31 +02:00

Overall result: **FAIL** - 84/86 checks passed.

This is a static release audit plus a full managed-code build. LoadFolder combinations are simulated from their declared conditions; an actual RimWorld GUI launch is not performed by this script.

## Release identity

| Status | Check | Details |
|---|---|---|
| PASS | Gameplay module count | 14 found; 14 expected |
| PASS | Translation module count | 4 found; 4 expected |
| PASS | No playable FIP-Sunset directory | Sunset content is integrated; Big MT owns its former identity |
| PASS | FIP-Arktos package ID | FIP.Arktos; expected FIP.Arktos |
| PASS | FIP-Big MT package ID | FIP.Sunset; expected FIP.Sunset |
| PASS | FIP-Corvega package ID | FIP.Corvega; expected FIP.Corvega |
| PASS | FIP-Donaustahl package ID | FIP.Donaustahl; expected FIP.Donaustahl |
| PASS | FIP-FutureTec package ID | FIP.FutureTec; expected FIP.FutureTec |
| PASS | FIP-Greenway package ID | FIP.Greenway; expected FIP.Greenway |
| PASS | FIP-H&HTools package ID | FIP.HHTools; expected FIP.HHTools |
| PASS | FIP-Hubris package ID | FIP.Hubris; expected FIP.Hubris |
| PASS | FIP-Lucky 38 package ID | FIP.Lucky38; expected FIP.Lucky38 |
| PASS | FIP-Poseidon package ID | FIP.Poseidon; expected FIP.Poseidon |
| PASS | FIP-Repconn package ID | FIP.Repconn; expected FIP.Repconn |
| PASS | FIP-RobCo package ID | FIP.RobCo; expected FIP.RobCo |
| PASS | FIP-WestTek package ID | FIP.WestTek; expected FIP.WestTek |
| PASS | FIP-Whitespring package ID | FIP.Whitespring; expected FIP.Whitespring |
| PASS | Package IDs are unique | 14 unique gameplay package IDs |
| PASS | Big MT Workshop identity | 3760676309; expected 3760676309 |
| PASS | Big MT display name | FIP - Big MT |

## XML

| Status | Check | Details |
|---|---|---|
| PASS | All release XML is well formed | 1519 files parsed; 0 invalid |

## Requirements

| Status | Check | Details |
|---|---|---|
| PASS | FIP-Arktos hard requirements | none |
| PASS | FIP-Big MT hard requirements | none |
| PASS | FIP-Corvega hard requirements | none |
| PASS | FIP-Donaustahl hard requirements | none |
| PASS | FIP-FutureTec hard requirements | none |
| PASS | FIP-Greenway hard requirements | Ludeon.RimWorld.Ideology |
| PASS | FIP-H&HTools hard requirements | none |
| PASS | FIP-Hubris hard requirements | none |
| PASS | FIP-Lucky 38 hard requirements | none |
| PASS | FIP-Poseidon hard requirements | none |
| PASS | FIP-Repconn hard requirements | Ludeon.RimWorld.Odyssey |
| PASS | FIP-RobCo hard requirements | Ludeon.RimWorld.Biotech |
| PASS | FIP-WestTek hard requirements | Ludeon.RimWorld.Biotech |
| PASS | FIP-Whitespring hard requirements | Ludeon.RimWorld.Royalty |
| PASS | Exactly five content requirement edges | FIP.Greenway -> Ludeon.RimWorld.Ideology; FIP.Repconn -> Ludeon.RimWorld.Odyssey; FIP.RobCo -> Ludeon.RimWorld.Biotech; FIP.WestTek -> Ludeon.RimWorld.Biotech; FIP.Whitespring -> Ludeon.RimWorld.Royalty |
| PASS | No hard Harmony requirement | Harmony is optional through LoadFolders |

## LoadFolders

| Status | Check | Details |
|---|---|---|
| PASS | Named nonempty base folders load first | 14 module-specific base folders |
| PASS | Every declared folder exists | all declared paths exist |
| PASS | No folder is named Base | 0 found |
| PASS | Conditions and static combination simulations | minimal, every optional entry, partial multi-mod exclusions and full-condition syntax passed |

## Ownership

| Status | Check | Details |
|---|---|---|
| PASS | Mechanoid Waiter plus RobCo belongs entirely to Lucky 38 | Lucky condition exact: True; RobCo duplicate references: 0 |
| PASS | Empire variants belong to Whitespring and are exclusive | exclusive conditions: True; Donaustahl Empire references: 0 |
| PASS | Big MT is safe without Anomaly | base gameplay defs: 0; optional conditions exact: True |
| PASS | Lucky 38 Props and Decor patch has an exact three-mod condition | condition exact: True; patch content split: True |
| PASS | No obsolete Sunset filenames or content references | files: 0; text references: 0 |
| PASS | No Combat Extended integration | 0 references |

## Storytellers

| Status | Check | Details |
|---|---|---|
| PASS | Visibility-patch folders have exact provider conditions | Donaustahl base plus six provider-specific optional folders |
| FAIL | Nine non-FCP storytellers are hidden exactly once by their owners | Cassandra: expected one exact false visibility contract in FIP-Donaustahl, found 0; Phoebe: expected one exact false visibility contract in FIP-Donaustahl, found 0; Randy: expected one exact false visibility contract in FIP-Donaustahl, found 0; VFEM_MaynardMedieval: expected one exact false visibility contract in FIP-H&HTools, found 0; VFET_TalonTribal: expected one exact false visibility contract in FIP-H&HTools, found 0; VFES_DD: expected one exact false visibility contract in FIP-H&HTools, found 0; VPE_Basilicus: expected one exact false visibility contract in FIP-Hubris, found 0; VFEE_AriadneArchduchess: expected one exact false visibility contract in FIP-Whitespring, found 0; VFED_Damocles: expected one exact false visibility contract in FIP-Whitespring, found 0 |
| PASS | Storyteller defs remain valid for DefOf and code references | 0 StorytellerDef deletion operations |
| PASS | FIP defines no replacement storytellers | FCP remains the sole content owner |
| PASS | FIP does not rename or redescribe hidden storytellers | 0 non-FCP StorytellerDef translation keys |
| PASS | All six FCP storytellers retain every shipped translation | 60 entries: 6 storytellers x label/description x 5 languages |

## Runtime schema

| Status | Check | Details |
|---|---|---|
| PASS | Big MT declares every conditional content provider in loadAfter | declared: Ludeon.RimWorld.Anomaly, VanillaExpanded.VAnomalyEInsanity, FIP.WestTek |
| PASS | Big MT PawnKindDefs use valid life-stage and WorkTags fields | life-stage minAge nodes: 0; invalid WorkTags:  |
| PASS | Big MT PawnKindDefs use SkillRange and prisoner ranges required by RimWorld 1.6 | legacy skill fields: 0; skill range nodes: 3; pawn kinds missing will/resistance: 0 |
| PASS | Big MT faction uses RimWorld 1.6 PawnGenOption dictionary syntax | legacy li options: 0; direct pawn-kind weights: 2 |
| PASS | Big MT humanlike faction supplies inherited and raid-generation requirements | FactionBase, backstory filter, raid loot curve and maximum pawn-cost curve present: True |
| PASS | Big MT research references the Anomaly EntityContainment Def | obsolete HoldingPlatform refs: 0; EntityContainment refs: 1 |

## Scenarios

| Status | Check | Details |
|---|---|---|
| PASS | Every visible FIP scenario has one complete English field bundle | 9 scenario contracts complete |
| PASS | Scenario identity is coherent across every visible field | all contract fields carry the same thematic identity |
| PASS | Every scenario language override has a direct ScenarioDef fallback | labels, descriptions, summaries and start dialogs are directly patched |

## Naming

| Status | Check | Details |
|---|---|---|
| PASS | Scenario and faction names begin uppercase | direct defs, patch fallbacks and English overrides passed |
| PASS | S'Lanter-family display spelling is canonical | S'Lanter, S'Lanters, S'Nuffy and S'Nuffies only |

## Ideology

| Status | Check | Details |
|---|---|---|
| FAIL | All Vanilla Memes Expanded origins stay internal but are hidden and excluded from random ideologies | family hide and zero-weight operation, faction random weights removed, Def deletion absent, retained preset references: 6; retained language keys: 18 |

## Research

| Status | Check | Details |
|---|---|---|
| PASS | VFE Tribals fence research gating is left untouched | no FIP fence-prerequisite override and no Tribals_Architect compatibility folder: True |
| PASS | Genetics is integrated below one canonical Vanilla Expanded tree without Cooking | Genetics-only folder, canonical tab, legacy tab removal, no Genetics_Cooking entry and optional ordering after all 16 tab providers: True |

## Assets

| Status | Check | Details |
|---|---|---|
| PASS | All textures are in unconditional module folders | 489 texture files; misplaced: 0; optional Texture directories: 0 |
| PASS | All FIP texture paths resolve with exact casing | 291 references / 133 unique; missing: 0; case mismatches: 0 |
| PASS | Numen cosmetics and Skinwalker head use validated invisible directional placeholders | six 128x128 fully transparent PNG contracts; invalid or missing: 0 |
| PASS | Skinwalker raccoon art replaces the human silhouette instead of overlaying it | FurDef body replacement for seven vanilla body types, transparent head, no AttachmentBody overlay, six directional art files: True |
| PASS | Overgrown use their own green Plantskin gene while Numen remain unfurred | Plantskin reuses Furskin body and head art with Skin color; only Overgrown carry it: True |
| PASS | Super mutants use WestTek heads and the custom naked body when Harmony is active | Hulk apparel compatibility, six WestTek heads and optional render replacement wired: True |

## Collisions

| Status | Check | Details |
|---|---|---|
| PASS | No cross-module direct Def identities | 1255 direct defs; 0 collisions |
| PASS | No cross-module English language keys | 3694 entries; 0 collisions |
| PASS | No cross-module concrete XPath plus field targets | 7988 target signatures; 0 collisions |
| PASS | Only documented root-XPath overlaps remain | Defs/FactionDef[defName="AncientsHostile"]; Defs/FactionDef[defName="Ancients"]; /Defs |

## Assemblies

| Status | Check | Details |
|---|---|---|
| PASS | No private 0Harmony.dll is bundled | 0 found |
| PASS | Harmony references are optional-only | base Harmony references: 0; optional Harmony assemblies: 3 |
| PASS | Assembly identities are unique | 27 assemblies; duplicate identities: 0 |
| PASS | Unique Harmony IDs and no unpatching | IDs: FIP.Lucky38.VanillaTradingExpanded, FIP.RobCo.SyntheticPawns, FIP.WestTek; Unpatch calls: 0 |

## Translations

| Status | Check | Details |
|---|---|---|
| PASS | All translation modules recognize FIP.Sunset as Big MT | 4 translation About files carry the documented identity |
| PASS | Translation loadAfter sets are aligned and exclude Harmony | 4 aligned source-order sets; technical Harmony library removed |
| PASS | No duplicate translated key signatures | Chinese Simplified/Traditional, Japanese, Korean and Russian passed |
| PASS | S'Lanter-family names retain canonical capitalization in every language | S'Lanter, S'Lanters, S'Nuffy and S'Nuffies only |

## Build

| Status | Check | Details |
|---|---|---|
| PASS | Managed solution builds | Skipped by caller; no build result recorded in this run |

## Documented non-colliding overlaps

- H&H Tools and WestTek both select the `Ancients` and `AncientsHostile` faction roots, but they modify different child fields: H&H Tools adds faction naming fields while WestTek adds `xenotypeSet`. The concrete XPath-plus-field audit confirms that these are not duplicate targets.
- Lucky 38 and WestTek both add distinct defs beneath `/Defs`: Lucky 38 adds cooking recipes while WestTek creates the one canonical `VanillaExpanded` research tab. Their concrete added fields and def identities do not overlap.
- `FIP.Sunset` in translation load order metadata intentionally means FIP Big MT in 1.0; it is not a surviving Sunset gameplay module.

## Test scope and residual release step

- Static minimum-load simulation was run for every gameplay module.
- Every single optional LoadFolder condition and every partial `IfModActiveAll` exclusion was evaluated.
- Storyteller ownership was checked down to exact visibility XPath, optional-provider condition, Def-preservation rule and translated key.
- All XML and all FIP-owned texture paths were checked across the complete release set.
- The managed solution was built and its staged assembly references were inspected.
- Before publishing, perform one manual RimWorld 1.6 smoke start with the intended installed mod set; that is the only runtime/UI check not reproducible in this repository-only validator.
