using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FIP_RobCo;

public class JobDriver_ReloadAbilityFromMap : JobDriver
{
    private const TargetIndex AmmoInd = TargetIndex.A;

    private Thing Ammo => job.GetTarget(AmmoInd).Thing;
    private Ability Ability => job.ability;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return Ammo == null || pawn.Reserve(Ammo, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(AmmoInd);

        Toil findAmmo = new();
        findAmmo.initAction = delegate
        {
            Thing foundAmmo = FindAmmoOnMap();
            if (foundAmmo == null)
            {
                EndJobWith(JobCondition.Incompletable);
                Messages.Message("NoAmmoFound".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            job.SetTarget(AmmoInd, foundAmmo);
            pawn.Reserve(foundAmmo, job, 1, -1);
        };
        yield return findAmmo;

        yield return Toils_Goto.GotoThing(AmmoInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(AmmoInd);

        Toil takeAmmo = new();
        takeAmmo.initAction = delegate
        {
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

        Toil reload = new();
        reload.initAction = delegate
        {
            Ability?.CompOfType<CompAbilityEffect_LaunchProjectileMultiple>()?.ReloadFromInventory();
        };
        reload.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return reload;
    }

    private Thing FindAmmoOnMap()
    {
        CompAbilityEffect_LaunchProjectileMultiple comp = Ability?.CompOfType<CompAbilityEffect_LaunchProjectileMultiple>();
        if (comp?.Props.ammoDef == null)
        {
            return null;
        }

        return GenClosest.ClosestThingReachable(
            pawn.Position,
            pawn.Map,
            ThingRequest.ForDef(comp.Props.ammoDef),
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn),
            9999f,
            t => !t.IsForbidden(pawn) && pawn.CanReserve(t));
    }
}