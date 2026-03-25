using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.Repconn2;

public sealed class WorldGenStep_RepconnBiomeClusters : WorldGenStep
{
    private const float RuinsExpansionChance = 0.35f;
    private const float RuinsExpansionChanceSecond = 0.18f;
    private const float WastelandExpansionChance = 0.45f;
    private const float WastelandExpansionChanceSecond = 0.22f;

    public override int SeedPart => 184356721;

    public override void GenerateFresh(string seed, PlanetLayer layer)
    {
        if (layer == null || !layer.IsRootSurface)
        {
            return;
        }

        var craterBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Scarlands");
        var ruinsBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("FIP_Ruins");
        var wastelandBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("FIP_Wasteland");
        if (craterBiome == null || ruinsBiome == null || wastelandBiome == null)
        {
            return;
        }

        var surfaceTiles = layer.Tiles.OfType<SurfaceTile>().Where(static tile => !tile.WaterCovered).ToList();
        if (surfaceTiles.Count == 0)
        {
            return;
        }

        var craterIds = new HashSet<int>(surfaceTiles.Where(tile => tile.PrimaryBiome == craterBiome).Select(tile => tile.tile.tileId));
        if (craterIds.Count == 0)
        {
            SeedCraterCluster(seed, layer, surfaceTiles, craterBiome, craterIds);
        }

        if (craterIds.Count == 0)
        {
            return;
        }

        var ruinsIds = ExpandGuaranteedRing(layer, craterIds, craterIds, static tile => !tile.WaterCovered);
        ApplyBiome(layer, ruinsIds, ruinsBiome);

        var optionalRuinsFrontier = new HashSet<int>(ruinsIds);
        ExpandChanceRing(seed, layer, optionalRuinsFrontier, craterIds, ruinsIds, ruinsBiome, 20737, RuinsExpansionChance);
        ExpandChanceRing(seed, layer, optionalRuinsFrontier, craterIds, ruinsIds, ruinsBiome, 20738, RuinsExpansionChanceSecond);

        var blockedForWasteland = new HashSet<int>(craterIds);
        blockedForWasteland.UnionWith(ruinsIds);
        var wastelandIds = ExpandGuaranteedRing(layer, ruinsIds, blockedForWasteland, static tile => !tile.WaterCovered);
        ApplyBiome(layer, wastelandIds, wastelandBiome);

        var optionalWastelandFrontier = new HashSet<int>(wastelandIds);
        ExpandChanceRing(seed, layer, optionalWastelandFrontier, blockedForWasteland, wastelandIds, wastelandBiome, 28929, WastelandExpansionChance);
        ExpandChanceRing(seed, layer, optionalWastelandFrontier, blockedForWasteland, wastelandIds, wastelandBiome, 28930, WastelandExpansionChanceSecond);
    }

    private static void SeedCraterCluster(string seed, PlanetLayer layer, List<SurfaceTile> surfaceTiles, BiomeDef craterBiome, HashSet<int> craterIds)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356721;
        var candidate = surfaceTiles
            .Where(static tile => tile.temperature > -15f)
            .OrderBy(tile => RollForTile(seedHash, tile.tile.tileId, 49153))
            .FirstOrDefault();
        if (candidate == null)
        {
            return;
        }

        candidate.PrimaryBiome = craterBiome;
        craterIds.Add(candidate.tile.tileId);

        foreach (var neighbor in GetNeighborTiles(layer, candidate.tile.tileId))
        {
            if (neighbor.WaterCovered || craterIds.Contains(neighbor.tile.tileId))
            {
                continue;
            }

            if (RollForTile(seedHash, neighbor.tile.tileId, 49154) <= 0.35f)
            {
                neighbor.PrimaryBiome = craterBiome;
                craterIds.Add(neighbor.tile.tileId);
            }
        }
    }

    private static HashSet<int> ExpandGuaranteedRing(PlanetLayer layer, IEnumerable<int> sourceIds, ISet<int> blockedIds, System.Func<SurfaceTile, bool> predicate)
    {
        var result = new HashSet<int>();
        foreach (var sourceId in sourceIds)
        {
            foreach (var neighbor in GetNeighborTiles(layer, sourceId))
            {
                var neighborId = neighbor.tile.tileId;
                if (blockedIds.Contains(neighborId) || !predicate(neighbor))
                {
                    continue;
                }

                result.Add(neighborId);
            }
        }

        return result;
    }

    private static void ExpandChanceRing(string seed, PlanetLayer layer, HashSet<int> frontier, ISet<int> blockedIds, HashSet<int> assignedIds, BiomeDef biome, int salt, float chance)
    {
        if (frontier.Count == 0)
        {
            return;
        }

        var seedHash = GenText.StableStringHash(seed) ^ 184356721;
        var nextFrontier = new HashSet<int>();
        foreach (var sourceId in frontier)
        {
            foreach (var neighbor in GetNeighborTiles(layer, sourceId))
            {
                var neighborId = neighbor.tile.tileId;
                if (blockedIds.Contains(neighborId) || assignedIds.Contains(neighborId) || neighbor.WaterCovered)
                {
                    continue;
                }

                if (RollForTile(seedHash, neighborId, salt) > chance)
                {
                    continue;
                }

                neighbor.PrimaryBiome = biome;
                assignedIds.Add(neighborId);
                nextFrontier.Add(neighborId);
            }
        }

        frontier.Clear();
        frontier.UnionWith(nextFrontier);
    }

    private static void ApplyBiome(PlanetLayer layer, IEnumerable<int> tileIds, BiomeDef biome)
    {
        foreach (var tileId in tileIds)
        {
            if (layer[tileId] is SurfaceTile surfaceTile)
            {
                surfaceTile.PrimaryBiome = biome;
            }
        }
    }

    private static IEnumerable<SurfaceTile> GetNeighborTiles(PlanetLayer layer, int tileId)
    {
        var neighbors = new List<PlanetTile>();
        Find.WorldGrid.GetTileNeighbors(layer.PlanetTileForID(tileId), neighbors);
        foreach (var neighbor in neighbors)
        {
            if (layer[neighbor] is SurfaceTile surfaceTile)
            {
                yield return surfaceTile;
            }
        }
    }

    private static float RollForTile(int seedHash, int tileId, int salt)
    {
        unchecked
        {
            var hash = seedHash;
            hash = (hash * 397) ^ tileId;
            hash = (hash * 397) ^ salt;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            var value = (uint)hash & 0x00FFFFFFu;
            return value / 16777215f;
        }
    }
}