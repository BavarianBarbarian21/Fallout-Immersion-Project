using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.Repconn;

public sealed class WorldGenStep_RepconnCraterClusters : WorldGenStep
{
    private const int ClusterSpacingRings = 5;
    private const int TilesPerCluster = 1200;
    private const int MinClusterCount = 3;
    private const int MaxClusterCount = 8;
    private const float RuinExpansionChance = 0.10f;
    private const float WastelandExpansionChance = 0.20f;

    public override int SeedPart => 184356723;

    public override void GenerateFresh(string seed, PlanetLayer layer)
    {
        if (layer == null)
        {
            return;
        }

        ApplyRiverBiomeVariants(seed, layer);

        var craterBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Crater");
        var ruinBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Ruin");
        var wastelandBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Wasteland");
        if (craterBiome == null || ruinBiome == null || wastelandBiome == null)
        {
            return;
        }

        var surfaceTiles = layer.Tiles.OfType<SurfaceTile>().Where(static tile => !tile.WaterCovered).ToList();
        if (surfaceTiles.Count == 0)
        {
            return;
        }

        var craterIds = SeedCraterClusters(seed, layer, surfaceTiles, craterBiome);
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

    private static void ApplyRiverBiomeVariants(string seed, PlanetLayer layer)
    {
        var ceekBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Ceek");
        var aqueductBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Aqueduct");
        var floodplainsBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Floodplains");
        var canyonBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Canyon");
        if (ceekBiome == null || aqueductBiome == null || floodplainsBiome == null || canyonBiome == null)
        {
            return;
        }

        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        foreach (var tile in layer.Tiles.OfType<SurfaceTile>())
        {
            if (tile.WaterCovered || !HasRiver(tile) || tile.PrimaryBiome == null)
            {
                continue;
            }

            var tileId = tile.tile.tileId;
            switch (tile.PrimaryBiome.defName)
            {
                case "Repconn_Preserve":
                    if (RollForTile(seedHash, tileId, 30011) <= 0.5f)
                    {
                        tile.PrimaryBiome = ceekBiome;
                    }
                    break;
                case "Repconn_CentralValley":
                case "Repconn_BrahminWoods":
                    tile.PrimaryBiome = RollForTile(seedHash, tileId, 30021) <= 0.05f
                        ? floodplainsBiome
                        : aqueductBiome;
                    break;
                case "Repconn_Mojave":
                    if (RollForTile(seedHash, tileId, 30031) <= 0.5f)
                    {
                        tile.PrimaryBiome = canyonBiome;
                    }
                    break;
            }
        }
    }

    private static HashSet<int> SeedCraterClusters(string seed, PlanetLayer layer, List<SurfaceTile> surfaceTiles, BiomeDef craterBiome)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        var craterIds = new HashSet<int>();
        var blockedIds = new HashSet<int>();
        var candidates = surfaceTiles
            .Where(static tile => IsCraterCandidate(tile))
            .OrderByDescending(tile => CraterCandidateWeight(tile) + RollForTile(seedHash, tile.tile.tileId, 37001) * 0.05f)
            .ToList();
        if (candidates.Count == 0)
        {
            return craterIds;
        }

        var clusterCount = GetClusterCount(surfaceTiles.Count);
        var seededClusters = 0;
        foreach (var candidate in candidates)
        {
            var tileId = candidate.tile.tileId;
            if (blockedIds.Contains(tileId))
            {
                continue;
            }

            var localCraterIds = SeedCraterClusterAt(layer, candidate, craterBiome);
            if (localCraterIds.Count == 0)
            {
                continue;
            }

            craterIds.UnionWith(localCraterIds);
            blockedIds.UnionWith(ExpandRings(layer, localCraterIds, ClusterSpacingRings, static tile => !tile.WaterCovered));
            seededClusters++;
            if (seededClusters >= clusterCount)
            {
                break;
            }
        }

        return craterIds;
    }

    private static HashSet<int> SeedCraterClusterAt(PlanetLayer layer, SurfaceTile candidate, BiomeDef craterBiome)
    {
        var craterIds = new HashSet<int>();
        candidate.PrimaryBiome = craterBiome;
        craterIds.Add(candidate.tile.tileId);

        var guaranteedCoreIds = ExpandGuaranteedRing(layer, craterIds, craterIds, static tile => !tile.WaterCovered);
        ApplyBiome(layer, guaranteedCoreIds, craterBiome);
        craterIds.UnionWith(guaranteedCoreIds);

        return craterIds;
    }

    private static int GetClusterCount(int surfaceTileCount)
    {
        var clusterCount = surfaceTileCount / TilesPerCluster;
        if (clusterCount < MinClusterCount)
        {
            return MinClusterCount;
        }

        if (clusterCount > MaxClusterCount)
        {
            return MaxClusterCount;
        }

        return clusterCount;
    }

    private static float CraterCandidateWeight(SurfaceTile tile)
    {
        return tile.temperature > -5f && tile.temperature <= 32f ? 1f : 0.1f;
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

    private static HashSet<int> ExpandRings(PlanetLayer layer, IEnumerable<int> seedIds, int ringCount, System.Func<SurfaceTile, bool> predicate)
    {
        var allIds = new HashSet<int>(seedIds);
        var frontier = new HashSet<int>(seedIds);
        for (var ringIndex = 0; ringIndex < ringCount && frontier.Count > 0; ringIndex++)
        {
            var nextFrontier = ExpandGuaranteedRing(layer, frontier, allIds, predicate);
            allIds.UnionWith(nextFrontier);
            frontier = nextFrontier;
        }

        return allIds;
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

    private static bool HasRiver(SurfaceTile tile)
    {
        try
        {
            return tile.Rivers != null && tile.Rivers.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}