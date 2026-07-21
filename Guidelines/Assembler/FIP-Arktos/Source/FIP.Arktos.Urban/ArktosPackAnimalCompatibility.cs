using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using Verse;

namespace FIP.Arktos;

[StaticConstructorOnStartup]
internal static class ArktosPackAnimalCompatibility
{
    private static readonly FieldInfo AllowedPackAnimalsField = typeof(BiomeDef).GetField(
        "allowedPackAnimals",
        BindingFlags.Instance | BindingFlags.NonPublic);

    static ArktosPackAnimalCompatibility()
    {
        LongEventHandler.ExecuteWhenFinished(ExpandAllowedPackAnimals);
    }

    private static void ExpandAllowedPackAnimals()
    {
        if (AllowedPackAnimalsField == null)
        {
            Log.Error("[FIP - Arktos] Could not access BiomeDef.allowedPackAnimals.");
            return;
        }

        List<ThingDef> loadedTraderCarrierRaces = DefDatabase<FactionDef>.AllDefsListForReading
            .Where(faction => faction.pawnGroupMakers != null)
            .SelectMany(faction => faction.pawnGroupMakers)
            .Where(maker => maker.kindDef == PawnGroupKindDefOf.Trader && maker.carriers != null)
            .SelectMany(maker => maker.carriers)
            .Select(option => option.kind?.race)
            .Where(race => race != null)
            .Distinct()
            .ToList();

        int addedEntries = 0;
        int affectedBiomes = 0;

        foreach (BiomeDef biome in DefDatabase<BiomeDef>.AllDefsListForReading)
        {
            if (!biome.defName.StartsWith("Arktos_"))
            {
                continue;
            }

            List<ThingDef> allowedPackAnimals =
                AllowedPackAnimalsField.GetValue(biome) as List<ThingDef>;
            if (allowedPackAnimals == null)
            {
                allowedPackAnimals = new List<ThingDef>();
                AllowedPackAnimalsField.SetValue(biome, allowedPackAnimals);
            }

            int countBefore = allowedPackAnimals.Count;

            foreach (ThingDef carrierRace in loadedTraderCarrierRaces)
            {
                if (!allowedPackAnimals.Contains(carrierRace))
                {
                    allowedPackAnimals.Add(carrierRace);
                }
            }

            int addedToBiome = allowedPackAnimals.Count - countBefore;
            if (addedToBiome > 0)
            {
                affectedBiomes++;
                addedEntries += addedToBiome;
            }
        }

        Log.Message(
            $"[FIP - Arktos] Added {addedEntries} trader-carrier compatibility entries " +
            $"across {affectedBiomes} Arktos biomes.");
    }
}
