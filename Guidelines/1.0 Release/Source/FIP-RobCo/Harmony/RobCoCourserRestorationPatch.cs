using HarmonyLib;
using RimWorld;
using Verse;

namespace FIP.RobCo;

/// <summary>
/// Selects the human-safe bill implementation for the single Courser recipe.
/// All ordinary mech gestation and resurrection bills remain vanilla.
/// </summary>
[HarmonyPatch(typeof(BillUtility), nameof(BillUtility.MakeNewBill))]
internal static class BillUtility_RobCoCourserRestorationPatch
{
    private static bool Prefix(RecipeDef recipe, Precept_ThingStyle precept, ref Bill __result)
    {
        if (recipe?.defName != "RobCo_RestoreQuestCourser")
        {
            return true;
        }

        __result = new Bill_RestoreCourser(recipe, precept);
        return false;
    }
}

internal static class RobCoCourserRestorationUtility
{
    public static bool IsQuestCourserCorpse(Thing thing)
    {
        return thing is Corpse corpse
            && corpse.InnerPawn?.kindDef?.defName == "RobCo_QuestCourser"
            && corpse.InnerPawn.genes?.GenesListForReading.Exists(gene => gene.def?.defName == "RobCo_Gene_Courser") == true;
    }

    public static void RejectAlternativeResurrection(Thing target)
    {
        Messages.Message(
            "RobCoQuestCourserRequiresGantry".Translate(),
            target,
            MessageTypeDefOf.RejectInput,
            historical: false);
    }
}

/// <summary>
/// Prevents a resurrector mech serum from bypassing the unique-chip recovery.
/// The serum is not consumed because its job is never started.
/// </summary>
[HarmonyPatch(typeof(CompTargetEffect_Resurrect), nameof(CompTargetEffect_Resurrect.DoEffectOn))]
internal static class CompTargetEffect_Resurrect_RobCoCourserPatch
{
    private static bool Prefix(Thing target)
    {
        if (!RobCoCourserRestorationUtility.IsQuestCourserCorpse(target))
        {
            return true;
        }

        RobCoCourserRestorationUtility.RejectAlternativeResurrection(target);
        return false;
    }
}

/// <summary>
/// Prevents resurrection abilities from bypassing the same recovery bill.
/// </summary>
[HarmonyPatch(typeof(CompAbilityEffect_Resurrect), nameof(CompAbilityEffect_Resurrect.Valid))]
internal static class CompAbilityEffect_Resurrect_RobCoCourserPatch
{
    private static void Postfix(LocalTargetInfo target, bool throwMessages, ref bool __result)
    {
        if (!__result || !RobCoCourserRestorationUtility.IsQuestCourserCorpse(target.Thing))
        {
            return;
        }

        __result = false;
        if (throwMessages)
        {
            RobCoCourserRestorationUtility.RejectAlternativeResurrection(target.Thing);
        }
    }
}
