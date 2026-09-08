using UnityEngine;
using Verse;

namespace FIP.Arktos;

public sealed class ArktosSettings : ModSettings
{
    public bool onlyImmersiveNativeWildlife = true;
    public bool onlyImmersiveBiotechWildlife = true;
    public bool onlyImmersiveVanillaAnimalsExpandedWildlife = true;
    public bool onlyImmersiveRoyalAnimalsWildlife = true;
    public bool onlyImmersiveOdysseyWildlife = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveNativeWildlife, "onlyImmersiveNativeWildlife", "restoreNativeWildlife");
        LookImmersive(ref onlyImmersiveBiotechWildlife, "onlyImmersiveBiotechWildlife", "restoreBiotechWildlife");
        LookImmersive(ref onlyImmersiveVanillaAnimalsExpandedWildlife, "onlyImmersiveVanillaAnimalsExpandedWildlife", "restoreVanillaAnimalsExpandedWildlife");
        LookImmersive(ref onlyImmersiveRoyalAnimalsWildlife, "onlyImmersiveRoyalAnimalsWildlife", "restoreRoyalAnimalsWildlife");
        LookImmersive(ref onlyImmersiveOdysseyWildlife, "onlyImmersiveOdysseyWildlife", "restoreOdysseyWildlife");
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

public sealed class ArktosSettingsMod : Mod
{
    internal static ArktosSettings Settings;

    public ArktosSettingsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ArktosSettings>();
    }

    public override string SettingsCategory() => "FIP - Arktos";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive wildlife");
        Text.Font = GameFont.Small;
        listing.Label("These options keep animals that do not fit the Fallout setting out of normal traders and generic animal incidents. Disable an option to allow that group again. All options are enabled by default.");
        listing.GapLine();

        listing.CheckboxLabeled("Only immersive native wildlife", ref Settings.onlyImmersiveNativeWildlife,
            "Prevents certain base-game animals, such as elephants and thrumbos, from appearing with traders or in generic animal incidents. Their defs and any special content made specifically for them remain available. Disable this option to allow them again. Enabled by default. Restart required.");
        listing.CheckboxLabeled("Only immersive Biotech wildlife", ref Settings.onlyImmersiveBiotechWildlife,
            "Prevents Toxalopes and Waste Rats from appearing with traders or in generic animal incidents. Their defs and special content remain available. Disable this option to allow them again. Enabled by default. Restart required.");
        listing.CheckboxLabeled("Only immersive Vanilla Animals Expanded wildlife", ref Settings.onlyImmersiveVanillaAnimalsExpandedWildlife,
            "Prevents certain Vanilla Animals Expanded animals, such as lions, from appearing with traders or in generic animal incidents. Their defs and special content remain available. Disable this option to allow them again. Enabled by default. Restart required.");
        listing.CheckboxLabeled("Only immersive Royal Animals wildlife", ref Settings.onlyImmersiveRoyalAnimalsWildlife,
            "Prevents selected Royal Animals wildlife from appearing with traders or in generic animal incidents. Their defs and special content remain available. Disable this option to allow them again. Enabled by default. Restart required.");
        listing.CheckboxLabeled("Only immersive Odyssey wildlife", ref Settings.onlyImmersiveOdysseyWildlife,
            "Prevents selected Odyssey animals from appearing with traders, in generic animal incidents, and as ordinary rare fishing catches. Their defs and special content remain available. Disable this option to allow them again. Enabled by default. Restart required.");

        listing.End();
    }
}
