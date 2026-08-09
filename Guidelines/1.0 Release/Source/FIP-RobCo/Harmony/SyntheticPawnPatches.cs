using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.RobCo;

[HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
internal static class PawnRenderNodeBody_SyntheticGraphicPatch
{
    private static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (SyntheticPawnUtility.IsThinkTank(pawn))
        {
            __result = SyntheticPawnUtility.InvisibleGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen1Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen1BodyGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen2Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen2BodyGraphic(__result);
        }
    }
}

[HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
internal static class PawnRenderNodeHead_SyntheticGraphicPatch
{
    private static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (SyntheticPawnUtility.IsThinkTank(pawn))
        {
            __result = SyntheticPawnUtility.ThinkTankGraphic(pawn, __result);
        }
        else if (SyntheticPawnUtility.IsGen1Synth(pawn))
        {
            __result = SyntheticPawnUtility.InvisibleGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen2Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen2HeadGraphic(__result);
        }
    }
}

[HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
internal static class RecipeSurgery_ThinkTankImplantPatch
{
    private static void Postfix(Recipe_Surgery __instance, Thing thing, ref bool __result)
    {
        if (!__result || thing is not Pawn pawn || !SyntheticPawnUtility.IsThinkTank(pawn))
        {
            return;
        }

        Type workerClass = __instance.recipe?.workerClass;
        bool installsAddedPart = __instance.recipe?.addsHediff?.countsAsAddedPartOrImplant == true;
        bool isInstallWorker = workerClass != null
            && (typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(workerClass)
                || typeof(Recipe_InstallImplant).IsAssignableFrom(workerClass));

        if (installsAddedPart || isInstallWorker)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear))]
internal static class ApparelUtility_ThinkTankPatch
{
	private static void Postfix(Pawn p, ref bool __result)
	{
		if (__result && SyntheticPawnUtility.IsThinkTank(p))
        {
            __result = false;
        }
    }
}
