using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Arktos;

public sealed class ArktosSettings : ModSettings
{
    public bool restoreNativeWildlife;
    public bool restoreBiotechWildlife;
    public bool restoreVanillaAnimalsExpandedWildlife;
    public bool restoreRoyalAnimalsWildlife;
    public bool restoreOdysseyWildlife;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref restoreNativeWildlife, "restoreNativeWildlife", false);
        Scribe_Values.Look(ref restoreBiotechWildlife, "restoreBiotechWildlife", false);
        Scribe_Values.Look(ref restoreVanillaAnimalsExpandedWildlife, "restoreVanillaAnimalsExpandedWildlife", false);
        Scribe_Values.Look(ref restoreRoyalAnimalsWildlife, "restoreRoyalAnimalsWildlife", false);
        Scribe_Values.Look(ref restoreOdysseyWildlife, "restoreOdysseyWildlife", false);
    }
}

public sealed class ArktosSettingsMod : Mod
{
    internal static ArktosSettings Settings;

    public ArktosSettingsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ArktosSettings>();
        ArktosWildlifeApplier.Initialize();
        ApplySettings();
        LongEventHandler.ExecuteWhenFinished(ApplySettings);
    }

    public override string SettingsCategory() => "FIP - Arktos";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        bool native = Settings.restoreNativeWildlife;
        listing.CheckboxLabeled("Restore native wildlife", ref native,
            "Restores native wildlife outside Arktos biomes. Trader and caravan sources are never changed. Requires a new world.");
        bool biotech = Settings.restoreBiotechWildlife;
        listing.CheckboxLabeled("Restore Biotech wildlife", ref biotech,
            "Restores Toxalope and Waste Rat outside Arktos biomes. Requires a new world.");
        bool vae = Settings.restoreVanillaAnimalsExpandedWildlife;
        listing.CheckboxLabeled("Restore Vanilla Animals Expanded wildlife", ref vae,
            "Restores suppressed VAE wildlife outside Arktos biomes. Requires a new world.");
        bool royal = Settings.restoreRoyalAnimalsWildlife;
        listing.CheckboxLabeled("Restore Royal Animals wildlife", ref royal,
            "Restores suppressed Royal Animals wildlife outside Arktos biomes. Requires a new world.");
        bool odyssey = Settings.restoreOdysseyWildlife;
        listing.CheckboxLabeled("Restore Odyssey wildlife", ref odyssey,
            "Restores suppressed Odyssey wildlife outside Arktos biomes. Requires a new world.");

        if (native != Settings.restoreNativeWildlife || biotech != Settings.restoreBiotechWildlife
            || vae != Settings.restoreVanillaAnimalsExpandedWildlife || royal != Settings.restoreRoyalAnimalsWildlife
            || odyssey != Settings.restoreOdysseyWildlife)
        {
            Settings.restoreNativeWildlife = native;
            Settings.restoreBiotechWildlife = biotech;
            Settings.restoreVanillaAnimalsExpandedWildlife = vae;
            Settings.restoreRoyalAnimalsWildlife = royal;
            Settings.restoreOdysseyWildlife = odyssey;
            ApplySettings();
        }

        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        ApplySettings();
    }

    private static void ApplySettings() => ArktosWildlifeApplier.Apply(Settings);
}

internal static class ArktosWildlifeApplier
{
    private sealed class BiomeState
    {
        public readonly Dictionary<FieldInfo, List<object>> AnimalLists = new();
    }

    private static readonly string[] NativeAnimalDefNames =
    {
        "Alpaca", "Alphabeaver", "Bear_Grizzly", "Boomalope", "Boomrat", "Capybara", "Cassowary", "Chicken",
        "Chinchilla", "Cow", "Deer", "Donkey", "Dromedary", "Elephant", "Elk", "Emu", "Fox_Fennec", "Gazelle",
        "Goat", "GuineaPig", "Hare", "Ibex", "Monkey", "Ostrich", "Panther", "Pig", "Rhinoceros", "Sheep",
        "Thrumbo", "Warg", "Yak"
    };
    private static readonly string[] BiotechAnimalDefNames = { "Toxalope", "WasteRat" };
    private static readonly string[] VanillaAnimalsExpandedDefNames =
    {
        "AEXP_Giraffe", "AEXP_Zebra", "AEXP_Wildebeest", "AEXP_Crocodile", "AEXP_Cheetah", "AEXP_Boombat",
        "AEXP_Kangaroo", "AEXP_Koala", "AEXP_Platypus", "AEXP_BlackBear", "AEXP_Hyena", "AEXP_Lion",
        "AEXP_Camel", "AEXP_Megascorpion", "AEXP_RedPanda", "AEXP_Jaguar", "AEXP_Lemur", "AEXP_Mandrill",
        "AEXP_Tapir", "AEXP_IndianElephant", "AEXP_MegaWolverine"
    };
    private static readonly string[] RoyalAnimalsDefNames =
    {
        "VAERoy_AngoraRabbit", "VAERoy_Megachicken", "VAERoy_Orangutan", "VAERoy_RoyalTiger"
    };
    private static readonly string[] OdysseyAnimalDefNames =
    {
        "Alligator", "AlphaThrumbo", "Crow", "Flamingo", "Gorilla", "Hippo", "LavaSnail", "Macaw", "Mastodon",
        "Megavole", "MonitorLizard", "Panda", "Peacock", "Penguin", "PrairieDog", "StoneCrab", "Tiger", "Wolf_Great"
    };

    private static readonly FieldInfo[] AnimalListFields =
    {
        typeof(BiomeDef).GetField("wildAnimals", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(BiomeDef).GetField("coastalWildAnimals", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(BiomeDef).GetField("pollutionWildAnimals", BindingFlags.Instance | BindingFlags.NonPublic)
    };
    private static readonly FieldInfo AnimalRecordPawnKind = typeof(BiomeAnimalRecord).GetField("animal", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly Dictionary<string, BiomeState> OriginalStatesByBiomeDefName = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        foreach (BiomeDef biomeDef in DefDatabase<BiomeDef>.AllDefsListForReading)
        {
            if (biomeDef == null || biomeDef.defName == null)
            {
                continue;
            }

            BiomeState state = new();
            foreach (FieldInfo field in AnimalListFields)
            {
                if (field?.GetValue(biomeDef) is not IList list)
                {
                    continue;
                }

                List<object> records = new();
                foreach (object record in list)
                {
                    records.Add(record);
                }
                state.AnimalLists[field] = records;
            }
            OriginalStatesByBiomeDefName[biomeDef.defName] = state;
        }

        initialized = true;
    }

    public static void Apply(ArktosSettings settings)
    {
        Initialize();
        HashSet<string> suppressed = new(StringComparer.OrdinalIgnoreCase);
        AddUnlessRestored(suppressed, NativeAnimalDefNames, settings.restoreNativeWildlife);
        AddUnlessRestored(suppressed, BiotechAnimalDefNames, settings.restoreBiotechWildlife);
        AddUnlessRestored(suppressed, VanillaAnimalsExpandedDefNames, settings.restoreVanillaAnimalsExpandedWildlife);
        AddUnlessRestored(suppressed, RoyalAnimalsDefNames, settings.restoreRoyalAnimalsWildlife);
        AddUnlessRestored(suppressed, OdysseyAnimalDefNames, settings.restoreOdysseyWildlife);

        foreach ((string biomeDefName, BiomeState state) in OriginalStatesByBiomeDefName)
        {
            BiomeDef biomeDef = DefDatabase<BiomeDef>.GetNamedSilentFail(biomeDefName);
            if (biomeDef == null)
            {
                continue;
            }

            HashSet<string> effectiveSuppressed = new(suppressed, StringComparer.OrdinalIgnoreCase);
            if (IsArktosBiome(biomeDef))
            {
                effectiveSuppressed.UnionWith(NativeAnimalDefNames);
                effectiveSuppressed.UnionWith(BiotechAnimalDefNames);
                effectiveSuppressed.UnionWith(VanillaAnimalsExpandedDefNames);
                effectiveSuppressed.UnionWith(RoyalAnimalsDefNames);
                effectiveSuppressed.UnionWith(OdysseyAnimalDefNames);
            }

            foreach ((FieldInfo field, List<object> originalRecords) in state.AnimalLists)
            {
                if (field.GetValue(biomeDef) is not IList list)
                {
                    continue;
                }

                list.Clear();
                foreach (object record in originalRecords)
                {
                    PawnKindDef pawnKind = AnimalRecordPawnKind?.GetValue(record) as PawnKindDef;
                    if (pawnKind?.defName == null || !effectiveSuppressed.Contains(pawnKind.defName))
                    {
                        list.Add(record);
                    }
                }
            }
        }
    }

    private static void AddUnlessRestored(HashSet<string> destination, IEnumerable<string> animalDefNames, bool restored)
    {
        if (!restored)
        {
            destination.UnionWith(animalDefNames);
        }
    }

    private static bool IsArktosBiome(BiomeDef biomeDef)
    {
        return biomeDef.defName.StartsWith("Arktos_", StringComparison.OrdinalIgnoreCase);
    }
}
