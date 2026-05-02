using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;

internal static class WestTekSpecialUtility
{
    private static readonly string[] GroupPrefixes =
    {
        "WestTek_Strength_",
        "WestTek_Perception_",
        "WestTek_Endurance_",
        "WestTek_Charisma_",
        "WestTek_Intelligence_",
        "WestTek_Agility_",
        "WestTek_Luck_"
    };

    public static bool HasAnySpecialGene(Pawn pawn)
    {
        if (pawn.genes == null)
        {
            return false;
        }

        foreach (Gene gene in pawn.genes.GenesListForReading)
        {
            if (gene?.def != null && IsSpecialGene(gene.def))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ShouldAssignGeneratedSpecials(Pawn pawn)
    {
        if (pawn == null || pawn.genes == null || !pawn.RaceProps.Humanlike)
        {
            return false;
        }

        if (HasAnySpecialGene(pawn))
        {
            return false;
        }

        // Leave newborns alone so childbirth keeps vanilla inherited genes.
        if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalTicks == 0)
        {
            return false;
        }

        return true;
    }

    public static void AssignGeneratedSpecials(Pawn pawn)
    {
        if (!ShouldAssignGeneratedSpecials(pawn))
        {
            return;
        }

        foreach (string prefix in GroupPrefixes)
        {
            List<GeneDef> options = GetSpecialGroup(prefix);
            if (options.Count == 0)
            {
                continue;
            }

            GeneDef chosen = options.RandomElementByWeight(gene => gene.selectionWeight);
            pawn.genes.AddGene(chosen, xenogene: false);
        }
    }

    private static List<GeneDef> GetSpecialGroup(string prefix)
    {
        return DefDatabase<GeneDef>.AllDefsListForReading
            .Where(gene => gene.defName != null && gene.defName.StartsWith(prefix))
            .OrderBy(gene => gene.defName)
            .ToList();
    }

    private static bool IsSpecialGene(GeneDef gene)
    {
        string defName = gene.defName;
        if (defName == null)
        {
            return false;
        }

        foreach (string prefix in GroupPrefixes)
        {
            if (defName.StartsWith(prefix))
            {
                return true;
            }
        }

        return false;
    }
}

[HarmonyPatch(typeof(PawnGenerator), "GenerateGenes")]
internal static class Patch_PawnGenerator_GenerateGenes
{
    private static void Postfix(Pawn pawn, XenotypeDef xenotype, PawnGenerationRequest request)
    {
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
    }
}