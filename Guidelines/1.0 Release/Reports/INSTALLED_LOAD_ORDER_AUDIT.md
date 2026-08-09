# Installed load-order audit

Generated: 2026-08-08 09:25:24 +02:00

Active mods: 135; mapped About.xml files: 135.

The audit checks the current active 1.6 content roots, declared dependency/loadAfter/loadBefore order, patch XPath ownership, XML ParentName inheritance and managed assembly references. Ordinary deferred Def cross-references are intentionally not treated as load-order requirements.

## Declared order violations

- `FIP.WestTek` must load after `OskarPotocki.VFE.Deserters`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.HelixienGas`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.Recycling`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.Temperature`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFEArt`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFECore`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFEFactory`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFEFarming`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFEProduction`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFESecurity`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VFESpacer`: dependency/loadAfter is ordered later.
- `FIP.WestTek` must load after `VanillaExpanded.VPlantsESucculents`: dependency/loadAfter is ordered later.

## Missing direct metadata edges in third-party mods

- **Vanilla Persona Weapons Expanded** (`VanillaExpanded.VPersonaWeaponsE`) should declare `loadAfter` **Vanilla Psycasts Expanded** (`VanillaExpanded.VPsycastsE`). Current positions: 34 -> 33. Evidence: Assembly reference: VanillaPsycastsExpanded [VanillaPersonaWeaponsExpanded.dll].
- **Vanilla Temperature Expanded** (`VanillaExpanded.Temperature`) should declare `loadAfter` **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`). Current positions: 10 -> 62. Evidence: Assembly reference: VFETribals [ProxyHeat.dll].
- **Vanilla Hair Expanded** (`VanillaExpanded.VHE`) should declare `loadAfter` **Harmony** (`brrainz.harmony`). Current positions: 1 -> 126. Evidence: Assembly reference: 0Harmony [VanillaHairExpanded.dll].

## Missing direct metadata edges in FIP/FCP

- None detected from active patch, inheritance or assembly references.

## Undeclared optional patch relations with reversed current order

These are compatibility Patch XPath relations, not proven loader requirements. The supplied runtime log contains no PatchOperation failure, so they are kept separate from the high-confidence assembly/inheritance edges above.

- **Vanilla Factions Expanded - Medieval 2** (`OskarPotocki.VFE.Medieval2`, position 9`) patches **Vanilla Helixien Gas Expanded** (`VanillaExpanded.HelixienGas`, position 60`) without an explicit order path. Evidence: ThingDef|VHGE_GasPoweredSmithy [VHGEPatch.xml].
- **Vanilla Factions Expanded - Medieval 2** (`OskarPotocki.VFE.Medieval2`, position 9`) patches **Vanilla Furniture Expanded - Production** (`VanillaExpanded.VFEProduction`, position 69`) without an explicit order path. Evidence: ThingDef|VFE_TableButcherElectric [VanillaFurnitureExpanded-ProductionPatch.xml]; ThingDef|VFE_TableSmithyLarge [VanillaFurnitureExpanded-ProductionPatch.xml]; ThingDef|VFE_TableStonecutterElectric [VanillaFurnitureExpanded-ProductionPatch.xml]; ThingDef|VFE_TableTailorLarge [VanillaFurnitureExpanded-ProductionPatch.xml].
- **Vanilla Factions Expanded - Medieval 2** (`OskarPotocki.VFE.Medieval2`, position 9`) patches **Vanilla Plants Expanded - More Plants** (`VanillaExpanded.VPlantsEMore`, position 99`) without an explicit order path. Evidence: ThingDef|VCE_Blueberry [VPEMorePlantsPatch.xml]; ThingDef|VCE_Canola [VPEMorePlantsPatch.xml]; ThingDef|VCE_FragrantbloomRose [VPEMorePlantsPatch.xml]; ThingDef|VCE_Hyacinth [VPEMorePlantsPatch.xml]; ThingDef|VCE_Jasmine [VPEMorePlantsPatch.xml]; ThingDef|VCE_Lavender [VPEMorePlantsPatch.xml]; ThingDef|VCE_Lily [VPEMorePlantsPatch.xml]; ThingDef|VCE_Orchid [VPEMorePlantsPatch.xml]; ThingDef|VCE_Plumeria [VPEMorePlantsPatch.xml]; ThingDef|VCE_Tulip [VPEMorePlantsPatch.xml]; ThingDef|VCE_Vanilla [VPEMorePlantsPatch.xml]; ThingDef|VCE_Violet [VPEMorePlantsPatch.xml]; ThingDef|VCE_YlangYlang [VPEMorePlantsPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Factions Expanded - Settlers** (`OskarPotocki.VanillaFactionsExpanded.SettlersModule`, position 11`) without an explicit order path. Evidence: ThingDef|DoorSaloon [VanillaFactionsExpandedSettlers.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Apparel Expanded — Accessories** (`VanillaExpanded.VAEAccessories`, position 30`) without an explicit order path. Evidence: ThingDef|VAEA_Apparel_Backpack [VanillaApparelExpandedAccessories.xml]; ThingDef|VAEA_Apparel_BattleBanner [VanillaApparelExpandedAccessories.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Ideology Expanded - Hats and Rags** (`VanillaExpanded.VIEHAR`, position 32`) without an explicit order path. Evidence: ThingDef|VIEHAR_Apparel_Beads [VanillaIdeologyExpandedHatsAndRags.xml]; ThingDef|VIEHAR_Apparel_Rags [VanillaIdeologyExpandedHatsAndRags.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Weapons Expanded - Tribal** (`VanillaExpanded.VWETB`, position 35`) without an explicit order path. Evidence: ThingDef|VWE_MeleeWeapon_HeavyClub [VanillaWeaponsExpandedTribal.xml]; ThingDef|VWE_Weapon_FireBomb [VanillaWeaponsExpandedTribal.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Cooking Expanded** (`VanillaExpanded.VCookE`, position 45`) without an explicit order path. Evidence: ThingDef|VCE_Allspice [VanillaCookingExpandedPatch.xml]; ThingDef|VCE_Wheat [VanillaCookingExpandedPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Furniture Expanded** (`VanillaExpanded.VFECore`, position 65`) without an explicit order path. Evidence: ThingDef|Bed_StoneSlab [VanillaFurnitureExpanded.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Furniture Expanded - Farming** (`VanillaExpanded.VFEFarming`, position 67`) without an explicit order path. Evidence: ThingDef|VFE_PlanterBox [VanillaFurnitureExpandedFarming.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Furniture Expanded - Security** (`VanillaExpanded.VFESecurity`, position 71`) without an explicit order path. Evidence: ThingDef|VFES_CavalrySpikes [VanillaFurnitureExpandedSecurity.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Brewing Expanded** (`VanillaExpanded.VBrewE`, position 85`) without an explicit order path. Evidence: RecipeDef|VBE_MakeCigarettes [VanillaBrewingExpanded.xml]; ThingDef|VBE_Plant_Coffee [VanillaBrewingExpandedPatch.xml]; ThingDef|VBE_Plant_Tobacco [VanillaBrewingExpandedPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Cooking Expanded - Sushi** (`VanillaExpanded.VCookESushi`, position 89`) without an explicit order path. Evidence: ThingDef|VCE_Soybean [VanillaSushiExpandedPatch.xml]; ThingDef|VCE_SushiPrepTable [VanillaSushiExpandedPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Plants Expanded** (`VanillaExpanded.VPlantsE`, position 91`) without an explicit order path. Evidence: TerrainDef|VCE_TilledSoil [VanillaPlantsExpandedPatch.xml]; ThingDef|VCE_Grass [VanillaPlantsExpandedPatch.xml]; ThingDef|VCE_Onion [VanillaPlantsExpandedPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Plants Expanded - More Plants** (`VanillaExpanded.VPlantsEMore`, position 99`) without an explicit order path. Evidence: ThingDef|VCE_BrusselsSprout [VanillaPlantsMorePlantsExpandedPatch.xml]; ThingDef|VCE_Celery [VanillaPlantsMorePlantsExpandedPatch.xml].
- **Vanilla Factions Expanded - Tribals** (`OskarPotocki.VFE.Tribals`, position 10`) patches **Vanilla Hair Expanded** (`VanillaExpanded.VHE`, position 126`) without an explicit order path. Evidence: ThingDef|VHE_TableBarber [VanillaHairExpanded.xml].
- **Vanilla Anomaly Expanded - Insanity** (`VanillaExpanded.VAnomalyEInsanity`, position 31`) patches **Vanilla Traits Expanded** (`VanillaExpanded.VanillaTraitsExpanded`, position 123`) without an explicit order path. Evidence: TraitDef|VTE_AbsentMinded [VETraits.xml]; TraitDef|VTE_Anxious [VETraits.xml]; TraitDef|VTE_Brave [VETraits.xml]; TraitDef|VTE_Coward [VETraits.xml]; TraitDef|VTE_Desensitized [VETraits.xml]; TraitDef|VTE_Dunce [VETraits.xml]; TraitDef|VTE_Eccentric [VETraits.xml]; TraitDef|VTE_Insomniac [VETraits.xml]; TraitDef|VTE_MadSurgeon [VETraits.xml]; TraitDef|VTE_Prodigy [VETraits.xml]; TraitDef|VTE_Schizoid [VETraits.xml].
- **Vanilla Persona Weapons Expanded** (`VanillaExpanded.VPersonaWeaponsE`, position 33`) patches **Vanilla Factions Expanded - Empire** (`OskarPotocki.VFE.Empire`, position 54`) without an explicit order path. Evidence: ThingDef|VFEE_MeleeWeapon_ToxbladeBladelink [VFEEmpire.xml].
- **Vanilla Gravship Expanded - Chapter 1** (`vanillaexpanded.gravship`, position 36`) patches **Vanilla Factions Expanded - Empire** (`OskarPotocki.VFE.Empire`, position 54`) without an explicit order path. Evidence: ThingDef|VFEE_Apparel_AbsolverHelmet [ArmorVacuumResistancePatch.xml]; ThingDef|VFEE_Apparel_DeserterHelmet [ArmorVacuumResistancePatch.xml]; ThingDef|VFEE_Apparel_JanissaryHelmet [ArmorVacuumResistancePatch.xml]; ThingDef|VFEE_Apparel_TechfriarCrown [ArmorVacuumResistancePatch.xml].
- **Vanilla Quests Expanded - The Generator** (`vanillaquestsexpanded.generator`, position 40`) patches **Vanilla Ideology Expanded - Memes and Structures** (`VanillaExpanded.VMemesE`, position 47`) without an explicit order path. Evidence: MemeDef|VME_HardcoreIndustrialism [MemeExclusionsPatch.xml]; MemeDef|VME_MechanoidSupremacy [MemeExclusionsPatch.xml]; MemeDef|VME_Progressive [MemeExclusionsPatch.xml].
- **Vanilla Factions Expanded - Empire** (`OskarPotocki.VFE.Empire`, position 54`) patches **Vanilla Furniture Expanded** (`VanillaExpanded.VFECore`, position 65`) without an explicit order path. Evidence: ThingDef|Bed_DoubleErgonomic [LinkablesVanillaExpanded.xml]; ThingDef|Bed_Ergonomic [LinkablesVanillaExpanded.xml]; ThingDef|Bed_Kingsize [LinkablesVanillaExpanded.xml]; ThingDef|Bed_Simple [LinkablesVanillaExpanded.xml]; ThingDef|Bed_StoneSlab [LinkablesVanillaExpanded.xml]; ThingDef|Joy_Piano [Instruments.xml].
- **Vanilla Factions Expanded - Empire** (`OskarPotocki.VFE.Empire`, position 54`) patches **Vanilla Furniture Expanded - Medical Module** (`VanillaExpanded.VFEMedical`, position 68`) without an explicit order path. Evidence: ThingDef|Bed_CryptoBed [LinkablesVanillaExpanded.xml]; ThingDef|Bed_OperatingTable [LinkablesVanillaExpanded.xml].
- **Vanilla Factions Expanded - Empire** (`OskarPotocki.VFE.Empire`, position 54`) patches **Vanilla Furniture Expanded - Spacer Module** (`VanillaExpanded.VFESpacer`, position 72`) without an explicit order path. Evidence: ThingDef|Bed_AdvBed [LinkablesVanillaExpanded.xml]; ThingDef|Bed_AdvDoubleBed [LinkablesVanillaExpanded.xml].
- **Vanilla Factions Expanded - Deserters** (`OskarPotocki.VFE.Deserters`, position 55`) patches **Vanilla Furniture Expanded - Security** (`VanillaExpanded.VFESecurity`, position 71`) without an explicit order path. Evidence: ThingDef|VFES_Turret_AutocannonDouble [VanillaFurnitureExpandedSecurity.xml].
- **Vanilla Temperature Expanded** (`VanillaExpanded.Temperature`, position 62`) patches **Vanilla Furniture Expanded** (`VanillaExpanded.VFECore`, position 65`) without an explicit order path. Evidence: ThingDef|Stone_Campfire [VanillaFurnituresExpanded.xml].
- **Vanilla Nutrient Paste Expanded** (`VanillaExpanded.VNutrientE`, position 73`) patches **Hospitality (Continued)** (`Orion.Hospitality`, position 75`) without an explicit order path. Evidence: Hospitality.HospitalityConfigDef|MainConfig [Mods.xml].
- **Vanilla Plants Expanded - More Plants** (`VanillaExpanded.VPlantsEMore`, position 99`) patches **Vanilla Plants Expanded - Succulents** (`VanillaExpanded.VPlantsESucculents`, position 101`) without an explicit order path. Evidence: ThingDef|VCE_Plant_AloeVera [SucculentsPatch.xml].

## Privately bundled Harmony copies

- **Dubs Bad Hygiene** (`Dubwise.DubsBadHygiene`) ships its own `0Harmony.dll`. The official Harmony mod currently loads first, but the private copy remains a library-conflict risk.

## Duplicate direct Def identities across active mods

- `DesignationCategoryDef|VCHE_PipeNetworks`: VanillaExpanded.HelixienGas, VanillaExpanded.VChemfuelE, VanillaExpanded.VNutrientE.
- `DutyDef|TakeWoundedGuest`: Ludeon.RimWorld, Orion.Hospitality.
- `HediffDef|ToxicHealing`: Rick.FCP.Core.Tools, Rick.FCP.Ghouls.
- `IncidentDef|VisitorGroup`: Ludeon.RimWorld, Orion.Hospitality.
- `JoyKindDef|Shopping`: Adamas.Storefront, Orion.Hospitality.
- `LetterDef|PurpleEvent`: VanillaExpanded.VEE, vanillaquestsexpanded.deadlife.
- `MentalStateDef|FCP_MentalState_PermanentBerserk`: Rick.FCP.Core.Tools, Rick.FCP.Ghouls.
- `PawnKindDef|FCP_Unique_Pawnkind_Karl`: Rick.FCP.GreatKhans, Rick.FCP.Legion.
- `ResearchTabDef|VanillaExpanded`: OskarPotocki.VFE.Deserters, OskarPotocki.VFE.Medieval2, VanillaExpanded.HelixienGas, VanillaExpanded.Recycling, VanillaExpanded.Temperature, VanillaExpanded.VBooksE, VanillaExpanded.VFEArt, VanillaExpanded.VFECore, VanillaExpanded.VFEFactory, VanillaExpanded.VFEFarming, VanillaExpanded.VFEPower, VanillaExpanded.VFEProduction, VanillaExpanded.VFESecurity, VanillaExpanded.VFESpacer, VanillaExpanded.VPlantsESucculents, VanillaExpanded.VPsycastsE.
- `RulePackDef|FCP_Namer_Settlement_NCR_Deserters`: Rick.FCP.Core.Tools, Rick.FCP.NCR.
- `ThingDef|FCP_Gun_Anti_Material_Rifle`: Rick.FCP.BallisticWeapons, Rick.FCP.NCR.
- `ThingDef|ShuttleIncoming`: Ludeon.RimWorld.Royalty, zal.spaceports.
- `ThingDef|ShuttleLeaving`: Ludeon.RimWorld.Royalty, zal.spaceports.
- `ThingSetMakerDef|MapGen_AncientComplexRoomLoot_Default`: Ludeon.RimWorld, Ludeon.RimWorld.Ideology.
- `TransportShipDef|Ship_Shuttle`: Ludeon.RimWorld.Royalty, zal.spaceports.
- `WorldObjectDef|TravelingShuttle`: Ludeon.RimWorld.Royalty, zal.spaceports.

## Notes

- A missing direct edge is a metadata defect even if the current numeric positions happen to be safe. Auto-sort may choose a different valid order later unless the relation is declared.
- Workshop metadata edits are overwritten by Steam updates. Prefer reporting third-party defects upstream or maintaining a documented local metadata patch.
