using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

[StaticConstructorOnStartup]
internal static class HHToolsHarmonyBootstrap
{
    static HHToolsHarmonyBootstrap()
    {
        new Harmony("FIP.HHTools.MainMenuExpansion").PatchAll();
    }
}

[HarmonyPatch(typeof(ModLister), nameof(ModLister.AllExpansions), MethodType.Getter)]
internal static class HHToolsExpansionListPatch
{
    private const string PackageId = "FIP.HHTools";
    private const string ExpansionDefName = "FIP_HHTools_MainMenu";
    private static ExpansionDef expansion;

    private static void Postfix(List<ExpansionDef> __result)
    {
        if (__result == null || __result.Any(item => item?.defName == ExpansionDefName))
        {
            return;
        }

        expansion ??= CreateExpansion();
        if (expansion != null)
        {
            __result.Add(expansion);
        }
    }

    private static ExpansionDef CreateExpansion()
    {
        ModMetaData mod = ModLister.GetActiveModWithIdentifier(PackageId);
        if (mod == null)
        {
            return null;
        }

        Texture2D icon = mod.Icon ?? BaseContent.BadTex;
        Texture2D preview = mod.PreviewImage ?? icon;
        ExpansionDef result = new()
        {
            defName = ExpansionDefName,
            label = "FIP - H&H Tools",
            description = "The active core and shared-resource layer of the Fallout Immersion Project.",
            linkedMod = PackageId.ToLowerInvariant(),
            isCore = false,
            primaryColor = new Color(0.55f, 0.84f, 0.43f)
        };

        AccessTools.Field(typeof(ExpansionDef), "cachedIcon").SetValue(result, icon);
        AccessTools.Field(typeof(ExpansionDef), "cachedNotOwnedIcon").SetValue(result, icon);
        AccessTools.Field(typeof(ExpansionDef), "cachedBG").SetValue(result, preview);
        AccessTools.Field(typeof(ExpansionDef), "cachedPreviewImages").SetValue(result, new List<Texture2D> { preview });
        return result;
    }
}
