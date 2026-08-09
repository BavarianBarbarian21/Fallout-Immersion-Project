using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Anthrosonae
{
    [HotSwappable]
    [HarmonyPatch(typeof(FloatMenu), MethodType.Constructor, new Type[] {typeof(List<FloatMenuOption>) })]
    public static class FloatMenu_Constructor_Patch
    {
        public static bool ignore;
        public static void Prefix(FloatMenu __instance, ref List<FloatMenuOption> options)
        {
            if (ignore) return;
            var groupXenotypes = DefDatabase<XenotypeDef>.AllDefs.Select(x => (x, x.GetModExtension<XenotypeExtension>())).Where(x => x.Item2 is not null).ToList();
            var groupOptions = new HashSet<FloatMenuOption>();
            var groupByKey = new Dictionary<(string groupName, string groupIcon, string groupDescription), List<FloatMenuOption>>();
            foreach (var option in options)
            {
                var match = groupXenotypes.FirstOrDefault(x => x.x.Icon == option.iconTex
            && XenotypeDef.IconColor == option.iconColor);
                if (match != default)
                {
                    var key = (match.Item2.groupName, match.Item2.groupIcon, match.Item2.groupDescription);
                    if (groupByKey.TryGetValue(key, out var group) is false)
                    {
                        groupByKey[key] = group = new List<FloatMenuOption>();
                    }
                    group.Add(option);
                    groupOptions.Add(option);
                }
            }
            options.RemoveAll(x => groupOptions.Contains(x));
            int index = options.FindIndex(option => option.Label.Contains("..."));
            if (index == -1 || options.IndexOf(options.LastOrDefault(x => x.Label.Contains("..."))) != options.Count - 1)
            {
                index = options.Count;
            }
            foreach (var option in groupByKey)
            {
                options.Insert(index, new FloatMenuOption(option.Key.groupName, delegate
                {
                    ignore = true;
                    Find.WindowStack.Add(new FloatMenu(option.Value));
                    ignore = false;
                }, iconTex: ContentFinder<Texture2D>.Get(option.Key.groupIcon), 
                iconColor: XenotypeDef.IconColor,
                mouseoverGuiAction: delegate (Rect rect)
                {
                    if (option.Key.groupDescription.NullOrEmpty() is false)
                    {
                        TooltipHandler.TipRegion(rect, option.Key.groupDescription);
                    }
                }));
                index++;
            }
        }
    }
}
