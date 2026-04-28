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
    public static HediffDef WestTek_Nightkin = null;
    public static TraitDef WestTek_NightkinSchizophrenia = null;
    public static AbilityDef WestTek_NightkinInvisibility = null;
    public static XenotypeDef WestTek_Xenotype_PureHumans = null;
    public static XenotypeDef WestTek_Xenotype_SuperMutant_1 = null;
    public static XenotypeDef WestTek_Xenotype_SuperMutant_2 = null;
    public static XenotypeDef WestTek_Xenotype_VaultDweller = null;
    public static ThingDef WestTek_FEVProbe = null;
}