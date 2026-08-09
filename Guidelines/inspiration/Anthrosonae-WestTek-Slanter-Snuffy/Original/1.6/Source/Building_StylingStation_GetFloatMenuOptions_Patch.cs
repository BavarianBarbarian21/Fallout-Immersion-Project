using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Anthrosonae
{
    [HarmonyPatch(typeof(Building_StylingStation), "GetFloatMenuOptions")]
    public static class Building_StylingStation_GetFloatMenuOptions_Patch
    {
        public static IEnumerable<FloatMenuOption> Postfix(IEnumerable<FloatMenuOption> options, Pawn selPawn, Building_StylingStation __instance)
        {
            foreach (var option in options)
            {
                yield return option;
                if (option.Label == "ChangeStyle".Translate().CapitalizeFirst())
                {
                    FurGene furGene = GetFurGene(selPawn);
                    if (furGene != null)
                    {
                        yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("ColorPicker.ChangeFur".Translate().CapitalizeFirst(), delegate
                        {
                            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefsOf.ATK_ChangeFur, __instance), JobTag.Misc);
                        }), selPawn, __instance);
                    }
                }
            }
        }

        public static FurGene GetFurGene(this Pawn selPawn, bool activeCheck = true)
        {
            if (selPawn.genes is not null)
            {
                return selPawn.genes.GenesListForReading.OfType<FurGene>().FirstOrDefault(x => x.Active || activeCheck is false);
            }
            return null;
        }
    }
}
