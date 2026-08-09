using System.Linq;
using System.Reflection;
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

public static class WestTekFloraGeneUtility
{
    private static readonly FieldInfo SkillRecordPawnField = typeof(SkillRecord).GetField("pawn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

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

    public static void TickRareEffects(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        bool solarPowered = false;
        bool skillDegradation = false;

        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
            if (gene.def == WestTekDefOf.WestTek_Gene_SolarPowered)
            {
                solarPowered = true;
            }

            WestTekFloraGeneExtension extension =
                gene.def.GetModExtension<WestTekFloraGeneExtension>();
            if (extension?.skillDegradation == true)
            {
                skillDegradation = true;
            }

            if (solarPowered && skillDegradation)
            {
                break;
            }
        }

        if (solarPowered
            && WestTekFloraMutationUtility.IsUnderSunlight(pawn)
            && pawn.needs?.food != null)
        {
            pawn.needs.food.CurLevel = Mathf.Min(
                pawn.needs.food.MaxLevel,
                pawn.needs.food.CurLevel + 0.006f
            );
        }

        if (!skillDegradation || pawn.skills == null)
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
