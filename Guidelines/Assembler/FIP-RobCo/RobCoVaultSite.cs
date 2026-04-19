using System;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP_RobCo;

public class RobCoVaultSite : Site
{
    public string branchDefName;
    public bool mapInitialized;
    public bool completed;

    public RobCoPlatinumQuestBranchDef Branch => DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(branchDefName);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref branchDefName, "branchDefName");
        Scribe_Values.Look(ref mapInitialized, "mapInitialized");
        Scribe_Values.Look(ref completed, "completed");
    }

    protected override void Tick()
    {
        base.Tick();
        if (!mapInitialized && HasMap)
        {
            EnsureMapInitialized(Map);
        }
    }

    public override void Notify_MyMapRemoved(Map map)
    {
        base.Notify_MyMapRemoved(map);
        if (!completed)
        {
            completed = true;
            RobCoQuestUtility.SendLetter(
                "RobCoQuestVaultClearedLabel".Translate().Resolve(),
                "RobCoQuestVaultClearedText".Translate().Resolve(),
                LetterDefOf.PositiveEvent);
        }

        RobCoQuestUtility.DestroyWorldObject(this);
    }

    public void EnsureMapInitialized(Map map)
    {
        if (mapInitialized || Branch == null)
        {
            return;
        }

        float targetCombatPower = Branch.stage2ThreatPoints;
        float currentCombatPower = 0f;
        int attempts = 0;
        while (currentCombatPower < targetCombatPower && attempts < 256)
        {
            attempts++;
            Pawn pawn = PawnGenerator.GeneratePawn(Branch.stage2EnemyPawnKind, Faction.OfMechanoids);
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 45);
            GenSpawn.Spawn(pawn, cell, map);
            currentCombatPower += Math.Max(1f, pawn.kindDef?.combatPower ?? 0f);
        }

        Pawn corpsePawn = PawnGenerator.GeneratePawn(Branch.stage2CorpsePawnKind, Faction.OfMechanoids);
        corpsePawn.Kill(null);
        if (corpsePawn.Corpse != null)
        {
            IntVec3 corpseCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 20);
            GenSpawn.Spawn(corpsePawn.Corpse, corpseCell, map);
        }

        mapInitialized = true;
    }
}