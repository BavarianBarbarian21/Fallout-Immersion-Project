# Arktos Bisotope — skin-tone revision (2026-07-31)

## Scope and production mode

- Scope: Bisotope only. Horse, Giant Mole Rat, and all four snake textures remain unchanged.
- Generator: built-in ImageGen, `precise-object-edit` mode.
- Binding edit target: `Arktos_Bisotope_v6.png`.
- Style/color inspiration: the user-provided rounded RimWorld animal reference, used only for broad organic skin areas and a warmer living-skin palette.
- Supporting references: Erin's Fluffy Fauna Bison for simple RimWorld bison construction; FCP Bighorner bull for bold line weight.
- Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_v7.png`.
- Processing: generated on uniform `#00FF00`, soft-matte chroma removal with despill, then Lanczos reduction to transparent RGBA 256×256.

## Final prompt

> Use case: precise-object-edit
>
> Asset type: east-facing RimWorld animal pawn texture, final display size 256 × 256 pixels.
>
> Image 1 is the binding edit target. Preserve its species identity, east-facing silhouette, right-facing bison head, single large cream horn, tail, fused legless RimWorld underside, dark shaggy head/neck/shoulder fur, sharp irregular fur-to-skin boundary, and heavy black line weight. Image 2 is inspiration only for broad rounded body masses, thick rounded black contour, and large organic flesh-color areas; do not copy its head, species, grid, white background, or full-body mottling. Image 3 confirms simple Erin-style bison construction. Image 4 supports RimWorld body-lobe construction and bold line weight only.
>
> Change only the hairless rear half so it reads unmistakably as living bare skin instead of brown or red fur. Use a lighter, desaturated warm dusty rose-peach / muted pink-beige base. Add only 3–5 large irregular graphic patches in related pale blush-beige and muted mauve-rose tones. Keep every patch broad and simple, without tiny spots, and confine all mottling to the naked rear behind the jagged shoulder-fur boundary. Keep the furry front dark warm brown. The rear may become subtly rounder and weightier while remaining recognizably the same Bisotope.
>
> Use minimalist hand-drawn RimWorld rendering: flat opaque fills, gently imperfect organic curves, very limited palette, and low detail at 256 px. Keep a continuous near-black outer contour about 4–5 px thick at final size and 2–3 px internal crease lines. Do not thin or soften the outline. No visible legs, feet, or hooves.
>
> Place the animal on a perfectly flat solid `#00FF00` chroma-key background with no grid, shadows, gradients, texture, floor, reflections, or lighting variation. Keep generous padding and do not use the key color in the animal.
>
> Keep horn, head, eye, ear, tail, furry-front distribution, shoulder boundary, orientation, and thick line art intact. Avoid extra horns, facial redesign, patches on the fur, brown-looking rear fur, individual hairs, pores, scales, wounds, scars, tumors, small spots, microdetail, legs, hooves, text, watermark, border, or grid.

## Result

The naked rear now uses a light dusty rose-peach skin base with a few large blush and muted rose patches. The dark furry head and shoulder mass, cream horn, legless RimWorld silhouette, and heavy near-black contour remain the defining Bisotope features.

Bisotope v7 was later superseded by the spotless, sex-specific v8 pair documented in `Development/Arktos_Animal_Texture_Prompts_2026-07-31_BisotopeSexes.md`.
