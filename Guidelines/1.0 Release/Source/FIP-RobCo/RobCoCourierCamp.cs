using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.RobCo;

public class RobCoCourierCamp : Site
{
    public string branchDefName;
    public int expirationTick;
    public int targetThingId = -1;
    public bool mapInitialized;
    public bool succeeded;
    public bool failed;
    public bool vaultQueued;
    public bool rewardPending;
    public bool fallbackPending;
    public bool rewardDelivered;

    private Pawn targetPawn;
    private Thing pendingChip;
    private Caravan rewardCaravan;

    private RobCoPlatinumQuestBranchDef Branch => DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(branchDefName);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref branchDefName, "branchDefName");
        Scribe_Values.Look(ref expirationTick, "expirationTick");
        Scribe_Values.Look(ref targetThingId, "targetThingId", -1);
        Scribe_Values.Look(ref mapInitialized, "mapInitialized");
        Scribe_Values.Look(ref succeeded, "succeeded");
        Scribe_Values.Look(ref failed, "failed");
        Scribe_Values.Look(ref vaultQueued, "vaultQueued");
        Scribe_Values.Look(ref rewardPending, "rewardPending");
        Scribe_Values.Look(ref fallbackPending, "fallbackPending");
        Scribe_Values.Look(ref rewardDelivered, "rewardDelivered");
        Scribe_References.Look(ref targetPawn, "targetPawn");
        Scribe_Deep.Look(ref pendingChip, "pendingChip");
        Scribe_References.Look(ref rewardCaravan, "rewardCaravan");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            EnsureSitePart();

            // Saves made by the original implementation only recorded
            // `succeeded`. Treat those rewards as committed so loading such a
            // save can never manufacture a second unique chip.
            if (succeeded)
            {
                rewardDelivered = true;
                rewardPending = false;
                fallbackPending = false;
                pendingChip = null;
                rewardCaravan = null;
            }

            if (failed)
            {
                rewardPending = false;
                fallbackPending = false;
                pendingChip = null;
                rewardCaravan = null;
            }
        }
    }

    protected override void Tick()
    {
        // A Site without a SitePart cannot provide a label/icon and RimWorld may
        // remove its map immediately.  Repair older saves before Site.Tick sees it.
        EnsureSitePart();
        base.Tick();

        if (succeeded)
        {
            QueueVaultIfNeeded();
            if (!HasMap)
            {
                RobCoQuestUtility.DestroyWorldObject(this);
            }

            return;
        }

        if (failed)
        {
            if (!HasMap)
            {
                RobCoQuestUtility.DestroyWorldObject(this);
            }

            return;
        }

        Map currentMap = HasMap ? Map : null;
        if (currentMap != null && !mapInitialized)
        {
            EnsureMapInitialized(currentMap);
        }

        if (!succeeded && !failed && currentMap != null && mapInitialized)
        {
            if (rewardPending)
            {
                TryCompletePendingReward(currentMap, mapBeingRemoved: false);
            }
            else if (targetPawn == null && targetThingId < 0)
            {
                // Compatibility recovery for a save made between the old
                // generation-failure path setting mapInitialized and granting
                // its fallback reward.
                BeginFallbackReward(currentMap, mapBeingRemoved: false);
            }
            else
            {
                // Deliberately run on every game tick. A courier killed just
                // before the player forms a caravan must not be mistaken for an
                // escaped live target by the map-removal callback.
                CheckCourierState(currentMap, mapBeingRemoved: false);
            }
        }
        else if (!succeeded && !failed && rewardPending && Find.TickManager.TicksGame % 250 == 0)
        {
            // A reward that could not be placed before map removal remains
            // owned by this world object and is retried against the caravan or
            // a player home map instead of being silently lost.
            TryCompletePendingReward(null, mapBeingRemoved: false);
        }

        if (succeeded)
        {
            QueueVaultIfNeeded();
            if (!HasMap)
            {
                RobCoQuestUtility.DestroyWorldObject(this);
            }

            return;
        }

        if (Find.TickManager.TicksGame % 250 != 0)
        {
            return;
        }

        // Resolve a kill before the deadline check so a courier killed during the
        // final 250-tick polling window cannot be reported as escaped.
        if (!failed && !succeeded && !rewardPending && expirationTick > 0 && Find.TickManager.TicksGame >= expirationTick)
        {
            FailCourier();
            if (!HasMap)
            {
                RobCoQuestUtility.DestroyWorldObject(this);
            }
        }
    }

    public override string GetInspectString()
    {
        string text = base.GetInspectString();
        if (expirationTick > 0 && !succeeded && !failed)
        {
            int ticksLeft = expirationTick - Find.TickManager.TicksGame;
            if (ticksLeft > 0)
            {
                text += (text.NullOrEmpty() ? string.Empty : "\n") + "RobCoQuestCourierWindow".Translate(ticksLeft.ToStringTicksToPeriod()).Resolve();
            }
        }

        return text;
    }

    public override void Notify_MyMapAboutToBeRemoved()
    {
        try
        {
            Map map = HasMap ? Map : null;
            if (!succeeded && !failed)
            {
                if (rewardPending)
                {
                    TryCompletePendingReward(map, mapBeingRemoved: true);
                }
                else if (map != null && mapInitialized)
                {
                    CheckCourierState(map, mapBeingRemoved: true);
                }
                else
                {
                    // The map is being deinitialized and there is neither a
                    // generated target nor a committed fallback reward. Record
                    // an explicit escape instead of leaving an unwinnable site.
                    FailCourier();
                }
            }

            if (succeeded)
            {
                QueueVaultIfNeeded();
            }
        }
        finally
        {
            // Site relays this notification to its SitePart workers. It must run
            // even if another mod throws while this quest is resolving.
            base.Notify_MyMapAboutToBeRemoved();
        }
    }

    public override void Notify_MyMapRemoved(Map map)
    {
        if (succeeded)
        {
            QueueVaultIfNeeded();
        }

        base.Notify_MyMapRemoved(map);
        if (succeeded || failed)
        {
            RobCoQuestUtility.DestroyWorldObject(this);
        }
    }

    private void EnsureMapInitialized(Map map)
    {
        // Set this before attempting pawn generation so a broken modded faction
        // cannot cause the fallback reward to repeat every polling tick.
        mapInitialized = true;
        Pawn target;
        try
        {
            target = GenerateCourierTarget();
        }
        catch (Exception ex)
        {
            Log.Error($"RobCo courier pawn generation failed for {Faction?.Name ?? "<no faction>"}: {ex}");
            target = null;
        }

        if (target == null)
        {
            BeginFallbackReward(map, mapBeingRemoved: false);
            return;
        }

        IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 12);
        try
        {
            GenSpawn.Spawn(target, spawnCell, map);
            targetPawn = target;
            targetThingId = target.thingIDNumber;
        }
        catch (Exception ex)
        {
            Log.Error($"RobCo courier could not be spawned; using the quest fallback: {ex}");
            if (!target.Destroyed)
            {
                target.Discard(true);
            }

            BeginFallbackReward(map, mapBeingRemoved: false);
        }
    }

    private Pawn GenerateCourierTarget()
    {
        if (Faction == null)
        {
            return null;
        }

        PawnGroupMakerParms parms = new()
        {
            groupKind = PawnGroupKindDefOf.Combat,
            tile = Tile,
            faction = Faction,
            points = Branch?.stage1TargetPoints ?? 600f,
            generateFightersOnly = true
        };

        List<Pawn> generated = PawnGroupMakerUtility.GeneratePawns(parms).ToList();
        Pawn target = generated
            .OrderByDescending(static pawn => pawn.kindDef?.combatPower ?? 0f)
            .FirstOrDefault();

        // GeneratePawns builds a complete combat group.  Only one member is the
        // courier; discard the unused, never-spawned pawns instead of leaking them.
        foreach (Pawn pawn in generated)
        {
            if (pawn != target && !pawn.Destroyed)
            {
                pawn.Discard(true);
            }
        }

        return target;
    }

    public void EnsureSitePart()
    {
        if (parts != null && parts.Count > 0)
        {
            return;
        }

        parts = new List<SitePart>
        {
            new(this, RobCoQuestDefOf.RobCo_CourierTrail, new SitePartParams())
        };
    }

    private void CheckCourierState(Map map, bool mapBeingRemoved)
    {
        ResolveTargetReference(map);
        if (targetPawn == null)
        {
            // Losing an unresolved/live pawn reference is an escape, not proof of
            // death.  This prevents unloading or despawning the courier from
            // fabricating a platinum chip.
            FailCourier();
            return;
        }

        if (!targetPawn.Dead)
        {
            if (mapBeingRemoved)
            {
                FailCourier();
            }

            return;
        }

        rewardPending = true;
        fallbackPending = false;
        TryCompletePendingReward(map, mapBeingRemoved);
    }

    private void ResolveTargetReference(Map map)
    {
        if (targetPawn != null || targetThingId < 0)
        {
            return;
        }

        targetPawn = map.mapPawns.AllPawns.FirstOrDefault(pawn => pawn.thingIDNumber == targetThingId);
        if (targetPawn != null)
        {
            return;
        }

        targetPawn = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
            .OfType<Corpse>()
            .Select(static corpse => corpse.InnerPawn)
            .FirstOrDefault(pawn => pawn?.thingIDNumber == targetThingId);
    }

    private void BeginFallbackReward(Map map, bool mapBeingRemoved)
    {
        if (succeeded || failed)
        {
            return;
        }

        rewardPending = true;
        fallbackPending = true;
        TryCompletePendingReward(map, mapBeingRemoved);
    }

    private void TryCompletePendingReward(Map map, bool mapBeingRemoved)
    {
        if (!rewardPending || succeeded || failed)
        {
            return;
        }

        IntVec3 preferredCell = map?.Center ?? IntVec3.Invalid;
        if (!fallbackPending && map != null && targetPawn?.Corpse?.PositionHeld.IsValid == true)
        {
            preferredCell = targetPawn.Corpse.PositionHeld;
        }

        Thing deliveredChip = TryDeliverPendingChip(map, preferredCell, mapBeingRemoved);
        if (deliveredChip == null)
        {
            return;
        }

        bool wasFallback = fallbackPending;

        // Commit the one-shot state before goodwill, quest scheduling, or letter
        // code runs. Any later exception therefore cannot issue another chip.
        pendingChip = null;
        rewardCaravan = null;
        rewardDelivered = true;
        rewardPending = false;
        fallbackPending = false;
        succeeded = true;

        if (!wasFallback)
        {
            Faction?.TryAffectGoodwillWith(Faction.OfPlayer, -100);
        }

        QueueVaultIfNeeded();
        LookTargets lookTargets = deliveredChip.Destroyed
            ? new LookTargets(this)
            : new LookTargets(deliveredChip);
        RobCoQuestUtility.SendLetter(
            (wasFallback ? "RobCoQuestCourierFledLabel" : "RobCoQuestChipRecoveredLabel").Translate().Resolve(),
            wasFallback
                ? "RobCoQuestCourierFledText".Translate().Resolve()
                : "RobCoQuestChipRecoveredText".Translate(Faction?.Name ?? "RobCoQuestBuyersFallback".Translate().Resolve()).Resolve(),
            LetterDefOf.PositiveEvent,
            lookTargets);
    }

    private Thing TryDeliverPendingChip(Map map, IntVec3 preferredCell, bool mapBeingRemoved)
    {
        pendingChip ??= ThingMaker.MakeThing(RobCoQuestDefOf.RobCo_PlatinumChip);
        Thing chip = pendingChip;

        // When a caravan is leaving the site, giving the reward directly to it
        // is the only placement that survives the imminent map deinitialization.
        if (mapBeingRemoved || map == null)
        {
            Caravan caravan = rewardCaravan;
            if (caravan == null || caravan.Destroyed)
            {
                caravan = Find.WorldObjects.PlayerControlledCaravanAt(Tile);
                rewardCaravan = caravan;
            }

            if (caravan != null && TryGiveToCaravan(chip, caravan))
            {
                return chip;
            }
        }

        if (map != null && !mapBeingRemoved && TryPlaceChip(chip, map, preferredCell.IsValid ? preferredCell : map.Center))
        {
            return chip;
        }

        Map homeMap = Find.AnyPlayerHomeMap;
        if (homeMap != null && homeMap != map)
        {
            try
            {
                IntVec3 homeCell = CellFinder.RandomClosewalkCellNear(homeMap.Center, homeMap, 8);
                if (TryPlaceChip(chip, homeMap, homeCell))
                {
                    return chip;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"RobCo could not find a safe home-map cell for the pending platinum chip: {ex}");
            }
        }

        // Keep the same unspawned Thing instance attached to this Site. It is
        // deep-scribed and retried; no failed attempt creates a second chip.
        return null;
    }

    private static bool TryGiveToCaravan(Thing chip, Caravan caravan)
    {
        try
        {
            CaravanInventoryUtility.GiveThing(caravan, chip);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning($"RobCo could not give the pending platinum chip to the departing caravan: {ex}");
        }

        return ChipHasOwnerOrWasMerged(chip);
    }

    private static bool TryPlaceChip(Thing chip, Map map, IntVec3 cell)
    {
        try
        {
            if (GenPlace.TryPlaceThing(chip, cell, map, ThingPlaceMode.Near))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"RobCo could not place the pending platinum chip on {map}: {ex}");
        }

        return ChipHasOwnerOrWasMerged(chip);
    }

    private static bool ChipHasOwnerOrWasMerged(Thing chip)
    {
        // GenPlace may merge stackable things and destroy the source instance.
        // In that case the requested count was still transferred successfully.
        return chip.Destroyed || chip.Spawned || chip.ParentHolder != null;
    }

    private void QueueVaultIfNeeded()
    {
        if (!succeeded || !rewardDelivered || vaultQueued || Branch == null)
        {
            return;
        }

        RobCoQuestGameComponent component = Current.Game?.GetComponent<RobCoQuestGameComponent>();
        if (component == null)
        {
            return;
        }

        component.QueueVaultSite(Branch);
        vaultQueued = true;
    }

    private void FailCourier()
    {
        if (failed || succeeded)
        {
            return;
        }

        rewardPending = false;
        fallbackPending = false;
        failed = true;
        RobCoQuestUtility.SendLetter(
            "RobCoQuestCourierEscapedLabel".Translate().Resolve(),
            "RobCoQuestCourierEscapedText".Translate().Resolve(),
            LetterDefOf.NegativeEvent,
            new LookTargets(this));
    }
}
