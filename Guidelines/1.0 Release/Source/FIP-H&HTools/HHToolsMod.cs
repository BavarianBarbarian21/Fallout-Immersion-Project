using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public sealed class HHToolsModSettings : ModSettings
{
    public bool restoreFactions;
    public bool restoreScenarios;
    public bool restoreBuildings;
    public bool restoreWeapons;
    public bool restoreApparel;
    public bool restoreQuests;
    public bool restoreStorytellers;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreFactions, "restoreFactions", false);
        Scribe_Values.Look(ref restoreScenarios, "restoreScenarios", false);
        Scribe_Values.Look(ref restoreBuildings, "restoreBuildings", false);
        Scribe_Values.Look(ref restoreWeapons, "restoreWeapons", false);
        Scribe_Values.Look(ref restoreApparel, "restoreApparel", false);
        Scribe_Values.Look(ref restoreQuests, "restoreQuests", false);
        Scribe_Values.Look(ref restoreStorytellers, "restoreStorytellers", false);
    }
}

public sealed class HHToolsMod : Mod
{
    internal static HHToolsModSettings Settings;

    public HHToolsMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<HHToolsModSettings>();
        HHToolsRestoreApplier.Initialize();
        HHToolsRestoreApplier.Apply(Settings);
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

        bool factions = Settings.restoreFactions;
        listing.CheckboxLabeled(
            "Restore factions",
            ref factions,
            "Restores the Core, Biotech, Settlers, and Medieval faction templates to faction selection and world generation. Requires a new world.");

        bool scenarios = Settings.restoreScenarios;
        listing.CheckboxLabeled("Restore scenarios", ref scenarios, "Restores the Medieval and Settlers scenarios hidden by FIP. Restart required.");

        bool buildings = Settings.restoreBuildings;
        listing.CheckboxLabeled("Restore buildings", ref buildings, "Restores Medieval construction designations and categories. Restart required.");

        bool weapons = Settings.restoreWeapons;
        listing.CheckboxLabeled("Restore weapons", ref weapons, "Restores Medieval weapons to crafting and normal generation. Restart required.");

        bool apparel = Settings.restoreApparel;
        listing.CheckboxLabeled("Restore apparel", ref apparel, "Restores Medieval apparel and shields to crafting and normal generation. Restart required.");

        bool quests = Settings.restoreQuests;
        listing.CheckboxLabeled("Restore quests", ref quests, "Restores disabled Medieval and Settlers quests. Restart required.");

        bool storytellers = Settings.restoreStorytellers;
        listing.CheckboxLabeled("Restore storytellers", ref storytellers, "Restores Maynard, Talon, and Diego Dire without FIP text overrides. Restart required.");

        if (factions != Settings.restoreFactions
            || scenarios != Settings.restoreScenarios
            || buildings != Settings.restoreBuildings
            || weapons != Settings.restoreWeapons
            || apparel != Settings.restoreApparel
            || quests != Settings.restoreQuests
            || storytellers != Settings.restoreStorytellers)
        {
            Settings.restoreFactions = factions;
            Settings.restoreScenarios = scenarios;
            Settings.restoreBuildings = buildings;
            Settings.restoreWeapons = weapons;
            Settings.restoreApparel = apparel;
            Settings.restoreQuests = quests;
            Settings.restoreStorytellers = storytellers;
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
