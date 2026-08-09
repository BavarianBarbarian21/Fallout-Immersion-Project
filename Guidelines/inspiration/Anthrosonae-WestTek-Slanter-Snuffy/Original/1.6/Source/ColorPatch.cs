using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using VFECore;
using static HarmonyLib.Code;

[HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.ColorFor))]
public static class PawnRenderNode_ColorForPatch
{
    public static bool Postfix(Pawn pawn, ref PawnRenderNodeProperties __props, ref Color __result)
    {
        if (__props.colorType == PawnRenderNodeProperties.AttachmentColorType.Custom)
        {
            Log.Message("Custom colorType found!!!!!!!!!!");
            Color color = pawn.story.skinColorOverride ?? pawn.story.HairColor;
            color *= __props.colorRGBPostFactor;
            if (__props.useRottenColor && pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Rotting)
            {
                color = PawnRenderUtility.GetRottenColor(color);
            }
            __result = color;
        }
        return false;
    }
}