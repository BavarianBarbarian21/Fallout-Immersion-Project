# Arktos Copperhead — rattle removal (2026-07-31)

## Scope and production mode

- Generator: built-in ImageGen, `precise-object-edit` mode.
- Binding edit target: the user-provided full-resolution Copperhead image.
- Supporting reference: active transparent Copperhead v5 texture for final sprite scale.
- Output: `New-Mods/FIP-Arktos/LoadFolders/Base/Textures/FIP-Arktos/Animals/Snakes/Copperhead/Arktos_Copperhead_v6.png`.
- Processing: uniform green chroma source, soft-matte removal with despill and one-pixel edge contraction, then Lanczos reduction to transparent RGBA 256×256.

## Final prompt

> Preserve the exact canvas, placement, complete S-shaped body curve, head, eye, neck, body thickness, copper-orange and tan saddle pattern, every color boundary, highlights, shading, and thick rounded black line art. Edit only the segmented rattle at the upper-left tail end. Remove the entire cream rattle stack and scalloped silhouette. Continue the existing tail stem along the same curve for a short distance and terminate it in one simple smooth narrow point, copper-brown with at most one existing tan band and the same near-black outline. Keep the replacement smaller than the old rattle.
>
> Everything from the former rattle base through the rest of the snake must remain visually unchanged. Do not move, redraw, recolor, resize, rotate, or restyle the head, eye, neck, coils, pattern blocks, shading, body outline, or composition. Preserve the uniform `#00FF00` backdrop. No rattle segments, beads, bulb, club, fork, decorative tail feature, text, or watermark.

## Def consistency

All three Copperhead life stages now reference v6. The obsolete description of a dry keratin rattle was removed; no DefInjected duplicate was added.
