using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Hubris;

public sealed class HubrisSettings : ModSettings
{
    public bool onlyImmersiveStorytellers = true;

    public override void ExposeData()
    {
        bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
        bool hasNewValue = loading && Scribe.loader.curXmlParent?["onlyImmersiveStorytellers"] != null;
        bool hasLegacyValue = loading && Scribe.loader.curXmlParent?["restoreStorytellers"] != null;
        Scribe_Values.Look(ref onlyImmersiveStorytellers, "onlyImmersiveStorytellers", true);
        if (loading && !hasNewValue && hasLegacyValue)
        {
            bool legacyRestore = false;
            Scribe_Values.Look(ref legacyRestore, "restoreStorytellers", false);
            onlyImmersiveStorytellers = !legacyRestore;
        }
    }
}

public sealed class HubrisSettingsMod : Mod
{
    private static readonly Dictionary<string, bool> OriginalVisibility = new();
    private static HubrisSettings settings;

    public HubrisSettingsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<HubrisSettings>();
        LongEventHandler.ExecuteWhenFinished(CaptureAndApply);
    }

    public override string SettingsCategory() => "FIP - Hubris";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);
        Text.Font = GameFont.Medium;
        listing.Label("Immersive storytellers");
        Text.Font = GameFont.Small;
        listing.Label("Keep storyteller selection focused on the curated Fallout experience.");
        listing.GapLine();
        bool value = settings.onlyImmersiveStorytellers;
        listing.CheckboxLabeled("Only immersive storytellers", ref value,
            "Hides Basilicus from storyteller selection. Restart required.");
        if (value != settings.onlyImmersiveStorytellers)
        {
            settings.onlyImmersiveStorytellers = value;
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

        def.listVisible = settings.onlyImmersiveStorytellers ? false : OriginalVisibility[def.defName];
    }
}
