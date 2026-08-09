# Arktos Bisotope — directional texture sets (2026-07-31)

## Scope and production mode

- Generator: built-in ImageGen, six separate `precise-object-edit` calls.
- Approved identity masters supplied by the user:
  - calf: `C:/Users/Matthias/Downloads/Generiertes Bild 1.png`
  - male: `C:/Users/Matthias/Downloads/Generiertes Bild 2.png`
  - female: `C:/Users/Matthias/Downloads/Generiertes Bild 1 (1).png`
- Directional construction references: Erin's Fluffy Fauna Bison north/south and FCP Bighorner bull north/south.
- The approved active east sprites were copied pixel-for-pixel to `_east`; only north and south were generated.
- All generated sources used uniform `#00FF00`, followed by soft-matte removal, despill, one-pixel edge contraction, and Lanczos reduction to RGBA 256×256.

## Final files

| Variant | East | North | South |
| --- | --- | --- | --- |
| Calf | `Arktos_Bisotope_Calf_v9_east.png` | `Arktos_Bisotope_Calf_v9_north.png` | `Arktos_Bisotope_Calf_v9_south.png` |
| Female | `Arktos_Bisotope_Female_v8_east.png` | `Arktos_Bisotope_Female_v8_north.png` | `Arktos_Bisotope_Female_v8_south.png` |
| Male | `Arktos_Bisotope_Male_v8_east.png` | `Arktos_Bisotope_Male_v8_north.png` | `Arktos_Bisotope_Male_v8_south.png` |

All files live under `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/`.

## Shared final prompt constraints

> Redraw the binding east-view identity master as a centered RimWorld north or south view using the corresponding Erin Bison as the binding directional-layout reference and FCP Bighorner only for bold contour and fused legless body construction. Preserve the exact variant identity, palette, hump, tail, fur distribution, approved thick near-black outer contour of roughly 4–5 px at final size, and low-detail flat RimWorld rendering. Use no visible legs, feet, or hooves. Render on a perfectly flat `#00FF00` background with no shadow, gradient, texture, grid, floor, reflection, text, or watermark.

## Calf directional prompts

> North: show the back of the oversized hornless juvenile head at upper center, two large rounded ears, the high broad hump below it, compact oval rump, and fur-covered tail with dark tuft at bottom center. Keep continuous short medium chocolate-brown fur and one broad darker head/hump area. Show no face and absolutely no horn or horn bud.
>
> South: place the oversized juvenile head in the lower/front center with two large rounded ears, two simple black eyes, and a short blunt muzzle. Keep the hump/body behind it, continuous short brown fur, no pink skin, and absolutely no horn or horn bud.

## Female directional prompts

> North: show the back of the dark furry head at upper center with two modest symmetric cream horns, the high shaggy shoulder hump, a broad jagged fur boundary, spotless dusty rose-peach rear with only two creases, and the flesh-colored tail stem with dark tuft at bottom center. Show no face.
>
> South: place the dark shaggy head front-center with two eyes, ears, and two modest symmetric cream horns, all shorter and slimmer than the male horns. Keep the pink spotless rear visible behind the furry shoulders and retain the approved palette and fur-point scale.

## Male directional prompts

> North: show the back of the massive dark furry head and darker crown with two enormous symmetric cream horns sweeping strongly outward then upward, followed by the high shaggy hump, broad fur boundary, spotless dusty rose-peach rear, and centered tail. Show no face; keep horn tips inside the canvas.
>
> South: place the massive shaggy head front-center with two eyes, ears, darker crown/forelock, and two enormous cream horns sweeping outward then upward without hiding the face. Keep the high hump and spotless pink rear behind it; no antlers or ram curls.

## XML integration

All Bison `bodyGraphicData` and `femaleGraphicData` entries now use `Graphic_Multi`. Their existing base `texPath` values remain unchanged, allowing RimWorld to resolve `_east`, `_north`, and `_south`; west uses the mirrored east texture. No new PawnKindDef or duplicate texture path was added.
