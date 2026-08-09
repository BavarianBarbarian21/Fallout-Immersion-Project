using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.WestTek;

public sealed class WestTekModSettings : ModSettings
{
    // WestTek keeps the original xenotype sources unless the player explicitly
    // asks for the curated FIP/FCP roster.
    public bool restoreXenotypes = true;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreXenotypes, "restoreXenotypes", true);
    }
}

public sealed class WestTekMod : Mod
{
    internal static WestTekModSettings Settings;

    public WestTekMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<WestTekModSettings>();

        // This is intentionally a load-time setting. The corresponding XML
        // operations are skipped before defs are constructed when it is on.
        if (!Settings.restoreXenotypes)
        {
            LongEventHandler.ExecuteWhenFinished(WestTekXenotypeRosterApplier.ApplyCuratedRoster);
        }
    }

    public override string SettingsCategory()
    {
        return "FIP - WestTek";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        bool updatedValue = Settings.restoreXenotypes;
        listing.CheckboxLabeled(
            "Restore Xenotypes",
            ref updatedValue,
            "Enabled by default. Keeps the original xenotype pools from RimWorld and installed third-party mods. Disable it to use only the curated FIP/FCP roster. Restart required; start a new world after changing faction or pawn generation.");

        if (updatedValue != Settings.restoreXenotypes)
        {
            Settings.restoreXenotypes = updatedValue;
        }

        listing.End();
    }
}

internal static class WestTekXenotypeRosterApplier
{
    private static readonly HashSet<string> NativeAllowList = new(StringComparer.OrdinalIgnoreCase)
    {
        "Baseliner",
        "Highmate",
        "Sanguophage"
    };

    private static readonly FieldInfo XenotypeChancesField = typeof(XenotypeSet).GetField(
        "xenotypeChances",
        BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo XenotypeChanceDefField = typeof(XenotypeChance).GetField(
        "xenotype",
        BindingFlags.Instance | BindingFlags.NonPublic);

    public static void ApplyCuratedRoster()
    {
        foreach (XenotypeDef xenotype in DefDatabase<XenotypeDef>.AllDefsListForReading)
        {
            if (xenotype == null || IsAllowed(xenotype))
            {
                continue;
            }

            // Keep the Def loaded for compatibility, but prevent it from being
            // selected by the ordinary pawn-generation paths.
            xenotype.canGenerateAsCombatant = false;
            xenotype.factionlessGenerationWeight = 0f;
        }

        foreach (FactionDef faction in DefDatabase<FactionDef>.AllDefsListForReading)
        {
            FilterXenotypeSet(faction?.xenotypeSet);
        }

        foreach (PawnKindDef pawnKind in DefDatabase<PawnKindDef>.AllDefsListForReading)
        {
            FilterXenotypeSet(pawnKind?.xenotypeSet);
        }
    }

    private static bool IsAllowed(XenotypeDef xenotype)
    {
        if (NativeAllowList.Contains(xenotype.defName))
        {
            return true;
        }

        string packageId = xenotype.modContentPack?.PackageId;
        if (packageId == null)
        {
            return false;
        }

        // Anthrosonae is deliberately left outside this setting. It is not in
        // the release load order, so WestTek must neither require nor suppress it.
        if (packageId.IndexOf("anthrosonae", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        return packageId.StartsWith("FIP.", StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith("Rick.FCP", StringComparison.OrdinalIgnoreCase);
    }

    private static void FilterXenotypeSet(XenotypeSet xenotypeSet)
    {
        if (xenotypeSet == null || XenotypeChancesField == null || XenotypeChanceDefField == null)
        {
            return;
        }

        IList entries = XenotypeChancesField.GetValue(xenotypeSet) as IList;
        if (entries == null)
        {
            return;
        }

        for (int index = entries.Count - 1; index >= 0; index--)
        {
            XenotypeDef xenotype = entries[index] == null
                ? null
                : XenotypeChanceDefField.GetValue(entries[index]) as XenotypeDef;
            if (xenotype == null || !IsAllowed(xenotype))
            {
                entries.RemoveAt(index);
            }
        }
    }
}
