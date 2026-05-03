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
        XenotypeDef xenotype = pawn.genes.Xenotype;
        if (xenotype == null)
        {
            return false;
        }

        return xenotype.defName is "WestTek_Xenotype_Ghoul"
            or "FCP_Xenotype_Ghoul"
            or "WestTek_Xenotype_Numen"
            or "WestTek_Xenotype_SLanter"
            or "Sanguophage"
            or "WestTek_Xenotype_SuperMutantFirst"
            or "WestTek_Xenotype_SuperMutantSecond";
    }

    public static bool IsSuperMutant(Pawn pawn)
    {
        XenotypeDef xenotype = pawn.genes?.Xenotype;
        return xenotype != null && IsSuperMutantXenotype(xenotype);
    }

    public static bool IsSuperMutantXenotype(XenotypeDef xenotype)
    {
        return xenotype.defName is "WestTek_Xenotype_SuperMutantFirst" or "WestTek_Xenotype_SuperMutantSecond";
    }

    public static bool IsNightkinImplantEligible(Pawn pawn)
    {
        return pawn != null && IsSuperMutant(pawn);
    }

    public static void ApplyNightkinGene(Pawn pawn)
    {
        if (!IsNightkinImplantEligible(pawn) || pawn.genes == null)
        {
            return;
        }

        if (pawn.genes.GenesListForReading.Any(gene => gene.def == WestTekDefOf.WestTek_Gene_Nightkin))
        {
            return;
        }

        foreach (Gene gene in pawn.genes.Xenogenes.Where(gene => gene.def.exclusionTags != null && gene.def.exclusionTags.Contains("SkinColorOverride")).ToList())
        {
            pawn.genes.RemoveGene(gene);
        }

        pawn.genes.AddGene(DefDatabase<GeneDef>.GetNamed("Skin_Purple"), xenogene: true);
        pawn.genes.AddGene(WestTekDefOf.WestTek_Gene_Nightkin, xenogene: true);

        if (pawn.story?.traits != null && pawn.story.traits.GetTrait(WestTekDefOf.WestTek_NightkinSchizophrenia) == null)
        {
            pawn.story.traits.GainTrait(new Trait(WestTekDefOf.WestTek_NightkinSchizophrenia));
        }

        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }

    public static bool IsSuperMutantSlave(Pawn pawn)
    {
        return pawn.IsSlaveOfColony && IsSuperMutant(pawn);
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

[StaticConstructorOnStartup]
internal static class WestTekHarmonyBootstrap
{
    static WestTekHarmonyBootstrap()
    {
        new Harmony("FIP.WestTek").PatchAll();
    }
}

[HarmonyPatch(typeof(SlaveRebellionUtility), nameof(SlaveRebellionUtility.CanParticipateInSlaveRebellion))]
internal static class Patch_SlaveRebellionUtility_CanParticipateInSlaveRebellion
{
    private static void Postfix(Pawn pawn, ref bool __result)
    {
        if (WestTekMutationUtility.IsSuperMutantSlave(pawn))
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(Verse.PawnRenderTree), "ComputeMatrix")]
internal static class Patch_PawnRenderTree_ComputeMatrix
{
    private static void Prefix(Verse.PawnRenderTree __instance, ref UnityEngine.Vector3 scale)
    {
        float renderScale = WestTekMutationUtility.GetRenderScale(__instance?.pawn);
        if (renderScale == 1f)
        {
            return;
        }

        scale *= renderScale;
    }
}

public sealed class ThoughtWorker_WestTek_MastersArmy : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        return WestTekMutationUtility.IsSuperMutantSlave(pawn);
    }
}