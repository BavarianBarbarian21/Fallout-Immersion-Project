using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class Gene_WestTekSuperMutantAppearance : Gene
{
    public override void PostMake()
    {
        base.PostMake();
        ApplySuperMutantBodyType();
    }

    public override void PostAdd()
    {
        base.PostAdd();
        ApplySuperMutantBodyType();
    }

    public override void Tick()
    {
        base.Tick();
        ApplySuperMutantBodyType();
    }

    private void ApplySuperMutantBodyType()
    {
        if (!Active
            || pawn?.story == null
            || pawn.story.bodyType == BodyTypeDefOf.Hulk)
        {
            return;
        }

        // Keep the vanilla Hulk body type so apparel can use its normal Hulk
        // graphics. The custom naked body is supplied by the render patch below.
        pawn.story.bodyType = BodyTypeDefOf.Hulk;
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }
}

[HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
internal static class PawnRenderNodeBody_SuperMutantGraphicPatch
{
    private const string SuperMutantBodyPath =
        "Things/Pawn/Humanlike/Bodies/WestTek/SuperMutant/WestTek_Naked_Hulk";

    private static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (__result == null || !HasActiveSuperMutantAppearance(pawn))
        {
            return;
        }

        __result = GraphicDatabase.Get<Graphic_Multi>(
            SuperMutantBodyPath,
            __result.Shader,
            __result.drawSize,
            __result.Color,
            __result.ColorTwo);
    }

    private static bool HasActiveSuperMutantAppearance(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
            if (gene.Active && gene.def.defName == "WestTek_Gene_SuperMutant")
            {
                return true;
            }
        }

        return false;
    }
}
