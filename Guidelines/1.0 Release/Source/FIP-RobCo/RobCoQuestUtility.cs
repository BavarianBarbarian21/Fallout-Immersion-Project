using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.RobCo;

public static class RobCoQuestUtility
{
    public const int TicksPerDay = 60000;
    public const int OfferDelayTicks = 14 * TicksPerDay;
    public const int CourierExpiryTicks = 7 * TicksPerDay;

    public static PlanetTile PlayerTile
    {
        get
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                return map.Tile;
            }

            Settlement settlement = Find.WorldObjects.Settlements.FirstOrDefault(static s => s.Faction != null && s.Faction.IsPlayer);
            if (settlement != null)
            {
                return settlement.Tile;
            }

            Caravan caravan = Find.WorldObjects.Caravans.FirstOrDefault(static c => c.Faction != null && c.Faction.IsPlayer);
            return caravan?.Tile ?? PlanetTile.Invalid;
        }
    }

    public static List<Faction> EligibleCourierFactions()
    {
        HashSet<Faction> factionsWithBases = Find.WorldObjects.Settlements
            .Where(static settlement => settlement.Faction != null && !settlement.Faction.IsPlayer)
            .Select(static settlement => settlement.Faction)
            .ToHashSet();

        List<Faction> settledFactions = Find.FactionManager.AllFactionsListForReading
            .Where(faction => faction is { IsPlayer: false } && !faction.def.hidden && factionsWithBases.Contains(faction))
            .ToList();

        if (settledFactions.Count > 0)
        {
            return settledFactions;
        }

        // Heavily customized worlds can remove every non-player settlement.  The
        // offer must still fire; a faction without a valid combat group is handled
        // by the courier site's explicit "courier fled" fallback.
        return Find.FactionManager.AllFactionsListForReading
            .Where(static faction => faction is { IsPlayer: false })
            .ToList();
    }

    public static bool TryFindSiteTile(int minDistance, int maxDistance, out PlanetTile tile)
    {
        PlanetTile playerTile = PlayerTile;
        if (!playerTile.Valid)
        {
            tile = PlanetTile.Invalid;
            return false;
        }

        static bool IsUnusedWorldTile(PlanetTile candidate) => !Find.WorldObjects.AnyWorldObjectAt(candidate);

        if (TileFinder.TryFindPassableTileWithTraversalDistance(
            playerTile,
            minDistance,
            maxDistance,
            out PlanetTile siteTile,
            IsUnusedWorldTile,
            false,
            TileFinderMode.Near,
            false,
            false))
        {
            tile = siteTile;
            return true;
        }

        // Small or unusually fragmented modded worlds may have no valid tile in
        // the narrative's preferred distance band.  Broaden the search instead of
        // consuming a unique quest because of world geometry.
        int fallbackMaxDistance = Math.Max(100, maxDistance + 40);
        if (TileFinder.TryFindPassableTileWithTraversalDistance(
            playerTile,
            1,
            fallbackMaxDistance,
            out siteTile,
            IsUnusedWorldTile,
            false,
            TileFinderMode.Near,
            false,
            false))
        {
            tile = siteTile;
            return true;
        }

        tile = PlanetTile.Invalid;
        return false;
    }

    public static string FormatOptionLabel(RobCoPlatinumQuestBranchDef branch, Faction faction)
    {
        return "RobCoQuestOptionLabel".Translate(branch.courierNumber, faction.Name, GetBranchChipTitle(branch)).Resolve();
    }

    public static string FormatOfferText(IEnumerable<(RobCoPlatinumQuestBranchDef branch, Faction faction)> options)
    {
        string optionText = string.Join("\n", options.Select(option => $"- {FormatOptionLabel(option.branch, option.faction)}"));
        return "RobCoQuestOfferText".Translate(optionText).Resolve();
    }

    public static string GetBranchChipTitle(RobCoPlatinumQuestBranchDef branch)
    {
        return $"RobCoQuestBranch.{branch.defName}.ChipTitle".Translate().Resolve();
    }

    public static string GetBranchStage2LetterLabel(RobCoPlatinumQuestBranchDef branch)
    {
        return $"RobCoQuestBranch.{branch.defName}.Stage2LetterLabel".Translate().Resolve();
    }

    public static string GetBranchStage2LetterText(RobCoPlatinumQuestBranchDef branch)
    {
        return $"RobCoQuestBranch.{branch.defName}.Stage2LetterText".Translate().Resolve();
    }

    public static void SendLetter(string label, string text, LetterDef letterDef, LookTargets lookTargets = null)
    {
        Find.LetterStack.ReceiveLetter(label, text, letterDef, lookTargets ?? LookTargets.Invalid);
    }

    public static void DestroyWorldObject(WorldObject worldObject)
    {
        if (worldObject != null && Find.WorldObjects.Contains(worldObject))
        {
            worldObject.Destroy();
        }
    }
}
