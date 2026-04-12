using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FIP_RobCo;

public class JobDriver_ReloadMechAbility : JobDriver
{
    private const TargetIndex MechInd = TargetIndex.A;
    private const TargetIndex AmmoInd = TargetIndex.B;

    private Pawn Mech => (Pawn)job.GetTarget(MechInd).Thing;
    private Thing Ammo => job.GetTarget(AmmoInd).Thing;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        if (!pawn.Reserve(Mech, job, 1, -1, null, errorOnFailed))
        {
            return false;
        }

        return Ammo == null || pawn.Reserve(Ammo, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(MechInd);
        this.FailOnForbidden(MechInd);

        Toil checkAmmo = new();
        checkAmmo.initAction = delegate
        {
            CompAbilityEffect_LaunchProjectileMultiple comp = GetReloadableComp();
            if (comp?.Props.ammoDef == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            if ((pawn.inventory?.innerContainer.TotalStackCountOfDef(comp.Props.ammoDef) ?? 0) >= comp.Props.ammoCountPerCharge)
            {
                return;
            }

            Thing foundAmmo = GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(comp.Props.ammoDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                t => !t.IsForbidden(pawn) && pawn.CanReserve(t) && t != Ammo);

            if (foundAmmo != null)
            {
                job.SetTarget(AmmoInd, foundAmmo);
                pawn.Reserve(foundAmmo, job);
            }
        };
        checkAmmo.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return checkAmmo;

        yield return Toils_Goto.GotoThing(AmmoInd, PathEndMode.ClosestTouch)
            .FailOnDespawnedNullOrForbidden(AmmoInd)
            .FailOn(() => Ammo == null);

        Toil takeAmmo = new();
        takeAmmo.initAction = delegate
        {
            CompAbilityEffect_LaunchProjectileMultiple comp = GetReloadableComp();
            if (comp == null || Ammo == null)
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }

            Thing takenAmmo = Ammo.SplitOff(System.Math.Min(job.count, Ammo.stackCount));
            if (!pawn.inventory.innerContainer.TryAdd(takenAmmo))
            {
                takenAmmo.Destroy();
            }

            if (Ammo.Destroyed)
            {
                pawn.Map.reservationManager.Release(Ammo, pawn, job);
            }
        };
        takeAmmo.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return takeAmmo;

        yield return Toils_Goto.GotoThing(MechInd, PathEndMode.Touch);

        Toil reload = new();
        reload.initAction = delegate
        {
            GetReloadableComp()?.ReloadFromInventory(pawn);
        };
        reload.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return reload;
    }

    private CompAbilityEffect_LaunchProjectileMultiple GetReloadableComp()
    {
        if (Mech?.abilities?.abilities == null)
        {
            return null;
        }

        foreach (Ability ability in Mech.abilities.abilities)
        {
            CompAbilityEffect_LaunchProjectileMultiple comp = ability.CompOfType<CompAbilityEffect_LaunchProjectileMultiple>();
            if (comp != null && comp.Props.ammoDef != null)
            {
                return comp;
            }
        }

        return null;
    }
}