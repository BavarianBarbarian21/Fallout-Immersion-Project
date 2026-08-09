# Arktos Bisotope — spotless sex-specific revision (2026-07-31)

## Scope and production mode

- Generator: built-in ImageGen, two separate `precise-object-edit` calls.
- Female binding target: Bisotope v7.
- Male binding target: the accepted female v8 sprite.
- Supporting references: Erin's Fluffy Fauna Bison for simple bison construction and FCP Bighorner bull for bold line weight and male horn mass.
- Outputs:
  - `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_Female_v8.png`
  - `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_Male_v8.png`
- Processing: uniform `#00FF00` chroma backdrop, soft-matte removal with despill and one-pixel edge contraction, then Lanczos reduction to RGBA 256×256.

## Female v8 final prompt

> Preserve Bisotope v7's east-facing identity, scale, legless RimWorld underside, dusty rose-peach naked rear, dark shaggy front, horn, face, and heavy near-black outline. Make only three changes: remove every differently colored patch so the complete naked rear is one uninterrupted flesh color; replace the pink tail-tip bulb with a compact dark-brown furry bison tuft using 2–3 broad points; and raise the central shoulder hump by roughly 12–18 percent into a broad imposing bison hump covered by the existing dark shaggy fur. Retain at most two simple anatomical crease lines. Use flat low-detail RimWorld rendering, a 4–5 px outer contour at final size, no visible legs, and no spots, mottling, extra horns, hair strands, gradients, wounds, or microdetail. Render on a perfectly uniform `#00FF00` background without shadows or grid.

## Male v8 final prompt

> Use female v8 as the binding master for the exact body, spotless skin, tail tuft, raised hump, shoulder boundary, scale, face placement, and heavy outline. Change only horns and upper-front-head fur. Give the male two readable cream bison horns: a near horn 35–50 percent longer, thick at the base and sweeping forward then upward, plus a slightly smaller far horn; use smooth bison curves, not antlers or tightly coiled ram horns. Add a solid darker chocolate-brown shaggy crown/forelock from the upper hump across the forehead and around the horn bases, with 4–6 broad points, without hiding the eye, ear, muzzle, or roots. Keep the rear uniform and spotless, the hump unchanged, the legless RimWorld silhouette, and a 4–5 px near-black outer contour. Render on a perfectly uniform `#00FF00` background without shadows or grid.

## XML integration

`bodyGraphicData` uses the male v8 texture. `femaleGraphicData` uses the female v8 texture for all three Bison life stages, with the Vanilla draw sizes and shadow data preserved explicitly.

The first life stage was later changed for both sexes to the shared hornless calf v9 texture documented in `Development/Arktos_Animal_Texture_Prompts_2026-07-31_BisotopeCalf.md`. The adult male and female v8 textures remain unchanged.

All three approved east sprites were later expanded into complete east/north/south `Graphic_Multi` sets. The six generated directional prompts and final filenames are documented in `Development/Arktos_Animal_Texture_Prompts_2026-07-31_BisotopeDirectional.md`.
