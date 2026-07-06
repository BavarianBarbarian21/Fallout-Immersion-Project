using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public enum WestTekFloraImplantCategory
{
    Senses,
    Motorics,
    Organs
}

public sealed class WestTekFloraImplantExtension : DefModExtension
{
    public WestTekFloraImplantCategory category;
}

internal static class WestTekFloraMutationUtility
{
    private const int OvergrownImplantThreshold = 5;

    public static bool IsEligibleForFloraMutation(Pawn pawn)
    {
        if (pawn == null || pawn.genes == null)
        {
            return false;
        }

        if (!pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh)
        {
            return false;
        }

        if (WestTekMutationUtility.IsMutationBlockedXenotype(pawn))
        {
            return false;
        }

        XenotypeDef xenotype = pawn.genes.Xenotype;
        if (xenotype?.defName is "WestTek_Xenotype_Overgrown")
        {
            return false;
        }

        return true;
    }

    public static bool HasGene(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes == null || geneDef == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def == geneDef);
    }

    public static void AddEndogeneIfMissing(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes == null || geneDef == null)
        {
            return;
        }

        if (HasGene(pawn, geneDef))
        {
            return;
        }

        pawn.genes.AddGene(geneDef, xenogene: false);
    }

    public static List<WestTekFloraImplantCategory> GetFloraImplantCategories(Pawn pawn)
    {
        List<WestTekFloraImplantCategory> result = new();

        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return result;
        }

        foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
        {
            WestTekFloraImplantExtension extension = hediff.def.GetModExtension<WestTekFloraImplantExtension>();
            if (extension == null)
            {
                continue;
            }

            result.Add(extension.category);
        }

        return result;
    }

    public static int CountFloraImplants(Pawn pawn)
    {
        return GetFloraImplantCategories(pawn).Count;
    }

    public static GeneDef GeneForCategory(WestTekFloraImplantCategory category)
    {
        return category switch
        {
            WestTekFloraImplantCategory.Senses => WestTekDefOf.WestTek_FEVSymbioticSenses,
            WestTekFloraImplantCategory.Motorics => WestTekDefOf.WestTek_FEVSymbioticMotorics,
            WestTekFloraImplantCategory.Organs => WestTekDefOf.WestTek_FEVSymbioticOrgans,
            _ => null
        };
    }

    public static void ApplyRandomSymbiosisGeneFromImplants(Pawn pawn)
    {
        List<WestTekFloraImplantCategory> categories = GetFloraImplantCategories(pawn)
            .Distinct()
            .ToList();

        if (categories.Count == 0)
        {
            return;
        }

        WestTekFloraImplantCategory chosenCategory = categories.RandomElement();
        GeneDef geneDef = GeneForCategory(chosenCategory);

        AddEndogeneIfMissing(pawn, geneDef);
    }

    public static void ApplyRefinedFloraMutation(Pawn pawn)
    {
        if (!IsEligibleForFloraMutation(pawn))
        {
            return;
        }

        int implantCount = CountFloraImplants(pawn);

        XenotypeDef targetXenotype = implantCount >= OvergrownImplantThreshold
            ? WestTekDefOf.WestTek_Xenotype_Overgrown
            : WestTekDefOf.WestTek_Xenotype_Numen;

        pawn.genes.SetXenotype(targetXenotype);

        ApplyRandomSymbiosisGeneFromImplants(pawn);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);

        RefreshGraphics(pawn);
    }

    public static void ApplyExperimentalFloraMutation(Pawn pawn)
    {
        if (!IsEligibleForFloraMutation(pawn))
        {
            return;
        }

        pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_SporeCarrier);

        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SporeCarrier);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);

        RefreshGraphics(pawn);
    }

    public static bool IsUnderSunlight(Pawn pawn)
    {
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        if (pawn.Position.Roofed(pawn.Map))
        {
            return false;
        }

        return pawn.Map.skyManager.CurSkyGlow > 0.35f;
    }

    public static bool IsOutside(Pawn pawn)
    {
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        return !pawn.Position.Roofed(pawn.Map);
    }

    public static bool IsNearGauranlenTree(Pawn pawn, float radius = 12f)
    {
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return false;
        }

        foreach (Thing thing in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, radius, true))
        {
            if (thing.def?.defName == "Plant_TreeGauranlen")
            {
                return true;
            }
        }

        return false;
    }

    public static void RefreshGraphics(Pawn pawn)
    {
        pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

        if (pawn != null)
        {
            PortraitsCache.SetDirty(pawn);
        }
    }
}