# Arktos Bisotope — calf texture (2026-07-31)

## Scope and production mode

- Generator: built-in ImageGen, `precise-object-edit` mode.
- Binding reference: female Bisotope v8 for the body family and silhouette.
- Supporting references: male Bisotope v8 for family resemblance and Erin's Fluffy Fauna Bison for simple short-coated RimWorld construction.
- Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_Calf_v9.png`.
- Processing: generated on uniform `#00FF00`, soft-matte chroma removal with despill and one-pixel edge contraction, then Lanczos reduction to transparent RGBA 256×256.

## Final prompt

> Use female Bisotope v8 as the binding master for the compact east-facing body, high rounded middle hump, rounded hindquarter, fused legless RimWorld underside, tail position, overall scale, and heavy near-black line hierarchy. Create a juvenile with a head roughly 30–40 percent larger relative to the torso: rounder forehead, short blunt muzzle, larger rounded ear, and a large simple black eye. Keep it recognizably a bison/Bisotope rather than a pig, capybara, cow, or dog.
>
> Remove the horn completely, including horn buds and bases. Cover the entire animal continuously in short warm medium chocolate-brown fur from tail through hindquarter, hump, head, cheeks, and muzzle. Leave no pink naked skin and no fur-to-skin boundary. The upper head and hump may be subtly darker in one broad flat area. Represent short fur mainly with a smooth rounded silhouette and only a few broad notches at cheek, underside, and the compact dark tail tuft. Make the tail stem fur-covered brown.
>
> Preserve the adult family's rounded hump and same basic body silhouette. Use minimalist flat RimWorld rendering, a 4–5 px near-black outer contour at final size, 2–3 px internal lines, no visible legs, and a perfectly uniform `#00FF00` chroma backdrop. Avoid every horn or stub, exposed skin, spots, mottling, long shaggy adult fur, individual hairs, realistic texture, wounds, text, watermark, or grid.

## XML integration

The calf texture is assigned to `bodyGraphicData` and `femaleGraphicData` of Bison life stage 1. Male and female calves therefore share the same artwork; the later stages retain their sex-specific v8 textures and original Vanilla draw sizes.

The approved east texture was later expanded with north and south views and converted to `Graphic_Multi`; see `Development/Arktos_Animal_Texture_Prompts_2026-07-31_BisotopeDirectional.md`.
