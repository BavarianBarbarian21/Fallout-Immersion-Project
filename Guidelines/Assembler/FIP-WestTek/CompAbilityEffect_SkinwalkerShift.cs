using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_AbilitySkinwalkerShift : CompProperties_AbilityEffect
{
    public CompProperties_AbilitySkinwalkerShift()
    {
        compClass = typeof(CompAbilityEffect_SkinwalkerShift);
    }
}

public sealed class CompAbilityEffect_SkinwalkerShift : CompAbilityEffect
{
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);

        Pawn pawn = parent.pawn;
        if (pawn == null)
        {
            return;
        }

        if (!WestTekFaunaMutationUtility.HasSkinwalkerMutation(pawn))
        {
            Messages.Message("This pawn does not carry the Skinwalker mutation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        if (!WestTekFaunaMutationUtility.IsSLanter(pawn) && !WestTekFaunaMutationUtility.IsSkinwalker(pawn))
        {
            Messages.Message("Only S'Lanter-derived Skinwalkers can use this transformation.", MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        bool wasSkinwalker = WestTekFaunaMutationUtility.IsSkinwalker(pawn);

        WestTekFaunaMutationUtility.ToggleSkinwalkerForm(pawn);

        if (wasSkinwalker)
        {
            Messages.Message($"{pawn.LabelShortCap} returns to S'Lanter form.", pawn, MessageTypeDefOf.PositiveEvent);
        }
        else
        {
            Messages.Message($"{pawn.LabelShortCap} shifts into Skinwalker form.", pawn, MessageTypeDefOf.PositiveEvent);
        }
    }

    public override bool GizmoDisabled(out string reason)
    {
        Pawn pawn = parent.pawn;

        if (pawn == null)
        {
            reason = "No pawn.";
            return true;
        }

        if (!WestTekFaunaMutationUtility.HasSkinwalkerMutation(pawn))
        {
            reason = "This pawn does not carry the Skinwalker mutation.";
            return true;
        }

        if (!WestTekFaunaMutationUtility.IsSLanter(pawn) && !WestTekFaunaMutationUtility.IsSkinwalker(pawn))
        {
            reason = "Only S'Lanter-derived Skinwalkers can use this transformation.";
            return true;
        }

        reason = null;
        return false;
    }
}