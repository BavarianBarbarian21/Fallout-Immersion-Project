using HarmonyLib;
using Ionic.Zlib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using LudeonTK;

namespace VIEAT
{
    [DefOf]
    public static class VIEAT_DefOf
    {
        public static PreceptDef TreeConnection;
        public static PreceptDef AnimaTreeLinking;
    }

    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        public static Harmony harmonyInstance;

        [TweakValue("0", 0, 255)] public static int colorR = 143;
        [TweakValue("0", 0, 255)] public static int colorG = 171;
        [TweakValue("0", 0, 255)] public static int colorB = 156;
        [TweakValue("0", 0, 255)] public static int colorA = 255;

        public static readonly Color Green = new ColorInt(143, 171, 156, 255).ToColor;

        public static readonly string LineTexPath = "UI/Overlays/ThingLine";

        public static readonly Material LineMatGreen;

        static HarmonyInit()
        {
            LineMatGreen = MaterialPool.MatFrom(LineTexPath, ShaderDatabase.Transparent, Green);
            harmonyInstance = new Harmony("VIEAT.Mod");
            harmonyInstance.PatchAll();
            AccessTools.Field(typeof(Gizmo_PruningConfig), "StrengthTex").SetValue(null, SolidColorMaterials.NewSolidColorTexture(Green));
            AccessTools.Field(typeof(Gizmo_PruningConfig), "StrengthHighlightTex").SetValue(null, SolidColorMaterials.NewSolidColorTexture(new ColorInt(173, 208, 195).ToColor));
            AccessTools.Field(typeof(Gizmo_PruningConfig), "StrengthTargetTex").SetValue(null, SolidColorMaterials.NewSolidColorTexture(new ColorInt(102, 119, 102).ToColor));
        }
    }

    [HarmonyPatch(typeof(Pawn_ConnectionsTracker), "DrawConnectionLine")]
    public static class DrawConnectionLine_Patch
    {
        public static bool Prefix(Pawn ___pawn, Thing t)
        {
            if (t.Spawned && t.Map == ___pawn.Map)
            {
                GenDraw.DrawLineBetween(___pawn.TrueCenter(), t.TrueCenter(), HarmonyInit.LineMatGreen, 0.2f);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Command_Ritual), MethodType.Constructor, new Type[] {
        typeof(Precept_Ritual), typeof(TargetInfo), typeof(RitualObligation), typeof(Dictionary<string, Pawn>)})]
    public static class Command_Ritual_Patch
    {
        public static void Postfix(Command_Ritual __instance)
        {
            if (__instance.ritual.def == VIEAT_DefOf.TreeConnection && !__instance.disabled)
{
                __instance.defaultIconColor = HarmonyInit.Green;
            }
        }
    }
}
