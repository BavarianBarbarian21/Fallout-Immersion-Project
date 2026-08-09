using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace FIP.HHTools;

public partial class HHToolsFactionPoliticsTracker
{
    private const int BrahminDeliveryIntervalTicks = 30 * 60000;
    private const int RangerAidCooldownTicks = 15 * 60000;
    private const int CoalitionAidCooldownTicks = 30 * 60000;
    private const int CoalitionReservationTimeoutTicks = 60000;
    private const int RangerResponseSeparationTicks = 25000;

    private int lastRangerDeploymentTick = -1;
    private Faction pendingCoalitionSupportFaction;
    private Faction pendingCoalitionTargetFaction;
    private int pendingCoalitionTargetTile = -1;
    private int pendingCoalitionReservationTick = -1;

    public HHToolsFactionPoliticalState GetAvailableCoalitionSupport(Faction targetFaction)
    {
        if (targetFaction == null
            || targetFaction == Faction.OfPlayer
            || pendingCoalitionSupportFaction != null)
        {
            return null;
        }

        int currentTick = Find.TickManager?.TicksGame ?? 0;
        return factionStates
            .Where(state =>
                state is
                {
                    system: HHToolsFactionPoliticalSystem.Authoritarian,
                    friendOfTheFamilies: true
                }
                && state.faction != null
                && state.faction != targetFaction
                && !state.faction.HostileTo(Faction.OfPlayer)
                && (state.nextCoalitionAidTick < 0 || state.nextCoalitionAidTick <= currentTick))
            .OrderBy(state => state.faction.Name)
            .FirstOrDefault();
    }

    public bool TryReserveCoalitionSupport(Faction targetFaction, out Faction supportFaction)
    {
        supportFaction = null;
        HHToolsFactionPoliticalState supportState = GetAvailableCoalitionSupport(targetFaction);
        if (supportState == null)
        {
            return false;
        }

        pendingCoalitionSupportFaction = supportState.faction;
        pendingCoalitionTargetFaction = targetFaction;
        pendingCoalitionTargetTile = -1;
        pendingCoalitionReservationTick = Find.TickManager.TicksGame;
        supportFaction = supportState.faction;
        return true;
    }

    public void SetPendingCoalitionTargetTile(PlanetTile tile)
    {
        if (pendingCoalitionSupportFaction != null)
        {
            pendingCoalitionTargetTile = tile;
        }
    }

    private void ProcessPoliticalBenefits()
    {
        ProcessPendingCoalitionSupport();
        ProcessBrahminDeliveries();
        ProcessRangerResponse();
    }

    private void ProcessBrahminDeliveries()
    {
        int currentTick = Find.TickManager.TicksGame;
        List<Map> playerHomeMaps = Find.Maps
            .Where(map => map.IsPlayerHome && !map.IsPocketMap)
            .ToList();

        foreach (HHToolsFactionPoliticalState state in factionStates)
        {
            if (state is not
                {
                    system: HHToolsFactionPoliticalSystem.Civilized,
                    civilizedControlLocked: true,
                    civilizedController: HHToolsCivilizedParty.BrahminBarons
                }
                || state.faction == null
                || state.faction.HostileTo(Faction.OfPlayer))
            {
                continue;
            }

            if (state.nextBrahminDeliveryTick < 0)
            {
                state.nextBrahminDeliveryTick = currentTick + BrahminDeliveryIntervalTicks;
                continue;
            }

            if (state.nextBrahminDeliveryTick > currentTick || playerHomeMaps.Count == 0)
            {
                continue;
            }

            Map destinationMap = playerHomeMaps.RandomElement();
            IntVec3 dropCell = DropCellFinder.TradeDropSpot(destinationMap);
            List<Thing> delivery = MakeBrahminDelivery();
            DropPodUtility.DropThingsNear(
                dropCell,
                destinationMap,
                delivery,
                forbid: false,
                faction: state.faction);

            Find.LetterStack.ReceiveLetter(
                "Brahmin Baron shipment",
                $"The Brahmin Barons of {state.faction.Name} have sent their regular food and resource shipment.",
                LetterDefOf.PositiveEvent,
                new TargetInfo(dropCell, destinationMap));

            state.nextBrahminDeliveryTick = currentTick + BrahminDeliveryIntervalTicks;
        }
    }

    private void ProcessRangerResponse()
    {
        int currentTick = Find.TickManager.TicksGame;
        if (lastRangerDeploymentTick >= 0
            && currentTick - lastRangerDeploymentTick < RangerResponseSeparationTicks)
        {
            return;
        }

        foreach (Map map in Find.Maps.Where(candidate => candidate.IsPlayerHome && !candidate.IsPocketMap))
        {
            List<Thing> activeThreats = map.attackTargetsCache.TargetsHostileToColony
                .Where(target => GenHostility.IsActiveThreatToPlayer(target))
                .OfType<Thing>()
                .ToList();
            float hostileCombatPower = activeThreats
                .OfType<Pawn>()
                .Where(pawn => !pawn.Downed)
                .Sum(pawn => pawn.kindDef.combatPower);

            if (hostileCombatPower <= 120f)
            {
                continue;
            }

            HHToolsFactionPoliticalState rangerState = factionStates
                .Where(state =>
                    state is
                    {
                        system: HHToolsFactionPoliticalSystem.Civilized,
                        civilizedControlLocked: true,
                        civilizedController: HHToolsCivilizedParty.DesertRangers
                    }
                    && state.faction != null
                    && !state.faction.HostileTo(Faction.OfPlayer)
                    && (state.nextRangerAidTick < 0 || state.nextRangerAidTick <= currentTick))
                .OrderBy(state => state.faction.Name)
                .FirstOrDefault();

            if (rangerState == null)
            {
                return;
            }

            MakeSupportFactionHostileToThreats(
                rangerState.faction,
                activeThreats.Select(threat => threat.Faction));

            if (TrySendFriendlyRaid(
                    map,
                    rangerState.faction,
                    Mathf.Clamp(hostileCombatPower * 0.45f, 250f, 1200f)))
            {
                rangerState.nextRangerAidTick = currentTick + RangerAidCooldownTicks;
                lastRangerDeploymentTick = currentTick;
            }

            return;
        }
    }

    private void ProcessPendingCoalitionSupport()
    {
        if (pendingCoalitionSupportFaction == null)
        {
            return;
        }

        int currentTick = Find.TickManager.TicksGame;
        if (pendingCoalitionReservationTick < 0
            || currentTick - pendingCoalitionReservationTick > CoalitionReservationTimeoutTicks)
        {
            ClearPendingCoalitionSupport();
            return;
        }

        HHToolsFactionPoliticalState supportState = GetOrCreateState(pendingCoalitionSupportFaction);
        if (supportState is not
            {
                system: HHToolsFactionPoliticalSystem.Authoritarian,
                friendOfTheFamilies: true
            }
            || pendingCoalitionTargetFaction == null
            || pendingCoalitionSupportFaction.HostileTo(Faction.OfPlayer)
            || pendingCoalitionTargetTile < 0)
        {
            return;
        }

        Map targetMap = Find.Maps.FirstOrDefault(map =>
            map.Tile == pendingCoalitionTargetTile
            && map.ParentFaction == pendingCoalitionTargetFaction
            && map.mapPawns.AllPawnsSpawned.Any(pawn => pawn.Faction == Faction.OfPlayer));
        if (targetMap == null)
        {
            return;
        }

        if (!pendingCoalitionSupportFaction.HostileTo(pendingCoalitionTargetFaction))
        {
            MakeFactionsHostile(
                pendingCoalitionSupportFaction,
                pendingCoalitionTargetFaction);
        }

        float supportPoints = Mathf.Clamp(
            StorytellerUtility.DefaultThreatPointsNow(targetMap) * 0.45f,
            300f,
            1600f);

        if (!TrySendFriendlyRaid(targetMap, pendingCoalitionSupportFaction, supportPoints))
        {
            return;
        }

        supportState.nextCoalitionAidTick = currentTick + CoalitionAidCooldownTicks;
        ClearPendingCoalitionSupport();
    }

    private static bool TrySendFriendlyRaid(Map map, Faction faction, float points)
    {
        IncidentParms parameters = StorytellerUtility.DefaultParmsNow(
            IncidentCategoryDefOf.ThreatBig,
            map);
        parameters.target = map;
        parameters.faction = faction;
        parameters.points = points;
        parameters.raidArrivalModeForQuickMilitaryAid = true;
        return IncidentDefOf.RaidFriendly.Worker.TryExecute(parameters);
    }

    private static void MakeSupportFactionHostileToThreats(
        Faction supportFaction,
        IEnumerable<Faction> threatFactions)
    {
        foreach (Faction threatFaction in threatFactions
                     .Where(faction => faction != null && faction != supportFaction)
                     .Distinct())
        {
            if (!supportFaction.HostileTo(threatFaction))
            {
                MakeFactionsHostile(supportFaction, threatFaction);
            }
        }
    }

    private static void MakeFactionsHostile(Faction first, Faction second)
    {
        if (first == null || second == null || first == second || first.HostileTo(second))
        {
            return;
        }

        if (first.HasGoodwill && second.HasGoodwill)
        {
            first.TryAffectGoodwillWith(
                second,
                first.GoodwillToMakeHostile(second),
                canSendMessage: false,
                canSendHostilityLetter: false);
            return;
        }

        first.SetRelationDirect(
            second,
            FactionRelationKind.Hostile,
            canSendHostilityLetter: false);
    }

    private static List<Thing> MakeBrahminDelivery()
    {
        List<Thing> delivery = [];
        AddStacks(delivery, "PackagedSurvivalMeal", Rand.RangeInclusive(18, 30));
        AddStacks(delivery, "Corn", Rand.RangeInclusive(90, 150));

        switch (Rand.RangeInclusive(0, 2))
        {
            case 0:
                AddStacks(delivery, "Steel", Rand.RangeInclusive(140, 220));
                break;
            case 1:
                AddStacks(delivery, "Cloth", Rand.RangeInclusive(80, 140));
                break;
            default:
                AddStacks(delivery, "ComponentIndustrial", Rand.RangeInclusive(6, 12));
                break;
        }

        return delivery;
    }

    private static void AddStacks(List<Thing> things, string thingDefName, int totalCount)
    {
        ThingDef thingDef = DefDatabase<ThingDef>.GetNamedSilentFail(thingDefName);
        if (thingDef == null)
        {
            return;
        }

        int remaining = totalCount;
        while (remaining > 0)
        {
            Thing thing = ThingMaker.MakeThing(thingDef);
            thing.stackCount = Mathf.Min(remaining, thingDef.stackLimit);
            remaining -= thing.stackCount;
            things.Add(thing);
        }
    }

    private void EnsureSettlementTraderTrackers()
    {
        foreach (Settlement settlement in Find.WorldObjects.SettlementBases)
        {
            HHToolsFactionPoliticalState state = GetOrCreateState(settlement.Faction);
            bool shouldUsePoliticalTracker =
                state?.system == HHToolsFactionPoliticalSystem.Civilized;

            if (shouldUsePoliticalTracker
                && settlement.trader is not HHToolsSettlementTraderTracker)
            {
                settlement.trader?.TryDestroyStock();
                settlement.trader = new HHToolsSettlementTraderTracker(settlement);
            }
            else if (!shouldUsePoliticalTracker
                     && settlement.trader is HHToolsSettlementTraderTracker)
            {
                settlement.trader.TryDestroyStock();
                settlement.trader = new Settlement_TraderTracker(settlement);
            }
            else if (state?.system == HHToolsFactionPoliticalSystem.Authoritarian
                     && settlement.trader?.TraderKind == null)
            {
                // Despotic settlements use only the four family markets.
                // Clear any vanilla settlement stock retained by an older save.
                settlement.trader.TryDestroyStock();
            }
        }
    }

    private void ClearPendingCoalitionSupport()
    {
        pendingCoalitionSupportFaction = null;
        pendingCoalitionTargetFaction = null;
        pendingCoalitionTargetTile = -1;
        pendingCoalitionReservationTick = -1;
    }

    private void ExposeBenefitData()
    {
        Scribe_Values.Look(ref lastRangerDeploymentTick, "lastRangerDeploymentTick", -1);
        Scribe_References.Look(
            ref pendingCoalitionSupportFaction,
            "pendingCoalitionSupportFaction");
        Scribe_References.Look(
            ref pendingCoalitionTargetFaction,
            "pendingCoalitionTargetFaction");
        Scribe_Values.Look(ref pendingCoalitionTargetTile, "pendingCoalitionTargetTile", -1);
        Scribe_Values.Look(
            ref pendingCoalitionReservationTick,
            "pendingCoalitionReservationTick",
            -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit
            && (pendingCoalitionSupportFaction == null || pendingCoalitionTargetFaction == null))
        {
            ClearPendingCoalitionSupport();
        }
    }
}
