using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Whitespring;

public sealed class WhitespringSettings : ModSettings
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

public sealed class WhitespringSettingsMod : Mod
{
    private static readonly string[] StorytellerDefNames = { "VFEE_AriadneArchduchess", "VFED_Damocles" };
    private static readonly Dictionary<string, bool> OriginalVisibility = new();
    private static WhitespringSettings settings;

    public WhitespringSettingsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<WhitespringSettings>();
        LongEventHandler.ExecuteWhenFinished(CaptureAndApply);
    }

    public override string SettingsCategory() => "FIP - Whitespring";

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
            "Hides Ariadne Archduchess and Damocles from storyteller selection. Restart required.");
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

            def.listVisible = settings.onlyImmersiveStorytellers ? false : OriginalVisibility[defName];
        }
    }
}
