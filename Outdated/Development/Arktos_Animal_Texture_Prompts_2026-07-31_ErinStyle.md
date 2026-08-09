# Arktos animal textures — Erin style revision (2026-07-31)

> Partial supersession: Horse v5, Giant Mole Rat v5, and Bisotope v5 were replaced by thick-line v6 textures documented in `Arktos_Animal_Texture_Prompts_2026-07-31_ThickLine.md`. The four snake sections and snake outputs in this document remain current and were not modified.

## Sources and production mode

- Generator: built-in ImageGen, reference-guided `precise-object-edit` and `style-transfer` workflows.
- Primary style reference: Erin's Fluffy Fauna, Workshop ID `3521909777`, package ID `Erin.FluffyFauna`.
- Horse construction reference: Horse Breeds - Skin Variations, Workshop ID `2737378747`, package ID `booleanne.horsebreedvariations`.
- Horse body master: `HorseBrown1_east.png`, whose body construction matches the Vanilla horse.
- Source backdrop: uniform `#00FF00`; final sprites were chroma-keyed with soft matte/despill and reduced to transparent 256×256 PNGs.
- Erin-style rules used: clean RimWorld silhouettes, broad color and shadow regions, few purposeful internal lines, and no uniform heavy comic stroke around every body.
- These Workshop mods are artistic source references only. No new FIP hard dependency or conditional LoadFolder was introduced.

## Horse v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Horse/Arktos_Horse_v5.png`

ImageGen antler prompt:

> Draw only one paired set of two dark brown roe-buck antlers. The two close roots meet at the bottom center. Both antlers share the same right-facing side perspective and sweep upward and slightly backward. The near antler is fully visible with three or four broad natural tines; the far antler is smaller, offset behind, and partially occluded but still readable. Match Erin's Fluffy Fauna horn simplification. No horse, skull, ears, skin, base plate, brain shape, tumor tissue, bone-white color, realistic texture, or micro-detail. Use a uniform #00FF00 chroma background.

Final construction:

> Keep `HorseBrown1_east.png` itself as the body layer. Do not regenerate or redraw the body. Preserve its complete Vanilla/Horse-Breeds silhouette, head, ears, eye, mane, one-piece tail, legless underside, scale, placement, colors, and shading. Chroma-key and scale only the separately generated antler pair, then place it at the forehead before the ears. The antler overlay may cover its own attachment point but must not remove, reshape, or rescale a single body pixel.

## Giant Mole Rat v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/GiantMoleRat/Arktos_GiantMoleRat_v5.png`

> Restyle the existing Giant Mole Rat using Erin's Ferret and Opossum as binding drawing-rule references and FCP Mole Rat for the face. Preserve the naked pink-brown body, incisors, pointed tail, huge single dromedary pack hump, right-facing head, and integrated body lobes. Use Erin's restrained edge treatment, one broad smooth body gradient, and only three or four purposeful anatomy or skin-fold lines. No heavy continuous comic outline, free-standing legs, feet, many wrinkles, wounds, or realistic skin detail.

## Bisotope v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_v5.png`

> Use Erin's Bison east texture as the binding master for silhouette, anatomy, pose, tail, fused underside, edge treatment, and scale. Keep the head, neck, and shoulders heavily furred in dark brown; change the entire rear body to hairless dusty pink-brown skin. Separate fur from skin with one irregular broad boundary near the rear edge of the shoulders. Enlarge the horn moderately using the FCP Bighorner only for horn proportions. Add at most two broad rear skin-fold lines and a few large fur clumps. No free-standing legs, individual hair, pores, wounds, or micro-detail.

## Shared Erin snake construction

All four snakes use `ERN_gartersnake/gartersnake_east.png` as the binding master for silhouette, coil path, head, smooth pointed tail, placement, scale, and line treatment. Only their large color regions differ. No individual scales, speckles, teeth, open mouths, extra coils, or rattles are allowed.

### Cottonmouth v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Cottonmouth/Arktos_Cottonmouth_v5.png`

> Preserve Erin's complete snake line art. Use a muted dark olive-brown body with five or six broad near-black crossbands and one small cream lower-mouth patch. Keep the tail smooth and pointed.

### Coral Snake v6

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/CoralSnake/Arktos_CoralSnake_v6.png`

> Preserve Erin's complete snake line art. Use broad brick-red fields, black rings, narrow cream rings immediately beside the black, and a simple black head. Keep the tail smooth and pointed.

### Copperhead v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Copperhead/Arktos_Copperhead_v5.png`

> Preserve Erin's complete snake line art. Use a copper-orange head, dusty tan body, and five or six broad chestnut hourglass crossbands. The tail must remain the normal continuous Erin gartersnake tail: smooth, unsegmented, and tapering to one point. Absolutely no rattle, stacked tail rings, bulb, segmented tip, or other rattlesnake anatomy.

### Wasteland/Eastern Indigo v6

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/WastelandIndigo/Arktos_WastelandIndigo_v6.png`

> Preserve Erin's complete snake line art. Color the entire snake with one uninterrupted deep indigo body color from nose to tail. Apart from one simple black eye and minimal dark head line art, there may be no spots, saddle blotches, bands, rings, underside stripe, lighter belly, decorative pattern, or rattle.

## Superseded files

- Previous Bighorner-balanced revision: `.work/texture-archive/2026-07-31-bighorner-balance`.
- Previous overly minimal revision: `.work/texture-archive/2026-07-31-too-minimal`.
- Earlier rejected high-resolution revision: `.work/texture-archive/2026-07-31-highres-rejected`.

Copperhead v5 did not actually satisfy its documented no-rattle constraint. It was superseded by the corrected v6 texture documented in `Development/Arktos_Animal_Texture_Prompts_2026-07-31_CopperheadNoRattle.md`.
