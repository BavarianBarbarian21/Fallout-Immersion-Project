using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Donaustahl;

public sealed class DonaustahlSettings : ModSettings
{
    public bool restoreBackstories;
    public bool restoreEquipmentRelics;
    public bool restoreStorytellers;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreBackstories, "restoreBackstories", false);
        Scribe_Values.Look(ref restoreEquipmentRelics, "restoreEquipmentRelics", false);
        Scribe_Values.Look(ref restoreStorytellers, "restoreStorytellers", false);
    }
}

public sealed class DonaustahlSettingsMod : Mod
{
    internal static DonaustahlSettings Settings;

    public DonaustahlSettingsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<DonaustahlSettings>();
        DonaustahlRestoreApplier.Initialize();
        ApplySettings();
        LongEventHandler.ExecuteWhenFinished(ApplySettings);
    }

    public override string SettingsCategory() => "FIP - Donaustahl";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        bool backstories = Settings.restoreBackstories;
        listing.CheckboxLabeled("Restore removed backstories", ref backstories,
            "Restores all Vanilla and Vanilla Expanded backstories suppressed by Donaustahl. Restart required.");

        bool relics = Settings.restoreEquipmentRelics;
        listing.CheckboxLabeled("Restore equipment relics", ref relics,
            "Restores original relic eligibility for apparel, armour, weapons, and artifacts. Restart required.");

        bool storytellers = Settings.restoreStorytellers;
        listing.CheckboxLabeled("Restore storytellers", ref storytellers,
            "Restores Cassandra, Phoebe, and Randy without FIP text changes. Restart required.");

        if (backstories != Settings.restoreBackstories || relics != Settings.restoreEquipmentRelics || storytellers != Settings.restoreStorytellers)
        {
            Settings.restoreBackstories = backstories;
            Settings.restoreEquipmentRelics = relics;
            Settings.restoreStorytellers = storytellers;
            ApplySettings();
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        ApplySettings();
    }

    private static void ApplySettings() => DonaustahlRestoreApplier.Apply(Settings);
}

internal static class DonaustahlRestoreApplier
{
    private static readonly string[] SuppressedBackstories = { "VBE_Oracle", "VBE_MedievalPage" };
    private static readonly string[] SuppressedStorytellers = { "Cassandra", "Phoebe", "Randy" };
    private static readonly Dictionary<string, bool> BackstoryShuffleableStates = new();
    private static readonly Dictionary<string, float> RelicChanceStates = new();
    private static readonly Dictionary<string, bool> StorytellerVisibilityStates = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        foreach (string defName in SuppressedBackstories)
        {
            BackstoryDef def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                BackstoryShuffleableStates[defName] = def.shuffleable;
            }
        }

        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def != null && def.relicChance > 0f && IsEquipment(def))
            {
                RelicChanceStates[def.defName] = def.relicChance;
            }
        }

        foreach (string defName in SuppressedStorytellers)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                StorytellerVisibilityStates[defName] = def.listVisible;
            }
        }

        initialized = true;
    }

    public static void Apply(DonaustahlSettings settings)
    {
        Initialize();

        foreach ((string defName, bool shuffleable) in BackstoryShuffleableStates)
        {
            BackstoryDef def = DefDatabase<BackstoryDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.shuffleable = settings.restoreBackstories ? shuffleable : false;
            }
        }

        foreach ((string defName, float relicChance) in RelicChanceStates)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.relicChance = settings.restoreEquipmentRelics ? relicChance : 0f;
            }
        }

        foreach ((string defName, bool listVisible) in StorytellerVisibilityStates)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.listVisible = settings.restoreStorytellers ? listVisible : false;
            }
        }
    }

    private static bool IsEquipment(ThingDef def)
    {
        return def.apparel != null || def.IsWeapon || def.defName.Contains("Apparel") || def.defName.Contains("Armor")
            || def.defName.Contains("Weapon") || def.defName.Contains("Gun") || def.defName.Contains("Melee");
    }
}
