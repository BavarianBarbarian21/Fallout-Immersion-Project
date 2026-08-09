# Arktos animal textures — Bighorner-balanced revision (2026-07-31)

> Superseded: This intermediate revision was replaced by the Erin's Fluffy Fauna / exact Vanilla horse revision documented in `Arktos_Animal_Texture_Prompts_2026-07-31_ErinStyle.md`. Its PNGs were removed from all LoadFolders and archived under `.work/texture-archive/2026-07-31-bighorner-balance`.

## Production mode

- Generator: built-in ImageGen, reference-guided `precise-object-edit` workflow.
- Binding style reference: `FCP_Bighorner_Bull_east.png` for anatomy, medium detail density, and line hierarchy.
- Source canvas: perfectly uniform `#00FF00` chroma background.
- Post-processing: soft-matte chroma removal with despill, followed by RGBA downscale to 256×256 using Lanczos.
- Runtime format: transparent east-facing `Graphic_Single` PNG.
- Line target at final resolution: rounded near-black outer contour around 4–5 pixels; internal contour and pattern lines around 2–3 pixels.

## Shared anatomy and detail constraints

Use the FCP Bighorner as the target balance: clearly readable chest, shoulder, belly, hindquarter, neck transition, and broad integrated limb lobes. Limbs remain fused into the RimWorld pawn silhouette; never draw thin free-standing legs, gaps between legs, detailed feet, or hooves. Use only a few broad internal anatomy lines and large color regions. Do not add individual hair strands, individual scales, pores, tiny wrinkles, wounds, micro-detail, text, or watermarks.

## Horse v4

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Horse/Arktos_Horse_v4.png`

References: previous Horse v3 edit target; FCP Bighorner for anatomy and line weight; VTE Horse for body proportions; FCP Radstag for roe-deer antler construction.

Final prompt specification:

> Preserve the right-facing horse identity and vanilla horse proportions. Give the torso the Bighorner's anatomical clarity: broad chest, shoulder, belly, hindquarter, and two integrated leg lobes without separate legs, gaps, feet, or hooves. Use a 4–5 pixel rounded near-black outer contour at 256×256 and 2–3 pixel internal contour lines. The mane is one solid geometric mass. The tail is one thick rounded geometric silhouette with exactly one uniform dark taupe-brown fill and no highlight, shadow, gradient, or secondary patch. Place two wild roe-buck antlers at close roots on the upper front of the skull immediately before the ears. Both follow the same right-facing side plane, sweep upward and slightly backward, and use only a few thick natural tines. The far antler is offset behind, partly occluded, and smaller through foreshortening. No frontal symmetrical spread, brain shape, bulbous tissue, or tumor base. Preserve the final antler overlap during the tail-only correction.

## Giant Mole Rat v4

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/GiantMoleRat/Arktos_GiantMoleRat_v4.png`

References: previous Giant Mole Rat v3; FCP Bighorner; FCP Mole Rat; VAE Camel only for the single hump.

Final prompt specification:

> Preserve the right-facing naked mole-rat identity, incisors, pointed tail, and enormous single dromedary-like pack hump. Replace the featureless blob anatomy with Bighorner-level structure: readable neck transition, shoulder, chest, belly, hindquarter, two broad integrated limb lobes, and three to five broad skin-fold lines. Do not draw separate legs, gaps, toes, claws, or feet. Use a continuous 4–5 pixel rounded near-black outer contour and 2–3 pixel internal lines at final size, with two or three broad pink-brown tonal regions at most. No tiny wrinkles, pores, wounds, or realistic skin texture.

## Bisotope v4

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_v4.png`

References: previous Bisotope v3; FCP Bighorner; VTE Bison.

Final prompt specification:

> Preserve the right-facing massive bison identity, large horn, dark furry head/neck/shoulders, naked dusty pink-brown rear half, and irregular fur-to-skin boundary. Use Bighorner-level anatomy: readable muzzle, jaw, neck, massive shoulder, chest, belly, hindquarter, and two broad integrated limb lobes without separate legs, gaps, feet, or hooves. Use only a few large fur clumps, never individual strands. The outer contour is rounded near-black at 4–5 pixels and internal contours 2–3 pixels at final size. Use two or three broad color regions and no pores, fine fur, tiny wrinkles, or micro-detail.

## Cottonmouth v4

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Cottonmouth/Arktos_Cottonmouth_v4.png`

> Preserve the compact S-curve, closed mouth, olive-brown body, black bands, cream lower jaw, and lack of a rattle. Thicken the continuous rounded near-black silhouette to 4–5 pixels at final size. Add only one broad darker underside/neck plane and at most two broad anatomical contour accents. Keep internal pattern lines around 2–3 pixels. No individual scales, speckles, teeth, tiny marks, extra coils, or realistic texture.

## Coral Snake v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/CoralSnake/Arktos_CoralSnake_v5.png`

> Preserve the compact S-curve, closed mouth, black head, and broad brick-red, black, and cream band sequence. Thicken the rounded near-black outer contour to 4–5 pixels and keep internal boundaries at 2–3 pixels at final size. Add only one broad darker underside/neck plane and at most two broad contour accents. No individual scales, texture, teeth, tiny marks, or extra coils.

## Copperhead v4

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Copperhead/Arktos_Copperhead_v4.png`

> Preserve the compact S-curve, closed mouth, simple rattle, copper-orange head, dusty-tan body, and broad chestnut hourglass pattern. Thicken the rounded near-black outer contour to 4–5 pixels and keep internal boundaries at 2–3 pixels at final size. Add only one broad darker underside/neck plane and at most two broad contour accents. No individual scales, speckles, teeth, tiny marks, or extra coils.

## Wasteland Indigo v5

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/WastelandIndigo/Arktos_WastelandIndigo_v5.png`

> Preserve the broad anaconda S-coil, closed mouth, indigo body, simple underside plane, and six or seven large near-black saddle blotches. Thicken the rounded near-black outer contour to 4–5 pixels and keep internal boundaries at 2–3 pixels at final size. Add no more than two broad anatomical contour accents. No individual scales, speckles, teeth, tiny marks, or extra coils.

## Superseded textures

The immediately preceding minimalist PNGs were removed from all active LoadFolders and moved to `.work/texture-archive/2026-07-31-too-minimal`. Earlier rejected high-resolution versions remain separately recoverable under `.work/texture-archive/2026-07-31-highres-rejected`.
