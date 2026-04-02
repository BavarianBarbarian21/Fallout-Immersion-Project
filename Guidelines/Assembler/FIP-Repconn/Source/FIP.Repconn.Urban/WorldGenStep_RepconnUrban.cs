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
    private const int TilesPerCluster = 900;
    private const int TilesPerLoneCrater = 2400;
    private const int MinClusterCount = 4;
    private const int MaxClusterCount = 10;
    private const int MinLoneCraterCount = 3;
    private const int MaxLoneCraterCount = 8;
    private const int MinimumWastelandRadius = 5;
    private const int MaximumWastelandRadius = 15;
    private const int MinimumRuinRadius = 2;
    private const int MaximumRuinRadius = 3;
    private const float WastelandInnerChance = 0.94f;
    private const float WastelandOuterChance = 0.30f;
    private const float WastelandFingerChance = 0.18f;
    private const int WastelandGapFillThreshold = 4;
    private const float RuinInnerChance = 1f;
    private const float RuinOuterChance = 0.72f;
    private const float RuinFingerChance = 0.10f;
    private const int RuinGapFillThreshold = 4;
    private const int MaxAdjacentCraterNoise = 6;
    private const int MaxAdjacentLoneCraterNoise = 6;
    private const int MaxGroundZeroConnections = 2;
    private const int MinimumLocalAnchorCount = 5;
    private const int MaximumLocalAnchorCount = 7;
    private const int MinimumLocalAnchorRadius = 3;
    private const int MaximumLocalAnchorRadius = 7;
    private const int MinimumLocalConnectorCount = 1;
    private const int MaximumLocalConnectorCount = 2;
    private const int MinimumLocalConnectorDistance = 3;
    private const int MaximumLocalConnectorDistance = 10;
    private const int MinimumLocalChordCount = 1;
    private const int MaximumLocalChordCount = 2;
    private const int LocalAnchorSeparation = 4;
    private const float TrunkRoadReuseDiscount = 0.28f;
    private const float LocalRoadReuseDiscount = 0.35f;
    private const float LocalUrbanTileDiscount = 0.30f;
    private const float RoadJitterWeight = 0.22f;
    private const float RoadHeuristicWeight = 0.55f;
    private const float HugeRiverCraterBonus = 1f;
    private const float CoastalCraterBonus = 1f;
    private const float LargeRiverCraterBonus = 0.65f;
    private const float MediumRiverCraterBonus = 0.35f;
    private const float SmallRiverCraterBonus = 0.12f;
    private const float HugeRiverAqueductChance = 0.98f;
    private const float LargeRiverAqueductChance = 0.80f;
    private const float MediumRiverAqueductChance = 0.45f;
    private const float SmallRiverAqueductChance = 0.18f;
    private const float HugeRiverFloodplainsChance = 0.30f;
    private const float LargeRiverFloodplainsChance = 0.12f;
    private const float MediumRiverFloodplainsChance = 0.03f;
    private const int MajorPollutionGuaranteedFullRadius = 9;
    private const int MajorPollutionFullRadius = 14;
    private const int MajorPollutionOuterRadius = 18;
    private const int LonePollutionGuaranteedFullRadius = 7;
    private const int LonePollutionFullRadius = 11;
    private const int LonePollutionOuterRadius = 16;
    private const int PollutionRadiusNoise = 2;
    private const float MajorPollutionCenter = 1f;
    private const float LonePollutionCenter = 1f;
    private const float PollutionOuterFloor = 0.12f;
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
        var intercityRoad = DefDatabase<RoadDef>.GetNamedSilentFail("AncientAsphaltHighway");
        var localRoad = DefDatabase<RoadDef>.GetNamedSilentFail("AncientAsphaltRoad");
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
        if (craterLayout.AllGroundZeroIds.Count == 0)
        {
            return;
        }

        ApplyGroundZeroBiomeSprawl(seed, layer, craterLayout, craterBiome, ruinBiome, wastelandBiome);

        if (craterLayout.AllGroundZeroIds.Count > 0)
        {
            ApplyCraterCenteredPollution(layer, allSurfaceTiles, craterLayout.ClusterGroundZeroIds, craterLayout.LoneGroundZeroIds);
        }

        if (intercityRoad == null || localRoad == null || craterLayout.AllGroundZeroIds.Count == 0)
        {
            return;
        }

        if (craterLayout.AllGroundZeroIds.Count >= 2)
        {
            GenerateGroundZeroTrunkRoads(seed, layer, craterLayout.AllGroundZeroIds, intercityRoad);
        }

        GenerateGroundZeroLocalRoads(seed, layer, craterLayout.AllGroundZeroIds, localRoad);
        RefreshWorldRoadState(layer);
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
                    var floodplainsRoll = RollForTile(seedHash, tileId, 30021);
                    var aqueductRoll = RollForTile(seedHash, tileId, 30022);
                    if (aqueductRoll <= aqueductChance)
                    {
                        tile.PrimaryBiome = aqueductBiome;
                    }
                    else if (floodplainsRoll <= floodplainsChance)
                    {
                        tile.PrimaryBiome = floodplainsBiome;
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
        craterIds.Add(groundZeroId);

        var adjacentCandidates = GetNeighborTiles(layer, groundZeroId)
            .Where(static tile => !tile.WaterCovered)
            .OrderBy(tile => RollForTile(seedHash, tile.tile.tileId, orderSalt))
            .ToList();

        var craterCountRoll = RollForTile(seedHash, groundZeroId, countSalt);
        var extraCraterCount = Math.Min(adjacentCandidates.Count, (int)(Math.Pow(craterCountRoll, 1.65) * (maxAdjacentNoise + 1)));
        var addedCraterCount = 0;
        for (var index = 0; index < adjacentCandidates.Count && addedCraterCount < extraCraterCount; index++)
        {
            var adjacentTile = adjacentCandidates[index];
            if (RollForTile(seedHash, adjacentTile.tile.tileId, countSalt + 100 + index) > 0.72f)
            {
                continue;
            }

            craterIds.Add(adjacentTile.tile.tileId);
            addedCraterCount++;
        }

        return craterIds;
    }

    private static void ApplyGroundZeroBiomeSprawl(string seed, PlanetLayer layer, CraterLayout layout, BiomeDef craterBiome, BiomeDef ruinBiome, BiomeDef wastelandBiome)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;

        foreach (var groundZeroId in layout.AllGroundZeroIds)
        {
            ApplySprawlBiome(seedHash, layer, groundZeroId, wastelandBiome, MinimumWastelandRadius, MaximumWastelandRadius, 37101, 38101, WastelandInnerChance, WastelandOuterChance, WastelandFingerChance, WastelandGapFillThreshold);
        }

        foreach (var groundZeroId in layout.AllGroundZeroIds)
        {
            ApplySprawlBiome(seedHash, layer, groundZeroId, ruinBiome, MinimumRuinRadius, MaximumRuinRadius, 37111, 38111, RuinInnerChance, RuinOuterChance, RuinFingerChance, RuinGapFillThreshold);
        }

        ApplyBiome(layer, layout.AllCraterIds, craterBiome);
    }

    private static void ApplySprawlBiome(int seedHash, PlanetLayer layer, int sourceId, BiomeDef biome, int minimumRadius, int maximumRadius, int radiusSalt, int noiseSalt, float innerChance, float outerChance, float fingerChance, int gapFillThreshold)
    {
        if (layer[sourceId] is not SurfaceTile sourceTile || sourceTile.WaterCovered)
        {
            return;
        }

        var radius = minimumRadius + (int)(RollForTile(seedHash, sourceId, radiusSalt) * (maximumRadius - minimumRadius + 1));
        var frontier = new Queue<(int tileId, int distance)>();
        var visited = new HashSet<int> { sourceId };
        var assigned = new HashSet<int> { sourceId };
        frontier.Enqueue((sourceId, 0));

        while (frontier.Count > 0)
        {
            var (tileId, distance) = frontier.Dequeue();
            if (distance > radius || layer[tileId] is not SurfaceTile tile || tile.WaterCovered)
            {
                continue;
            }

            tile.PrimaryBiome = biome;

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

                var neighborDistance = distance + 1;
                if (neighborDistance > radius)
                {
                    continue;
                }

                var normalizedDistance = radius == 0 ? 0f : neighborDistance / (float)radius;
                var baseChance = innerChance + (outerChance - innerChance) * normalizedDistance;
                var shapeRoll = RollForTile(seedHash, CombineTileIds(sourceId, neighborId), noiseSalt + neighborDistance);
                var branchRoll = RollForTile(seedHash, CombineTileIds(tileId, neighborId), noiseSalt + 500 + neighborDistance);
                if (shapeRoll > baseChance && branchRoll > fingerChance)
                {
                    continue;
                }

                assigned.Add(neighborId);
                frontier.Enqueue((neighborId, neighborDistance));
            }
        }

        FillSprawlGaps(layer, assigned, biome, gapFillThreshold);
    }

    private static void FillSprawlGaps(PlanetLayer layer, HashSet<int> assignedIds, BiomeDef biome, int requiredNeighborCount)
    {
        var additions = new HashSet<int>();
        foreach (var tileId in assignedIds)
        {
            foreach (var neighbor in GetNeighborTiles(layer, tileId))
            {
                var neighborId = neighbor.tile.tileId;
                if (assignedIds.Contains(neighborId) || neighbor.WaterCovered)
                {
                    continue;
                }

                var assignedNeighborCount = 0;
                foreach (var secondNeighbor in GetNeighborTiles(layer, neighborId))
                {
                    if (assignedIds.Contains(secondNeighbor.tile.tileId))
                    {
                        assignedNeighborCount++;
                    }
                }

                if (assignedNeighborCount >= requiredNeighborCount)
                {
                    additions.Add(neighborId);
                }
            }
        }

        ApplyBiome(layer, additions, biome);
        assignedIds.UnionWith(additions);
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
            SpreadPollutionFromSource(layer, groundZeroId, MajorPollutionCenter, MajorPollutionGuaranteedFullRadius, MajorPollutionFullRadius, MajorPollutionOuterRadius, 37501);
        }

        foreach (var groundZeroId in loneGroundZeroIds)
        {
            SpreadPollutionFromSource(layer, groundZeroId, LonePollutionCenter, LonePollutionGuaranteedFullRadius, LonePollutionFullRadius, LonePollutionOuterRadius, 37551);
        }
    }

    private static void SpreadPollutionFromSource(PlanetLayer layer, int sourceId, float centerPollution, int guaranteedFullRadius, int noisyFullRadius, int outerRadius, int salt)
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

            if (distance > outerRadius + PollutionRadiusNoise || tile.PrimaryBiome == null || !tile.PrimaryBiome.allowPollution)
            {
                continue;
            }

            var fullRadiusNoise = Math.Max(guaranteedFullRadius, GetPollutionRadiusWithNoise(sourceId, tileId, noisyFullRadius, salt));
            var outerRadiusNoise = Math.Max(fullRadiusNoise + 1, GetPollutionRadiusWithNoise(sourceId, tileId, outerRadius, salt + 1));
            var pollutionValue = GetPollutionAtDistance(centerPollution, distance, guaranteedFullRadius, fullRadiusNoise, outerRadiusNoise);
            if (pollutionValue > tile.pollution)
            {
                tile.pollution = pollutionValue;
            }

            if (distance >= outerRadius + PollutionRadiusNoise)
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

    private static int GetPollutionRadiusWithNoise(int sourceId, int tileId, int baseRadius, int salt)
    {
        var radiusNoise = RollForTile(CombineTileIds(sourceId, tileId), tileId, salt) * ((PollutionRadiusNoise * 2) + 1);
        return baseRadius + (int)Math.Floor(radiusNoise) - PollutionRadiusNoise;
    }

    private static float GetPollutionAtDistance(float centerPollution, int distance, int guaranteedFullRadius, int fullRadius, int outerRadius)
    {
        if (distance <= guaranteedFullRadius)
        {
            return centerPollution;
        }

        if (distance <= fullRadius)
        {
            return centerPollution;
        }

        if (distance >= outerRadius)
        {
            return 0f;
        }

        var outerBand = Math.Max(1, outerRadius - fullRadius);
        var progress = (distance - fullRadius) / (float)outerBand;
        return Math.Max(0f, PollutionOuterFloor + (centerPollution - PollutionOuterFloor) * (1f - progress));
    }

    private static void GenerateGroundZeroTrunkRoads(string seed, PlanetLayer layer, List<int> groundZeroIds, RoadDef roadDef)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        var connectedPairs = new HashSet<long>();
        var trunkRoadIds = new HashSet<int>();
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

                var path = FindWeightedRoadPath(seedHash, layer, sourceId, targetId, false, trunkRoadIds, 37240 + successfulConnections);
                if (path == null || path.Count < 2)
                {
                    continue;
                }

                ApplyRoadPath(layer, path, roadDef);
                trunkRoadIds.UnionWith(path);
                connectedPairs.Add(pairKey);
                successfulConnections++;
            }
        }
    }

    private static void GenerateGroundZeroLocalRoads(string seed, PlanetLayer layer, List<int> groundZeroIds, RoadDef roadDef)
    {
        var seedHash = GenText.StableStringHash(seed) ^ 184356723;
        foreach (var groundZeroId in groundZeroIds)
        {
            var anchorIds = SelectLocalRoadAnchors(seedHash, layer, groundZeroId);
            if (anchorIds.Count == 0)
            {
                continue;
            }

            var localRoadIds = new HashSet<int> { groundZeroId };
            foreach (var anchorId in anchorIds)
            {
                var hubPath = FindWeightedRoadPath(seedHash, layer, groundZeroId, anchorId, true, localRoadIds, 37310 + anchorId);
                if (hubPath == null || hubPath.Count < 2)
                {
                    continue;
                }

                ApplyRoadPath(layer, hubPath, roadDef);
                localRoadIds.UnionWith(hubPath);
            }

            ConnectLocalRoadAnchors(seedHash, layer, groundZeroId, anchorIds, roadDef, localRoadIds);
        }
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

    private static List<int> FindWeightedRoadPath(int seedHash, PlanetLayer layer, int sourceId, int targetId, bool localRoadMode, HashSet<int> preferredRoadIds, int salt)
    {
        if (sourceId == targetId)
        {
            return new List<int> { sourceId };
        }

        var frontier = new MinPriorityQueue();
        var cameFrom = new Dictionary<int, int>();
        var bestCostByTile = new Dictionary<int, float>();
        cameFrom[sourceId] = -1;
        bestCostByTile[sourceId] = 0f;
        frontier.Enqueue(sourceId, GetPathHeuristic(layer, sourceId, targetId));

        while (frontier.Count != 0)
        {
            var currentEntry = frontier.Dequeue();
            var currentId = currentEntry.TileId;
            if (!bestCostByTile.TryGetValue(currentId, out var currentCost))
            {
                continue;
            }

            var expectedPriority = currentCost + GetPathHeuristic(layer, currentId, targetId);
            if (currentEntry.Priority > expectedPriority + 0.0001f)
            {
                continue;
            }

            if (currentId == targetId)
            {
                return ReconstructPath(cameFrom, targetId);
            }

            foreach (var neighbor in GetNeighborTiles(layer, currentId))
            {
                var neighborId = neighbor.tile.tileId;
                var passable = localRoadMode ? IsRoadTilePassable(neighbor) : IsHighwayTilePassable(neighbor);
                if (!passable)
                {
                    continue;
                }

                var stepCost = GetRoadStepCost(seedHash, layer, currentId, neighbor, localRoadMode, preferredRoadIds, salt);
                var newCost = currentCost + stepCost;
                if (bestCostByTile.TryGetValue(neighborId, out var knownCost) && newCost >= knownCost)
                {
                    continue;
                }

                cameFrom[neighborId] = currentId;
                bestCostByTile[neighborId] = newCost;
                frontier.Enqueue(neighborId, newCost + GetPathHeuristic(layer, neighborId, targetId));
            }
        }

        return null;
    }

    private static List<int> SelectLocalRoadAnchors(int seedHash, PlanetLayer layer, int groundZeroId)
    {
        var desiredAnchorCount = MinimumLocalAnchorCount + (int)(RollForTile(seedHash, groundZeroId, 37301) * (MaximumLocalAnchorCount - MinimumLocalAnchorCount + 1));
        var idealRadius = (MinimumLocalAnchorRadius + MaximumLocalAnchorRadius) * 0.5f;
        var candidates = GetReachableTiles(layer, groundZeroId, MaximumLocalAnchorRadius, IsRoadTilePassable)
            .Where(reachableTile => reachableTile.Distance >= MinimumLocalAnchorRadius && IsLocalRoadAnchorTile(reachableTile.Tile))
            .OrderByDescending(reachableTile => GetLocalAnchorScore(seedHash, groundZeroId, reachableTile.Tile, reachableTile.Distance, idealRadius))
            .ToList();

        var anchorIds = new List<int>();
        foreach (var candidate in candidates)
        {
            if (anchorIds.Count >= desiredAnchorCount)
            {
                break;
            }

            if (anchorIds.All(existingId => ApproxTileDistance(layer, existingId, candidate.TileId) >= LocalAnchorSeparation))
            {
                anchorIds.Add(candidate.TileId);
            }
        }

        if (anchorIds.Count >= desiredAnchorCount)
        {
            return anchorIds;
        }

        foreach (var candidate in candidates)
        {
            if (anchorIds.Count >= desiredAnchorCount)
            {
                break;
            }

            if (!anchorIds.Contains(candidate.TileId))
            {
                anchorIds.Add(candidate.TileId);
            }
        }

        return anchorIds;
    }

    private static void ConnectLocalRoadAnchors(int seedHash, PlanetLayer layer, int groundZeroId, List<int> anchorIds, RoadDef roadDef, HashSet<int> localRoadIds)
    {
        var connectedPairs = new HashSet<long>();

        foreach (var anchorId in anchorIds)
        {
            var desiredConnections = MinimumLocalConnectorCount + (int)(RollForTile(seedHash, anchorId, 37360) * (MaximumLocalConnectorCount - MinimumLocalConnectorCount + 1));
            var successfulConnections = 0;
            var candidateTargets = anchorIds
                .Where(targetId => targetId != anchorId)
                .OrderBy(targetId => ApproxTileDistance(layer, anchorId, targetId))
                .ThenBy(targetId => Math.Abs(ApproxTileDistance(layer, groundZeroId, targetId) - ApproxTileDistance(layer, groundZeroId, anchorId)))
                .ThenBy(targetId => RollForTile(seedHash, targetId, 37361))
                .ToList();

            foreach (var targetId in candidateTargets)
            {
                if (successfulConnections >= desiredConnections)
                {
                    break;
                }

                var directDistance = ApproxTileDistance(layer, anchorId, targetId);
                if (directDistance < MinimumLocalConnectorDistance || directDistance > MaximumLocalConnectorDistance)
                {
                    continue;
                }

                var pairKey = GetConnectionKey(anchorId, targetId);
                if (connectedPairs.Contains(pairKey))
                {
                    continue;
                }

                var connectorPath = FindWeightedRoadPath(seedHash, layer, anchorId, targetId, true, localRoadIds, 37380 + successfulConnections);
                if (connectorPath == null || connectorPath.Count < 2)
                {
                    continue;
                }

                ApplyRoadPath(layer, connectorPath, roadDef);
                localRoadIds.UnionWith(connectorPath);
                connectedPairs.Add(pairKey);
                successfulConnections++;
            }
        }

        var chordCount = MinimumLocalChordCount + (int)(RollForTile(seedHash, groundZeroId, 37420) * (MaximumLocalChordCount - MinimumLocalChordCount + 1));
        for (var chordIndex = 0; chordIndex < chordCount; chordIndex++)
        {
            var sourceAnchor = anchorIds
                .OrderBy(anchorId => RollForTile(seedHash, anchorId, 37430 + chordIndex))
                .FirstOrDefault();
            if (sourceAnchor == 0 && !anchorIds.Contains(0))
            {
                continue;
            }

            var targetAnchor = anchorIds
                .Where(anchorId => anchorId != sourceAnchor)
                .OrderByDescending(anchorId => ApproxTileDistance(layer, sourceAnchor, anchorId))
                .ThenBy(anchorId => RollForTile(seedHash, anchorId, 37431 + chordIndex))
                .FirstOrDefault();
            if (targetAnchor == 0 && !anchorIds.Contains(0))
            {
                continue;
            }

            var chordKey = GetConnectionKey(sourceAnchor, targetAnchor);
            if (connectedPairs.Contains(chordKey))
            {
                continue;
            }

            var chordPath = FindWeightedRoadPath(seedHash, layer, sourceAnchor, targetAnchor, true, localRoadIds, 37440 + chordIndex);
            if (chordPath == null || chordPath.Count < 2)
            {
                continue;
            }

            ApplyRoadPath(layer, chordPath, roadDef);
            localRoadIds.UnionWith(chordPath);
            connectedPairs.Add(chordKey);
        }
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

            Find.WorldGrid.OverlayRoad(sourceTile.tile, targetTile.tile, roadDef);
        }
    }

    private static float GetRoadStepCost(int seedHash, PlanetLayer layer, int currentId, SurfaceTile neighbor, bool localRoadMode, HashSet<int> preferredRoadIds, int salt)
    {
        var cost = 1f;
        cost += GetHillinessPenalty(neighbor.hilliness);
        cost += Math.Max(0f, neighbor.PrimaryBiome?.movementDifficulty ?? 0f) * 0.08f;
        cost += CountWaterNeighbors(layer, neighbor.tile.tileId) * 0.10f;
        cost += RollForTile(seedHash, CombineTileIds(currentId, neighbor.tile.tileId), salt) * RoadJitterWeight;

        if (preferredRoadIds != null && preferredRoadIds.Contains(neighbor.tile.tileId))
        {
            cost -= localRoadMode ? LocalRoadReuseDiscount : TrunkRoadReuseDiscount;
        }

        if (localRoadMode && IsUrbanRoadBiome(neighbor))
        {
            cost -= LocalUrbanTileDiscount;
        }

        return Math.Max(0.55f, cost);
    }

    private static float GetPathHeuristic(PlanetLayer layer, int sourceId, int targetId)
    {
        return ApproxTileDistance(layer, sourceId, targetId) * RoadHeuristicWeight;
    }

    private static float ApproxTileDistance(PlanetLayer layer, int sourceId, int targetId)
    {
        return Find.WorldGrid.ApproxDistanceInTiles(layer.PlanetTileForID(sourceId), layer.PlanetTileForID(targetId));
    }

    private static float GetHillinessPenalty(Hilliness hilliness)
    {
        return hilliness switch
        {
            Hilliness.Flat => 0f,
            Hilliness.SmallHills => 0.18f,
            Hilliness.LargeHills => 0.55f,
            Hilliness.Mountainous => 1.15f,
            _ => 3f,
        };
    }

    private static int CountWaterNeighbors(PlanetLayer layer, int tileId)
    {
        var waterNeighborCount = 0;
        foreach (var neighbor in GetNeighborTiles(layer, tileId))
        {
            if (neighbor.WaterCovered)
            {
                waterNeighborCount++;
            }
        }

        return waterNeighborCount;
    }

    private static List<ReachableTile> GetReachableTiles(PlanetLayer layer, int sourceId, int maxDistance, System.Func<SurfaceTile, bool> predicate)
    {
        var reachableTiles = new List<ReachableTile>();
        var frontier = new Queue<ReachableTile>();
        var visited = new HashSet<int> { sourceId };
        frontier.Enqueue(new ReachableTile(sourceId, layer[sourceId] as SurfaceTile, 0));

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            if (current.Tile == null || current.Distance > maxDistance)
            {
                continue;
            }

            if (current.Distance > 0)
            {
                reachableTiles.Add(current);
            }

            if (current.Distance == maxDistance)
            {
                continue;
            }

            foreach (var neighbor in GetNeighborTiles(layer, current.TileId))
            {
                var neighborId = neighbor.tile.tileId;
                if (!visited.Add(neighborId) || !predicate(neighbor))
                {
                    continue;
                }

                frontier.Enqueue(new ReachableTile(neighborId, neighbor, current.Distance + 1));
            }
        }

        return reachableTiles;
    }

    private static float GetLocalAnchorScore(int seedHash, int groundZeroId, SurfaceTile tile, int distance, float idealRadius)
    {
        var radiusScore = 1f - Math.Min(1f, Math.Abs(distance - idealRadius) / Math.Max(1f, idealRadius));
        var urbanBonus = IsUrbanRoadBiome(tile) ? 0.4f : 0f;
        var edgeBonus = CountWaterNeighbors(Find.WorldGrid.Surface, tile.tile.tileId) == 0 ? 0.15f : 0f;
        return radiusScore + urbanBonus + edgeBonus + RollForTile(seedHash, CombineTileIds(groundZeroId, tile.tile.tileId), 37305) * 0.35f;
    }

    private static bool IsLocalRoadAnchorTile(SurfaceTile tile)
    {
        return IsRoadTilePassable(tile) && IsUrbanRoadBiome(tile);
    }

    private static bool IsUrbanRoadBiome(SurfaceTile tile)
    {
        return tile?.PrimaryBiome?.defName switch
        {
            "Repconn_Crater" => true,
            "Repconn_Ruin" => true,
            "Repconn_Wasteland" => true,
            _ => false,
        };
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

    private static bool IsHighwayTilePassable(SurfaceTile tile)
    {
        return tile != null
            && !tile.WaterCovered
            && tile.PrimaryBiome != null
            && !tile.PrimaryBiome.impassable
            && tile.hilliness != Hilliness.Impassable;
    }

    private static void RefreshWorldRoadState(PlanetLayer layer)
    {
        Find.WorldPathGrid?.RecalculateLayerPerceivedPathCosts(layer);
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

    private static int CombineTileIds(int firstId, int secondId)
    {
        unchecked
        {
            return (firstId * 397) ^ secondId;
        }
    }

    private static HashSet<int> ExpandRings(PlanetLayer layer, IEnumerable<int> seedIds, int ringCount, System.Func<SurfaceTile, bool> predicate)
    {
        var allIds = new HashSet<int>(seedIds);
        var frontier = new HashSet<int>(seedIds);
        for (var ringIndex = 0; ringIndex < ringCount && frontier.Count > 0; ringIndex++)
        {
            var nextFrontier = new HashSet<int>();
            foreach (var sourceId in frontier)
            {
                foreach (var neighbor in GetNeighborTiles(layer, sourceId))
                {
                    var neighborId = neighbor.tile.tileId;
                    if (!allIds.Contains(neighborId) && predicate(neighbor))
                    {
                        nextFrontier.Add(neighborId);
                    }
                }
            }

            allIds.UnionWith(nextFrontier);
            frontier = nextFrontier;
        }

        return allIds;
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

    private readonly struct ReachableTile
    {
        public ReachableTile(int tileId, SurfaceTile tile, int distance)
        {
            TileId = tileId;
            Tile = tile;
            Distance = distance;
        }

        public int TileId { get; }

        public SurfaceTile Tile { get; }

        public int Distance { get; }
    }

    private sealed class MinPriorityQueue
    {
        private readonly List<QueueEntry> entries = new List<QueueEntry>();

        public int Count => entries.Count;

        public void Enqueue(int tileId, float priority)
        {
            entries.Add(new QueueEntry(tileId, priority));
            var index = entries.Count - 1;
            while (index > 0)
            {
                var parentIndex = (index - 1) / 2;
                if (entries[parentIndex].Priority <= entries[index].Priority)
                {
                    break;
                }

                (entries[parentIndex], entries[index]) = (entries[index], entries[parentIndex]);
                index = parentIndex;
            }
        }

        public QueueEntry Dequeue()
        {
            var root = entries[0];
            var lastIndex = entries.Count - 1;
            entries[0] = entries[lastIndex];
            entries.RemoveAt(lastIndex);

            var index = 0;
            while (index < entries.Count)
            {
                var leftIndex = index * 2 + 1;
                var rightIndex = leftIndex + 1;
                if (leftIndex >= entries.Count)
                {
                    break;
                }

                var smallestIndex = rightIndex < entries.Count && entries[rightIndex].Priority < entries[leftIndex].Priority
                    ? rightIndex
                    : leftIndex;
                if (entries[index].Priority <= entries[smallestIndex].Priority)
                {
                    break;
                }

                (entries[index], entries[smallestIndex]) = (entries[smallestIndex], entries[index]);
                index = smallestIndex;
            }

            return root;
        }
    }

    private readonly struct QueueEntry
    {
        public QueueEntry(int tileId, float priority)
        {
            TileId = tileId;
            Priority = priority;
        }

        public int TileId { get; }

        public float Priority { get; }
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