using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Hubris;

public sealed class HubrisSettings : ModSettings
{
    public bool restoreStorytellers;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreStorytellers, "restoreStorytellers", false);
    }
}

public sealed class HubrisSettingsMod : Mod
{
    private static readonly Dictionary<string, bool> OriginalVisibility = new();
    private static HubrisSettings settings;

    public HubrisSettingsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<HubrisSettings>();
        CaptureAndApply();
        LongEventHandler.ExecuteWhenFinished(CaptureAndApply);
    }

    public override string SettingsCategory() => "FIP - Hubris";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);
        bool value = settings.restoreStorytellers;
        listing.CheckboxLabeled("Restore storytellers", ref value,
            "Restores Basilicus without FIP text changes. Restart required.");
        if (value != settings.restoreStorytellers)
        {
            settings.restoreStorytellers = value;
            CaptureAndApply();
        }
        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        CaptureAndApply();
    }

    private static void CaptureAndApply()
    {
        StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail("VPE_Basilicus");
        if (def == null)
        {
            return;
        }

        if (!OriginalVisibility.ContainsKey(def.defName))
        {
            OriginalVisibility[def.defName] = def.listVisible;
        }

        def.listVisible = settings.restoreStorytellers ? OriginalVisibility[def.defName] : false;
    }
}
