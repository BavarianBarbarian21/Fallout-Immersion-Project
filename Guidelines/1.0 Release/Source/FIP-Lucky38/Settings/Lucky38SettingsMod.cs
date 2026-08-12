using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.Lucky38;

public sealed class Lucky38Settings : ModSettings
{
    public bool onlyImmersiveResearchTree = true;

    public override void ExposeData()
    {
        bool loading = Scribe.mode == LoadSaveMode.LoadingVars;
        bool hasNewValue = loading && Scribe.loader.curXmlParent?["onlyImmersiveResearchTree"] != null;
        bool hasLegacyValue = loading && Scribe.loader.curXmlParent?["restoreResearchTree"] != null;
        Scribe_Values.Look(ref onlyImmersiveResearchTree, "onlyImmersiveResearchTree", true);
        if (loading && !hasNewValue && hasLegacyValue)
        {
            bool legacyRestore = false;
            Scribe_Values.Look(ref legacyRestore, "restoreResearchTree", false);
            onlyImmersiveResearchTree = !legacyRestore;
        }
    }
}

public sealed class Lucky38SettingsMod : Mod
{
    private static Lucky38Settings settings;

    public Lucky38SettingsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<Lucky38Settings>();
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            Lucky38ResearchTreeApplier.Initialize();
            ApplySettings();
        });
    }

    public override string SettingsCategory() => "FIP - Lucky 38";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listing = new();
        listing.Begin(inRect);
        Text.Font = GameFont.Medium;
        listing.Label("Immersive research");
        Text.Font = GameFont.Small;
        listing.Label("Keep overlapping research integrated into the curated FIP technology tree.");
        listing.GapLine();
        bool value = settings.onlyImmersiveResearchTree;
        listing.CheckboxLabeled("Only immersive research tree", ref value,
            "Integrates Brewing projects into Cooking and hides the separate Brewing and schematic tabs. Restart required.");
        if (value != settings.onlyImmersiveResearchTree)
        {
            settings.onlyImmersiveResearchTree = value;
            ApplySettings();
        }
        listing.End();
    }

    public override void WriteSettings()
    {
        base.WriteSettings();
        ApplySettings();
    }

    private static void ApplySettings() => Lucky38ResearchTreeApplier.Apply(settings.onlyImmersiveResearchTree);
}

internal static class Lucky38ResearchTreeApplier
{
    private sealed class ProjectState
    {
        public ResearchTabDef Tab;
        public float X;
        public float Y;
    }

    private sealed class TabListState
    {
        public IList List;
        public List<object> OriginalValues;
    }

    private static readonly Dictionary<string, ProjectState> OriginalProjects = new();
    private static readonly List<TabListState> SchematicTabLists = new();
    private static readonly MethodInfo RemoveResearchTab = typeof(DefDatabase<ResearchTabDef>).GetMethod("Remove", BindingFlags.Static | BindingFlags.NonPublic);
    private static ResearchTabDef brewingTab;
    private static ResearchTabDef cookingTab;
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (DefDatabase<ResearchTabDef>.AllDefsListForReading.Count == 0)
        {
            return;
        }

        brewingTab = DefDatabase<ResearchTabDef>.GetNamedSilentFail("VCE_Brewing");
        cookingTab = DefDatabase<ResearchTabDef>.GetNamedSilentFail("VCE_Cooking");
        foreach (string defName in new[] { "VBE_LiquorBrewing", "VBE_MixologyResearch", "VBE_EspressoMachine" })
        {
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            if (project != null)
            {
                OriginalProjects[defName] = new ProjectState { Tab = project.tab, X = project.researchViewX, Y = project.researchViewY };
            }
        }

        ThingDef schematic = DefDatabase<ThingDef>.GetNamedSilentFail("Schematic");
        if (schematic?.comps != null)
        {
            foreach (object comp in schematic.comps)
            {
                CaptureTabLists(comp, 0);
            }
        }

        initialized = true;
    }

    public static void Apply(bool onlyImmersive)
    {
        Initialize();
        RestoreResearchTabsToSource();
        RestoreProjectStates();
        RestoreSchematicTabs();

        if (!onlyImmersive)
        {
            return;
        }

        foreach ((string defName, ProjectState _) in OriginalProjects)
        {
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            if (project == null || cookingTab == null)
            {
                continue;
            }

            project.tab = cookingTab;
            if (defName == "VBE_LiquorBrewing") { project.researchViewX = 3f; project.researchViewY = 0f; }
            if (defName == "VBE_MixologyResearch") { project.researchViewX = 4f; project.researchViewY = 0f; }
            if (defName == "VBE_EspressoMachine") { project.researchViewX = 3f; project.researchViewY = 1f; }
        }

        RemoveBrewingFromSchematicTabs();
        RemoveBrewingTabFromDatabase();
    }

    private static void RestoreProjectStates()
    {
        foreach ((string defName, ProjectState state) in OriginalProjects)
        {
            ResearchProjectDef project = DefDatabase<ResearchProjectDef>.GetNamedSilentFail(defName);
            if (project != null)
            {
                project.tab = state.Tab;
                project.researchViewX = state.X;
                project.researchViewY = state.Y;
            }
        }
    }

    private static void RestoreResearchTabsToSource()
    {
        if (brewingTab != null && DefDatabase<ResearchTabDef>.GetNamedSilentFail(brewingTab.defName) == null)
        {
            DefDatabase<ResearchTabDef>.Add(brewingTab);
        }
    }

    private static void RemoveBrewingTabFromDatabase()
    {
        if (brewingTab != null && DefDatabase<ResearchTabDef>.GetNamedSilentFail(brewingTab.defName) != null)
        {
            RemoveResearchTab?.Invoke(null, new object[] { brewingTab });
        }
    }

    private static void RestoreSchematicTabs()
    {
        foreach (TabListState state in SchematicTabLists)
        {
            state.List.Clear();
            foreach (object value in state.OriginalValues)
            {
                state.List.Add(value);
            }
        }
    }

    private static void RemoveBrewingFromSchematicTabs()
    {
        foreach (TabListState state in SchematicTabLists)
        {
            for (int index = state.List.Count - 1; index >= 0; index--)
            {
                if (state.List[index] is ResearchTabDef tab && tab.defName == "VCE_Brewing")
                {
                    state.List.RemoveAt(index);
                }
            }
        }
    }

    private static void CaptureTabLists(object value, int depth)
    {
        if (value == null || depth > 4)
        {
            return;
        }

        Type type = value.GetType();
        foreach (FieldInfo field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            object fieldValue = field.GetValue(value);
            if (field.Name == "tabs" && fieldValue is IList tabs)
            {
                List<object> copy = new();
                foreach (object tab in tabs) { copy.Add(tab); }
                SchematicTabLists.Add(new TabListState { List = tabs, OriginalValues = copy });
                continue;
            }

            if (fieldValue is IEnumerable enumerable && fieldValue is not string)
            {
                foreach (object child in enumerable)
                {
                    CaptureTabLists(child, depth + 1);
                }
            }
            else if (fieldValue != null && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType.Namespace != "System")
            {
                CaptureTabLists(fieldValue, depth + 1);
            }
        }
    }
}
