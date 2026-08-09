# Arktos animal textures — minimalist correction (2026-07-31)

> Superseded: These textures were judged too minimal and were replaced by the Bighorner-balanced revision documented in `Arktos_Animal_Texture_Prompts_2026-07-31_BighornerBalance.md`. The former active PNGs are archived under `.work/texture-archive/2026-07-31-too-minimal` and are no longer present in any LoadFolder.

## Production mode

- Generator: built-in ImageGen (`image_gen`), reference-guided raster generation.
- Generation canvas: square image with solid `#00FF00` chroma background.
- Post-processing: chroma removal with soft matte and despill, then RGBA downscale to exactly 256×256 with Lanczos resampling.
- Runtime format: transparent PNG, one east-facing `Graphic_Single` texture per animal.
- Style target: deliberately low-detail RimWorld pawn line art. Thick rounded near-black outline, large geometric shapes, flat fills, no gradients, no tiny surface detail, no independently drawn legs or feet. Locomotion is represented only by shallow body humps.

## Global negative constraints

Do not add realistic anatomy, visible legs, feet, hooves, toes, individual hairs, individual scales, wounds, pores, scars, highlights, shadows, gradients, painterly texture, micro-detail, or any feature smaller than roughly six pixels at the final 256×256 size. Keep the silhouette readable when viewed as a small game pawn.

## Horse

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Horse/Arktos_Horse_v3.png`

References:

- VTE Horse east texture as the strict body and line-art master.
- FCP Radstag east texture only as a loose reference for antler silhouette.

Final prompt:

> Create one east-facing 256×256 RimWorld animal pawn sprite on a perfectly flat #00FF00 background. Treat the supplied vanilla-style horse as the strict master for scale, pose, proportions, thick rounded near-black outline and minimal geometric construction. The horse must have no separately visible legs, hooves or feet; show locomotion only as two shallow rounded humps under the continuous body silhouette. Use one flat muted body fill. Draw the mane as one simple geometric mass and the tail as one thick, rounded, solid single-color shape with no individual hair strands and no light/dark subdivision. Add two thin, irregular, wild-growing roe-deer-like antlers emerging from the front/top of the forehead. The antlers should branch naturally forward and upward, remain visually separate, and must not have bulbous roots, tumors, pink tissue or a brain-like mass. No wounds, scars, muscles, fur texture, shading, gradients or details smaller than six pixels. Preserve generous empty chroma space around the pawn.

## Giant Mole Rat

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/GiantMoleRat/Arktos_GiantMoleRat_v3.png`

References:

- FCP Mole Rat east texture as the strict head, face and line-art master.
- VAE Camel east texture only as a loose reference for a single pack-animal hump.

Final prompt:

> Create one east-facing 256×256 RimWorld animal pawn sprite on a perfectly flat #00FF00 background. Use the supplied FCP mole rat as the strict master for head, face, thick rounded outline and simplified pawn language. Turn the body into a larger wasteland pack animal with one enormous smooth dromedary-like hump. Do not draw separate legs, feet, toes or claws; the underside must be a continuous rounded pawn silhouette with only two shallow locomotion humps. Use one flat muted pink-brown body fill and at most three broad graphic wrinkle lines. No wounds, scars, pores, skin texture, shading, gradients or micro-detail. Preserve generous empty chroma space.

## Bisotope

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_v3.png`

References:

- VTE Bison east texture as the strict body, pose and line-art master.
- FCP Bighorner bull east texture only as a loose reference for larger horn proportions.

Final prompt:

> Create one east-facing 256×256 RimWorld animal pawn sprite on a perfectly flat #00FF00 background. Use the supplied vanilla-style bison as the strict master for pose, proportions, thick rounded near-black outline and low-detail geometric construction. No independently visible legs, hooves or feet; use only shallow body humps beneath a continuous silhouette. Keep the head and shoulders as one dark shaggy mass, representing fur with only a few large angular edge notches, never individual hairs. Make the rear half hairless and smooth with one flat dusty pink-brown fill and at most two broad wrinkle lines. Separate the furry front from the naked rear with one simple three-to-four-point zigzag boundary. Give the animal a larger, simple flat horn silhouette inspired by the bighorner reference. No wounds, pores, fine fur, shadows, gradients or micro-detail.

## Cottonmouth

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Cottonmouth/Arktos_Cottonmouth_v3.png`

Reference: VAE Rattlesnake east texture as the strict pose, S-curve and line-art master.

Final prompt:

> Create one east-facing 256×256 RimWorld snake pawn sprite on a perfectly flat #00FF00 background. Preserve the reference snake's compact S-curve, proportions and thick rounded near-black outline. Remove the rattle. Use one flat dark olive-brown body fill with five or six broad black crossbands and one simple cream lower-jaw shape. Keep the mouth closed. No teeth, individual scales, speckles, highlights, shading, gradients, texture or small details.

## Coral Snake

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/CoralSnake/Arktos_CoralSnake_v4.png`

Reference: the corrected Cottonmouth source as the strict shared line-art and silhouette master.

Final prompt:

> Precisely preserve the supplied Cottonmouth sprite's silhouette, compact S-curve, body thickness, head geometry and thick rounded outline on a perfectly flat #00FF00 background. Change only the flat color pattern: broad brick-red fields separated by black bands with narrow cream bands directly beside the black. Use a simple black head and remove the cream lower jaw. Do not alter the pose and do not add scales, texture, speckles, shading, gradients or micro-detail.

## Copperhead

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Copperhead/Arktos_Copperhead_v3.png`

Reference: VAE Rattlesnake east texture as the strict pose, rattle and line-art master.

Final prompt:

> Create one east-facing 256×256 RimWorld snake pawn sprite on a perfectly flat #00FF00 background. Preserve the reference rattlesnake's compact S-curve, proportions, simple rattle and thick rounded near-black outline. Use one flat dusty-tan base, five or six broad chestnut hourglass-shaped blocks, one flat copper-orange head and a plain beige rattle. No individual scales, green flecks, speckles, texture, highlights, shading, gradients or small details.

## Wasteland Indigo

Active output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/WastelandIndigo/Arktos_WastelandIndigo_v4.png`

Reference: VAE Anaconda east texture as the strict silhouette, pose and line-art master.

Final prompt:

> Create one east-facing 256×256 RimWorld snake pawn sprite on a perfectly flat #00FF00 background. Preserve the supplied anaconda's exact broad silhouette, head shape, coiled pose, body thickness and thick rounded near-black outline. Use one flat muted indigo body fill with six or seven broad near-black saddle blotches and, if needed for readability, one uninterrupted simple underside stripe. No individual scales, flecks, highlights, shading, gradients, texture or small details.

## Superseded versions

The rejected high-resolution predecessors were removed from the active LoadFolders and preserved for recovery in `.work/texture-archive/2026-07-31-highres-rejected`. They must not be copied back into active texture folders without updating and revalidating the XML texture paths.
