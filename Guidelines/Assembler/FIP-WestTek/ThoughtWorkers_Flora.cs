using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class ThoughtWorker_WestTek_StarchedOutside : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        if (!WestTekFloraGeneUtility.HasGene(pawn, WestTekDefOf.WestTek_Gene_Starched))
        {
            return ThoughtState.Inactive;
        }

        return WestTekFloraMutationUtility.IsOutside(pawn);
    }
}

public sealed class ThoughtWorker_WestTek_HeterotrophDark : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        if (!WestTekFloraGeneUtility.HasGene(pawn, WestTekDefOf.WestTek_Gene_Heterotroph))
        {
            return ThoughtState.Inactive;
        }

        return !WestTekFloraMutationUtility.IsUnderSunlight(pawn);
    }
}

public sealed class ThoughtWorker_WestTek_HeterotrophSunlight : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        if (!WestTekFloraGeneUtility.HasGene(pawn, WestTekDefOf.WestTek_Gene_Heterotroph))
        {
            return ThoughtState.Inactive;
        }

        return WestTekFloraMutationUtility.IsUnderSunlight(pawn);
    }
}

public sealed class ThoughtWorker_WestTek_MycelineNetwork : ThoughtWorker
{
    protected override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        if (!WestTekFloraGeneUtility.HasGene(pawn, WestTekDefOf.WestTek_Gene_Myceline))
        {
            return ThoughtState.Inactive;
        }

        return WestTekFloraMutationUtility.IsNearGauranlenTree(pawn);
    }
}