using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VIESAS
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        public static Harmony harmonyInstance;
        static HarmonyInit()
        {
            harmonyInstance = new Harmony("VIESAS.Mod");
            harmonyInstance.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Pawn_IdeoTracker), "SetIdeo")]
    public static class SetIdeo_Patch
    {
        public static void Postfix(Pawn ___pawn)
        {
            if (___pawn.IsColonist)
            {
                IdeologyTracker.Instance.RecheckIdeos();
            }
        }
    }
}