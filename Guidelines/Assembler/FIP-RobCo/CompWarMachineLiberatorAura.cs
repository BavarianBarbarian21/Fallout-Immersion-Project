using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP_RobCo;

public class CompWarMachineLiberatorAura : ThingComp
{
    private readonly HashSet<int> buffedLiberators = new();
    private int nextRefreshTick;

    public CompProperties_WarMachineLiberatorAura Props => (CompProperties_WarMachineLiberatorAura)props;

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
        if (previousMap == null)
        {
            return;
        }

        foreach (Pawn pawn in previousMap.mapPawns.AllPawnsSpawned)
        {
            if (buffedLiberators.Contains(pawn.thingIDNumber))
            {
                Hediff hediff = pawn.health?.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (hediff != null)
                {
                    pawn.health.RemoveHediff(hediff);
                }
            }
        }
    }

    private void RefreshAura()
    {
        if (parent is not Pawn warMachine || !warMachine.Spawned || warMachine.Map == null)
        {
            return;
        }

        HashSet<int> inRange = new();
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(warMachine.Position, Props.radius, true))
        {
            if (!cell.InBounds(warMachine.Map))
            {
                continue;
            }

            List<Thing> things = cell.GetThingList(warMachine.Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is not Pawn pawn || pawn.Dead || pawn.Faction != warMachine.Faction || pawn.def.defName != Props.targetThingDefName)
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

        foreach (int pawnId in buffedLiberators)
        {
            if (inRange.Contains(pawnId))
            {
                continue;
            }

            Pawn pawn = null;
            foreach (Pawn spawnedPawn in warMachine.Map.mapPawns.AllPawnsSpawned)
            {
                if (spawnedPawn.thingIDNumber == pawnId)
                {
                    pawn = spawnedPawn;
                    break;
                }
            }

            Hediff hediff = pawn?.health?.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        buffedLiberators.Clear();
        foreach (int pawnId in inRange)
        {
            buffedLiberators.Add(pawnId);
        }
    }
}

public class CompProperties_WarMachineLiberatorAura : CompProperties
{
    public float radius = 8f;
    public int refreshTicks = 60;
    public string targetThingDefName = "RobCo_Liberator";
    public HediffDef hediffDef;

    public CompProperties_WarMachineLiberatorAura()
    {
        compClass = typeof(CompWarMachineLiberatorAura);
    }
}