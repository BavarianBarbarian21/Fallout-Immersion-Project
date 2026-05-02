using RimWorld;
using Verse;

namespace FIP.WestTek;

[DefOf]
internal static class WestTekDefOf
{
    static WestTekDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(WestTekDefOf));
    }

    public static PawnKindDef WestTek_Centaur = null;
    public static GeneDef WestTek_Gene_Nightkin = null;
    public static XenotypeDef WestTek_Xenotype_PureHumans = null;
    public static XenotypeDef WestTek_Xenotype_SuperMutantSecond = null;
    public static XenotypeDef WestTek_Xenotype_SuperMutantFirst = null;
    public static TraitDef WestTek_NightkinSchizophrenia = null;
    public static XenotypeDef WestTek_Xenotype_VaultDweller = null;
    public static ThingDef WestTek_FEVProbe = null;
    public static ThingDef WestTek_UnrefinedFEVDosage = null;
    public static PawnKindDef WestTek_TameBehemoth = null;
}