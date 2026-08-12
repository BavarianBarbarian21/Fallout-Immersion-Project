using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Greenway;

public sealed class GreenwayModSettings : ModSettings
{
    public bool onlyImmersiveIdeologyOrigins = true;
    public bool onlyImmersiveMemes = true;
    public bool onlyImmersiveFactions = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveIdeologyOrigins, "onlyImmersiveIdeologyOrigins", "restoreIdeologyOrigins");
        LookImmersive(ref onlyImmersiveMemes, "onlyImmersiveMemes", "restoreMemes");
        LookImmersive(ref onlyImmersiveFactions, "onlyImmersiveFactions", "restoreFactions");
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

public sealed class GreenwayMod : Mod
{
    internal static GreenwayModSettings Settings;

    public GreenwayMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<GreenwayModSettings>();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            GreenwayVanillaIdeologyOriginApplier.Initialize();
            GreenwayVanillaMemeApplier.Initialize();
            GreenwayVanillaIdeologyFactionApplier.Initialize();
            GreenwayVanillaExpandedFactionMemeApplier.Initialize();
            ApplySettings();
        });
    }

    public override string SettingsCategory()
    {
        return "FIP - Greenway";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive ideology");
        Text.Font = GameFont.Small;
        listing.Label("Enabled options keep ideology generation focused on the curated FIP selection.");
        listing.GapLine();

        bool updatedOriginsValue = Settings.onlyImmersiveIdeologyOrigins;
        listing.CheckboxLabeled(
            "Only immersive ideology origins",
            ref updatedOriginsValue,
            "Hides the replaced native and Vanilla Memes Expanded origins from the chooser and random ideology generation. Restart recommended.");

        bool updatedMemesValue = Settings.onlyImmersiveMemes;
        listing.CheckboxLabeled(
            "Only immersive memes",
            ref updatedMemesValue,
            "Hides the replaced native and Vanilla Expanded memes from the chooser and random ideology generation. Restart recommended.");

        bool updatedFactionValue = Settings.onlyImmersiveFactions;
        listing.CheckboxLabeled(
            "Only immersive factions",
            ref updatedFactionValue,
            "Hides the faction variants replaced by Greenway from faction selection and world generation. Requires a new world.");

        if (updatedOriginsValue != Settings.onlyImmersiveIdeologyOrigins
            || updatedMemesValue != Settings.onlyImmersiveMemes
            || updatedFactionValue != Settings.onlyImmersiveFactions)
        {
            Settings.onlyImmersiveIdeologyOrigins = updatedOriginsValue;
            Settings.onlyImmersiveMemes = updatedMemesValue;
            Settings.onlyImmersiveFactions = updatedFactionValue;
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
        GreenwayVanillaMemeApplier.Apply(Settings.onlyImmersiveMemes);
        GreenwayVanillaIdeologyOriginApplier.Apply(Settings.onlyImmersiveIdeologyOrigins);
        GreenwayVanillaExpandedFactionMemeApplier.Apply(Settings.onlyImmersiveMemes && Settings.onlyImmersiveIdeologyOrigins);
        GreenwayVanillaIdeologyFactionApplier.Apply(Settings.onlyImmersiveFactions);
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

    private sealed class MemeState
    {
        public bool Hidden;
        public float RandomizationWeight;
    }

    private static readonly Dictionary<string, MemeState> OriginalStatesByMemeDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        foreach (string memeDefName in TargetMemeDefNames)
        {
            Capture(memeDefName);
        }

        foreach (MemeDef memeDef in DefDatabase<MemeDef>.AllDefsListForReading)
        {
            if (memeDef?.defName != null && memeDef.defName.StartsWith("VME_Structure_", StringComparison.OrdinalIgnoreCase))
            {
                Capture(memeDef.defName);
            }
        }

        initialized = initialized || OriginalStatesByMemeDefName.Count > 0;
    }

    private static void Capture(string memeDefName)
    {
        if (OriginalStatesByMemeDefName.ContainsKey(memeDefName))
        {
            return;
        }

        MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
        if (memeDef == null)
        {
            return;
        }

        OriginalStatesByMemeDefName[memeDefName] = new MemeState
        {
            Hidden = memeDef.hiddenInChooseMemes,
            RandomizationWeight = memeDef.randomizationSelectionWeightFactor
        };
    }

    public static void Apply(bool hideVanillaIdeologyOrigins)
    {
        Initialize();

        foreach ((string memeDefName, MemeState originalState) in OriginalStatesByMemeDefName)
        {
            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef == null)
            {
                continue;
            }

            memeDef.hiddenInChooseMemes = hideVanillaIdeologyOrigins || originalState.Hidden;
            memeDef.randomizationSelectionWeightFactor = hideVanillaIdeologyOrigins ? 0f : originalState.RandomizationWeight;
        }
    }
}

internal static class GreenwayVanillaMemeApplier
{
    private sealed class MemeState
    {
        public bool Hidden;
        public float RandomizationWeight;
    }

    private static readonly Dictionary<string, MemeState> OriginalStatesByMemeDefName = new();
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
            if (modContentPack == null || !IsTargetPackage(modContentPack.PackageId))
            {
                continue;
            }

            if (OriginalStatesByMemeDefName.ContainsKey(memeDef.defName))
            {
                continue;
            }

            OriginalStatesByMemeDefName[memeDef.defName] = new MemeState
            {
                Hidden = memeDef.hiddenInChooseMemes,
                RandomizationWeight = memeDef.randomizationSelectionWeightFactor
            };
        }

        initialized = initialized || OriginalStatesByMemeDefName.Count > 0;
    }

    public static void Apply(bool hideVanillaMemes)
    {
        Initialize();

        foreach ((string memeDefName, MemeState originalState) in OriginalStatesByMemeDefName)
        {
            MemeDef memeDef = DefDatabase<MemeDef>.GetNamedSilentFail(memeDefName);
            if (memeDef == null)
            {
                continue;
            }

            memeDef.hiddenInChooseMemes = hideVanillaMemes || originalState.Hidden;
            memeDef.randomizationSelectionWeightFactor = hideVanillaMemes ? 0f : originalState.RandomizationWeight;
        }
    }

    private static bool IsTargetPackage(string packageId)
    {
        return !string.IsNullOrEmpty(packageId)
            && (packageId.StartsWith("ludeon.rimworld", StringComparison.OrdinalIgnoreCase)
                || packageId.StartsWith("vanillaexpanded.", StringComparison.OrdinalIgnoreCase));
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

internal static class GreenwayVanillaExpandedFactionMemeApplier
{
    private sealed class FactionMemeState
    {
        public List<MemeDef> AllowedMemes;
        public List<MemeWeight> StructureMemeWeights;
    }

    private static readonly Dictionary<string, FactionMemeState> OriginalStatesByFactionDefName = new();

    public static void Initialize()
    {
        foreach (FactionDef factionDef in DefDatabase<FactionDef>.AllDefsListForReading)
        {
            if (factionDef == null || factionDef.defName == null || OriginalStatesByFactionDefName.ContainsKey(factionDef.defName))
            {
                continue;
            }

            bool hasVanillaExpandedOrigin = factionDef.structureMemeWeights != null
                && factionDef.structureMemeWeights.Exists(weight => weight?.meme?.defName != null
                    && weight.meme.defName.StartsWith("VME_Structure_", StringComparison.OrdinalIgnoreCase));
            bool hasAnonymity = factionDef.allowedMemes != null
                && factionDef.allowedMemes.Exists(meme => meme?.defName == "VME_Anonymity");

            if (!hasVanillaExpandedOrigin && !hasAnonymity)
            {
                continue;
            }

            OriginalStatesByFactionDefName[factionDef.defName] = new FactionMemeState
            {
                AllowedMemes = factionDef.allowedMemes == null ? null : new List<MemeDef>(factionDef.allowedMemes),
                StructureMemeWeights = factionDef.structureMemeWeights == null ? null : new List<MemeWeight>(factionDef.structureMemeWeights)
            };
        }
    }

    public static void Apply(bool hideVanillaExpandedOrigins)
    {
        Initialize();

        foreach ((string factionDefName, FactionMemeState state) in OriginalStatesByFactionDefName)
        {
            FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(factionDefName);
            if (factionDef == null)
            {
                continue;
            }

            factionDef.allowedMemes = state.AllowedMemes == null ? null : new List<MemeDef>(state.AllowedMemes);
            factionDef.structureMemeWeights = state.StructureMemeWeights == null ? null : new List<MemeWeight>(state.StructureMemeWeights);

            if (!hideVanillaExpandedOrigins)
            {
                continue;
            }

            factionDef.allowedMemes?.RemoveAll(meme => meme?.defName == "VME_Anonymity");
            factionDef.structureMemeWeights?.RemoveAll(weight => weight?.meme?.defName != null
                && weight.meme.defName.StartsWith("VME_Structure_", StringComparison.OrdinalIgnoreCase));
        }
    }
}
