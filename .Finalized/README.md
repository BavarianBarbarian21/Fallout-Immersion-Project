# Finalized Mod Rebuild

This folder is the staging area for the finalized FIP mod restructure.

Current live mods in the repository root remain unchanged and serve as source/reference material.

Target finalized mapping:

- `FIP-Repconn` = gravship and aerospace content
- `FIP-Arktos` = biome and worldgen content
- `FIP-Greenway` = Ideology, dryads, and gauranlen content
- `FIP-H&HTools` = shared defs and centralized naming ruledefs
- `FIP-Poseidon` = Vanilla Expanded energy and technology compatches
- `FIP-Lucky 38` = Vanilla Expanded hospitality compatibility

Current implementation status:

- Finalized target mod folders have been seeded from the current source mods or scaffolded as new staging placeholders.
- Finalized `About.xml` files have been updated for the new staging identities.
- Copied `PublishedFileId.txt` files were removed from the finalized tree.
- Repconn standalone naming files were moved from finalized `FIP-Arktos` into finalized `FIP-H&HTools`.

Planned next steps:

1. Move more naming ruledefs into finalized `FIP-H&HTools`.
2. Split finalized `FIP-Arktos` into core biome content and Odyssey-only biome integration.
3. Move Ideology and dryad content from finalized `FIP-Greenway` sources into the finalized Greenway target.
4. Move gravship and Odyssey content into the finalized Repconn target.
