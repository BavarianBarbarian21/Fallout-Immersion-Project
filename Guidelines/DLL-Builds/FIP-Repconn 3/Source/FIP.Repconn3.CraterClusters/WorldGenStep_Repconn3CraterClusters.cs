using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.Repconn3;

public sealed class WorldGenStep_Repconn3CraterClusters : WorldGenStep
{
    private const float ExtraCraterChance = 0.55f;
    private const float ThirdCraterChance = 0.28f;
    private const float RuinExpansionChance = 0.10f;
    private const float WastelandExpansionChance = 0.20f;

    public override int SeedPart => 184356723;

    public override void GenerateFresh(string seed, PlanetLayer layer)
    {
        if (layer == null || !layer.IsRootSurface)
        {
            return;
        }

        var craterBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("FIP3_Crater");
        var ruinBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("FIP3_Ruin");
        var wastelandBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("FIP3_Wasteland");
        if (craterBiome == null || ruinBiome == null || wastelandBiome == null)
        {
            return;
        }

        var surfaceTiles = layer.Tiles.OfType<SurfaceTile>().Where(static tile => !tile.WaterCovered).ToList();
        if (surfaceTiles.Count == 0)
        {
            return;
        }

        var craterIds = new HashSet<int>();
        SeedCraterCluster(seed, layer, surfaceTiles, craterBiome, craterIds);
        if (craterIds.Count == 0)
        {
            return;
        }

        var ruinsIds = ExpandGuaranteedRing(layer, craterIds, craterIds, static tile => !tile.WaterCovered);
        ApplyBiome(layer, ruinsIds, ruinBiome);

        var ruinsFrontier = new HashSet<int>(ruinsIds);
        ExpandChanceRing(seed, layer, ruinsFrontier, craterIds, ruinsIds, ruinBiome, 37101, RuinExpansionChance);

        var blockedForWasteland = new HashSet<int>(craterIds);
        blockedForWasteland.UnionWith(ruinsIds);

        var wastelandIds = ExpandGuaranteedRing(layer, ruinsIds, blockedForWasteland, static tile => !tile.WaterCovered);
        ApplyBiome(layer, wastelandIds, wastelandBiome);

        var wastelandFrontier = new HashSet<int>(wastelandIds);
        ExpandChanceRing(seed, layer, wastelandFrontier, blockedForWasteland, wastelandIds, wastelandBiome, 37102, WastelandExpansionChance);
    }

    private static void SeedCraterCluster(string seed, PlanetLayer layer, List<SurfaceTile> surfaceTiles, BiomeDef craterBiome, HashSet<int> craterIds)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        var candidate = surfaceTiles
            .Where(static tile => IsCraterCandidate(tile))
            .OrderByDescending(tile => CraterCandidateWeight(tile) + RollForTile(seedHash, tile.tile.tileId, 37001) * 0.05f)
            .FirstOrDefault();
        if (candidate == null)
        {
            return;
        }

        candidate.PrimaryBiome = craterBiome;
        craterIds.Add(candidate.tile.tileId);

        foreach (var neighbor in GetNeighborTiles(layer, candidate.tile.tileId).OrderBy(tile => RollForTile(seedHash, tile.tile.tileId, 37002)))
        {
            if (craterIds.Count >= 3)
            {
                break;
            }

            if (!IsCraterCandidate(neighbor) || craterIds.Contains(neighbor.tile.tileId))
            {
                continue;
            }

            var chance = craterIds.Count == 1 ? ExtraCraterChance : ThirdCraterChance;
            if (RollForTile(seedHash, neighbor.tile.tileId, 37003 + craterIds.Count) > chance)
            {
                continue;
            }

            neighbor.PrimaryBiome = craterBiome;
            craterIds.Add(neighbor.tile.tileId);
        }
    }

    private static float CraterCandidateWeight(SurfaceTile tile)
    {
        return tile.temperature > BiomeBandScoring.ArcticMax && tile.temperature <= BiomeBandScoring.AridMax ? 1f : 0.1f;
    }

    private static bool IsCraterCandidate(SurfaceTile tile)
    {
        return tile != null && !tile.WaterCovered;
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

        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
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