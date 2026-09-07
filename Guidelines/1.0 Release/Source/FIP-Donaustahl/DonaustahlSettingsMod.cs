using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Donaustahl;

public sealed class DonaustahlSettings : ModSettings
{
    public bool onlyImmersiveBackstories = true;
    public bool onlyImmersiveEquipmentRelics = true;
    public bool onlyImmersiveStorytellers = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveBackstories, "onlyImmersiveBackstories", "restoreBackstories");
        LookImmersive(ref onlyImmersiveEquipmentRelics, "onlyImmersiveEquipmentRelics", "restoreEquipmentRelics");
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

public sealed class DonaustahlSettingsMod : Mod
{
    internal static DonaustahlSettings Settings;

    public DonaustahlSettingsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<DonaustahlSettings>();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            DonaustahlRestoreApplier.Initialize();
            ApplySettings();
        });
    }

    public override string SettingsCategory() => "FIP - Donaustahl";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive core content");
        Text.Font = GameFont.Small;
        listing.Label("These options hide content that does not fit the Fallout setting. Disable an option to restore that content. All options are enabled by default.");
        listing.GapLine();

        bool backstories = Settings.onlyImmersiveBackstories;
        listing.CheckboxLabeled("Only immersive backstories", ref backstories,
            "Removes certain character backstories that do not fit FIP. Disable this option to restore them. Enabled by default. Restart required.");

        bool relics = Settings.onlyImmersiveEquipmentRelics;
        listing.CheckboxLabeled("Only immersive equipment relics", ref relics,
            "Prevents ordinary clothing, armour, and weapons from being chosen as ideology relics. Disable this option to allow them again. Enabled by default. Restart required.");

        bool storytellers = Settings.onlyImmersiveStorytellers;
        listing.CheckboxLabeled("Only immersive storytellers", ref storytellers,
            "Removes Cassandra, Phoebe, and Randy from storyteller selection. Disable this option to restore them. Enabled by default. Restart required.");

        if (backstories != Settings.onlyImmersiveBackstories || relics != Settings.onlyImmersiveEquipmentRelics || storytellers != Settings.onlyImmersiveStorytellers)
        {
            Settings.onlyImmersiveBackstories = backstories;
            Settings.onlyImmersiveEquipmentRelics = relics;
            Settings.onlyImmersiveStorytellers = storytellers;
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

        if (DefDatabase<StorytellerDef>.GetNamedSilentFail("Cassandra") == null)
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
                def.shuffleable = settings.onlyImmersiveBackstories ? false : shuffleable;
            }
        }

        foreach ((string defName, float relicChance) in RelicChanceStates)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.relicChance = settings.onlyImmersiveEquipmentRelics ? 0f : relicChance;
            }
        }

        foreach ((string defName, bool listVisible) in StorytellerVisibilityStates)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.listVisible = settings.onlyImmersiveStorytellers ? false : listVisible;
            }
        }
    }

    private static bool IsEquipment(ThingDef def)
    {
        // Inherited apparel/weapon properties identify equipment after loading;
        // substrings also matched unrelated things such as weapon display cases.
        return def.apparel != null || def.IsWeapon;
    }
}
