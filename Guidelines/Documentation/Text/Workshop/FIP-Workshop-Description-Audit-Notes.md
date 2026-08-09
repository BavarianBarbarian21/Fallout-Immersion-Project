# FIP Workshop Description Audit Notes

## Scope

The Workshop copy now covers all fourteen published FIP modules in the repository:

1. Donaustahl
2. H&H Tools
3. Arktos
4. West Tek
5. RobCo
6. Greenway
7. Whitespring
8. Hubris
9. REPCONN
10. Poseidon
11. Lucky 38
12. Sunset
13. Future-Tec
14. Corvega

Big MT and Translation are present in the repository but are outside this Workshop-description set.

Every description uses the Whitespring structure: title and slogan, open-beta notice, Features, Requirements, Recommended, Development Notes, cross-linked FIP modules, Credits, and Ko-fi support.

## Repository audit basis

Claims were checked against each module's current `About`, `Defs`, `Patches`, `Languages`, `Assemblies` or source, and `Textures` content. Public Workshop IDs were taken from the modules' current metadata and cross-checked throughout the complete link matrix.

Only shipped, active content is described. Planned work, TODOs, disabled definitions, commented XML, and development-only ideas are excluded.

## Module findings

| Module | Actual scope | Hard requirements | Description highlights |
|---|---|---|---|
| Donaustahl | Compatibility and terminology patches | None | Vault-Tec medicine; renamed VEE incidents, VSI events, Aspirations, VBE backstories, and Books Extended newspaper content. No invented faction or mechanoid removal and no texture claim. |
| H&H Tools | Shared factions, pawn kinds, cultures, names, and scenarios | None | Regional civil, rough, raider, tribal, and mutant faction identities; culture and name generators; scenarios; nine custom faction or identity icons. |
| Arktos | World generation and ecosystem expansion | H&H Tools | 23 biomes, custom biome/world logic, landmarks and mutators, fire-ant faction and creatures, punga conversion, animal and plant integration, biome and creature visuals. |
| West Tek | Genetics, mutation, research, quests, creatures, and equipment | Biotech; H&H Tools | Fallout xenotypes, four-tier S.P.E.C.I.A.L., FEV laboratory and research route, fauna and flora mutagens, Numen/Overgrown/Spore Carrier systems, mutant creatures, gear, textures, and sound. |
| RobCo | Mechanitor and robotics expansion | Biotech | Large robot roster, gestation and research, weapons and abilities, signals and bosses, Platinum Chip routes, sites, mechanic lair, Synth and Think Tank content, extensive textures and audio. |
| Greenway | Ideology expansion and Treefather conversion | Ideology; H&H Tools | Government and custom memes, religious groups, mutant-policy precepts and thought workers, Treefather/Treeminder conversion, Pungaling dryad, plant/chem integration, and custom visuals. |
| Whitespring | Royalty-to-Enclave localization and compatibility | Royalty | Empire to Whitespring Enclave, ranks, honor to votes, shuttles to vertibirds/XVB02, and conditional VFE Empire/Deserters conversion. No new mechanics or in-game textures. |
| Hubris | Psychic-to-cryptid localization with small visual replacement | Royalty | Psylink to cryptid insight, psyfocus to cryptid focus, psycasts to revelations, occultist terminology, Mothman totem and egg retextures, VPE path conversions. |
| REPCONN | Odyssey aerospace localization | Odyssey | Gravship to Hellion, gravcore to reactor core, grav engine to Hellion reactor, thrusters to VTOL, console to cockpit, plus VGE research and expertise support. No texture or flight-mechanic claim. |
| Poseidon | Energy, fuel, and industrial compatibility localization | None | Chemfuel to oil, Helixien to propane, and Poseidon/HELIOS Two/Corvega/Med-Tek/Wattz branding across supported power, factory, medical, production, security, spacer, nutrient, recycling, and temperature modules. |
| Lucky 38 | Hospitality, casino, food, service, and spaceport compatibility | None | Lucky 38/Vault 21 casino terminology, atomic cocktails, homebrew sarsaparilla, pungashine, vertibirds, canned ingredient texture, and coffee-machine/workbench retextures. |
| Sunset | Active compatibility conversion for three VFE faction modules | None | Removes incompatible Medieval 2 world factions/scenarios/content, modernizes traders, reworks Settlers factions/pawns/incidents, renames Tribals eras, and replaces archery target/training dummy textures. |
| Future-Tec | Vanilla Quests Expanded narrative localization | None | Ancients to Vault-Tec blacksites/FEV, Cryptoforge to mobile base crawler, Deadlife to continuity bunkers/feral ghouls, Generator to Lee Moldaver/restoration reactor/fusion core. No new quest logic or textures. |
| Corvega | Vehicle and upgrade localization | Vanilla Vehicles Expanded; Tier 3; Upgrades | Chryslus Highwayman, Corvega APC/Prowler/Roadkill/Roadrunner/Lightning, and Fallout-style upgrade names. No vehicle-stat, mechanics, or texture claim. |

## Corrected publication data

- Greenway's current Workshop ID is `3760675636`; the previous stale link was removed everywhere.
- Sunset (`3760676309`), Future-Tec (`3760677126`), and Corvega (`3760683002`) are now included in every cross-link list.
- Every module links exactly once to each of the other thirteen modules and does not link to itself in the module list.

## Publishing cautions

- The descriptions distinguish new gameplay systems from localization, compatibility, and visual replacements.
- Conditional integrations are listed as recommendations, not hard dependencies.
- Content already generated in an existing save can retain cached labels, factions, quests, or objects after a localization update.
- If a feature is removed or moved to an unreleased branch, update the corresponding Workshop text before publishing.
