using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public enum FEVMutationOutcome
{
    Centaur,
    SuperMutant,
    Destroyed
}

public abstract class Recipe_AdministerFEVBase : Recipe_Surgery
{
    protected abstract bool AllowsColonists { get; }

    protected abstract bool AllowsPrisoners { get; }

    protected abstract FEVMutationOutcome RollOutcome();

    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        if (!base.AvailableOnNow(thing, part))
        {
            return false;
        }

        if (thing is not Pawn pawn)
        {
            return false;
        }

        if (!pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
        {
            return false;
        }

        bool colonist = pawn.IsFreeColonist;
        bool prisoner = pawn.IsPrisonerOfColony;

        return (colonist && AllowsColonists) || (prisoner && AllowsPrisoners);
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        if (pawn == null || pawn.MapHeld == null)
        {
            return;
        }

        switch (RollOutcome())
        {
            case FEVMutationOutcome.Centaur:
                TransformIntoCentaur(pawn);
                break;
            case FEVMutationOutcome.SuperMutant:
                TransformIntoSuperMutant(pawn);
                break;
            default:
                DestroyBody(pawn);
                break;
        }
    }

    private static void TransformIntoSuperMutant(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive || pawn.genes == null)
        {
            return;
        }

        pawn.genes.SetXenotype(ChooseSuperMutantXenotype(pawn));
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }

    private static void TransformIntoCentaur(Pawn pawn)
    {
        Map map = pawn.MapHeld;
        IntVec3 position = pawn.PositionHeld;
        Faction faction = ResolveCentaurFaction(pawn);

        KillWithoutCorpse(pawn);

        Pawn centaur = PawnGenerator.GeneratePawn(WestTekDefOf.WestTek_Centaur, faction);
        GenSpawn.Spawn(centaur, position, map);

        if (faction == Faction.OfPlayer && centaur.training != null)
        {
            centaur.training.Train(TrainableDefOf.Obedience, null, true);
            centaur.training.Train(TrainableDefOf.Release, null, true);
        }
    }

    private static void DestroyBody(Pawn pawn)
    {
        Map map = pawn.MapHeld;
        IntVec3 position = pawn.PositionHeld;

        SpawnBloodBurst(position, map);
        KillWithoutCorpse(pawn);

        Thing probe = ThingMaker.MakeThing(WestTekDefOf.WestTek_FEVProbe);
        GenPlace.TryPlaceThing(probe, position, map, ThingPlaceMode.Near);
    }

    private static void SpawnBloodBurst(IntVec3 center, Map map)
    {
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 1.9f, true))
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            Thing filth = ThingMaker.MakeThing(ThingDefOf.Filth_Blood);
            GenPlace.TryPlaceThing(filth, cell, map, ThingPlaceMode.Near);
        }
    }

    private static void KillWithoutCorpse(Pawn pawn)
    {
        pawn.Kill(null);
        pawn.Corpse?.Destroy();
    }

    private static Faction ResolveCentaurFaction(Pawn pawn)
    {
        if (pawn.IsColonist || pawn.IsPrisonerOfColony || pawn.IsSlaveOfColony)
        {
            return Faction.OfPlayer;
        }

        return pawn.Faction;
    }

    private static XenotypeDef ChooseSuperMutantXenotype(Pawn pawn)
    {
        XenotypeDef xenotype = pawn.genes.Xenotype;
        if (xenotype == WestTekDefOf.WestTek_Xenotype_PureHumans || xenotype == WestTekDefOf.WestTek_Xenotype_VaultDweller)
        {
            return WestTekDefOf.WestTek_Xenotype_SuperMutant_2;
        }

        return WestTekDefOf.WestTek_Xenotype_SuperMutant_1;
    }
}

public sealed class Recipe_AdministerUnrefinedFEVDosage : Recipe_AdministerFEVBase
{
    protected override bool AllowsColonists => false;

    protected override bool AllowsPrisoners => true;

    protected override FEVMutationOutcome RollOutcome()
    {
        return (FEVMutationOutcome)Rand.RangeInclusive(0, 2);
    }
}

public sealed class Recipe_AdministerRefinedFEVDosage : Recipe_AdministerFEVBase
{
    protected override bool AllowsColonists => true;

    protected override bool AllowsPrisoners => true;

    protected override FEVMutationOutcome RollOutcome()
    {
        return Rand.Chance(0.5f) ? FEVMutationOutcome.Centaur : FEVMutationOutcome.SuperMutant;
    }
}

public sealed class Recipe_AdministerIsolatedFEVDosage : Recipe_AdministerFEVBase
{
    protected override bool AllowsColonists => true;

    protected override bool AllowsPrisoners => false;

    protected override FEVMutationOutcome RollOutcome()
    {
        return FEVMutationOutcome.SuperMutant;
    }
}