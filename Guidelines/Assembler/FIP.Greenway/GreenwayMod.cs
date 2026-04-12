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
    public bool hideVanillaIdeologyFactions = true;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref hideVanillaIdeologyOrigins, "hideVanillaIdeologyOrigins", true);
        Scribe_Values.Look(ref hideVanillaMemes, "hideVanillaMemes", true);
        Scribe_Values.Look(ref hideVanillaIdeologyFactions, "hideVanillaIdeologyFactions", true);
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
        GreenwayVanillaIdeologyFactionApplier.Initialize();
        ApplySettings();
        LongEventHandler.ExecuteWhenFinished(ApplySettings);
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

        bool updatedFactionValue = Settings.hideVanillaIdeologyFactions;
        listing.CheckboxLabeled(
            "Hide ideology faction variants",
            ref updatedFactionValue,
            "Enabled by default. Hides Ideology's cannibal tribe, nudist tribe, and cannibal pirate gang from faction selection so only the Fallout faction set remains configurable.");

        if (updatedOriginsValue != Settings.hideVanillaIdeologyOrigins
            || updatedMemesValue != Settings.hideVanillaMemes
            || updatedFactionValue != Settings.hideVanillaIdeologyFactions)
        {
            Settings.hideVanillaIdeologyOrigins = updatedOriginsValue;
            Settings.hideVanillaMemes = updatedMemesValue;
            Settings.hideVanillaIdeologyFactions = updatedFactionValue;
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
        GreenwayVanillaIdeologyFactionApplier.Apply(Settings.hideVanillaIdeologyFactions);
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
        foreach (string memeDefName in TargetMemeDefNames)
        {
            if (OriginalHiddenInChooseMemesStatesByMemeDefName.ContainsKey(memeDefName))
            {
                continue;
            }

            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef == null)
            {
                continue;
            }

            OriginalHiddenInChooseMemesStatesByMemeDefName[memeDefName] = memeDef.hiddenInChooseMemes;
        }

        initialized = initialized || OriginalHiddenInChooseMemesStatesByMemeDefName.Count > 0;
    }

    public static void Apply(bool hideVanillaIdeologyOrigins)
    {
        Initialize();

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
        foreach (MemeDef memeDef in DefDatabase<MemeDef>.AllDefsListForReading)
        {
            if (memeDef == null || string.IsNullOrEmpty(memeDef.defName))
            {
                continue;
            }

            ModContentPack modContentPack = memeDef.modContentPack;
            if (modContentPack == null || !IsVanillaPackage(modContentPack.PackageId))
            {
                continue;
            }

            if (OriginalHiddenInChooseMemesStatesByMemeDefName.ContainsKey(memeDef.defName))
            {
                continue;
            }

            OriginalHiddenInChooseMemesStatesByMemeDefName[memeDef.defName] = memeDef.hiddenInChooseMemes;
        }

        initialized = initialized || OriginalHiddenInChooseMemesStatesByMemeDefName.Count > 0;
    }

    public static void Apply(bool hideVanillaMemes)
    {
        Initialize();

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

internal static class GreenwayVanillaIdeologyFactionApplier
{
    private sealed class FactionVisibilityState
    {
        public bool Hidden;
        public bool DisplayInFactionSelection;
        public int MaxConfigurableAtWorldCreation;
    }

    private static readonly string[] TargetFactionDefNames =
    {
        "TribeCannibal",
        "NudistTribe",
        "CannibalPirate"
    };

    private static readonly Dictionary<string, FactionVisibilityState> OriginalStatesByFactionDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        foreach (string factionDefName in TargetFactionDefNames)
        {
            if (OriginalStatesByFactionDefName.ContainsKey(factionDefName))
            {
                continue;
            }

            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef == null)
            {
                continue;
            }

            OriginalStatesByFactionDefName[factionDefName] = new FactionVisibilityState
            {
                Hidden = factionDef.hidden,
                DisplayInFactionSelection = factionDef.displayInFactionSelection,
                MaxConfigurableAtWorldCreation = factionDef.maxConfigurableAtWorldCreation
            };
        }

        initialized = initialized || OriginalStatesByFactionDefName.Count > 0;
    }

    public static void Apply(bool hideVanillaIdeologyFactions)
    {
        Initialize();

        foreach ((string factionDefName, FactionVisibilityState originalState) in OriginalStatesByFactionDefName)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef == null)
            {
                continue;
            }

            factionDef.hidden = hideVanillaIdeologyFactions || originalState.Hidden;
            factionDef.displayInFactionSelection = !hideVanillaIdeologyFactions && originalState.DisplayInFactionSelection;
            factionDef.maxConfigurableAtWorldCreation = hideVanillaIdeologyFactions ? 0 : originalState.MaxConfigurableAtWorldCreation;
        }
    }
}