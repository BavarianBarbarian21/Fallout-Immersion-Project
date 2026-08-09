using HarmonyLib;
using Verse;

namespace Anthrosonae
{
    [HarmonyPatch(typeof(PawnRenderNode_Fur), "GraphicFor")]
    public static class PawnRenderNode_Fur_GraphicFor_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(PawnRenderNode_Fur __instance, Pawn pawn, ref Graphic __result)
        {
            if (__instance.gene is FurGene furGene && __result != null
                && pawn.Drawer.renderer.CurRotDrawMode != RotDrawMode.Dessicated)
            {
                if (__result.path == pawn.story?.furDef.GetFurBodyGraphicPath(pawn))
                {
                    __result = furGene.GetGraphicOverriden(__result, __instance, pawn);
                }
            }
        }
    }
}
