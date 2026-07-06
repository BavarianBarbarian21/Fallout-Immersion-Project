using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_TargetEffectUnrefinedFaunaMutagen : CompProperties
{
    public CompProperties_TargetEffectUnrefinedFaunaMutagen()
    {
        compClass = typeof(CompTargetEffect_UnrefinedFaunaMutagen);
    }
}

public sealed class CompTargetEffect_UnrefinedFaunaMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn raccoon || !WestTekFaunaMutationUtility.IsRaccoon(raccoon))
        {
            Messages.Message("This mutagen can only be used on a raccoon.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (raccoon.Dead || raccoon.MapHeld == null)
        {
            Messages.Message("The raccoon must be alive and present on the map.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Pawn slanter = WestTekFaunaMutationUtility.TransformRaccoonIntoSLanter(raccoon);
        if (slanter == null)
        {
            Messages.Message("The fauna mutagen failed to stabilize.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        ConsumeOne();

        Messages.Message(
            $"{slanter.LabelShortCap} emerges from the unstable FEV reaction as a S'Lanter.",
            slanter,
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

public sealed class CompProperties_TargetEffectSkinwalkerFaunaMutagen : CompProperties
{
    public CompProperties_TargetEffectSkinwalkerFaunaMutagen()
    {
        compClass = typeof(CompTargetEffect_SkinwalkerFaunaMutagen);
    }
}

public sealed class CompTargetEffect_SkinwalkerFaunaMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFaunaMutationUtility.IsSLanter(pawn))
        {
            Messages.Message("Skinwalker fauna mutagen can only be used on S'Lanter.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (WestTekFaunaMutationUtility.HasSkinwalkerMutation(pawn))
        {
            Messages.Message("This S'Lanter already carries the experimental Skinwalker mutation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFaunaMutationUtility.ApplyExperimentalSkinwalkerMutation(pawn);
        ConsumeOne();

        Messages.Message(
            $"{pawn.LabelShortCap} is now carrying the Skinwalker mutation.",
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

public sealed class CompProperties_TargetEffectDomesticationFaunaMutagen : CompProperties
{
    public CompProperties_TargetEffectDomesticationFaunaMutagen()
    {
        compClass = typeof(CompTargetEffect_DomesticationFaunaMutagen);
    }
}

public sealed class CompTargetEffect_DomesticationFaunaMutagen : CompTargetEffect
{
    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFaunaMutationUtility.IsSLanter(pawn))
        {
            Messages.Message("Domestication fauna mutagen can only be used on S'Lanter.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (WestTekFaunaMutationUtility.HasSkinwalkerMutation(pawn))
        {
            Messages.Message("This S'Lanter already carries the Skinwalker mutation and cannot be domesticated into a S'Nuffy line.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFaunaMutationUtility.TransformSLanterIntoSNuffy(pawn);
        ConsumeOne();

        Messages.Message(
            $"{pawn.LabelShortCap} stabilizes into a S'Nuffy fauna strain.",
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

public sealed class CompProperties_TargetEffectFaunaGenePackageMutagen : CompProperties
{
    public List<GeneDef> genesToAdd;
    public bool blockIfAnyFaunaPackageGenePresent = true;
    public string packageLabel = "fauna package";

    public CompProperties_TargetEffectFaunaGenePackageMutagen()
    {
        compClass = typeof(CompTargetEffect_FaunaGenePackageMutagen);
    }
}

public sealed class CompTargetEffect_FaunaGenePackageMutagen : CompTargetEffect
{
    private CompProperties_TargetEffectFaunaGenePackageMutagen Props => (CompProperties_TargetEffectFaunaGenePackageMutagen)props;

    public override void DoEffectOn(Pawn user, Thing target)
    {
        if (target is not Pawn pawn)
        {
            Messages.Message("This mutagen can only be used on a pawn.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFaunaMutationUtility.IsSNuffy(pawn))
        {
            Messages.Message("This mutagen can only be used on S'Nuffies.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (Props.blockIfAnyFaunaPackageGenePresent && WestTekFaunaMutationUtility.HasAnyFaunaPackageGene(pawn))
        {
            Messages.Message("This pawn already carries a stabilized fauna package.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (Props.genesToAdd == null || Props.genesToAdd.Count == 0)
        {
            Messages.Message("This fauna mutagen has no configured genes.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        WestTekFaunaMutationUtility.AddEndogenesIfMissing(pawn, Props.genesToAdd.Where(gene => gene != null));
        WestTekFaunaMutationUtility.AssignRandomFurGene(pawn);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
        WestTekFaunaMutationUtility.RefreshGraphics(pawn);

        ConsumeOne();

        Messages.Message(
            $"{pawn.LabelShortCap} now carries the {Props.packageLabel}.",
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
