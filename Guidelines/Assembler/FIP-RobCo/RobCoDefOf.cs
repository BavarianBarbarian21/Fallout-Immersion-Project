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
    public static GeneDef WestTek_Gene_SynthBody = null;
    public static GeneDef WestTek_Gene_Courser = null;

    public static GeneDef WestTek_Gene_SystemOfCells = null;
    public static GeneDef WestTek_Gene_WithinOneStem = null;
    public static GeneDef WestTek_Gene_DreadfullyDistinct = null;
    public static GeneDef WestTek_Gene_TallWhiteFountain = null;
    public static GeneDef WestTek_Gene_Interlinked = null;
    public static GeneDef WestTek_Gene_AgainstTheDark = null;
    public static GeneDef WestTek_Gene_BloodBlackNothingness = null;

    public static GeneDef RobCo_Gene_ThinkTankCore = null;
    public static GeneDef RobCo_Gene_Biosphere = null;
    public static GeneDef RobCo_Gene_ThinkTankEyes = null;
    public static GeneDef RobCo_Gene_Lobotomized = null;

    public static XenotypeDef RobCo_ThinkTank = null;
    public static BodyTypeDef RobCo_ThinkTankBodyType = null;
    public static HeadTypeDef RobCo_ThinkTankHead = null;

    public static ThingDef RobCo_Gen1Synth = null;
}
