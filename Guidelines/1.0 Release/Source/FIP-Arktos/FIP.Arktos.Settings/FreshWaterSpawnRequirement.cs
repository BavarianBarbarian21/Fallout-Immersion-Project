using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.Arktos;

public sealed class ModExtension_BiomeFeatureRequirements : DefModExtension
{
    public bool requireFreshWater;
}

[StaticConstructorOnStartup]
internal static class ArktosHarmonyBootstrap
{
    static ArktosHarmonyBootstrap()
    {
        new Harmony("FIP.Arktos").PatchAll();
    }
}

[HarmonyPatch(typeof(WildAnimalSpawner), "CommonalityOfAnimalNow")]
internal static class WildAnimalSpawner_FreshWaterRequirement_Patch
{
    private static void Postfix(PawnKindDef def, Map ___map, ref float __result)
    {
        if (__result <= 0f || def?.race == null || ___map == null)
        {
            return;
        }

        ModExtension_BiomeFeatureRequirements extension =
            def.race.GetModExtension<ModExtension_BiomeFeatureRequirements>();

        if (extension?.requireFreshWater == true && !FreshWaterUtility.HasFreshWater(___map))
        {
            __result = 0f;
        }
    }
}

internal static class FreshWaterUtility
{
    public static bool HasFreshWater(Map map)
    {
        Tile tile = map.TileInfo;
        if (tile is SurfaceTile surfaceTile && !surfaceTile.Rivers.NullOrEmpty())
        {
            return true;
        }

        return tile.Mutators.Any(IsFreshWaterMutator);
    }

    private static bool IsFreshWaterMutator(TileMutatorDef mutator)
    {
        if (mutator == null)
        {
            return false;
        }

        string defName = mutator.defName ?? string.Empty;
        if (defName.IndexOf("Dry", StringComparison.OrdinalIgnoreCase) >= 0
            || defName.IndexOf("Lava", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        if (defName.Equals("Lakeshore", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mutator.categories.Any(category =>
            category.Equals("River", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Lake", StringComparison.OrdinalIgnoreCase)
            || category.Equals("Groundwater", StringComparison.OrdinalIgnoreCase));
    }
}
