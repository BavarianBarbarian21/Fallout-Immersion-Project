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
        AcceptanceReport baseReport = base.CanBeUsedBy(p);
        if (!baseReport.Accepted)
        {
            return baseReport;
        }

        if (!WestTekMutationUtility.IsSuperMutant(p))
        {
            return "Only first- and second-generation WestTek super mutants can use this mutagen.";
        }

        if (WestTekMutationUtility.HasNightkinGene(p))
        {
            return "This pawn already has the Nightkin implant.";
        }

        return true;
    }

    public override void DoEffect(Pawn usedBy)
    {
        if (!WestTekMutationUtility.IsNightkinImplantEligible(usedBy))
        {
            Messages.Message(
                "Only first- and second-generation WestTek super mutants without an existing Nightkin implant can survive a Nightkin mutagen.",
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return;
        }

        base.DoEffect(usedBy);

        WestTekMutationUtility.ApplyNightkinGene(usedBy);

        Messages.Message(
            $"{usedBy.LabelShort} has been implanted with the Nightkin mutation.",
            usedBy,
            MessageTypeDefOf.PositiveEvent
        );
    }
}