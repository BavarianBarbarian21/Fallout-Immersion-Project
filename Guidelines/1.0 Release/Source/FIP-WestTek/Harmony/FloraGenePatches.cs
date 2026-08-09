using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.WestTek;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]
internal static class Patch_Pawn_TickRare_FloraGenes
{
    private static void Postfix(Pawn __instance)
    {
        if (__instance == null || !__instance.RaceProps.Humanlike)
        {
            return;
        }

        WestTekFloraGeneUtility.TickRareEffects(__instance);
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
internal static class Patch_Pawn_Kill_SporeCarrierFertility
{
    private readonly struct KillState
    {
        public KillState(Map map, IntVec3 position, bool shouldFertilize)
        {
            Map = map;
            Position = position;
            ShouldFertilize = shouldFertilize;
        }

        public Map Map { get; }
        public IntVec3 Position { get; }
        public bool ShouldFertilize { get; }
    }

    private static void Prefix(Pawn __instance, out KillState __state)
    {
        __state = new KillState(
            __instance?.Map,
            __instance?.Position ?? IntVec3.Invalid,
            WestTekSporeCarrierDeathUtility.ShouldFertilizeOnDeath(__instance)
        );
    }

    private static void Postfix(KillState __state)
    {
        if (!__state.ShouldFertilize)
        {
            return;
        }

        if (__state.Map == null || !__state.Position.IsValid)
        {
            return;
        }

        WestTekSporeCarrierDeathUtility.FertilizeDeathArea(__state.Map, __state.Position);
    }
}
[HarmonyPatch(typeof(SkillRecord), "Interval")]
internal static class Patch_SkillRecord_Interval_FloraSkillLoss
{
    private static void Prefix(SkillRecord __instance, out float __state)
    {
        __state = __instance.xpSinceLastLevel;
    }

    private static void Postfix(SkillRecord __instance, float __state)
    {
        Pawn pawn = WestTekFloraGeneUtility.GetPawnFromSkillRecord(__instance);
        if (pawn == null)
        {
            return;
        }

        float multiplier = WestTekFloraGeneUtility.GetSkillLossMultiplier(pawn);
        if (multiplier == 1f)
        {
            return;
        }

        float after = __instance.xpSinceLastLevel;
        if (after >= __state)
        {
            return;
        }

        float originalLoss = __state - after;
        float modifiedLoss = originalLoss * multiplier;

        __instance.xpSinceLastLevel = Mathf.Max(0f, __state - modifiedLoss);
    }
}
