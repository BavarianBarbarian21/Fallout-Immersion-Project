using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP_RobCo;

public static class RobCoQuestUtility
{
    public const int TicksPerDay = 60000;
    public const int OfferDelayTicks = 14 * TicksPerDay;
    public const int CourierExpiryTicks = 7 * TicksPerDay;

    public static int PlayerTile
    {
        get
        {
            Map map = Find.AnyPlayerHomeMap;
            if (map != null)
            {
                return map.Tile;
            }

            Settlement settlement = Find.WorldObjects.Settlements.FirstOrDefault(static s => s.Faction != null && s.Faction.IsPlayer);
            return settlement?.Tile ?? Find.WorldObjects.Caravans.FirstOrDefault(static c => c.Faction != null && c.Faction.IsPlayer)?.Tile ?? -1;
        }
    }

    public static List<Faction> EligibleCourierFactions()
    {
        HashSet<Faction> factionsWithBases = Find.WorldObjects.Settlements
            .Where(static settlement => settlement.Faction != null && !settlement.Faction.IsPlayer)
            .Select(static settlement => settlement.Faction)
            .ToHashSet();

        return Find.FactionManager.AllFactionsListForReading
            .Where(faction => faction is { IsPlayer: false } && !faction.def.hidden && factionsWithBases.Contains(faction))
            .ToList();
    }

    public static bool TryFindSiteTile(int minDistance, int maxDistance, out int tile)
    {
        int playerTile = PlayerTile;
        if (playerTile < 0)
        {
            tile = -1;
            return false;
        }

        if (TileFinder.TryFindPassableTileWithTraversalDistance(
            playerTile,
            minDistance,
            maxDistance,
            out PlanetTile siteTile,
            null,
            false,
            TileFinderMode.Near,
            false,
            false))
        {
            tile = siteTile;
            return true;
        }

        tile = -1;
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