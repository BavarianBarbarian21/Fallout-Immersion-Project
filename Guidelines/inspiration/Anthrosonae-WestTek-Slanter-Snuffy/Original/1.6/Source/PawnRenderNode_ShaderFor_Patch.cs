using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.Networking.Types;
using Verse;

namespace Anthrosonae
{
    [HotSwappable]
    [HarmonyPatch(typeof(PawnRenderNode), "ShaderFor")]
    public static class PawnRenderNode_ShaderFor_Patch
    {
        [HarmonyPriority(int.MinValue)]
        public static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Shader __result)
        {
            if (__result != null)
            {
                Log.Message(__result);
                Log.Message(__instance.GetType());
                if (__instance.gene != null)
                {
                    Log.Message(__instance.gene.def.defName);
                    try
                    {
                        Log.Message(__instance.Props.shaderTypeDef.shaderPath);
                    }
                    catch
                    {
                        Log.Message("No shaderPath");
                    }
                }
                var furGene = pawn.GetFurGene(activeCheck: pawn.IsMutant is false);
                if (furGene != null && __instance.gene?.def.endogeneCategory != EndogeneCategory.Headbone && __instance.Props.colorType != PawnRenderNodeProperties.AttachmentColorType.Custom)
                {
                        __result = furGene.GetShaderOverriden(__result, __instance);
                }
                if (furGene == null && __instance.props.shaderTypeDef?.Shader == ShaderTypeDefOf.CutoutComplex.Shader)
                {
                    __result = ShaderTypeDefOf.CutoutComplex.Shader;
                }
            }
        }
    }
}
