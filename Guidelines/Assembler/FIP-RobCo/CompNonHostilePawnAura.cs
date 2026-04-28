using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP_RobCo;

public class CompNonHostilePawnAura : ThingComp
{
    private readonly HashSet<int> buffedPawnIds = new();
    private int nextRefreshTick;

    public CompProperties_NonHostilePawnAura Props => (CompProperties_NonHostilePawnAura)props;

    public override void CompTick()
    {
        if (Find.TickManager.TicksGame < nextRefreshTick)
        {
            return;
        }

        nextRefreshTick = Find.TickManager.TicksGame + Props.refreshTicks;
        RefreshAura();
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        Cleanup(previousMap);
    }

    private void RefreshAura()
    {
        if (parent is not Pawn sourcePawn || !sourcePawn.Spawned || sourcePawn.Map == null)
        {
            return;
        }

        HashSet<int> inRange = new();
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(sourcePawn.Position, Props.radius, true))
        {
            if (!cell.InBounds(sourcePawn.Map))
            {
                continue;
            }

            List<Thing> things = cell.GetThingList(sourcePawn.Map);
            for (int index = 0; index < things.Count; index++)
            {
                if (things[index] is not Pawn pawn || !CanAffectPawn(sourcePawn, pawn))
                {
                    continue;
                }

                inRange.Add(pawn.thingIDNumber);
                if (pawn.health?.hediffSet.GetFirstHediffOfDef(Props.hediffDef) == null)
                {
                    pawn.health.AddHediff(Props.hediffDef);
                }
            }
        }

        foreach (int pawnId in buffedPawnIds)
        {
            if (inRange.Contains(pawnId))
            {
                continue;
            }

            Pawn pawn = FindSpawnedPawnById(sourcePawn.Map, pawnId);
            Hediff hediff = pawn?.health?.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        buffedPawnIds.Clear();
        foreach (int pawnId in inRange)
        {
            buffedPawnIds.Add(pawnId);
        }
    }

    private void Cleanup(Map map)
    {
        if (map == null)
        {
            return;
        }

        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (!buffedPawnIds.Contains(pawn.thingIDNumber))
            {
                continue;
            }

            Hediff hediff = pawn.health?.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        buffedPawnIds.Clear();
    }

    private bool CanAffectPawn(Pawn sourcePawn, Pawn targetPawn)
    {
        if (targetPawn == sourcePawn || targetPawn.Dead || targetPawn.health == null)
        {
            return false;
        }

        if (targetPawn.HostileTo(sourcePawn))
        {
            return false;
        }

        if (Props.toolUserOnly && !targetPawn.RaceProps.ToolUser)
        {
            return false;
        }

        if (Props.humanlikeOnly && !targetPawn.RaceProps.Humanlike)
        {
            return false;
        }

        return true;
    }

    private static Pawn FindSpawnedPawnById(Map map, int pawnId)
    {
        IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
        for (int index = 0; index < pawns.Count; index++)
        {
            if (pawns[index].thingIDNumber == pawnId)
            {
                return pawns[index];
            }
        }

        return null;
    }
}

public class CompProperties_NonHostilePawnAura : CompProperties
{
    public HediffDef hediffDef;
    public float radius = 8f;
    public int refreshTicks = 60;
    public bool toolUserOnly = true;
    public bool humanlikeOnly;

    public CompProperties_NonHostilePawnAura()
    {
        compClass = typeof(CompNonHostilePawnAura);
    }
}