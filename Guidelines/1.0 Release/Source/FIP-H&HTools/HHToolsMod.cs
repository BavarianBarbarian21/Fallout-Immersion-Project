using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public sealed class HHToolsModSettings : ModSettings
{
    public bool onlyImmersiveFactions = true;
    public bool onlyImmersiveScenarios = true;
    public bool onlyImmersiveWallStructures = true;
    public bool onlyImmersiveFurniture = true;
    public bool onlyImmersiveWeapons = true;
    public bool onlyImmersiveApparel = true;
    public bool onlyImmersiveIdeologyOrigins = true;
    public bool onlyImmersiveQuests = true;
    public bool onlyImmersiveStorytellers = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveFactions, "onlyImmersiveFactions", "restoreFactions");
        LookImmersive(ref onlyImmersiveScenarios, "onlyImmersiveScenarios", "restoreScenarios");
        LookBuildingOptions();
        LookImmersive(ref onlyImmersiveWeapons, "onlyImmersiveWeapons", "restoreWeapons");
        LookImmersive(ref onlyImmersiveApparel, "onlyImmersiveApparel", "restoreApparel");
        LookImmersive(ref onlyImmersiveIdeologyOrigins, "onlyImmersiveIdeologyOrigins", "restoreIdeologyOrigins");
        LookImmersive(ref onlyImmersiveQuests, "onlyImmersiveQuests", "restoreQuests");
        LookImmersive(ref onlyImmersiveStorytellers, "onlyImmersiveStorytellers", "restoreStorytellers");
    }

    private void LookBuildingOptions()
    {
        bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
        bool hasWallStructures = loading && (Scribe.loader.curXmlParent?["onlyImmersiveWallStructures"] != null
            || Scribe.loader.curXmlParent?["restoreWallStructures"] != null);
        bool hasFurniture = loading && (Scribe.loader.curXmlParent?["onlyImmersiveFurniture"] != null
            || Scribe.loader.curXmlParent?["restoreFurniture"] != null);
        bool hasLegacyBuildings = loading && Scribe.loader.curXmlParent?["onlyImmersiveBuildings"] != null;
        bool hasLegacyRestore = loading && Scribe.loader.curXmlParent?["restoreBuildings"] != null;

        LookImmersive(ref onlyImmersiveWallStructures, "onlyImmersiveWallStructures", "restoreWallStructures");
        LookImmersive(ref onlyImmersiveFurniture, "onlyImmersiveFurniture", "restoreFurniture");

        if (loading && (!hasWallStructures || !hasFurniture) && (hasLegacyBuildings || hasLegacyRestore))
        {
            bool legacyValue = true;
            if (hasLegacyBuildings)
            {
                Scribe_Values.Look(ref legacyValue, "onlyImmersiveBuildings", true);
            }
            else
            {
                bool legacyRestore = false;
                Scribe_Values.Look(ref legacyRestore, "restoreBuildings", false);
                legacyValue = !legacyRestore;
            }

            if (!hasWallStructures)
            {
                onlyImmersiveWallStructures = legacyValue;
            }

            if (!hasFurniture)
            {
                onlyImmersiveFurniture = legacyValue;
            }
        }
    }

    private static void LookImmersive(ref bool value, string key, string legacyKey)
    {
        bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
        bool hasNewValue = loading && Scribe.loader.curXmlParent?[key] != null;
        bool hasLegacyValue = loading && Scribe.loader.curXmlParent?[legacyKey] != null;
        Scribe_Values.Look(ref value, key, true);
        if (loading && !hasNewValue && hasLegacyValue)
        {
            bool legacyRestore = false;
            Scribe_Values.Look(ref legacyRestore, legacyKey, false);
            value = !legacyRestore;
        }
    }
}

public sealed class HHToolsMod : Mod
{
    internal static HHToolsModSettings Settings;

    public HHToolsMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<HHToolsModSettings>();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            HHToolsRestoreApplier.Initialize();
            HHToolsRestoreApplier.Apply(Settings);
        });
    }

    public override string SettingsCategory()
    {
        return "FIP - H&H Tools";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive world generation");
        Text.Font = GameFont.Small;
        listing.Label("These options hide content that FIP replaces. Disable an option to restore that content. All options are enabled by default.");
        listing.GapLine();

        bool factions = Settings.onlyImmersiveFactions;
        listing.CheckboxLabeled(
            "Only immersive factions",
            ref factions,
            "Removes original factions that are replaced by FIP factions. Disable this option to restore them. Enabled by default. Start a new world after changing it.");

        bool scenarios = Settings.onlyImmersiveScenarios;
        listing.CheckboxLabeled("Only immersive scenarios", ref scenarios, "Removes original Medieval and Settlers starting scenarios, such as New Kingdom. Disable this option to restore them. Enabled by default. Restart required.");

        listing.Gap();
        Text.Font = GameFont.Medium;
        listing.Label("Immersive content pools");
        Text.Font = GameFont.Small;
        listing.GapLine();

        bool wallStructures = Settings.onlyImmersiveWallStructures;
        listing.CheckboxLabeled("Only immersive wall structures", ref wallStructures, "Hides Medieval castle walls, gates, doors, cobblestone walls, cloth and timbered walls, and wall-mounted Medieval weapons from the build menu. Disable this option to make them buildable again. Enabled by default. Restart required.");

        bool furniture = Settings.onlyImmersiveFurniture;
        listing.CheckboxLabeled("Only immersive furniture", ref furniture, "Hides Medieval fur beds, the hearth, and heraldic rugs and banners from the build menu. Disable this option to make them buildable again. Enabled by default. Restart required.");

        bool weapons = Settings.onlyImmersiveWeapons;
        listing.CheckboxLabeled("Only immersive weapons", ref weapons, "Removes Medieval weapons that do not fit FIP from crafting and normal gameplay. Disable this option to restore them. Enabled by default. Restart required.");

        bool apparel = Settings.onlyImmersiveApparel;
        listing.CheckboxLabeled("Only immersive apparel", ref apparel, "Removes Medieval clothing, armour, and shields that do not fit FIP. Disable this option to restore them. Enabled by default. Restart required.");

        bool origins = Settings.onlyImmersiveIdeologyOrigins;
        listing.CheckboxLabeled("Only immersive Medieval ideology origins", ref origins, "Removes the Medieval Norse ideology origin. Disable this option to restore it. Enabled by default. Restart required.");

        bool quests = Settings.onlyImmersiveQuests;
        listing.CheckboxLabeled("Only immersive quests", ref quests, "Removes certain Medieval and Settlers quests that do not fit FIP. Disable this option to restore them. Enabled by default. Restart required.");

        bool storytellers = Settings.onlyImmersiveStorytellers;
        listing.CheckboxLabeled("Only immersive storytellers", ref storytellers, "Removes Maynard, Talon, and Diego Dire from storyteller selection. Disable this option to restore them. Enabled by default. Restart required.");

        if (factions != Settings.onlyImmersiveFactions
            || scenarios != Settings.onlyImmersiveScenarios
            || wallStructures != Settings.onlyImmersiveWallStructures
            || furniture != Settings.onlyImmersiveFurniture
            || weapons != Settings.onlyImmersiveWeapons
            || apparel != Settings.onlyImmersiveApparel
            || origins != Settings.onlyImmersiveIdeologyOrigins
            || quests != Settings.onlyImmersiveQuests
            || storytellers != Settings.onlyImmersiveStorytellers)
        {
            Settings.onlyImmersiveFactions = factions;
            Settings.onlyImmersiveScenarios = scenarios;
            Settings.onlyImmersiveWallStructures = wallStructures;
            Settings.onlyImmersiveFurniture = furniture;
            Settings.onlyImmersiveWeapons = weapons;
            Settings.onlyImmersiveApparel = apparel;
            Settings.onlyImmersiveIdeologyOrigins = origins;
            Settings.onlyImmersiveQuests = quests;
            Settings.onlyImmersiveStorytellers = storytellers;
            HHToolsRestoreApplier.Apply(Settings);
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        HHToolsRestoreApplier.Apply(Settings);
    }
}

internal static class HHToolsVanillaFactionSelectionApplier
{
    private static readonly string[] TargetFactionDefNames =
    {
        "OutlanderCivil",
        "OutlanderRough",
        "TribeCivil",
        "TribeRough",
        "TribeSavage",
        "Pirate",
        "TribeRoughNeanderthal",
        "PirateYttakin",
        "TribeSavageImpid",
        "OutlanderRoughPig",
        "PirateWaster",
        "SettlerCivil",
        "SettlerRough",
        "SettlerSavage",
        "VFEM2_KingdomCivil",
        "VFEM2_KingdomRough",
        "VFEM2_KingdomSavage",
        "VFEM2_CivilClan",
        "VFEM2_ClanRough",
        "VFEM2_ClanSavage"
    };

    private sealed class FactionSelectionState
    {
        public bool DisplayInFactionSelection;
        public int RequiredCountAtGameStart;
        public int StartingCountAtWorldCreation;
        public int MaxConfigurableAtWorldCreation;
        public float SettlementGenerationWeight;
    }

    private static readonly Dictionary<string, FactionSelectionState> OriginalStatesByFactionDefName = new();

    public static void Initialize()
    {
        foreach (string factionDefName in TargetFactionDefNames)
        {
            if (OriginalStatesByFactionDefName.ContainsKey(factionDefName))
            {
                continue;
            }

            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef != null)
            {
                OriginalStatesByFactionDefName[factionDefName] = new FactionSelectionState
                {
                    DisplayInFactionSelection = factionDef.displayInFactionSelection,
                    RequiredCountAtGameStart = factionDef.requiredCountAtGameStart,
                    StartingCountAtWorldCreation = factionDef.startingCountAtWorldCreation,
                    MaxConfigurableAtWorldCreation = factionDef.maxConfigurableAtWorldCreation,
                    SettlementGenerationWeight = factionDef.settlementGenerationWeight
                };
            }
        }
    }

    public static int Apply(bool hideVanillaFactions)
    {
        Initialize();
        int appliedCount = 0;

        foreach (string factionDefName in TargetFactionDefNames)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef == null)
            {
                continue;
            }

            if (hideVanillaFactions)
            {
                factionDef.displayInFactionSelection = false;
                factionDef.requiredCountAtGameStart = 0;
                factionDef.startingCountAtWorldCreation = 0;
                factionDef.maxConfigurableAtWorldCreation = 0;
                factionDef.settlementGenerationWeight = 0f;
            }
            else if (OriginalStatesByFactionDefName.TryGetValue(factionDefName, out FactionSelectionState originalState))
            {
                factionDef.displayInFactionSelection = originalState.DisplayInFactionSelection;
                factionDef.requiredCountAtGameStart = originalState.RequiredCountAtGameStart;
                factionDef.startingCountAtWorldCreation = originalState.StartingCountAtWorldCreation;
                factionDef.maxConfigurableAtWorldCreation = originalState.MaxConfigurableAtWorldCreation;
                factionDef.settlementGenerationWeight = originalState.SettlementGenerationWeight;
            }

            appliedCount++;
        }

        return appliedCount;
    }
}
