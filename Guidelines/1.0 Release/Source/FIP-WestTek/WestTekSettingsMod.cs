using UnityEngine;
using Verse;

namespace FIP.WestTek;

public sealed class WestTekModSettings : ModSettings
{
    public bool onlyImmersiveXenotypes = true;

    public override void ExposeData()
    {
        bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
        bool hasNewValue = loading && Scribe.loader.curXmlParent?["onlyImmersiveXenotypes"] != null;
        bool hasLegacyValue = loading && Scribe.loader.curXmlParent?["restoreXenotypes"] != null;
        Scribe_Values.Look(ref onlyImmersiveXenotypes, "onlyImmersiveXenotypes", true);
        if (loading && !hasNewValue && hasLegacyValue)
        {
            bool legacyRestore = true;
            Scribe_Values.Look(ref legacyRestore, "restoreXenotypes", true);
            onlyImmersiveXenotypes = !legacyRestore;
        }
    }
}

public sealed class WestTekMod : Mod
{
    internal static WestTekModSettings Settings;

    public WestTekMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<WestTekModSettings>();

        // The original faction and Sanguophage XML patches read this setting.
    }

    public override string SettingsCategory()
    {
        return "FIP - WestTek";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive xenotypes");
        Text.Font = GameFont.Small;
        listing.Label("This option replaces certain original xenotype appearances with the FIP selection. Disable it to restore the original selection. It is enabled by default.");
        listing.GapLine();

        bool updatedValue = Settings.onlyImmersiveXenotypes;
        listing.CheckboxLabeled(
            "Only immersive xenotypes",
            ref updatedValue,
            "Removes certain original xenotypes from factions that WestTek replaces. Disable this option to restore them. Other mods' xenotypes remain available. Enabled by default. Restart required; start a new world for faction changes.");

        if (updatedValue != Settings.onlyImmersiveXenotypes)
        {
            Settings.onlyImmersiveXenotypes = updatedValue;
        }

        listing.End();
    }
}
