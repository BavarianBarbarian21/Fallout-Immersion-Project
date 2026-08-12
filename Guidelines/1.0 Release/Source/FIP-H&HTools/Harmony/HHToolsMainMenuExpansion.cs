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
    private const string MainMenuIconPath = "FIP-H&HTools/UI/MainMenu/FIP_Title_Logo";
    private static ExpansionDef expansion;

    private static void Postfix(List<ExpansionDef> __result)
    {
        if (__result == null || __result.Any(item => item?.defName == ExpansionDefName))
        {
            return;
        }

        ExpansionDef visualFallback = __result.FirstOrDefault(item => item?.isCore == true)
            ?? __result.FirstOrDefault(item => item != null);
        expansion ??= CreateExpansion(visualFallback);
        if (expansion != null)
        {
            __result.Add(expansion);
        }
    }

    private static ExpansionDef CreateExpansion(ExpansionDef visualFallback)
    {
        ModMetaData mod = ModLister.GetActiveModWithIdentifier(PackageId);
        if (mod == null)
        {
            return null;
        }

        Texture2D icon = ContentFinder<Texture2D>.Get(MainMenuIconPath, false) ?? BaseContent.BadTex;
        Texture2D background = GetNativeVisual<Texture2D>(visualFallback, "BG", "cachedBG") ?? BaseContent.BadTex;
        List<Texture2D> previewImages = GetNativeVisual<List<Texture2D>>(
            visualFallback,
            "PreviewImages",
            "cachedPreviewImages") ?? new List<Texture2D>();
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
        AccessTools.Field(typeof(ExpansionDef), "cachedBG").SetValue(result, background);
        AccessTools.Field(typeof(ExpansionDef), "cachedPreviewImages").SetValue(result, previewImages ?? new List<Texture2D>());
        return result;
    }

    private static T GetNativeVisual<T>(ExpansionDef source, string propertyName, string fieldName) where T : class
    {
        if (source == null)
        {
            return null;
        }

        T value = AccessTools.Property(typeof(ExpansionDef), propertyName)?.GetValue(source) as T;
        return value ?? AccessTools.Field(typeof(ExpansionDef), fieldName)?.GetValue(source) as T;
    }
}
