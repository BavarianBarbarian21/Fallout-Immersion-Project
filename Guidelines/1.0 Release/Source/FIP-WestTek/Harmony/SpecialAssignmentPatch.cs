using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;

[HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]
internal static class Patch_PawnGenerator_GenerateGenes
{
    private static void Postfix(Pawn pawn, XenotypeDef xenotype, PawnGenerationRequest request)
    {
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);

        if (WestTekFaunaMutationUtility.IsGeneratedFaunaXenotype(xenotype))
        {
            WestTekFaunaMutationUtility.AssignRandomFurGene(pawn);
        }
    }
}
