using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_TargetEffectCultivationFloraMutagen : CompProperties
{
    public CompProperties_TargetEffectCultivationFloraMutagen()
    {
        compClass = typeof(CompTargetEffect_CultivationFloraMutagen);
    }
}

public sealed class CompTargetEffect_CultivationFloraMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFloraMutationUtility.IsEligibleForNumenMutation(pawn))
        {
            Messages.Message("This subject needs exactly one plant symbiote and a compatible human xenotype.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFloraMutationUtility.ApplyNumenMutation(pawn);
        ConsumeOne();

        Messages.Message(
            $"{pawn.LabelShortCap} cultivates into a Numen flora strain.",
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

public sealed class CompProperties_TargetEffectPollinationFloraMutagen : CompProperties
{
    public CompProperties_TargetEffectPollinationFloraMutagen()
    {
        compClass = typeof(CompTargetEffect_PollinationFloraMutagen);
    }
}

public sealed class CompTargetEffect_PollinationFloraMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFloraMutationUtility.IsEligibleForSporeCarrierMutation(pawn))
        {
            Messages.Message("This subject is not compatible with pollination flora mutation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFloraMutationUtility.ApplySporeCarrierMutation(pawn);
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
