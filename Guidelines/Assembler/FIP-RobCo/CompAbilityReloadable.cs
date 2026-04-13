using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace FIP_RobCo;

public class CompAbilityReloadable : ThingComp
{
    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (parent is not Pawn mech || !mech.Faction.IsPlayerSafe())
        {
            yield break;
        }

        CompAbilityEffect_LaunchProjectileMultiple reloadableAbility = GetReloadableAbility(mech);
        if (reloadableAbility == null || reloadableAbility.Props.ammoDef == null)
        {
            yield break;
        }

        if (reloadableAbility.CurrentCharges >= reloadableAbility.Props.maxCharges)
        {
            yield break;
        }

        if (!selPawn.CanReach(mech, PathEndMode.Touch, Danger.Deadly))
        {
            yield return new FloatMenuOption("CannotReload".Translate() + ": " + "NoPath".Translate(), null);
            yield break;
        }

        int ammoInColonistInventory = selPawn.inventory?.innerContainer.TotalStackCountOfDef(reloadableAbility.Props.ammoDef) ?? 0;
        int ammoNeeded = (reloadableAbility.Props.maxCharges - reloadableAbility.CurrentCharges) * reloadableAbility.Props.ammoCountPerCharge;

        if (ammoInColonistInventory < reloadableAbility.Props.ammoCountPerCharge)
        {
            Thing ammoOnMap = GenClosest.ClosestThingReachable(
                selPawn.Position,
                selPawn.Map,
                ThingRequest.ForDef(reloadableAbility.Props.ammoDef),
                PathEndMode.ClosestTouch,
                TraverseParms.For(selPawn),
                9999f,
                t => !t.IsForbidden(selPawn) && selPawn.CanReserve(t));

            if (ammoOnMap == null)
            {
                yield return new FloatMenuOption("CannotReload".Translate() + ": " + "CommandReload_NoAmmo".Translate(reloadableAbility.Props.ammoDef.label), null);
                yield break;
            }
        }

        string label = "ReloadMechAbility".Translate(mech.LabelShort, reloadableAbility.Props.ammoDef.label);
        yield return FloatMenuUtility.DecoratePrioritizedTask(
            new FloatMenuOption(label, delegate
            {
                Job job = JobMaker.MakeJob(JobDefOf_FIPRobCo.ReloadMechAbility, mech);
                job.count = ammoNeeded;
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }),
            selPawn,
            mech);
    }

    public override string CompInspectStringExtra()
    {
        if (parent is not Pawn mech)
        {
            return null;
        }

        CompAbilityEffect_LaunchProjectileMultiple reloadableAbility = GetReloadableAbility(mech);
        if (reloadableAbility == null || reloadableAbility.Props.maxCharges <= 0)
        {
            return null;
        }

        return $"{reloadableAbility.Props.chargeNoun.CapitalizeFirst()}: {reloadableAbility.CurrentCharges} / {reloadableAbility.Props.maxCharges}";
    }

    private static CompAbilityEffect_LaunchProjectileMultiple GetReloadableAbility(Pawn mech)
    {
        if (mech.abilities?.abilities == null)
        {
            return null;
        }

        foreach (Ability ability in mech.abilities.abilities)
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

public class CompProperties_AbilityReloadable : CompProperties
{
    public CompProperties_AbilityReloadable()
    {
        compClass = typeof(CompAbilityReloadable);
    }
}