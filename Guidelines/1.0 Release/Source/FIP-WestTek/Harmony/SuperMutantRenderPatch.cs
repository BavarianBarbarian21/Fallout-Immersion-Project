using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;

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
