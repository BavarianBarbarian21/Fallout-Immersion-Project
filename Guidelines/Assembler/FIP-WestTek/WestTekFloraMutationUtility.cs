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
    private const int NumenRequiredImplantCount = 1;
    private const int OvergrownImplantThreshold = 3;
    public const int PlantRegrowthDelayTicks = 60000 * 7;

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

    public static bool IsEligibleForNumenMutation(Pawn pawn)
    {
        return IsHumanFloraCandidate(pawn)
            && IsAllowedHumanXenotype(pawn.genes.Xenotype)
            && CountFloraImplants(pawn) == NumenRequiredImplantCount;
    }

    public static bool IsEligibleForSporeCarrierMutation(Pawn pawn)
    {
        return IsHumanFloraCandidate(pawn)
            && IsAllowedHumanXenotype(pawn.genes.Xenotype)
            && !IsBlockedFloraXenotype(pawn.genes.Xenotype);
    }

    public static bool IsEligibleForOvergrownMutation(Pawn pawn)
    {
        if (!IsHumanFloraCandidate(pawn))
        {
            return false;
        }

        XenotypeDef xenotype = pawn.genes.Xenotype;
        if (xenotype == WestTekDefOf.WestTek_Xenotype_Overgrown)
        {
            return false;
        }

        return IsAllowedHumanXenotype(xenotype)
            || xenotype == WestTekDefOf.WestTek_Xenotype_Numen
            || xenotype == WestTekDefOf.WestTek_Xenotype_SporeCarrier;
    }

    private static bool IsHumanFloraCandidate(Pawn pawn)
    {
        return pawn?.genes != null
            && pawn.RaceProps.Humanlike
            && pawn.RaceProps.IsFlesh;
    }

    private static bool IsAllowedHumanXenotype(XenotypeDef xenotype)
    {
        if (xenotype == null)
        {
            return false;
        }

        return xenotype.defName is
            "Baseliner"
            or "WestTek_Xenotype_PureHumans"
            or "WestTek_Xenotype_VaultDweller"
            or "WestTek_Xenotype_Atlantic"
            or "WestTek_Xenotype_Cascadia"
            or "WestTek_Xenotype_NatL"
            or "WestTek_Xenotype_NuevoTexico"
            or "WestTek_Xenotype_TwoCalifornia";
    }

    private static bool IsBlockedFloraXenotype(XenotypeDef xenotype)
    {
        if (xenotype == null)
        {
            return false;
        }

        return xenotype.defName is
            "WestTek_Xenotype_Ghoul"
            or "FCP_Xenotype_Ghoul"
            or "WestTek_Xenotype_Numen"
            or "WestTek_Xenotype_Overgrown"
            or "WestTek_Xenotype_SLanter"
            or "WestTek_Xenotype_SNuffy"
            or "WestTek_Xenotype_Skinwalker"
            or "WestTek_Xenotype_SporeCarrier"
            or "WestTek_Xenotype_SuperMutantFirst"
            or "WestTek_Xenotype_SuperMutantSecond"
            or "Sanguophage"
            or "Highmate";
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

    public static Hediff GetFirstFloraImplant(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return null;
        }

        return pawn.health.hediffSet.hediffs
            .FirstOrDefault(hediff => hediff.def.GetModExtension<WestTekFloraImplantExtension>() != null);
    }

    public static bool HasEnoughFloraImplantsForOvergrown(Pawn pawn)
    {
        return CountFloraImplants(pawn) >= OvergrownImplantThreshold;
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

    public static void ApplyNumenMutation(Pawn pawn)
    {
        if (!IsEligibleForNumenMutation(pawn))
        {
            return;
        }

        pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_Numen);
        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_SaplingGrowth);
        AddNumenAbilityGeneFromImplant(pawn);
        ApplyRandomSymbiosisGeneFromImplants(pawn);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);

        RefreshGraphics(pawn);
    }

    public static void ApplySporeCarrierMutation(Pawn pawn)
    {
        if (!IsEligibleForSporeCarrierMutation(pawn))
        {
            return;
        }

        pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_SporeCarrier);

        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_SaplingGrowth);
        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SporeCarrier);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);

        RefreshGraphics(pawn);
    }

    public static bool TryApplyOvergrownMutation(Pawn pawn)
    {
        if (!IsEligibleForOvergrownMutation(pawn) || !HasEnoughFloraImplantsForOvergrown(pawn))
        {
            return false;
        }

        pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_Overgrown);
        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_SaplingGrowth);
        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SporeCarrier);
        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_Heterotroph);
        ApplyRandomSymbiosisGeneFromImplants(pawn);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
        RefreshGraphics(pawn);

        return true;
    }

    private static void AddNumenAbilityGeneFromImplant(Pawn pawn)
    {
        Hediff implant = GetFirstFloraImplant(pawn);
        string partDefName = implant?.Part?.def?.defName;
        if (partDefName.NullOrEmpty())
        {
            return;
        }

        GeneDef geneDef = DefDatabase<GeneDef>.GetNamedSilentFail($"WestTek_NumenAbility_{partDefName}");
        AddEndogeneIfMissing(pawn, geneDef);
    }

    public static List<HediffDef> GetRegrowthOptionsForPart(BodyPartDef bodyPartDef)
    {
        if (bodyPartDef == null)
        {
            return new List<HediffDef>();
        }

        return DefDatabase<HediffDef>.AllDefsListForReading
            .Where(def =>
            {
                WestTekPlantRegrowthExtension extension = def.GetModExtension<WestTekPlantRegrowthExtension>();
                return extension?.appliesToParts != null && extension.appliesToParts.Contains(bodyPartDef);
            })
            .ToList();
    }

    public static bool TryRegrowSymbiotePart(Pawn pawn, BodyPartRecord part)
    {
        if (pawn?.health == null || part?.def == null)
        {
            return false;
        }

        List<HediffDef> options = GetRegrowthOptionsForPart(part.def);
        if (options.Count == 0)
        {
            return false;
        }

        Hediff missingPart = pawn.health.hediffSet.hediffs
            .FirstOrDefault(hediff => hediff is Hediff_MissingPart && hediff.Part == part);

        if (missingPart != null)
        {
            pawn.health.RemoveHediff(missingPart);
        }

        HediffDef chosenHediff = options.RandomElement();
        Hediff symbiote = HediffMaker.MakeHediff(chosenHediff, pawn, part);
        pawn.health.AddHediff(symbiote, part);

        TryApplyOvergrownMutation(pawn);
        return true;
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
