using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace VBE
{
    public static class ModCompat
    {
        public static bool RimThemes;
        public static bool BetterLoading;

        public static void ApplyCompat(Harmony harm)
        {
            RimThemes = ModLister.HasActiveModWithName("RimThemes");
            BetterLoading = ModLister.HasActiveModWithName("BetterLoading");
            if (RimThemes)
            {
                Log.Message("[VBE] RimThemes detected, activating compatibility...");
                if (VBEMod.Settings.rimThemesOverride)
                {
                    Log.Message("[VBE] Unpatching RimThemes background patch...");
                    var type = AccessTools.TypeByName("aRandomKiwi.RimThemes.UI_BackgroundMain_Patch");
                    var prefix = AccessTools.Method(type, "Prefix");
                    var target = AccessTools.Method(typeof(UI_BackgroundMain), nameof(UI_BackgroundMain.BackgroundOnGUI));
                    harm.Unpatch(target, prefix);
                }

                harm.Patch(AccessTools.Method(AccessTools.TypeByName("aRandomKiwi.RimThemes.LoaderGM"), "loadThemesTextures"),
                    postfix: new HarmonyMethod(typeof(ModCompat), nameof(LoadRimThemesImages)));
            }

            if (BetterLoading)
            {
                Log.Message("[VBE] BetterLoading detected, activating compatibility...");
                Log.Warning("[VBE] BetterLoading compatibility is currently broken! Disabling.");
                // if (VBEMod.Settings.betterLoadingOverride)
                //     harm.Patch(AccessTools.Method(AccessTools.TypeByName("BetterLoading.LoadingScreen"), "DrawBackground"),
                //         new HarmonyMethod(AccessTools.TypeByName("BetterLoading.Utils"), "HarmonyPatchCancelMethod"),
                //         new HarmonyMethod(typeof(BackgroundController), nameof(BackgroundController.DrawBackground)));

                // if (VBEMod.Settings.betterLoadingAlternative)
                //     harm.Patch(AccessTools.Method(AccessTools.TypeByName("BetterLoading.LoadingScreen"), "OnGUI"),
                //         transpiler: new HarmonyMethod(typeof(ModCompat), nameof(TranspileOnGUI)));
            }

            if (BetterLoading)
            {
                var type = AccessTools.TypeByName("BetterLoading.BetterLoadingApi");
                var ev = type.GetEvent("OnGameLoadComplete");
                var method = AccessTools.Method(typeof(VBEMod), nameof(VBEMod.Initialize));
                var action = AccessTools.MethodDelegate<Action>(method, VBEMod.Instance);
                var add = ev.GetAddMethod();
                add.Invoke(ev, new object[] {action});
            }
            else LongEventHandler.ExecuteWhenFinished(VBEMod.Instance.Initialize);
        }

        public static void LoadRimThemesImages()
        {
            var themes = AccessTools.TypeByName("aRandomKiwi.RimThemes.Themes");
            var defaultIcon = (Texture2D) AccessTools.Field(AccessTools.TypeByName("aRandomKiwi.RimThemes.Loader"), "defaultIconTex")?.GetValue(null) ??
                              Texture2D.normalTexture;
            var textDB = (Dictionary<string, Dictionary<string, string>>) AccessTools.Field(themes, "DBText")?.GetValue(null);
            var texDB = (Dictionary<string, Dictionary<string, Dictionary<string, Texture2D>>>) AccessTools.Field(themes, "DBTex")?.GetValue(null);
            var iconDB = (Dictionary<string, Texture2D>) AccessTools.Field(themes, "DBTexThemeIcon")?.GetValue(null);
            var animatedBackgroundDB = (Dictionary<string, string>) AccessTools.Field(themes, "DBAnimatedBackground")?.GetValue(null);
            if (texDB is not null)
                foreach (var (key, dict) in texDB)
                    if (dict.ContainsKey("UI_BackgroundMain") && dict["UI_BackgroundMain"].ContainsKey("BGPlanet"))
                    {
                        var tex = dict["UI_BackgroundMain"]["BGPlanet"];
                        var icn = iconDB is not null && iconDB.TryGetValue(key, out var icon) ? icon : defaultIcon;
                        var arr = key.Split('§');
                        var defName = arr[1] + arr[0].Replace(' ', '_');
                        if (DefDatabase<BackgroundImageDef>.GetNamedSilentFail(defName) is not null) continue;
                        DefGenerator.AddImpliedDef(new BackgroundImageDef(tex, icn)
                        {
                            label = arr[1].Trim(),
                            description = textDB?[key]?["description"] ?? "VBE.NoDesc".Translate(),
                            defName = defName,
                            isTheme = true
                        });
                    }

            if (animatedBackgroundDB is not null)
                foreach (var (key, path) in animatedBackgroundDB)
                {
                    var icn = iconDB is not null && iconDB.TryGetValue(key, out var icon) ? icon : defaultIcon;
                    var arr = key.Split('§');
                    var defName = arr[1] + arr[0].Replace(' ', '_') + "_Animated";
                    if (DefDatabase<BackgroundImageDef>.GetNamedSilentFail(defName) is not null) continue;
                    DefGenerator.AddImpliedDef(new BackgroundImageDef(path, icn)
                    {
                        label = arr[1].Trim() + " (" + "VBE.Animated".Translate() + ")",
                        description = textDB?[key]?["description"] ?? "VBE.NoDesc".Translate(),
                        defName = defName,
                        isTheme = true,
                        animated = true,
                        previewFrame = 30
                    });
                }

            VBEMod.Settings.CheckInit();
            VBEMod.Instance.InitializeAnimated();
        }

        public static IEnumerable<CodeInstruction> TranspileOnGUI(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = instructions.ToList();
            var info1 = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] {typeof(Rect), typeof(string)});
            var idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 2, 3);
            idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 11, 12);
            var info2 = AccessTools.Method(typeof(GUI), nameof(GUI.DrawTexture), new[] {typeof(Rect), typeof(Texture)});
            idx1 = codes.FindIndex(ins => ins.Calls(info2));
            codes.RemoveRange(idx1 - 5, 6);
            var info3 = AccessTools.Method(typeof(Widgets), nameof(Widgets.FillableBar),
                new[] {typeof(Rect), typeof(float), typeof(Texture2D), typeof(Texture2D), typeof(bool)});
            idx1 = codes.FindIndex(ins => ins.Calls(info3));
            codes.RemoveRange(idx1 - 14, 16);
            idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 9, 10);
            idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 4, 5);
            idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 2, 3);
            idx1 = codes.FindIndex(ins => ins.Calls(info2));
            codes.RemoveRange(idx1 - 5, 6);
            idx1 = codes.FindIndex(ins => ins.Calls(info3));
            codes.RemoveRange(idx1 - 9, 11);
            idx1 = codes.FindIndex(ins => ins.Calls(info1));
            codes.RemoveRange(idx1 - 6, 7);
            return codes;
        }
    }
}