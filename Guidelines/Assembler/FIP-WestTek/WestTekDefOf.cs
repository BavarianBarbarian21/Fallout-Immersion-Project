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
    public static PawnKindDef WestTek_Floater = null;
    public static PawnKindDef WestTek_TameBehemoth = null;

    public static XenotypeDef WestTek_Xenotype_SLanter = null;
public static XenotypeDef WestTek_Xenotype_Skinwalker = null;
public static XenotypeDef Highmate = null;

public static GeneDef WestTek_Gene_SkinwalkerMutation = null;

public static GeneDef WestTek_White = null;
public static GeneDef WestTek_Gray = null;
public static GeneDef WestTek_Brown = null;
public static GeneDef WestTek_Purple = null;
public static GeneDef WestTek_Black = null;
public static GeneDef WestTek_Blonde = null;
public static GeneDef WestTek_Teal = null;

public static GeneDef WestTek_Gene_BAja = null;
public static GeneDef WestTek_Gene_MErowi = null;
public static GeneDef WestTek_Gene_RUffian = null;
public static GeneDef WestTek_Gene_SNuffy = null;

public static ThingDef WestTek_UnrefinedFaunaMutagen = null;
public static ThingDef WestTek_ExperimentalFaunaMutagen = null;
public static ThingDef WestTek_RefinedFaunaMutagen = null;
public static ThingDef WestTek_CanineMutagen = null;
public static ThingDef WestTek_FelineMutagen = null;
public static ThingDef WestTek_LeporineMutagen = null;
public static ThingDef WestTek_ProcyonineMutagen = null;

public static AbilityDef WestTek_Ability_SkinwalkerShift = null;
}