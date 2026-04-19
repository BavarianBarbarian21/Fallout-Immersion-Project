using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP_RobCo;

public class RobCoCourierCamp : Camp
{
    public string branchDefName;
    public int expirationTick;
    public int targetThingId = -1;
    public bool mapInitialized;
    public bool succeeded;
    public bool failed;

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
    }

    protected override void Tick()
    {
        base.Tick();
        if (Find.TickManager.TicksGame % 250 != 0)
        {
            return;
        }

        if (!failed && !succeeded && expirationTick > 0 && Find.TickManager.TicksGame >= expirationTick)
        {
            failed = true;
            RobCoQuestUtility.SendLetter(
                "RobCoQuestCourierEscapedLabel".Translate().Resolve(),
                "RobCoQuestCourierEscapedText".Translate().Resolve(),
                LetterDefOf.NegativeEvent,
                new LookTargets(this));

            if (!HasMap)
            {
                RobCoQuestUtility.DestroyWorldObject(this);
                return;
            }
        }

        if (HasMap && !mapInitialized)
        {
            EnsureMapInitialized(Map);
        }

        if (HasMap && mapInitialized && !succeeded && !failed)
        {
            CheckCourierState(Map);
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

    public override void Notify_MyMapRemoved(Map map)
    {
        base.Notify_MyMapRemoved(map);
        if (succeeded || failed)
        {
            RobCoQuestUtility.DestroyWorldObject(this);
        }
    }

    private void EnsureMapInitialized(Map map)
    {
        Pawn target = GenerateCourierTarget();
        if (target == null)
        {
            failed = true;
            return;
        }

        IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 12);
        GenSpawn.Spawn(target, spawnCell, map);
        targetThingId = target.thingIDNumber;
        mapInitialized = true;
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

        return PawnGroupMakerUtility.GeneratePawns(parms)
            .OrderByDescending(static pawn => pawn.kindDef?.combatPower ?? 0f)
            .FirstOrDefault();
    }

    private void CheckCourierState(Map map)
    {
        Pawn target = map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn => pawn.thingIDNumber == targetThingId);
        if (target != null && !target.Dead)
        {
            return;
        }

        succeeded = true;
        IntVec3 rewardCell = target?.PositionHeld.IsValid == true ? target.PositionHeld : map.Center;
        Thing chip = ThingMaker.MakeThing(RobCoQuestDefOf.PlatinumChip);
        GenPlace.TryPlaceThing(chip, rewardCell, map, ThingPlaceMode.Near);
        Faction?.TryAffectGoodwillWith(Faction.OfPlayer, -100);
        if (Branch != null)
        {
            Current.Game.GetComponent<RobCoQuestGameComponent>()?.RevealVaultSite(Branch);
        }

        RobCoQuestUtility.SendLetter(
            "RobCoQuestChipRecoveredLabel".Translate().Resolve(),
            "RobCoQuestChipRecoveredText".Translate(Faction?.Name ?? "RobCoQuestBuyersFallback".Translate().Resolve()).Resolve(),
            LetterDefOf.PositiveEvent,
            new LookTargets(chip));
    }
}