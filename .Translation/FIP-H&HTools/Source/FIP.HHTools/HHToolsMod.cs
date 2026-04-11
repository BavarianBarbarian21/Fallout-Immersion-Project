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
            "Hide vanilla factions at world creation",
            ref updatedValue,
            "Enabled by default. Sets vanilla and vanilla-biotech faction counts to zero in the world-creation configurator so the FIP faction set is foregrounded.");

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
        "PirateWaster"
    };

    private static readonly Dictionary<string, int> OriginalValuesByFactionDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        foreach (string factionDefName in TargetFactionDefNames)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef != null)
            {
                OriginalValuesByFactionDefName[factionDefName] = factionDef.maxConfigurableAtWorldCreation;
            }
        }

        initialized = true;
    }

    public static void Apply(bool hideVanillaFactions)
    {
        if (!initialized)
        {
            Initialize();
        }

        foreach ((string factionDefName, int originalValue) in OriginalValuesByFactionDefName)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef == null)
            {
                continue;
            }

            factionDef.maxConfigurableAtWorldCreation = hideVanillaFactions ? 0 : originalValue;
        }
    }
}