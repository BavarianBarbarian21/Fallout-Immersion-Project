using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VBE
{
    public static class HarmonyPatches
    {
        public static void DoPatches(Harmony harm)
        {
            harm.Patch(AccessTools.Method(typeof(Dialog_Options), nameof(Dialog_Options.DoWindowContents)),
                transpiler: new HarmonyMethod(typeof(HarmonyPatches), nameof(OptionsTranspile)));
            harm.Patch(AccessTools.Method(typeof(UI_BackgroundMain), nameof(UI_BackgroundMain.DoOverlay)),
                postfix: new HarmonyMethod(typeof(BackgroundController), nameof(BackgroundController.DoOverlay)));
            harm.Patch(AccessTools.Method(typeof(MainMenuDrawer), nameof(MainMenuDrawer.Init)), postfix:
                new HarmonyMethod(typeof(BackgroundController), nameof(BackgroundController.Initialize)));
            harm.Patch(AccessTools.Method(typeof(Current), nameof(Current.Notify_LoadedSceneChanged)),
                postfix: new HarmonyMethod(typeof(BackgroundController), nameof(BackgroundController.Notify_SceneChanged)));
        }

        public static IEnumerable<CodeInstruction> OptionsTranspile(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            var info = AccessTools.PropertyGetter(typeof(ModLister), nameof(ModLister.AllExpansions));
            var info2 = AccessTools.Method(typeof(WindowStack), nameof(WindowStack.Add));
            var idx = list.FindIndex(ins => ins.Calls(info));
            var idx2 = list.FindIndex(idx, ins => ins.Calls(info2));
            list.InsertRange(idx2 + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldloc_1).WithLabels(list[idx].ExtractLabels()),
                CodeInstruction.Call(typeof(HarmonyPatches), nameof(DoMenuBackgroundButton))
            });
            list.RemoveRange(idx, idx2 - idx + 1);
            return list;
        }

        public static void DoMenuBackgroundButton(Listing_Standard listing)
        {
            if (DefDatabase<BackgroundImageDef>.AllDefsListForReading.Any() && listing.ButtonTextLabeled("SetBackgroundImage".Translate(),
                DefDatabase<BackgroundImageDef>.GetNamedSilentFail(BackgroundController.Current.defName)?.LabelCap))
                Find.WindowStack.Add(new FloatMenu((from image in VBEMod.AllDefsInOrder
                    select
                        new FloatMenuOption(image.label, delegate
                        {
                            BackgroundController.Current = image;
                            VBEMod.Settings.current = image.defName;
                            VBEMod.Settings.randomize = false;
                            VBEMod.Settings.cycle = false;
                            VBEMod.Settings.Write();
                        }, image.Icon, Color.white)).ToList()));
        }
    }
}