# FCP and Vanilla Expanded UI texture reference

This folder contains byte-identical reference copies of UI-adjacent textures from the FCP and Vanilla Expanded mods active in the RimWorld 1.6 mod list on 2026-08-05.

## Layout

- `FCP/<packageId>/...` contains Fallout Collaboration Project textures.
- `VanillaExpanded/<packageId>/...` contains Vanilla Expanded ecosystem textures.
- Every copied file retains its original path relative to the source mod, including version or `Common` folders.
- `texture-manifest.csv` records the source, target, byte size, and SHA-256 hash of every copied texture.
- `mod-manifest.csv` records every active FCP/VE mod considered by the collection, including mods with no matching texture.

## Scope

The collection includes PNG, DDS, TGA, JPG, and JPEG files located below a RimWorld `Textures` directory when their path or filename identifies them as UI, interface, gizmo, command, ability, gene, icon, button, hediff, trait, meme, ideology, research, skill, status, category, symbol, overlay, xenotype, or psycast artwork. This intentionally excludes ordinary world, pawn, apparel, building, plant, animal, and weapon sprites unless they are stored or named as one of those UI categories.

The Vanilla Expanded group covers active packages whose IDs belong to the `VanillaExpanded`, `OskarPotocki`, `VanillaQuestsExpanded`, or `VanillaRacesExpanded` families.

## Inventory

| Group | Active mods checked | Mods with matching textures | Copied files | Size |
| --- | ---: | ---: | ---: | ---: |
| FCP | 22 | 9 | 131 | 24.51 MiB |
| Vanilla Expanded | 72 | 49 | 1,384 | 62.16 MiB |
| **Total** | **94** | **58** | **1,515** | **86.67 MiB** |

These files are reference material copied from the locally installed source mods. They are not FIP-owned replacement assets and should not be redistributed independently of the permissions of their respective source mods.
