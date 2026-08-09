using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Whitespring;

public sealed class WhitespringSettings : ModSettings
{
    public bool restoreStorytellers;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreStorytellers, "restoreStorytellers", false);
    }
}

public sealed class WhitespringSettingsMod : Mod
{
    private static readonly string[] StorytellerDefNames = { "VFEE_AriadneArchduchess", "VFED_Damocles" };
    private static readonly Dictionary<string, bool> OriginalVisibility = new();
    private static WhitespringSettings settings;

    public WhitespringSettingsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<WhitespringSettings>();
        CaptureAndApply();
        LongEventHandler.ExecuteWhenFinished(CaptureAndApply);
    }

    public override string SettingsCategory() => "FIP - Whitespring";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);
        bool value = settings.restoreStorytellers;
        listing.CheckboxLabeled("Restore storytellers", ref value,
            "Restores Ariadne Archduchess and Damocles without FIP text changes. Restart required.");
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
        foreach (string defName in StorytellerDefNames)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            if (!OriginalVisibility.ContainsKey(defName))
            {
                OriginalVisibility[defName] = def.listVisible;
            }

            def.listVisible = settings.restoreStorytellers ? OriginalVisibility[defName] : false;
        }
    }
}
