using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FIP_RobCo;

public class CompPawnNuzzler : ThingComp
{
    private int nextNuzzleTick = -1;

    public CompProperties_PawnNuzzler Props => (CompProperties_PawnNuzzler)props;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (nextNuzzleTick < 0)
        {
            ScheduleNextNuzzleTick();
        }
    }

    public override void CompTick()
    {
        if (parent is not Pawn nuzzler || !nuzzler.Spawned || nuzzler.Map == null)
        {
            return;
        }

        if (Find.TickManager.TicksGame < nextNuzzleTick)
        {
            return;
        }

        ScheduleNextNuzzleTick();
        TryStartNuzzleJob(nuzzler);
    }

    private void TryStartNuzzleJob(Pawn nuzzler)
    {
        if (nuzzler.Faction != Faction.OfPlayer)
        {
            return;
        }

        if (nuzzler.Dead || nuzzler.Downed || nuzzler.InMentalState || nuzzler.Awake() == false)
        {
            return;
        }

        if (nuzzler.Drafted)
        {
            return;
        }

        if (nuzzler.jobs?.curDriver != null && !nuzzler.CurJobDef.casualInterruptible)
        {
            return;
        }

        Pawn targetPawn = FindTargetPawn(nuzzler);
        if (targetPawn == null)
        {
            return;
        }

        JobDef nuzzleJobDef = DefDatabase<JobDef>.GetNamedSilentFail("Nuzzle");
        if (nuzzleJobDef == null)
        {
            return;
        }

        Job nuzzleJob = JobMaker.MakeJob(nuzzleJobDef, targetPawn);
        nuzzler.jobs.TryTakeOrderedJob(nuzzleJob);
    }

    private Pawn FindTargetPawn(Pawn nuzzler)
    {
        List<Pawn> candidates = new();
        IReadOnlyList<Pawn> colonists = nuzzler.Map.mapPawns.FreeColonistsSpawned;
        for (int index = 0; index < colonists.Count; index++)
        {
            Pawn candidate = colonists[index];
            if (candidate == nuzzler || candidate.Dead || candidate.Downed || !candidate.RaceProps.Humanlike)
            {
                continue;
            }

            if (candidate.Position.DistanceToSquared(nuzzler.Position) > Props.radius * Props.radius)
            {
                continue;
            }

            if (!nuzzler.CanReach(candidate, PathEndMode.Touch, Danger.Deadly) || !nuzzler.CanReserve(candidate))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates.Count == 0 ? null : candidates.RandomElement();
    }

    private void ScheduleNextNuzzleTick()
    {
        float meanHours = Props.nuzzleMtbHours > 0f
            ? Props.nuzzleMtbHours
            : (parent as Pawn)?.RaceProps?.nuzzleMtbHours ?? 12f;

        int meanTicks = (int)(meanHours * 2500f);
        int minTicks = meanTicks / 2;
        int maxTicks = meanTicks + meanTicks / 2;
        nextNuzzleTick = Find.TickManager.TicksGame + Rand.RangeInclusive(minTicks, maxTicks);
    }
}

public class CompProperties_PawnNuzzler : CompProperties
{
    public float nuzzleMtbHours = 12f;
    public float radius = 18f;

    public CompProperties_PawnNuzzler()
    {
        compClass = typeof(CompPawnNuzzler);
    }
}