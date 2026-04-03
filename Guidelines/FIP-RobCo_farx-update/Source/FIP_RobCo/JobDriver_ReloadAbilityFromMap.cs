using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace FIP_RobCo
{
    public class JobDriver_ReloadAbilityFromMap : JobDriver
    {
        private const TargetIndex AmmoInd = TargetIndex.A;
        private const TargetIndex AbilityInd = TargetIndex.B;

        private Thing Ammo => job.GetTarget(AmmoInd).Thing;
        private Ability Ability => job.ability;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (Ammo != null)
            {
                return pawn.Reserve(Ammo, job, 1, -1, null, errorOnFailed);
            }
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(AmmoInd);
            
            Toil findAmmo = new Toil();
            findAmmo.initAction = delegate
            {
                Thing foundAmmo = FindAmmoOnMap();
                if (foundAmmo == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    Messages.Message("NoAmmoFound".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                else
                {
                    job.SetTarget(AmmoInd, foundAmmo);
                    pawn.Reserve(foundAmmo, job, 1, -1);
                }
            };
            yield return findAmmo;

            yield return Toils_Goto.GotoThing(AmmoInd, PathEndMode.ClosestTouch).FailOnDespawnedNullOrForbidden(AmmoInd);

            Toil takeAmmo = new Toil();
            takeAmmo.initAction = delegate
            {
                int ammoNeeded = job.count;
                Thing ammo = Ammo;
                int ammoToTake = System.Math.Min(ammoNeeded, ammo.stackCount);
                
                Thing takenAmmo = ammo.SplitOff(ammoToTake);
                if (!pawn.inventory.innerContainer.TryAdd(takenAmmo))
                {
                    takenAmmo.Destroy();
                }
                
                if (ammo.Destroyed)
                {
                    pawn.Map.reservationManager.Release(ammo, pawn, job);
                }
            };
            takeAmmo.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return takeAmmo;

            Toil reload = new Toil();
            reload.initAction = delegate
            {
                CompAbilityEffect_LaunchProjectileMultiple comp = Ability?.CompOfType<CompAbilityEffect_LaunchProjectileMultiple>();
                if (comp != null)
                {
                    comp.ReloadFromInventory();
                }
            };
            reload.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return reload;
        }

        private Thing FindAmmoOnMap()
        {
            CompAbilityEffect_LaunchProjectileMultiple comp = Ability?.CompOfType<CompAbilityEffect_LaunchProjectileMultiple>();
            if (comp == null || comp.Props.ammoDef == null)
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
                (Thing t) => !t.IsForbidden(pawn) && pawn.CanReserve(t)
            );
        }
    }
}
