using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.Repconn;

public sealed class WorldGenStep_RepconnUrban : WorldGenStep
{
    private const int ClusterSpacingRings = 5;
    private const int LoneCraterSpacingRings = 2;
    private const int TilesPerCluster = 1200;
    private const int TilesPerLoneCrater = 3200;
    private const int MinClusterCount = 3;
    private const int MaxClusterCount = 8;
    private const int MinLoneCraterCount = 2;
    private const int MaxLoneCraterCount = 6;
    private const float RuinExpansionChance = 0.20f;
    private const float WastelandExpansionChance = 0.60f;
    private const int MaxAdjacentCraterNoise = 6;
    private const int MaxAdjacentLoneCraterNoise = 2;
    private const int MaxGroundZeroConnections = 2;
    private const int MinimumBranchCount = 3;
    private const int MaximumBranchCount = 4;
    private const int MinimumBranchLength = 2;
    private const int MaximumBranchLength = 5;
    private const float HugeRiverCraterBonus = 1f;
    private const float CoastalCraterBonus = 1f;
    private const float LargeRiverCraterBonus = 0.65f;
    private const float MediumRiverCraterBonus = 0.35f;
    private const float SmallRiverCraterBonus = 0.12f;
    private const float HugeRiverAqueductChance = 0.92f;
    private const float LargeRiverAqueductChance = 0.55f;
    private const float MediumRiverAqueductChance = 0.12f;
    private const float SmallRiverAqueductChance = 0f;
    private const float HugeRiverFloodplainsChance = 0.30f;
    private const float LargeRiverFloodplainsChance = 0.12f;
    private const float MediumRiverFloodplainsChance = 0.03f;
    private const int MajorPollutionRadius = 7;
    private const int LonePollutionRadius = 4;
    private const float MajorPollutionCenter = 1f;
    private const float LonePollutionCenter = 0.65f;
    private const float MajorPollutionFalloff = 0.14f;
    private const float LonePollutionFalloff = 0.17f;
    private static readonly FieldInfo PotentialRoadsField = typeof(SurfaceTile).GetField("potentialRoads", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo PotentialRiversField = typeof(SurfaceTile).GetField("potentialRivers", BindingFlags.Instance | BindingFlags.NonPublic);

    public override int SeedPart => 184356723;

    public override void GenerateFresh(string seed, PlanetLayer layer)
    {
        if (layer == null)
        {
            return;
        }

        var allSurfaceTiles = layer.Tiles.OfType<SurfaceTile>().ToList();
        ApplyRiverBiomeVariants(seed, allSurfaceTiles);

        var craterBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Crater");
        var ruinBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Ruin");
        var wastelandBiome = DefDatabase<BiomeDef>.GetNamedSilentFail("Repconn_Wasteland");
        var highwayRoad = DefDatabase<RoadDef>.GetNamedSilentFail("AncientAsphaltHighway");
        var branchRoad = DefDatabase<RoadDef>.GetNamedSilentFail("AncientAsphaltRoad");
        if (craterBiome == null || ruinBiome == null || wastelandBiome == null)
        {
            return;
        }

        var surfaceTiles = allSurfaceTiles.Where(static tile => !tile.WaterCovered).ToList();
        if (surfaceTiles.Count == 0)
        {
            return;
        }

        var craterLayout = SeedMajorCraterClusters(seed, layer, surfaceTiles, craterBiome);
        SeedLoneCraters(seed, layer, surfaceTiles, craterBiome, craterLayout);
        if (craterLayout.AllCraterIds.Count == 0)
        {
            return;
        }

        var clusteredCraterIds = craterLayout.ClusterCraterIds;
        var ruinsIds = ExpandGuaranteedRing(layer, clusteredCraterIds, clusteredCraterIds, static tile => !tile.WaterCovered);
        ApplyBiome(layer, ruinsIds, ruinBiome);

        var ruinsFrontier = new HashSet<int>(ruinsIds);
        ExpandChanceRing(seed, layer, ruinsFrontier, clusteredCraterIds, ruinsIds, ruinBiome, 37101, RuinExpansionChance);

        var blockedForWasteland = new HashSet<int>(clusteredCraterIds);
        blockedForWasteland.UnionWith(ruinsIds);

        var wastelandIds = ExpandGuaranteedRing(layer, ruinsIds, blockedForWasteland, static tile => !tile.WaterCovered);
        ApplyBiome(layer, wastelandIds, wastelandBiome);

        var wastelandFrontier = new HashSet<int>(wastelandIds);
        ExpandChanceRing(seed, layer, wastelandFrontier, blockedForWasteland, wastelandIds, wastelandBiome, 37102, WastelandExpansionChance);

        if (craterLayout.AllGroundZeroIds.Count > 0)
        {
            ApplyCraterCenteredPollution(layer, allSurfaceTiles, craterLayout.ClusterGroundZeroIds, craterLayout.LoneGroundZeroIds);
        }

        if (highwayRoad == null || branchRoad == null || craterLayout.ClusterGroundZeroIds.Count == 0)
        {
            return;
        }

        if (craterLayout.ClusterGroundZeroIds.Count >= 2)
        {
            GenerateGroundZeroHighways(seed, layer, craterLayout.ClusterGroundZeroIds, highwayRoad);
        }

        GenerateGroundZeroBranches(seed, layer, craterLayout.ClusterGroundZeroIds, highwayRoad, branchRoad);
    }

    private static void ApplyRiverBiomeVariants(string seed, List<SurfaceTile> surfaceTiles)
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
        foreach (var tile in surfaceTiles)
        {
            var riverSize = GetRiverSize(tile);
            if (tile.WaterCovered || riverSize == RiverSize.None || tile.PrimaryBiome == null)
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
                    var floodplainsChance = GetFloodplainsChance(riverSize);
                    var aqueductChance = GetAqueductChance(riverSize);
                    var aqueductRoll = RollForTile(seedHash, tileId, 30021);
                    if (aqueductRoll <= floodplainsChance)
                    {
                        tile.PrimaryBiome = floodplainsBiome;
                    }
                    else if (aqueductRoll <= aqueductChance)
                    {
                        tile.PrimaryBiome = aqueductBiome;
                    }
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

    private static CraterLayout SeedMajorCraterClusters(string seed, PlanetLayer layer, List<SurfaceTile> surfaceTiles, BiomeDef craterBiome)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        var layout = new CraterLayout();
        var blockedIds = new HashSet<int>();
        var candidates = surfaceTiles
            .Where(static tile => IsCraterCandidate(tile))
            .OrderByDescending(tile => CraterCandidateWeight(tile) + RollForTile(seedHash, tile.tile.tileId, 37001) * 0.05f)
            .ToList();
        if (candidates.Count == 0)
        {
            return layout;
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

            var localCraterIds = SeedCraterFeatureAt(seedHash, layer, candidate, craterBiome, MaxAdjacentCraterNoise, 37011, 37012);
            if (localCraterIds.Count == 0)
            {
                continue;
            }

            layout.ClusterCraterIds.UnionWith(localCraterIds);
            layout.ClusterGroundZeroIds.Add(tileId);
            blockedIds.UnionWith(ExpandRings(layer, localCraterIds, ClusterSpacingRings, static tile => !tile.WaterCovered));
            seededClusters++;
            if (seededClusters >= clusterCount)
            {
                break;
            }
        }

        return layout;
    }

    private static void SeedLoneCraters(string seed, PlanetLayer layer, List<SurfaceTile> surfaceTiles, BiomeDef craterBiome, CraterLayout layout)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        var blockedIds = ExpandRings(layer, layout.ClusterCraterIds, ClusterSpacingRings, static tile => !tile.WaterCovered);
        blockedIds.UnionWith(layout.ClusterCraterIds);
        var candidates = surfaceTiles
            .Where(static tile => IsCraterCandidate(tile))
            .OrderByDescending(tile => CraterCandidateWeight(tile) + RollForTile(seedHash, tile.tile.tileId, 37041) * 0.05f)
            .ToList();
        var targetCount = GetLoneCraterCount(surfaceTiles.Count);
        var seededCount = 0;
        foreach (var candidate in candidates)
        {
            var tileId = candidate.tile.tileId;
            if (blockedIds.Contains(tileId))
            {
                continue;
            }

            var localCraterIds = SeedCraterFeatureAt(seedHash, layer, candidate, craterBiome, MaxAdjacentLoneCraterNoise, 37042, 37043);
            if (localCraterIds.Count == 0)
            {
                continue;
            }

            layout.LoneCraterIds.UnionWith(localCraterIds);
            layout.LoneGroundZeroIds.Add(tileId);
            blockedIds.UnionWith(ExpandRings(layer, localCraterIds, LoneCraterSpacingRings, static tile => !tile.WaterCovered));
            seededCount++;
            if (seededCount >= targetCount)
            {
                break;
            }
        }
    }

    private static HashSet<int> SeedCraterFeatureAt(int seedHash, PlanetLayer layer, SurfaceTile candidate, BiomeDef craterBiome, int maxAdjacentNoise, int orderSalt, int countSalt)
    {
        var craterIds = new HashSet<int>();
        var groundZeroId = candidate.tile.tileId;
        candidate.PrimaryBiome = craterBiome;
        craterIds.Add(groundZeroId);

        var adjacentCandidates = GetNeighborTiles(layer, groundZeroId)
            .Where(static tile => !tile.WaterCovered)
            .OrderBy(tile => RollForTile(seedHash, tile.tile.tileId, orderSalt))
            .ToList();
        var extraCraterCount = Math.Min(adjacentCandidates.Count, (int)(RollForTile(seedHash, groundZeroId, countSalt) * (maxAdjacentNoise + 1)));
        for (var index = 0; index < extraCraterCount; index++)
        {
            var adjacentTile = adjacentCandidates[index];
            adjacentTile.PrimaryBiome = craterBiome;
            craterIds.Add(adjacentTile.tile.tileId);
        }

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

    private static int GetLoneCraterCount(int surfaceTileCount)
    {
        var loneCraterCount = surfaceTileCount / TilesPerLoneCrater;
        if (loneCraterCount < MinLoneCraterCount)
        {
            return MinLoneCraterCount;
        }

        if (loneCraterCount > MaxLoneCraterCount)
        {
            return MaxLoneCraterCount;
        }

        return loneCraterCount;
    }

    private static float CraterCandidateWeight(SurfaceTile tile)
    {
        var baseWeight = tile.temperature > -5f && tile.temperature <= 32f ? 1f : 0.1f;
        var riverBonus = GetRiverSize(tile) switch
        {
            RiverSize.Huge => HugeRiverCraterBonus,
            RiverSize.Large => LargeRiverCraterBonus,
            RiverSize.Medium => MediumRiverCraterBonus,
            RiverSize.Small => SmallRiverCraterBonus,
            _ => 0f,
        };
        var coastalBonus = IsCoastal(tile) ? CoastalCraterBonus : 0f;
        return baseWeight + Math.Max(riverBonus, coastalBonus);
    }

    private static bool IsCraterCandidate(SurfaceTile tile)
    {
        return tile != null && !tile.WaterCovered;
    }

    private static void ApplyCraterCenteredPollution(PlanetLayer layer, List<SurfaceTile> surfaceTiles, List<int> clusterGroundZeroIds, List<int> loneGroundZeroIds)
    {
        foreach (var tile in surfaceTiles)
        {
            tile.pollution = 0f;
        }

        foreach (var groundZeroId in clusterGroundZeroIds)
        {
            SpreadPollutionFromSource(layer, groundZeroId, MajorPollutionCenter, MajorPollutionFalloff, MajorPollutionRadius);
        }

        foreach (var groundZeroId in loneGroundZeroIds)
        {
            SpreadPollutionFromSource(layer, groundZeroId, LonePollutionCenter, LonePollutionFalloff, LonePollutionRadius);
        }
    }

    private static void SpreadPollutionFromSource(PlanetLayer layer, int sourceId, float centerPollution, float falloffPerRing, int radius)
    {
        if (layer[sourceId] is not SurfaceTile)
        {
            return;
        }

        var frontier = new Queue<(int tileId, int distance)>();
        var visited = new HashSet<int> { sourceId };
        frontier.Enqueue((sourceId, 0));

        while (frontier.Count > 0)
        {
            var (tileId, distance) = frontier.Dequeue();
            if (layer[tileId] is not SurfaceTile tile)
            {
                continue;
            }

            if (distance > radius || tile.PrimaryBiome == null || !tile.PrimaryBiome.allowPollution)
            {
                continue;
            }

            var pollutionValue = Math.Max(0f, centerPollution - distance * falloffPerRing);
            if (pollutionValue > tile.pollution)
            {
                tile.pollution = pollutionValue;
            }

            if (distance == radius)
            {
                continue;
            }

            foreach (var neighbor in GetNeighborTiles(layer, tileId))
            {
                var neighborId = neighbor.tile.tileId;
                if (!visited.Add(neighborId) || neighbor.WaterCovered)
                {
                    continue;
                }

                frontier.Enqueue((neighborId, distance + 1));
            }
        }
    }

    private static void GenerateGroundZeroHighways(string seed, PlanetLayer layer, List<int> groundZeroIds, RoadDef highwayRoad)
    {
        var connectedPairs = new HashSet<long>();
        foreach (var sourceId in groundZeroIds)
        {
            var desiredConnections = 1 + (RollForTile(seed, sourceId, 37201) < 0.5f ? 0 : 1);
            var successfulConnections = 0;
            var sourceTile = layer.PlanetTileForID(sourceId);
            var candidateTargets = groundZeroIds
                .Where(targetId => targetId != sourceId)
                .OrderBy(targetId => Find.WorldGrid.ApproxDistanceInTiles(sourceTile, layer.PlanetTileForID(targetId)))
                .ThenBy(targetId => RollForTile(seed, targetId, 37202))
                .ToList();

            foreach (var targetId in candidateTargets)
            {
                if (successfulConnections >= desiredConnections || successfulConnections >= MaxGroundZeroConnections)
                {
                    break;
                }

                var pairKey = GetConnectionKey(sourceId, targetId);
                if (connectedPairs.Contains(pairKey))
                {
                    continue;
                }

                var path = FindShortestRoadPath(layer, sourceId, targetId);
                if (path == null || path.Count < 2)
                {
                    continue;
                }

                ApplyRoadPath(layer, path, highwayRoad);
                connectedPairs.Add(pairKey);
                successfulConnections++;
            }
        }
    }

    private static void GenerateGroundZeroBranches(string seed, PlanetLayer layer, List<int> groundZeroIds, RoadDef highwayRoad, RoadDef branchRoad)
    {
        foreach (var groundZeroId in groundZeroIds)
        {
            var branchCount = MinimumBranchCount + (RollForTile(seed, groundZeroId, 37301) < 0.5f ? 0 : 1);
            var validStarts = GetNeighborTiles(layer, groundZeroId)
                .Where(IsRoadTilePassable)
                .OrderBy(tile => RollForTile(seed, tile.tile.tileId, 37302))
                .Take(branchCount)
                .ToList();

            for (var branchIndex = 0; branchIndex < validStarts.Count; branchIndex++)
            {
                var branchLength = MinimumBranchLength + (int)(RollForTile(seed, groundZeroId, 37310 + branchIndex) * (MaximumBranchLength - MinimumBranchLength + 1));
                var roadDef = RollForTile(seed, groundZeroId, 37320 + branchIndex) <= 0.35f ? highwayRoad : branchRoad;
                var branchPath = BuildBranchPath(seed, layer, groundZeroId, validStarts[branchIndex].tile.tileId, branchLength, branchIndex);
                if (branchPath.Count >= 2)
                {
                    ApplyRoadPath(layer, branchPath, roadDef);
                }
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

    private static List<int> FindShortestRoadPath(PlanetLayer layer, int sourceId, int targetId)
    {
        var frontier = new Queue<int>();
        var cameFrom = new Dictionary<int, int>();
        frontier.Enqueue(sourceId);
        cameFrom[sourceId] = -1;

        while (frontier.Count > 0)
        {
            var currentId = frontier.Dequeue();
            if (currentId == targetId)
            {
                return ReconstructPath(cameFrom, targetId);
            }

            foreach (var neighbor in GetNeighborTiles(layer, currentId))
            {
                var neighborId = neighbor.tile.tileId;
                if (cameFrom.ContainsKey(neighborId) || !IsRoadTilePassable(neighbor))
                {
                    continue;
                }

                cameFrom[neighborId] = currentId;
                frontier.Enqueue(neighborId);
            }
        }

        return null;
    }

    private static List<int> BuildBranchPath(string seed, PlanetLayer layer, int groundZeroId, int startId, int desiredLength, int branchIndex)
    {
        var path = new List<int> { groundZeroId, startId };
        var visited = new HashSet<int>(path);
        var previousId = groundZeroId;
        var currentId = startId;

        while (path.Count - 1 < desiredLength)
        {
            var currentDistance = Find.WorldGrid.ApproxDistanceInTiles(layer.PlanetTileForID(groundZeroId), layer.PlanetTileForID(currentId));
            var nextTile = GetNeighborTiles(layer, currentId)
                .Where(tile => tile.tile.tileId != previousId && !visited.Contains(tile.tile.tileId) && IsRoadTilePassable(tile))
                .OrderByDescending(tile => Find.WorldGrid.ApproxDistanceInTiles(layer.PlanetTileForID(groundZeroId), tile.tile))
                .ThenBy(tile => Math.Abs(Find.WorldGrid.ApproxDistanceInTiles(layer.PlanetTileForID(groundZeroId), tile.tile) - (currentDistance + 1f)))
                .ThenBy(tile => RollForTile(seed, tile.tile.tileId, 37340 + branchIndex + path.Count))
                .FirstOrDefault();

            if (nextTile == null)
            {
                break;
            }

            var nextDistance = Find.WorldGrid.ApproxDistanceInTiles(layer.PlanetTileForID(groundZeroId), nextTile.tile);
            if (nextDistance < currentDistance)
            {
                break;
            }

            previousId = currentId;
            currentId = nextTile.tile.tileId;
            path.Add(currentId);
            visited.Add(currentId);
        }

        return path;
    }

    private static List<int> ReconstructPath(Dictionary<int, int> cameFrom, int targetId)
    {
        var path = new List<int>();
        var currentId = targetId;
        while (currentId >= 0)
        {
            path.Add(currentId);
            currentId = cameFrom[currentId];
        }

        path.Reverse();
        return path;
    }

    private static void ApplyRoadPath(PlanetLayer layer, List<int> path, RoadDef roadDef)
    {
        for (var index = 0; index < path.Count - 1; index++)
        {
            if (layer[path[index]] is not SurfaceTile sourceTile || layer[path[index + 1]] is not SurfaceTile targetTile)
            {
                continue;
            }

            AddRoadLink(sourceTile, targetTile.tile, roadDef);
            AddRoadLink(targetTile, sourceTile.tile, roadDef);
        }
    }

    private static void AddRoadLink(SurfaceTile tile, PlanetTile neighborTile, RoadDef roadDef)
    {
        var roads = EnsureRoadList(tile);
        if (roads.Any(link => link.neighbor == neighborTile))
        {
            return;
        }

        roads.Add(new SurfaceTile.RoadLink
        {
            neighbor = neighborTile,
            road = roadDef,
        });
    }

    private static List<SurfaceTile.RoadLink> EnsureRoadList(SurfaceTile tile)
    {
        var roads = tile.Roads;
        if (roads != null)
        {
            return roads;
        }

        roads = new List<SurfaceTile.RoadLink>();
        PotentialRoadsField?.SetValue(tile, roads);
        return tile.Roads ?? roads;
    }

    private static bool IsRoadTilePassable(SurfaceTile tile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.PrimaryBiome != null
            && !tile.PrimaryBiome.impassable
            && tile.PrimaryBiome.allowRoads
            && tile.hilliness != Hilliness.Impassable;
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

    private static float RollForTile(string seed, int tileId, int salt)
    {
        return RollForTile(GenText.StableStringHash(seed) ^ 184356723, tileId, salt);
    }

    private static long GetConnectionKey(int firstId, int secondId)
    {
        var lower = Math.Min(firstId, secondId);
        var higher = Math.Max(firstId, secondId);
        return ((long)lower << 32) | (uint)higher;
    }

    private static bool HasRiver(SurfaceTile tile)
    {
        return GetRiverSize(tile) != RiverSize.None;
    }

    private static RiverSize GetRiverSize(SurfaceTile tile)
    {
        var riverLinks = GetRiverLinks(tile);
        if (riverLinks == null || riverLinks.Count == 0)
        {
            return RiverSize.None;
        }

        var strongestWidth = 0f;
        foreach (var riverLink in riverLinks)
        {
            if (riverLink.river == null)
            {
                continue;
            }

            if (riverLink.river.widthOnWorld > strongestWidth)
            {
                strongestWidth = riverLink.river.widthOnWorld;
            }
        }

        if (strongestWidth >= 0.7f)
        {
            return RiverSize.Huge;
        }

        if (strongestWidth >= 0.5f)
        {
            return RiverSize.Large;
        }

        if (strongestWidth >= 0.3f)
        {
            return RiverSize.Medium;
        }

        return strongestWidth > 0f ? RiverSize.Small : RiverSize.None;
    }

    private static List<SurfaceTile.RiverLink> GetRiverLinks(SurfaceTile tile)
    {
        try
        {
            if (tile.Rivers != null && tile.Rivers.Count > 0)
            {
                return tile.Rivers;
            }
        }
        catch
        {
        }

        return PotentialRiversField?.GetValue(tile) as List<SurfaceTile.RiverLink>;
    }

    private static float GetAqueductChance(RiverSize riverSize)
    {
        return riverSize switch
        {
            RiverSize.Huge => HugeRiverAqueductChance,
            RiverSize.Large => LargeRiverAqueductChance,
            RiverSize.Medium => MediumRiverAqueductChance,
            RiverSize.Small => SmallRiverAqueductChance,
            _ => 0f,
        };
    }

    private static float GetFloodplainsChance(RiverSize riverSize)
    {
        return riverSize switch
        {
            RiverSize.Huge => HugeRiverFloodplainsChance,
            RiverSize.Large => LargeRiverFloodplainsChance,
            RiverSize.Medium => MediumRiverFloodplainsChance,
            _ => 0f,
        };
    }

    private static bool IsCoastal(Tile tile)
    {
        try
        {
            return tile.IsCoastal;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private sealed class CraterLayout
    {
        public HashSet<int> ClusterCraterIds { get; } = new HashSet<int>();

        public List<int> ClusterGroundZeroIds { get; } = new List<int>();

        public HashSet<int> LoneCraterIds { get; } = new HashSet<int>();

        public List<int> LoneGroundZeroIds { get; } = new List<int>();

        public HashSet<int> AllCraterIds
        {
            get
            {
                var allIds = new HashSet<int>(ClusterCraterIds);
                allIds.UnionWith(LoneCraterIds);
                return allIds;
            }
        }

        public List<int> AllGroundZeroIds
        {
            get
            {
                var allIds = new List<int>(ClusterGroundZeroIds);
                allIds.AddRange(LoneGroundZeroIds);
                return allIds;
            }
        }
    }

    private enum RiverSize
    {
        None,
        Small,
        Medium,
        Large,
        Huge,
    }
}