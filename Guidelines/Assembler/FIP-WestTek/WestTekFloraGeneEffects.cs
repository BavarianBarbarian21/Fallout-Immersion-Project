using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.WestTek;

public sealed class WestTekFloraGeneExtension : DefModExtension
{
    public float skillLossMultiplier = 1f;
    public bool skillDegradation;
}

public sealed class Gene_WestTekSolarPowered : Gene
{
}

internal static class WestTekFloraGeneUtility
{
    private static readonly FieldInfo SkillRecordPawnField = AccessTools.Field(typeof(SkillRecord), "pawn");

    public static bool HasGene(Pawn pawn, string defName)
    {
        if (pawn?.genes == null || defName == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def?.defName == defName);
    }

    public static bool HasGene(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes == null || geneDef == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def == geneDef);
    }

    public static Pawn GetPawnFromSkillRecord(SkillRecord record)
    {
        return SkillRecordPawnField?.GetValue(record) as Pawn;
    }

    public static float GetSkillLossMultiplier(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return 1f;
        }

        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
            WestTekFloraGeneExtension extension = gene.def.GetModExtension<WestTekFloraGeneExtension>();
            if (extension != null && extension.skillLossMultiplier != 1f)
            {
                return extension.skillLossMultiplier;
            }
        }

        return 1f;
    }

    public static bool HasSkillDegradationGene(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
            WestTekFloraGeneExtension extension = gene.def.GetModExtension<WestTekFloraGeneExtension>();
            if (extension?.skillDegradation == true)
            {
                return true;
            }
        }

        return false;
    }

    public static void TickSolarPowered(Pawn pawn)
    {
        if (!HasGene(pawn, WestTekDefOf.WestTek_Gene_SolarPowered))
        {
            return;
        }

        if (!WestTekFloraMutationUtility.IsUnderSunlight(pawn))
        {
            return;
        }

        if (pawn.needs?.food == null)
        {
            return;
        }

        pawn.needs.food.CurLevel = Mathf.Min(
            pawn.needs.food.MaxLevel,
            pawn.needs.food.CurLevel + 0.006f
        );
    }

    public static void TickSkillDegradation(Pawn pawn)
    {
        if (!HasSkillDegradationGene(pawn))
        {
            return;
        }

        if (pawn.skills == null)
        {
            return;
        }

        foreach (SkillRecord skill in pawn.skills.skills)
        {
            if (skill.TotallyDisabled)
            {
                continue;
            }

            if (skill.Level >= 10)
            {
                continue;
            }

            skill.xpSinceLastLevel = Mathf.Max(0f, skill.xpSinceLastLevel - 0.05f);
        }
    }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]
internal static class Patch_Pawn_TickRare_FloraGenes
{
    private static void Postfix(Pawn __instance)
    {
        if (__instance == null || !__instance.RaceProps.Humanlike)
        {
            return;
        }

        WestTekFloraGeneUtility.TickSolarPowered(__instance);
        WestTekFloraGeneUtility.TickSkillDegradation(__instance);
    }
}
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
internal static class Patch_Pawn_Kill_SporeCarrierFertility
{
    private static void Prefix(Pawn __instance, out Map __stateMap, out IntVec3 __statePosition, out bool __stateShouldFertilize)
    {
        __stateMap = __instance?.Map;
        __statePosition = __instance?.Position ?? IntVec3.Invalid;
        __stateShouldFertilize = WestTekSporeCarrierDeathUtility.ShouldFertilizeOnDeath(__instance);
    }

    private static void Postfix(Map __stateMap, IntVec3 __statePosition, bool __stateShouldFertilize)
    {
        if (!__stateShouldFertilize)
        {
            return;
        }

        if (__stateMap == null || !__statePosition.IsValid)
        {
            return;
        }

        WestTekSporeCarrierDeathUtility.FertilizeDeathArea(__stateMap, __statePosition);
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