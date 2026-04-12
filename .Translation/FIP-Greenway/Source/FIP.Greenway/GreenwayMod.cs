using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Greenway;

public sealed class GreenwayModSettings : ModSettings
{
    public bool hideVanillaIdeologyOrigins = true;
    public bool hideVanillaMemes = true;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref hideVanillaIdeologyOrigins, "hideVanillaIdeologyOrigins", true);
        Scribe_Values.Look(ref hideVanillaMemes, "hideVanillaMemes", true);
    }
}

public sealed class GreenwayMod : Mod
{
    internal static GreenwayModSettings Settings;

    public GreenwayMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<GreenwayModSettings>();
        GreenwayVanillaIdeologyOriginApplier.Initialize();
        GreenwayVanillaMemeApplier.Initialize();
        ApplySettings();
    }

    public override string SettingsCategory()
    {
        return "FIP - Greenway";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        bool updatedOriginsValue = Settings.hideVanillaIdeologyOrigins;
        listing.CheckboxLabeled(
            "Hide vanilla ideology origins",
            ref updatedOriginsValue,
            "Enabled by default. Hides the vanilla ideology origin structure memes so Greenway's settlement and religion replacements appear in the structure chooser instead.");

        bool updatedMemesValue = Settings.hideVanillaMemes;
        listing.CheckboxLabeled(
            "Hide vanilla memes",
            ref updatedMemesValue,
            "Enabled by default. Hides Ludeon's non-structure memes from the chooser so Greenway's custom memes replace them in the regular meme flow.");

        if (updatedOriginsValue != Settings.hideVanillaIdeologyOrigins || updatedMemesValue != Settings.hideVanillaMemes)
        {
            Settings.hideVanillaIdeologyOrigins = updatedOriginsValue;
            Settings.hideVanillaMemes = updatedMemesValue;
            ApplySettings();
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        ApplySettings();
    }

    private static void ApplySettings()
    {
        GreenwayVanillaIdeologyOriginApplier.Apply(Settings.hideVanillaIdeologyOrigins);
        GreenwayVanillaMemeApplier.Apply(Settings.hideVanillaMemes);
    }
}

internal static class GreenwayVanillaIdeologyOriginApplier
{
    private static readonly string[] TargetMemeDefNames =
    {
        "Structure_Ideological",
        "Structure_Animist",
        "Structure_OriginBuddhist",
        "Structure_Archist",
        "Structure_OriginIslamic",
        "Structure_OriginChristian",
        "Structure_TheistAbstract",
        "Structure_OriginHindu",
        "Structure_TheistEmbodied"
    };

    private static readonly Dictionary<string, bool> OriginalHiddenInChooseMemesStatesByMemeDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        foreach (string memeDefName in TargetMemeDefNames)
        {
            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef != null)
            {
                OriginalHiddenInChooseMemesStatesByMemeDefName[memeDefName] = memeDef.hiddenInChooseMemes;
            }
        }

        initialized = true;
    }

    public static void Apply(bool hideVanillaIdeologyOrigins)
    {
        if (!initialized)
        {
            Initialize();
        }

        foreach ((string memeDefName, bool originalHiddenInChooseMemesState) in OriginalHiddenInChooseMemesStatesByMemeDefName)
        {
            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef == null)
            {
                continue;
            }

            memeDef.hiddenInChooseMemes = hideVanillaIdeologyOrigins || originalHiddenInChooseMemesState;
        }
    }
}

internal static class GreenwayVanillaMemeApplier
{
    private static readonly Dictionary<string, bool> OriginalHiddenInChooseMemesStatesByMemeDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        foreach (MemeDef memeDef in DefDatabase<MemeDef>.AllDefsListForReading)
        {
            if (memeDef == null || memeDef.category == MemeCategory.Structure)
            {
                continue;
            }

            ModContentPack modContentPack = memeDef.modContentPack;
            if (modContentPack == null || !IsVanillaPackage(modContentPack.PackageId))
            {
                continue;
            }

            OriginalHiddenInChooseMemesStatesByMemeDefName[memeDef.defName] = memeDef.hiddenInChooseMemes;
        }

        initialized = true;
    }

    public static void Apply(bool hideVanillaMemes)
    {
        if (!initialized)
        {
            Initialize();
        }

        foreach ((string memeDefName, bool originalHiddenInChooseMemesState) in OriginalHiddenInChooseMemesStatesByMemeDefName)
        {
            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef == null)
            {
                continue;
            }

            memeDef.hiddenInChooseMemes = hideVanillaMemes || originalHiddenInChooseMemesState;
        }
    }

    private static bool IsVanillaPackage(string packageId)
    {
        return !string.IsNullOrEmpty(packageId) && packageId.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase);
    }
}