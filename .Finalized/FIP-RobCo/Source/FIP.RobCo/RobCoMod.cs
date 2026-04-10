using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.RobCo;

public sealed class RobCoModSettings : ModSettings
{
    public bool removeVanillaMechanoids = true;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref removeVanillaMechanoids, "removeVanillaMechanoids", true);
    }
}

public sealed class RobCoMod : Mod
{
    internal static RobCoModSettings Settings;

    public RobCoMod(ModContentPack content)
        : base(content)
    {
        Settings = GetSettings<RobCoModSettings>();
        RobCoDefSettingsApplier.Initialize();
        RobCoDefSettingsApplier.Apply(Settings.removeVanillaMechanoids);
    }

    public override string SettingsCategory()
    {
        return "FIP - RobCo";
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        bool updatedValue = Settings.removeVanillaMechanoids;
        listing.CheckboxLabeled(
            "Remove vanilla mechanoid access",
            ref updatedValue,
            "Enabled by default. Limits mech gestators to RobCo recipes plus vanilla mech resurrection, and disables vanilla Ludeon mechanoids as combat mech threats.");

        if (updatedValue != Settings.removeVanillaMechanoids)
        {
            Settings.removeVanillaMechanoids = updatedValue;
            RobCoDefSettingsApplier.Apply(Settings.removeVanillaMechanoids);
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        RobCoDefSettingsApplier.Apply(Settings.removeVanillaMechanoids);
    }
}

internal static class RobCoDefSettingsApplier
{
    private static readonly string[] CivilWorkbenchRecipeDefNames =
    {
        "ResurrectLightMech",
        "RobCo_Eyebot_Recipe",
        "RobCo_EyebotDuraframe_Recipe",
        "RobCo_Constructron_Recipe",
        "RobCo_Factotron_Recipe",
        "RobCo_Utilitron_Recipe",
        "RobCo_Collectron_Recipe",
        "RobCo_Protectron_Recipe",
        "RobCo_Liberator_Recipe",
        "RobCo_SecuritronMkI_Recipe",
        "RobCo_SecuritronMkII_Recipe"
    };

    private static readonly string[] MilitaryWorkbenchRecipeDefNames =
    {
        "ResurrectMediumMech",
        "ResurrectHeavyMech",
        "ResurrectUltraheavyMech",
        "RobCo_MisterFarmhand_Recipe",
        "RobCo_MisterHandy_Recipe",
        "RobCo_MisterGutsy_Recipe",
        "RobCo_MissNanny_Recipe",
        "RobCo_Assaultron_Recipe",
        "RobCo_Cyberdog_Recipe",
        "RobCo_Robobrain_Recipe",
        "RobCo_SentrybotSiegebreaker_Recipe",
        "RobCo_SentrybotPestcontrol_Recipe",
        "RobCo_SentrybotAnnihilator_Recipe",
        "RobCo_WarMachine_Recipe",
        "RobCo_LibertyPrime_Recipe"
    };

    private static readonly Dictionary<string, List<RecipeDef>> OriginalRecipesByThingDef = new();
    private static readonly Dictionary<string, (bool isFighter, bool allowInMechClusters)> OriginalPawnKindFlagsByDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        CaptureRecipes("MechGestator");
        CaptureRecipes("LargeMechGestator");

        foreach (PawnKindDef pawnKindDef in DefDatabase<PawnKindDef>.AllDefsListForReading)
        {
            if (pawnKindDef?.race == null || !pawnKindDef.RaceProps.IsMechanoid)
            {
                continue;
            }

            if (pawnKindDef.defName.NullOrEmpty() || pawnKindDef.defName.StartsWith("RobCo_"))
            {
                continue;
            }

            string packageId = pawnKindDef.modContentPack?.PackageId;
            if (packageId == null || !packageId.StartsWith("ludeon.", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            OriginalPawnKindFlagsByDefName[pawnKindDef.defName] = (pawnKindDef.isFighter, pawnKindDef.allowInMechClusters);
        }

        initialized = true;
    }

    public static void Apply(bool removeVanillaMechanoids)
    {
        if (!initialized)
        {
            Initialize();
        }

        ApplyRecipes("MechGestator", removeVanillaMechanoids ? CivilWorkbenchRecipeDefNames : null);
        ApplyRecipes("LargeMechGestator", removeVanillaMechanoids ? MilitaryWorkbenchRecipeDefNames : null);

        foreach ((string pawnKindDefName, (bool isFighter, bool allowInMechClusters) originalFlags) in OriginalPawnKindFlagsByDefName)
        {
            PawnKindDef pawnKindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindDefName);
            if (pawnKindDef == null)
            {
                continue;
            }

            if (removeVanillaMechanoids)
            {
                pawnKindDef.isFighter = false;
                pawnKindDef.allowInMechClusters = false;
            }
            else
            {
                pawnKindDef.isFighter = originalFlags.isFighter;
                pawnKindDef.allowInMechClusters = originalFlags.allowInMechClusters;
            }
        }
    }

    private static void CaptureRecipes(string thingDefName)
    {
        ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
        if (thingDef?.recipes == null)
        {
            return;
        }

        OriginalRecipesByThingDef[thingDefName] = thingDef.recipes.ToList();
    }

    private static void ApplyRecipes(string thingDefName, string[] recipeDefNames)
    {
        ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
        if (thingDef == null)
        {
            return;
        }

        if (!OriginalRecipesByThingDef.TryGetValue(thingDefName, out List<RecipeDef> originalRecipes))
        {
            return;
        }

        if (recipeDefNames == null)
        {
            thingDef.recipes = originalRecipes.ToList();
            return;
        }

        List<RecipeDef> configuredRecipes = new(recipeDefNames.Length);
        foreach (string recipeDefName in recipeDefNames)
        {
            RecipeDef recipeDef = DefDatabase<RecipeDef>.GetNamedSilentFail(recipeDefName);
            if (recipeDef != null)
            {
                configuredRecipes.Add(recipeDef);
            }
        }

        thingDef.recipes = configuredRecipes;
    }
}