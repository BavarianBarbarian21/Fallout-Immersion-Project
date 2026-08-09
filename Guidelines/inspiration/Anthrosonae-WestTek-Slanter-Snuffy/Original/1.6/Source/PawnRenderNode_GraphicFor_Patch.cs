using HarmonyLib;
using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;

namespace Anthrosonae
{
    [HarmonyPatch(typeof(PawnRenderNode), "GraphicFor")]
    public static class PawnRenderNode_GraphicFor_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Graphic __result)
        {
            if (__result != null && __instance.Props.colorType != PawnRenderNodeProperties.AttachmentColorType.Custom)
            {
                var furGene = pawn.GetFurGene(activeCheck: pawn.IsMutant is false);
                if (furGene != null)
                {
                        __result = furGene.GetGraphicOverriden(__result, __instance, pawn);
                }
                if (furGene == null && __instance.gene != null)
                    if(__instance.gene.def.defName.StartsWith("ATK"))
                    {
                        __result = FurGene.GetGraphicOverridenNoFurgene(__result, __instance, pawn);
                    }
            }
        }
    }
}
//(__instance.gene is null || (__instance.gene.def.endogeneCategory != EndogeneCategory.Headbone && __instance.gene.def.defName.StartsWith("ATK")))
