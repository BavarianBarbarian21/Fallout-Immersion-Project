# Arktos mammals — thick-line revision (2026-07-31)

## Scope and production mode

- Scope: Horse, Giant Mole Rat, and Bisotope only. All four snake textures and their XML references were explicitly left unchanged.
- Generator: built-in ImageGen using `precise-object-edit` prompts.
- Line-weight reference: FCP Bighorner bull east texture.
- Final target: continuous rounded near-black outer contour, approximately 4–5 pixels at 256×256; internal lines approximately 2–3 pixels.
- Generated sources used a uniform `#00FF00` background, followed by soft-matte chroma removal, despill, and Lanczos reduction to transparent 256×256 PNGs.

## Horse v6

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Horse/Arktos_Horse_v6.png`

ImageGen edit prompt:

> Use Horse v5 as the binding master for the exact Vanilla body silhouette, scale, position, proportions, head, ears, mane, one-piece tail, fused legless underside, brown gradient, eye, and paired dark roe-buck antlers. Use HorseBrown1 east to confirm the Vanilla body construction and FCP Bighorner only for line weight. Change only the line hierarchy: add a continuous near-black outer contour approximately 4–5 pixels thick at final 256×256 size around body, head, muzzle, ears, mane, tail, underside, and every antler branch. Use 2–3 pixel near-black internal lines. Do not change the body shape, antler design, colors, gradient, or anatomy.

The generated horse candidate achieved the requested body-line weight but altered the antlers into an unacceptable fused crown. It was rejected. The final Horse v6 therefore retains every pixel of the accepted Horse v5 body and antler artwork and adds a deterministic four-pixel near-black external contour behind the unchanged sprite. This preserves the Vanilla body and correct roe-buck antlers while implementing the requested line thickness.

## Giant Mole Rat v6

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/GiantMoleRat/Arktos_GiantMoleRat_v6.png`

Final prompt:

> Use Giant Mole Rat v5 as the binding master for silhouette, pose, huge single hump, pink-brown palette, head, incisors, pointed tail, integrated body lobes, broad shading, and sparse skin folds. Use FCP Mole Rat only for facial anatomy and FCP Bighorner only for line weight. Add a continuous near-black outer contour approximately 4–5 pixels thick at final size around the complete tail, back, hump, head, muzzle, underside, and body lobes. Render existing anatomy lines near-black at 2–3 pixels. Preserve all proportions, colors, shading, and details; add no legs, toes, wrinkles, wounds, or new color patches.

## Bisotope v6

Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Bisotope/Arktos_Bisotope_v6.png`

Final prompt:

> Use Bisotope v5 as the binding master for silhouette, right-facing pose, dark furry head/neck/shoulders, hairless dusty pink-brown rear, irregular fur boundary, horn, tail, fused underside, colors, and shading. Use Erin's Bison only to confirm anatomy and FCP Bighorner only for line weight. Add a continuous near-black outer contour approximately 4–5 pixels thick at final size around tail, rear, underside, fur clumps, head, muzzle, ear, and horn. Use 2–3 pixel near-black internal lines for eye, ear, mouth, fur boundary, and existing skin folds. Preserve all forms, colors, and sparse detail count.

## Superseded files

The thin-line v5 versions of these three mammals were removed from all LoadFolders and moved to `.work/texture-archive/2026-07-31-erin-thin-line`. The current Erin-style snake versions remain in place.

Bisotope v6 was later superseded by the skin-tone revision v7 documented in `Development/Arktos_Animal_Texture_Prompts_2026-07-31_BisotopeSkin.md`. Horse v6 and Giant Mole Rat v6 remain current.
