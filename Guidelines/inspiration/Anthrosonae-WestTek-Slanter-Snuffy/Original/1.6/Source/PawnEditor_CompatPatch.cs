using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Anthrosonae
{
    [HotSwappable]
    [HarmonyPatch]
    public static class PawnEditor_CompatPatch
    {
        public static bool Prepare() => ModsConfig.IsActive("ISOREX.PawnEditor");
        public static Type windowType;
        public static FieldInfo mainTabField;
        public static MethodBase TargetMethod()
        {
            windowType = AccessTools.TypeByName("PawnEditor.Dialog_AppearanceEditor");
            mainTabField = AccessTools.Field(windowType, "mainTab");
            return AccessTools.Method(windowType, "DoWindowContents");
        }

        public static void Postfix(Window __instance, Pawn ___pawn, Rect inRect)
        {
            if ((int)mainTabField.GetValue(__instance) == 0)
            {
                var gene = ___pawn.GetFurGene();
                if (gene != null)
                {
                    var button = new Rect(inRect.xMax - 125, inRect.y - 36, 120, 32);
                    if (Widgets.ButtonText(button, "ColorPicker.ChangeFur".Translate()))
                    {
                        Find.WindowStack.Add(new Window_ColorPicker(gene));
                    }
                }
            }
        }
    }
}
