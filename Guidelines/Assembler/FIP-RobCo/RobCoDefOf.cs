using RimWorld;
using Verse;

namespace FIP.RobCo;

[DefOf]
internal static class RobCoDefOf
{
    static RobCoDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(RobCoDefOf));
    }

    public static GeneDef WestTek_Gene_SynthComponents = null;
    public static GeneDef WestTek_Gene_Courser = null;

    public static GeneDef WestTek_Gene_SystemOfCells = null;
    public static GeneDef WestTek_Gene_WithinOneStem = null;
    public static GeneDef WestTek_Gene_DreadfullyDistinct = null;
    public static GeneDef WestTek_Gene_TallWhiteFountain = null;
    public static GeneDef WestTek_Gene_Interlinked = null;
    public static GeneDef WestTek_Gene_AgainstTheDark = null;
    public static GeneDef WestTek_Gene_BloodBlackNothingness = null;
}