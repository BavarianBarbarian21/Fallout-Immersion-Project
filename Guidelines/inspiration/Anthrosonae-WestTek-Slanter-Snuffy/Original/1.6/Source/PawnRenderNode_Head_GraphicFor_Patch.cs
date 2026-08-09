using HarmonyLib;
using System.Linq;
using Verse;

namespace Anthrosonae
{
    [HarmonyPatch(typeof(PawnRenderNode_Head), "GraphicFor")]
    public static class PawnRenderNode_Head_GraphicFor_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(PawnRenderNode_Head __instance, Pawn pawn, ref Graphic __result)
        {
            if (__result != null && pawn.Drawer.renderer.CurRotDrawMode != RotDrawMode.Dessicated)
            {
                var furGene = pawn.GetFurGene();
                if (furGene != null)
                {
                    __result = furGene.GetGraphicOverriden(__result, __instance, pawn);
                }
            }
        }
    }
}
