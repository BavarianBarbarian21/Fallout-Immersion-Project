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
    public bool onlyImmersiveNativeWildlife = true;
    public bool onlyImmersiveBiotechWildlife = true;
    public bool onlyImmersiveVanillaAnimalsExpandedWildlife = true;
    public bool onlyImmersiveRoyalAnimalsWildlife = true;
    public bool onlyImmersiveOdysseyWildlife = true;

    public override void ExposeData()
    {
        LookImmersive(ref onlyImmersiveNativeWildlife, "onlyImmersiveNativeWildlife", "restoreNativeWildlife");
        LookImmersive(ref onlyImmersiveBiotechWildlife, "onlyImmersiveBiotechWildlife", "restoreBiotechWildlife");
        LookImmersive(ref onlyImmersiveVanillaAnimalsExpandedWildlife, "onlyImmersiveVanillaAnimalsExpandedWildlife", "restoreVanillaAnimalsExpandedWildlife");
        LookImmersive(ref onlyImmersiveRoyalAnimalsWildlife, "onlyImmersiveRoyalAnimalsWildlife", "restoreRoyalAnimalsWildlife");
        LookImmersive(ref onlyImmersiveOdysseyWildlife, "onlyImmersiveOdysseyWildlife", "restoreOdysseyWildlife");
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

public sealed class ArktosSettingsMod : Mod
{
    internal static ArktosSettings Settings;

    public ArktosSettingsMod(ModContentPack content) : base(content)
    {
        Settings = GetSettings<ArktosSettings>();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            ArktosWildlifeApplier.Initialize();
            ApplySettings();
        });
    }

    public override string SettingsCategory() => "FIP - Arktos";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);

        Text.Font = GameFont.Medium;
        listing.Label("Immersive wildlife");
        Text.Font = GameFont.Small;
        listing.Label("Enabled options keep the curated Arktos ecosystem and suppress the corresponding original wildlife outside Arktos biomes.");
        listing.GapLine();

        bool native = Settings.onlyImmersiveNativeWildlife;
        listing.CheckboxLabeled("Only immersive native wildlife", ref native,
            "Hides the selected native wildlife outside Arktos biomes. Trader and caravan sources are never changed. Requires a new world.");
        bool biotech = Settings.onlyImmersiveBiotechWildlife;
        listing.CheckboxLabeled("Only immersive Biotech wildlife", ref biotech,
            "Hides Toxalope and Waste Rat outside Arktos biomes. Requires a new world.");
        bool vae = Settings.onlyImmersiveVanillaAnimalsExpandedWildlife;
        listing.CheckboxLabeled("Only immersive Vanilla Animals Expanded wildlife", ref vae,
            "Hides the replaced Vanilla Animals Expanded wildlife outside Arktos biomes. Requires a new world.");
        bool royal = Settings.onlyImmersiveRoyalAnimalsWildlife;
        listing.CheckboxLabeled("Only immersive Royal Animals wildlife", ref royal,
            "Hides the replaced Royal Animals wildlife outside Arktos biomes. Requires a new world.");
        bool odyssey = Settings.onlyImmersiveOdysseyWildlife;
        listing.CheckboxLabeled("Only immersive Odyssey wildlife", ref odyssey,
            "Hides the replaced Odyssey wildlife outside Arktos biomes. Requires a new world.");

        if (native != Settings.onlyImmersiveNativeWildlife || biotech != Settings.onlyImmersiveBiotechWildlife
            || vae != Settings.onlyImmersiveVanillaAnimalsExpandedWildlife || royal != Settings.onlyImmersiveRoyalAnimalsWildlife
            || odyssey != Settings.onlyImmersiveOdysseyWildlife)
        {
            Settings.onlyImmersiveNativeWildlife = native;
            Settings.onlyImmersiveBiotechWildlife = biotech;
            Settings.onlyImmersiveVanillaAnimalsExpandedWildlife = vae;
            Settings.onlyImmersiveRoyalAnimalsWildlife = royal;
            Settings.onlyImmersiveOdysseyWildlife = odyssey;
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

        if (DefDatabase<BiomeDef>.AllDefsListForReading.Count == 0)
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
        AddIfImmersive(suppressed, NativeAnimalDefNames, settings.onlyImmersiveNativeWildlife);
        AddIfImmersive(suppressed, BiotechAnimalDefNames, settings.onlyImmersiveBiotechWildlife);
        AddIfImmersive(suppressed, VanillaAnimalsExpandedDefNames, settings.onlyImmersiveVanillaAnimalsExpandedWildlife);
        AddIfImmersive(suppressed, RoyalAnimalsDefNames, settings.onlyImmersiveRoyalAnimalsWildlife);
        AddIfImmersive(suppressed, OdysseyAnimalDefNames, settings.onlyImmersiveOdysseyWildlife);

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

    private static void AddIfImmersive(HashSet<string> destination, IEnumerable<string> animalDefNames, bool onlyImmersive)
    {
        if (onlyImmersive)
        {
            destination.UnionWith(animalDefNames);
        }
    }

    private static bool IsArktosBiome(BiomeDef biomeDef)
    {
        return biomeDef.defName.StartsWith("Arktos_", StringComparison.OrdinalIgnoreCase);
    }
}
