using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public sealed class HHToolsModSettings : ModSettings
{
    public bool onlyImmersiveFactions = true;
    public bool onlyImmersiveScenarios = true;
    public bool onlyImmersiveBuildings = true;
    public bool onlyImmersiveWeapons = true;
    public bool onlyImmersiveApparel = true;
    public bool onlyImmersiveQuests = true;
    public bool onlyImmersiveStorytellers = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveFactions, "onlyImmersiveFactions", "restoreFactions");
        LookImmersive(ref onlyImmersiveScenarios, "onlyImmersiveScenarios", "restoreScenarios");
        LookImmersive(ref onlyImmersiveBuildings, "onlyImmersiveBuildings", "restoreBuildings");
        LookImmersive(ref onlyImmersiveWeapons, "onlyImmersiveWeapons", "restoreWeapons");
        LookImmersive(ref onlyImmersiveApparel, "onlyImmersiveApparel", "restoreApparel");
        LookImmersive(ref onlyImmersiveQuests, "onlyImmersiveQuests", "restoreQuests");
        LookImmersive(ref onlyImmersiveStorytellers, "onlyImmersiveStorytellers", "restoreStorytellers");
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
        listing.Label("Enabled options keep replaced Medieval, Tribal, and Settlers content out of normal selection and generation.");
        listing.GapLine();

        bool factions = Settings.onlyImmersiveFactions;
        listing.CheckboxLabeled(
            "Only immersive factions",
            ref factions,
            "Hides the replaced Core, Biotech, Settlers, and Medieval faction templates from selection and world generation. Requires a new world.");

        bool scenarios = Settings.onlyImmersiveScenarios;
        listing.CheckboxLabeled("Only immersive scenarios", ref scenarios, "Hides the original Medieval and Settlers scenarios, including New Kingdom. Restart required.");

        listing.Gap();
        Text.Font = GameFont.Medium;
        listing.Label("Immersive content pools");
        Text.Font = GameFont.Small;
        listing.GapLine();

        bool buildings = Settings.onlyImmersiveBuildings;
        listing.CheckboxLabeled("Only immersive buildings", ref buildings, "Hides replaced Medieval construction designations and categories. Restart required.");

        bool weapons = Settings.onlyImmersiveWeapons;
        listing.CheckboxLabeled("Only immersive weapons", ref weapons, "Removes replaced Medieval weapons from crafting and normal generation. Restart required.");

        bool apparel = Settings.onlyImmersiveApparel;
        listing.CheckboxLabeled("Only immersive apparel", ref apparel, "Removes replaced Medieval apparel and shields from crafting and normal generation. Restart required.");

        bool quests = Settings.onlyImmersiveQuests;
        listing.CheckboxLabeled("Only immersive quests", ref quests, "Disables the replaced Medieval and Settlers quests. Restart required.");

        bool storytellers = Settings.onlyImmersiveStorytellers;
        listing.CheckboxLabeled("Only immersive storytellers", ref storytellers, "Hides Maynard, Talon, and Diego Dire from storyteller selection. Restart required.");

        if (factions != Settings.onlyImmersiveFactions
            || scenarios != Settings.onlyImmersiveScenarios
            || buildings != Settings.onlyImmersiveBuildings
            || weapons != Settings.onlyImmersiveWeapons
            || apparel != Settings.onlyImmersiveApparel
            || quests != Settings.onlyImmersiveQuests
            || storytellers != Settings.onlyImmersiveStorytellers)
        {
            Settings.onlyImmersiveFactions = factions;
            Settings.onlyImmersiveScenarios = scenarios;
            Settings.onlyImmersiveBuildings = buildings;
            Settings.onlyImmersiveWeapons = weapons;
            Settings.onlyImmersiveApparel = apparel;
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
