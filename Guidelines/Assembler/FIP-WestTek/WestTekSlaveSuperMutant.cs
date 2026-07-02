using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.WestTek;

internal static class WestTekMutationUtility
{
    public const float SlanterRenderScale = 0.5f;
    public const float SuperMutantRenderScale = 1.2f;

    public static bool IsMutationBlockedXenotype(Pawn pawn)
    {
        XenotypeDef xenotype = pawn?.genes?.Xenotype;
        if (xenotype == null)
        {
            return false;
        }

        return xenotype.defName is
            "WestTek_Xenotype_Ghoul"
            or "FCP_Xenotype_Ghoul"
            or "WestTek_Xenotype_Numen"
            or "WestTek_Xenotype_SLanter"
            or "Highmate"
            or "WestTek_Xenotype_SporeCarrier"
            or "WestTek_Xenotype_SuperMutantFirst"
            or "WestTek_Xenotype_SuperMutantSecond"
            or "Sanguophage";
    }

    public static bool IsSuperMutant(Pawn pawn)
    {
        XenotypeDef xenotype = pawn?.genes?.Xenotype;
        return xenotype != null && IsSuperMutantXenotype(xenotype);
    }

    public static bool IsSuperMutantXenotype(XenotypeDef xenotype)
    {
        return xenotype?.defName is
            "WestTek_Xenotype_SuperMutantFirst"
            or "WestTek_Xenotype_SuperMutantSecond";
    }

    public static bool HasNightkinGene(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def == WestTekDefOf.WestTek_Gene_Nightkin);
    }

    public static bool IsNightkinImplantEligible(Pawn pawn)
    {
        return pawn != null
            && IsSuperMutant(pawn)
            && !HasNightkinGene(pawn);
    }

    public static void ApplyNightkinGene(Pawn pawn)
    {
        if (!IsNightkinImplantEligible(pawn) || pawn.genes == null)
        {
            return;
        }

        foreach (Gene gene in pawn.genes.Xenogenes
            .Where(gene => gene.def.exclusionTags != null && gene.def.exclusionTags.Contains("SkinColorOverride"))
            .ToList())
        {
            pawn.genes.RemoveGene(gene);
        }

        pawn.genes.AddGene(WestTekDefOf.WestTek_Gene_Nightkin, xenogene: false);

        if (pawn.story?.traits != null && pawn.story.traits.GetTrait(WestTekDefOf.WestTek_NightkinSchizophrenia) == null)
        {
            pawn.story.traits.GainTrait(new Trait(WestTekDefOf.WestTek_NightkinSchizophrenia));
        }

        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }

    public static bool IsSuperMutantSlave(Pawn pawn)
    {
        return pawn != null && pawn.IsSlaveOfColony && IsSuperMutant(pawn);
    }

    public static float GetRenderScale(Pawn pawn)
    {
        XenotypeDef xenotype = pawn?.genes?.Xenotype;
        if (xenotype == null)
        {
            return 1f;
        }

        if (xenotype.defName == "WestTek_Xenotype_SLanter")
        {
            return SlanterRenderScale;
        }

        return IsSuperMutantXenotype(xenotype) ? SuperMutantRenderScale : 1f;
    }
}