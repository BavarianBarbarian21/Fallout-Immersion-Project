using HarmonyLib;
using Verse;

namespace Anthrosonae
{
    [HarmonyPatch(typeof(PawnGenerator), "GeneratePawn", new[] { typeof(PawnGenerationRequest) })]
    public static class PawnGenerator_GeneratePawn_Patch
    {
        public static void Postfix(ref Pawn __result)
        {
            if (__result != null && __result.genes != null)
            {
                var gene = __result.GetFurGene();
                if (gene != null)
                {
                    gene.ApplyColors();
                }
            }
        }
    }
}
