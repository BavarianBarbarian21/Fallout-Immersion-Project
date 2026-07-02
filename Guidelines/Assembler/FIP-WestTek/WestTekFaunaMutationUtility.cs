using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.WestTek;

internal static class WestTekFaunaMutationUtility
{
    private const int SlanterAgeYears = 18;

    private static IEnumerable<GeneDef> FurGenes
    {
        get
        {
            yield return WestTekDefOf.WestTek_White;
            yield return WestTekDefOf.WestTek_Gray;
            yield return WestTekDefOf.WestTek_Brown;
            yield return WestTekDefOf.WestTek_Purple;
            yield return WestTekDefOf.WestTek_Black;
            yield return WestTekDefOf.WestTek_Blonde;
            yield return WestTekDefOf.WestTek_Teal;
        }
    }

    private static IEnumerable<GeneDef> FaunaPackageGenes
    {
        get
        {
            yield return WestTekDefOf.WestTek_Gene_BAja;
            yield return WestTekDefOf.WestTek_Gene_MErowi;
            yield return WestTekDefOf.WestTek_Gene_RUffian;
            yield return WestTekDefOf.WestTek_Gene_SNuffy;
        }
    }

    public static bool IsRaccoon(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        return pawn.def?.defName == "Raccoon" || pawn.kindDef?.defName == "Raccoon";
    }

    public static bool IsSLanter(Pawn pawn)
    {
        return pawn?.genes?.Xenotype == WestTekDefOf.WestTek_Xenotype_SLanter;
    }

    public static bool IsSkinwalker(Pawn pawn)
    {
        return pawn?.genes?.Xenotype == WestTekDefOf.WestTek_Xenotype_Skinwalker;
    }

    public static bool IsHighmate(Pawn pawn)
    {
        return pawn?.genes?.Xenotype == WestTekDefOf.Highmate;
    }

    public static bool HasGene(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes == null || geneDef == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def == geneDef);
    }

    public static bool HasSkinwalkerMutation(Pawn pawn)
    {
        return HasGene(pawn, WestTekDefOf.WestTek_Gene_SkinwalkerMutation);
    }

    public static bool HasAnyFurGene(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (GeneDef furGene in FurGenes)
        {
            if (furGene != null && HasGene(pawn, furGene))
            {
                return true;
            }
        }

        return false;
    }

    public static bool HasAnyFaunaPackageGene(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (GeneDef packageGene in FaunaPackageGenes)
        {
            if (packageGene != null && HasGene(pawn, packageGene))
            {
                return true;
            }
        }

        return false;
    }

    public static void AssignRandomFurGene(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        if (HasAnyFurGene(pawn))
        {
            return;
        }

        List<GeneDef> options = FurGenes.Where(gene => gene != null).ToList();
        if (options.Count == 0)
        {
            return;
        }

        GeneDef chosen = options.RandomElement();
        pawn.genes.AddGene(chosen, xenogene: false);
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

    public static void AddEndogenesIfMissing(Pawn pawn, IEnumerable<GeneDef> genes)
    {
        if (pawn?.genes == null || genes == null)
        {
            return;
        }

        foreach (GeneDef geneDef in genes)
        {
            AddEndogeneIfMissing(pawn, geneDef);
        }
    }

    public static void SetExactBiologicalAndChronologicalAge(Pawn pawn, int years)
    {
        if (pawn?.ageTracker == null)
        {
            return;
        }

        long ticks = years * GenDate.TicksPerYear;

        pawn.ageTracker.AgeBiologicalTicks = ticks;
        pawn.ageTracker.BirthAbsTicks = GenTicks.TicksAbs - ticks;
    }

    public static Pawn TransformRaccoonIntoSLanter(Pawn raccoon)
    {
        if (raccoon == null || raccoon.MapHeld == null)
        {
            return null;
        }

        Map map = raccoon.MapHeld;
        IntVec3 position = raccoon.PositionHeld;
        Gender gender = raccoon.gender;

        Pawn slanter = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);

        if (gender == Gender.Male || gender == Gender.Female)
        {
            slanter.gender = gender;
        }

        slanter.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_SLanter);
        SetExactBiologicalAndChronologicalAge(slanter, SlanterAgeYears);

        WestTekSpecialUtility.AssignGeneratedSpecials(slanter);
        AssignRandomFurGene(slanter);

        raccoon.DeSpawnOrDeselect();
        raccoon.Destroy(DestroyMode.Vanish);

        GenSpawn.Spawn(slanter, position, map, WipeMode.Vanish);

        RefreshGraphics(slanter);

        return slanter;
    }

    public static void ApplyExperimentalSkinwalkerMutation(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SkinwalkerMutation);
        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
        AssignRandomFurGene(pawn);
        RefreshGraphics(pawn);
    }

    public static void TransformSLanterIntoHighmate(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        pawn.genes.SetXenotype(WestTekDefOf.Highmate);

        WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
        AssignRandomFurGene(pawn);
        RefreshGraphics(pawn);
    }

    public static void ToggleSkinwalkerForm(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        if (IsSkinwalker(pawn))
        {
            pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_SLanter);
            AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SkinwalkerMutation);
            WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
            AssignRandomFurGene(pawn);
            RefreshGraphics(pawn);
            return;
        }

        if (IsSLanter(pawn) && HasSkinwalkerMutation(pawn))
        {
            pawn.genes.SetXenotype(WestTekDefOf.WestTek_Xenotype_Skinwalker);
            AddEndogeneIfMissing(pawn, WestTekDefOf.WestTek_Gene_SkinwalkerMutation);
            WestTekSpecialUtility.AssignGeneratedSpecials(pawn);
            AssignRandomFurGene(pawn);
            RefreshGraphics(pawn);
        }
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