using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public sealed class HHToolsModSettings : ModSettings
{
    public bool hideVanillaFactions = true;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref hideVanillaFactions, "hideVanillaFactions", true);
    }
}

public sealed class HHToolsMod : Mod
{
    internal static HHToolsModSettings Settings;

    public HHToolsMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<HHToolsModSettings>();
        HHToolsVanillaFactionSelectionApplier.Initialize();
        HHToolsVanillaFactionSelectionApplier.Apply(Settings.hideVanillaFactions);
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            HHToolsVanillaFactionSelectionApplier.Initialize();
            int appliedCount = HHToolsVanillaFactionSelectionApplier.Apply(Settings.hideVanillaFactions);
            Log.Message(
                $"[FIP - H&H Tools] World-creation faction filter applied to {appliedCount} faction definitions " +
                $"(enabled: {Settings.hideVanillaFactions}).");
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

        bool updatedValue = Settings.hideVanillaFactions;
        listing.CheckboxLabeled(
            "Hide replaced factions at world creation",
            ref updatedValue,
            "Enabled by default. Removes the vanilla, vanilla-biotech, Settlers, and Medieval 2 faction templates replaced by the FIP faction set.");

        if (updatedValue != Settings.hideVanillaFactions)
        {
            Settings.hideVanillaFactions = updatedValue;
            HHToolsVanillaFactionSelectionApplier.Apply(Settings.hideVanillaFactions);
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        HHToolsVanillaFactionSelectionApplier.Apply(Settings.hideVanillaFactions);
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
        public int MaxCountAtGameStart;
        public int MaxConfigurableAtWorldCreation;
        public bool CanMakeRandomly;
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
                    MaxCountAtGameStart = factionDef.maxCountAtGameStart,
                    MaxConfigurableAtWorldCreation = factionDef.maxConfigurableAtWorldCreation,
                    CanMakeRandomly = factionDef.canMakeRandomly,
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
                factionDef.maxCountAtGameStart = 0;
                factionDef.maxConfigurableAtWorldCreation = 0;
                factionDef.canMakeRandomly = false;
                factionDef.settlementGenerationWeight = 0f;
            }
            else if (OriginalStatesByFactionDefName.TryGetValue(factionDefName, out FactionSelectionState originalState))
            {
                factionDef.displayInFactionSelection = originalState.DisplayInFactionSelection;
                factionDef.requiredCountAtGameStart = originalState.RequiredCountAtGameStart;
                factionDef.startingCountAtWorldCreation = originalState.StartingCountAtWorldCreation;
                factionDef.maxCountAtGameStart = originalState.MaxCountAtGameStart;
                factionDef.maxConfigurableAtWorldCreation = originalState.MaxConfigurableAtWorldCreation;
                factionDef.canMakeRandomly = originalState.CanMakeRandomly;
                factionDef.settlementGenerationWeight = originalState.SettlementGenerationWeight;
            }

            appliedCount++;
        }

        return appliedCount;
    }
}
