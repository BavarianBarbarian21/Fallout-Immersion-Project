using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP.WestTek;

internal static class WestTekSporeCarrierDeathUtility
{
    public static bool ShouldFertilizeOnDeath(Pawn pawn)
    {
        if (pawn == null || pawn.genes == null)
        {
            return false;
        }

        if (!pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        return WestTekFloraMutationUtility.HasGene(pawn, WestTekDefOf.WestTek_Gene_SporeCarrier);
    }

    public static void FertilizeDeathArea(Map map, IntVec3 center)
    {
        if (map == null)
        {
            return;
        }

        TerrainDef fertileTerrain = DefDatabase<TerrainDef>.GetNamedSilentFail("SoilRich");
        if (fertileTerrain == null)
        {
            Log.Warning("[FIP.WestTek] Could not find TerrainDef SoilRich for Spore Carrier death fertility.");
            return;
        }

        foreach (IntVec3 cell in CellsInSquare(center, 1))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            if (!CanReplaceTerrain(map, cell, fertileTerrain))
            {
                continue;
            }

            map.terrainGrid.SetTerrain(cell, fertileTerrain);
            FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Dirt, 1);
        }
    }

    private static IEnumerable<IntVec3> CellsInSquare(IntVec3 center, int radius)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                yield return new IntVec3(center.x + x, center.y, center.z + z);
            }
        }
    }

    private static bool CanReplaceTerrain(Map map, IntVec3 cell, TerrainDef targetTerrain)
    {
        TerrainDef currentTerrain = map.terrainGrid.TerrainAt(cell);

        if (currentTerrain == null)
        {
            return false;
        }

        if (currentTerrain == targetTerrain)
        {
            return false;
        }

        if (currentTerrain.fertility >= targetTerrain.fertility)
        {
            return false;
        }

        if (currentTerrain.IsWater)
        {
            return false;
        }

        if (currentTerrain.passability == Traversability.Impassable)
        {
            return false;
        }

        if (map.edificeGrid[cell] != null)
        {
            return false;
        }

        return true;
    }
}