# Anthrosonae inspiration for WestTek S'Lanter and S'Nuffy

This directory is a local reference snapshot of the installed RimWorld 1.6
version of **Anthrosonae**, copied on 2026-08-07 from Steam Workshop item
`2902258418` (`ATK.Anthrosonae`). It is deliberately stored under
`Guidelines/inspiration`; it is not a loadable FIP mod and nothing here is
referenced by `New-Mods/FIP-WestTek` at runtime.

The original mod credits Willow, Erin, and Taranchuk. No explicit license file
is present in the installed Workshop package. Treat the snapshot as local
research material. Do not publish, redistribute, or ship copied code or art in
FIP until the authors' reuse terms have been checked and any required
permission and attribution have been obtained.

## What Anthrosonae does

Anthrosonae is a Biotech xenotype and appearance framework. It defines fifteen
animal-styled human xenotypes:

- bear, cat, cow, deer, drake, fox, goat, griffin, horse, hyena, panda, rabbit,
  shark, red panda ("wah"), and wolf;
- the xenotypes are intended to remain functionally close to baseliner humans;
- each xenotype is assembled primarily from cosmetic fur/skin, ear, tail, horn,
  wing, and facial-overlay genes rather than animal-stat packages.

Biotech and Vanilla Expanded Framework are declared dependencies. The installed
package supports RimWorld 1.4, 1.5, and 1.6; this snapshot intentionally uses
the active 1.6 implementation plus the shared `Common` art selected for the
WestTek fauna lines.

### Definition and rendering pipeline

The implementation is layered:

1. `XenotypeDef` entries bundle species-specific cosmetic genes. The cat,
   rabbit, red-panda, wolf, hyena, and panda xenotypes use a fur gene, an ear
   gene, and a tail gene. Fox also adds a facial-stripe gene.
2. The abstract fur-gene base uses the custom C# class
   `Anthrosonae.FurGene`. It excludes conflicting vanilla skin, fur, body, and
   beard genes and normally cannot appear in random genepacks.
3. Each fur gene selects a species `FurDef`, forces a species-specific male or
   female `HeadTypeDef`, and lists its allowed `FurColorDef` presets.
4. Each `FurDef` maps six body forms—male, female, hulk, fat, thin, and
   child—with the child graphic also serving babies.
5. Ears, tails, horns, wings, and facial marks are separate gene render nodes.
   Their XML controls parent node, layer, offsets, rotation behavior, shader,
   and whether hair, skin, or custom coloring is used.
6. The body and head textures have directional south/east/north images. Most
   species bodies and heads also have corresponding `m` mask images for the
   two-channel `CutoutComplex` shader.

The result is a full-body animal silhouette rather than only human pawns with
ears and a tail. Apparel still renders through RimWorld's humanlike pawn system.

### Two-channel fur colors

The mod defines 22 basic color records and 70 named two-color presets. Species
genes provide weighted lists of suitable presets—for example red-panda, wolf,
hare, fox, leopard, and panda palettes—then a patch appends generic colors.

`FurGene` stores:

- the chosen `FurColorDef`;
- a primary color (`colorOne`);
- an optional secondary color (`colorTwo`).

On gene addition it chooses a preset by `selectionWeight`. Renderer Harmony
patches then rebuild body, head, fur, ear, and tail graphics with either
`Cutout` or `CutoutComplex`. In masked images, the two mask channels allow the
primary and secondary colors to affect different markings. The gene serializes
the preset and both colors into the save.

The code also adjusts colors for rotting pawns, shamblers, and mutant skin-color
overrides. `ApplyColors()` writes the secondary fur color into
`pawn.story.skinColorOverride` and invalidates all pawn graphics.

### Player customization

If a pawn has an active `FurGene`, Anthrosonae adds a **Change fur** action to
the Ideology styling station. A custom window provides:

- a live, rotatable pawn portrait without clothing or headgear;
- separate primary and secondary color channels;
- species palette swatches;
- HSV wheel, hue/saturation fields, brightness, and hexadecimal color input;
- accept/cancel behavior followed by a graphics refresh.

There is also optional Pawn Editor integration that exposes the same fur-color
window in that mod's appearance editor. A developer gizmo can open it directly.

### Xenotype menus, spawning, and compatibility

A `XenotypeExtension` marks all Anthrosonae xenotypes as one menu group. A
Harmony patch recognizes their icons in float menus, removes the individual
entries from the parent menu, and inserts a single nested **Anthrosona** entry.

XML patches add low Anthrosonae spawn chances to outlanders, pirates, refugees,
beggars, pilgrims, Odyssey traders/salvagers, Spacefarers, VFE Pirates, and VFE
Settlers. A separate pawn-kind patch handles space refugees and optional
Spacefarer combat roles.

Vanilla Expanded Framework toggles control:

- inheritability: disabled by default;
- unnatural fur colors: enabled by default;
- random genepack generation: disabled by default;
- debug colonist pawn kinds: disabled by default.

Compatibility patches register the cosmetic gene category with VRE Androids
and disable fur genes for Anomaly ghouls. Pawn generation reapplies saved fur
colors after the pawn is constructed.

### Hair content

The package carries fourteen three-direction hairstyles. Thirteen are direct
`HairDef` entries; one fringe is added only when Erin's Hairstyles 2 is absent.
These are not required by the fur system, but they are useful silhouette and
layering references for S'Nuffy and the female variants of the WestTek fauna
lines.

## What was copied

The `Original` directory preserves paths relative to the installed mod:

- all 1.6 C# source, project files, compiled DLL, XML defs, and XML patches;
- the English keyed strings, metadata, Workshop ID, preview, and
  `LoadFolders.xml`;
- complete body, head, ear, and tail art for cat, fox, hyena, panda, rabbit,
  red panda (`wah`), and wolf;
- the matching gene and xenotype icons;
- cat eye overlays and all facial overlays;
- all shared hairstyles.

This is 547 files totaling 6,619,973 bytes. See
[`COPIED-CONTENT.md`](COPIED-CONTENT.md) for the category counts and selection
rationale.

## Relevance to WestTek

The active WestTek implementation currently gives S'Lanter and S'Nuffy rounded
`CoonEars` plus one generic `FurryTail`. It does not replace the normal human
body or head, does not provide a raccoon mask, and couples ear color to skin
while tail color follows hair. Its S'Nuffy palette is implemented as separate
vanilla skin/hair override genes.

Anthrosonae demonstrates a more cohesive alternative:

- use one identity/fur gene to select body, head, palette, and renderer logic;
- keep ears, tail, and facial mask as modular render-node genes;
- use a two-channel body/head mask for a stable coat plus face/belly markings;
- keep palette selection on the pawn and serialize it;
- offer a styling-station recolor path without changing the xenotype;
- support every body type and life stage explicitly.

### Suggested visual mappings

| WestTek line | Best references | Why |
|---|---|---|
| S'Lanter | `Anthrowah`, `Anthrofox`, `Anthropanda` | Red-panda and fox shapes are the nearest procyonid/canine silhouettes; panda markings demonstrate strong two-color separation. A new raccoon mask and ringed tail would still be needed. |
| S'Nuffy | `Anthrowah_Female`, `Anthrocat_Female`, matching female heads, cat eyes, hairstyles | Provides a softer companion-strain silhouette while retaining a fur-capable full body and head. The final face should remain recognizably procyonine rather than feline. |
| B'Aja | `Anthrorabbit` | Complete rabbit body, female/male heads, long ears, short tail, icon, and hare palettes. |
| M'Erowi | `Anthrocat` | Complete feline body/head system, cat ears/tail, eye overlays, and leopard/snow-leopard masks. |
| R'Uffian | `Anthrowolf`, `Anthrohyena` | The installed mod has no dog-named set; wolf and hyena are the useful canine references. |

The current `WestTek_Gene_BAja`, `WestTek_Gene_MErowi`, and
`WestTek_Gene_RUffian` definitions all reference
`Things/Pawn/Humanlike/HeadAttachments/DogEars/DogEars`, but no matching
`DogEars` texture is present in the active WestTek texture tree. Only the
S'Lanter/S'Nuffy `CoonEars` and generic `FurryTail` sets are present. The copied
rabbit, cat, wolf, and hyena material therefore addresses a real missing-art
gap, not only a stylistic opportunity.

## Recommended integration approach

Do not add Anthrosonae as a runtime dependency merely to reuse its rendering
architecture. For a self-contained WestTek implementation:

1. Obtain reuse permission or create original FIP artwork using these files
   only as visual/technical references.
2. Create WestTek-prefixed `FurDef` and `HeadTypeDef` records for the desired
   fauna lines, covering female, male, hulk, fat, thin, child, and baby.
3. Create original south/east/north base images and `m` masks. For S'Lanter and
   S'Nuffy, reserve one channel for base coat and the other for eye mask,
   muzzle, belly, limb, and tail-ring markings.
4. Port only the minimal two-color state and renderer logic needed by WestTek.
   Replace all hard-coded `ATK` name checks with WestTek gene types or explicit
   mod extensions.
5. Decide whether WestTek really wants
   `pawn.story.skinColorOverride = colorTwo`; that Anthrosonae behavior can
   recolor other skin-driven nodes and may interact with existing S'Nuffy
   coloration genes.
6. Keep the separate ear, tail, and facial-mask genes so mutation code can add
   or remove parts independently.
7. Validate naked and clothed rendering in all rotations, body types, ages,
   crawling, corpses/rotting, mutants, portraits, and styling-station previews.
8. Replace the missing shared `DogEars` references with species-specific
   WestTek texture paths.

## Source-code caveats

The copied `Source` folder contains nineteen `.cs` files, but `Anthro.csproj`
explicitly compiles only fifteen. These loose files are not part of the shipped
project build:

- `ColorPatch.cs`
- `FurGenebackup.cs`
- `PawnRenderNode_ShaderFor_Patch.cs`
- `Properties/AssemblyInfo.cs`

This matters because `PawnRenderNode_ShaderFor_Patch.cs` calls a method that is
commented out in the active `FurGene.cs`, and `FurGenebackup.cs` declares a
second class with the same name. Copying every `.cs` file into a new SDK-style
project would therefore create errors or restore stale behavior. Use the
`<Compile Include=...>` list in `Anthro.csproj` as the authoritative source set.

Other points to review before adapting the code:

- renderer patches run at the lowest Harmony priority and replace graphics
  late in the pipeline, which can conflict with other appearance mods;
- graphics are requested with `Vector2.one` rather than preserving every
  original mesh size;
- several branches identify compatible parts by the `ATK` def-name prefix;
- the styling action requires Ideology's styling station;
- the menu grouping code identifies entries by icon texture and color, which
  is less robust than passing the xenotype identity directly;
- the XML toggle operations depend on Vanilla Expanded Framework;
- the installed source and the compiled DLL should be treated as a snapshot,
  not as an API contract.

## Important paths

- `Original/1.6/Source/FurGene.cs`: two-color storage and graphic rebuilding.
- `Original/1.6/Source/Window_ColorPicker.cs`: complete recolor interface.
- `Original/1.6/Source/PawnRenderNode_GraphicFor_Patch.cs`: general render-node
  integration.
- `Original/1.6/Defs/GeneDefs/GeneDefs_Cosmetic.xml`: fur and attachment gene
  architecture.
- `Original/1.6/Defs/FurDefs/FurDefs.xml`: body-type-to-texture mapping.
- `Original/1.6/Defs/HeadTypeDefs/HeadTypeDefs.xml`: gendered head mapping.
- `Original/1.6/Defs/FurColorDefs/`: base colors and two-color presets.
- `Original/Common/Textures/Things/Pawn/Humanlike/Anthrosonae/`: selected body,
  head, ear, tail, eye, and marking source art.

