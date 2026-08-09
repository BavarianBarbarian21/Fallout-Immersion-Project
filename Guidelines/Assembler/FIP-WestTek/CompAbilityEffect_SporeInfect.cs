using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_AbilitySporeInfect : CompProperties_AbilityEffect
{
    public CompProperties_AbilitySporeInfect()
    {
        compClass = typeof(CompAbilityEffect_SporeInfect);
    }
}

public sealed class CompAbilityEffect_SporeInfect : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);

        Pawn caster = parent.pawn;
        Pawn victim = target.Pawn;

        if (caster == null || victim == null)
        {
            return;
        }

        if (!WestTekFloraMutationUtility.HasGene(caster, WestTekDefOf.WestTek_Gene_SporeCarrier))
        {
            Messages.Message("This pawn does not carry infectious spores.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFloraMutationUtility.IsEligibleForSporeCarrierMutation(victim))
        {
            Messages.Message("The target is not compatible with spore infection.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFloraMutationUtility.ApplySporeCarrierMutation(victim);

        Messages.Message(
            $"{victim.LabelShortCap} is overtaken by infectious FEV spores.",
            victim,
            MessageTypeDefOf.NegativeEvent
        );
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        Pawn victim = target.Pawn;

        if (victim == null)
        {
            return false;
        }

        return WestTekFloraMutationUtility.IsEligibleForSporeCarrierMutation(victim);
    }
}
