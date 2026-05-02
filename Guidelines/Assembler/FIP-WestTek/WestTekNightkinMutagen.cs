using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_UseEffectNightkinMutagen : CompProperties_UseEffect
{
    public CompProperties_UseEffectNightkinMutagen()
    {
        compClass = typeof(CompUseEffect_NightkinMutagen);
    }
}

public sealed class CompUseEffect_NightkinMutagen : CompUseEffect
{
    public override AcceptanceReport CanBeUsedBy(Pawn p)
    {
        if (!WestTekMutationUtility.IsNightkinImplantEligible(p))
        {
            return "Only first- and second-generation WestTek super mutants can use this mutagen.";
        }

        return base.CanBeUsedBy(p);
    }

    public override void DoEffect(Pawn usedBy)
    {
        base.DoEffect(usedBy);

        if (!WestTekMutationUtility.IsNightkinImplantEligible(usedBy))
        {
            Messages.Message("Only first- and second-generation WestTek super mutants can survive a Nightkin mutagen.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekMutationUtility.ApplyNightkinGene(usedBy);
    }
}