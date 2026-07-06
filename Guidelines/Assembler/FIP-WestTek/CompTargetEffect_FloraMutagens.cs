using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_TargetEffectRefinedFloraMutagen : CompProperties
{
    public CompProperties_TargetEffectRefinedFloraMutagen()
    {
        compClass = typeof(CompTargetEffect_RefinedFloraMutagen);
    }
}

public sealed class CompTargetEffect_RefinedFloraMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFloraMutationUtility.IsEligibleForFloraMutation(pawn))
        {
            Messages.Message("This subject is not compatible with refined flora mutation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        int implantCount = WestTekFloraMutationUtility.CountFloraImplants(pawn);

        WestTekFloraMutationUtility.ApplyRefinedFloraMutation(pawn);
        ConsumeOne();

        string result = implantCount >= 5 ? "Overgrown" : "Numen";

        Messages.Message(
            $"{pawn.LabelShortCap} stabilizes into a {result} flora strain.",
            pawn,
            MessageTypeDefOf.PositiveEvent
        );
    }

    private void ConsumeOne()
    {
        if (parent == null || parent.Destroyed)
        {
            return;
        }

        if (parent.stackCount > 1)
        {
            Thing split = parent.SplitOff(1);
            split.Destroy(DestroyMode.Vanish);
        }
        else
        {
            parent.Destroy(DestroyMode.Vanish);
        }
    }
}

public sealed class CompProperties_TargetEffectExperimentalFloraMutagen : CompProperties
{
    public CompProperties_TargetEffectExperimentalFloraMutagen()
    {
        compClass = typeof(CompTargetEffect_ExperimentalFloraMutagen);
    }
}

public sealed class CompTargetEffect_ExperimentalFloraMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFloraMutationUtility.IsEligibleForFloraMutation(pawn))
        {
            Messages.Message("This subject is not compatible with experimental flora mutation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFloraMutationUtility.ApplyExperimentalFloraMutation(pawn);
        ConsumeOne();

        Messages.Message(
            $"{pawn.LabelShortCap} becomes a Spore Carrier.",
            pawn,
            MessageTypeDefOf.PositiveEvent
        );
    }

    private void ConsumeOne()
    {
        if (parent == null || parent.Destroyed)
        {
            return;
        }

        if (parent.stackCount > 1)
        {
            Thing split = parent.SplitOff(1);
            split.Destroy(DestroyMode.Vanish);
        }
        else
        {
            parent.Destroy(DestroyMode.Vanish);
        }
    }
}