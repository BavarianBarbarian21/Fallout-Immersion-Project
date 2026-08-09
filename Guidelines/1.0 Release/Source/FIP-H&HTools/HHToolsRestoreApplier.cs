using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.HHTools;

internal static class HHToolsRestoreApplier
{
    private static readonly string[] FactionDefNames =
    {
        "OutlanderCivil", "OutlanderRough", "TribeCivil", "TribeRough", "TribeSavage", "Pirate",
        "TribeRoughNeanderthal", "PirateYttakin", "TribeSavageImpid", "OutlanderRoughPig", "PirateWaster",
        "SettlerCivil", "SettlerRough", "SettlerSavage", "VFEM2_KingdomCivil", "VFEM2_KingdomRough",
        "VFEM2_KingdomSavage", "VFEM2_CivilClan", "VFEM2_ClanRough", "VFEM2_ClanSavage"
    };

    private static readonly string[] ScenarioDefNames = { "VFEM2_NewKingdom", "VFES_Bandits" };
    private static readonly string[] QuestDefNames =
    {
        "VFEM2_OpportunitySite_Skirmish", "VFEM2_OpportunitySite_SiegeCamp", "VFES_Wanted", "VFES_CaravanRaid"
    };
    private static readonly string[] StorytellerDefNames = { "VFEM_MaynardMedieval", "VFET_TalonTribal", "VFES_DD" };

    private sealed class FactionState
    {
        public bool DisplayInFactionSelection;
        public int RequiredCountAtGameStart;
        public int StartingCountAtWorldCreation;
        public int MaxConfigurableAtWorldCreation;
        public float SettlementGenerationWeight;
    }

    private sealed class BuildingState
    {
        public DesignationCategoryDef DesignationCategory;
        public List<ThingCategoryDef> ThingCategories;
    }

    private sealed class ItemState
    {
        public float GenerateCommonality;
        public float GenerateAllowChance;
        public List<RecipeDef> Recipes;
    }

    private static readonly Dictionary<string, FactionState> FactionStates = new();
    private static readonly Dictionary<string, bool> ScenarioStates = new();
    private static readonly Dictionary<string, float> QuestStates = new();
    private static readonly Dictionary<string, bool> StorytellerStates = new();
    private static readonly Dictionary<string, BuildingState> BuildingStates = new();
    private static readonly Dictionary<string, ItemState> ItemStates = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        CaptureFactions();
        CaptureScenarios();
        CaptureQuests();
        CaptureStorytellers();
        CaptureBuildings();
        CaptureItems();
        initialized = true;
    }

    public static void Apply(HHToolsModSettings settings)
    {
        Initialize();
        ApplyFactions(!settings.restoreFactions);
        ApplyScenarios(!settings.restoreScenarios);
        ApplyBuildings(!settings.restoreBuildings);
        ApplyItems(!settings.restoreWeapons, !settings.restoreApparel);
        ApplyQuests(!settings.restoreQuests);
        ApplyStorytellers(!settings.restoreStorytellers);
    }

    private static void CaptureFactions()
    {
        foreach (string defName in FactionDefNames)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            FactionStates[defName] = new FactionState
            {
                DisplayInFactionSelection = def.displayInFactionSelection,
                RequiredCountAtGameStart = def.requiredCountAtGameStart,
                StartingCountAtWorldCreation = def.startingCountAtWorldCreation,
                MaxConfigurableAtWorldCreation = def.maxConfigurableAtWorldCreation,
                SettlementGenerationWeight = def.settlementGenerationWeight
            };
        }
    }

    private static void ApplyFactions(bool hide)
    {
        foreach ((string defName, FactionState state) in FactionStates)
        {
            FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            def.displayInFactionSelection = hide ? false : state.DisplayInFactionSelection;
            def.requiredCountAtGameStart = hide ? 0 : state.RequiredCountAtGameStart;
            def.startingCountAtWorldCreation = hide ? 0 : state.StartingCountAtWorldCreation;
            def.maxConfigurableAtWorldCreation = hide ? 0 : state.MaxConfigurableAtWorldCreation;
            def.settlementGenerationWeight = hide ? 0f : state.SettlementGenerationWeight;
        }
    }

    private static void CaptureScenarios()
    {
        foreach (string defName in ScenarioDefNames)
        {
            ScenarioDef def = DefDatabase<ScenarioDef>.GetNamedSilentFail(defName);
            if (def?.scenario != null)
            {
                ScenarioStates[defName] = def.scenario.showInUI;
            }
        }
    }

    private static void ApplyScenarios(bool hide)
    {
        foreach ((string defName, bool visible) in ScenarioStates)
        {
            ScenarioDef def = DefDatabase<ScenarioDef>.GetNamedSilentFail(defName);
            if (def?.scenario != null)
            {
                def.scenario.showInUI = hide ? false : visible;
            }
        }
    }

    private static void CaptureQuests()
    {
        foreach (string defName in QuestDefNames)
        {
            QuestScriptDef def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                QuestStates[defName] = def.rootSelectionWeight;
            }
        }
    }

    private static void ApplyQuests(bool hide)
    {
        foreach ((string defName, float weight) in QuestStates)
        {
            QuestScriptDef def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.rootSelectionWeight = hide ? 0f : weight;
            }
        }
    }

    private static void CaptureStorytellers()
    {
        foreach (string defName in StorytellerDefNames)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                StorytellerStates[defName] = def.listVisible;
            }
        }
    }

    private static void ApplyStorytellers(bool hide)
    {
        foreach ((string defName, bool visible) in StorytellerStates)
        {
            StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                def.listVisible = hide ? false : visible;
            }
        }
    }

    private static void CaptureBuildings()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def == null || !IsTargetBuilding(def))
            {
                continue;
            }

            BuildingStates[def.defName] = new BuildingState
            {
                DesignationCategory = def.designationCategory,
                ThingCategories = def.thingCategories == null ? null : new List<ThingCategoryDef>(def.thingCategories)
            };
        }
    }

    private static bool IsTargetBuilding(ThingDef def)
    {
        if (def.defName == "VFEM2_Palisade" || def.defName == "VFEM2_ArcheryTarget" || def.defName == "VFEM2_TrainingDummy")
        {
            return false;
        }

        return def.designationCategory?.defName == "Structure"
            || (def.defName != null && def.defName.StartsWith("VFEM2_") && def.category == ThingCategory.Building);
    }

    private static void ApplyBuildings(bool hide)
    {
        ThingCategoryDef textiles = DefDatabase<ThingCategoryDef>.GetNamedSilentFail("Textiles");
        foreach ((string defName, BuildingState state) in BuildingStates)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            def.designationCategory = hide ? null : state.DesignationCategory;
            def.thingCategories = state.ThingCategories == null ? null : new List<ThingCategoryDef>(state.ThingCategories);
            if (hide && def.defName != null && def.defName.StartsWith("VFEM2_"))
            {
                def.thingCategories?.Remove(textiles);
            }
        }
    }

    private static void CaptureItems()
    {
        foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (def == null || !IsTargetWeapon(def) && !IsTargetApparel(def))
            {
                continue;
            }

            ItemStates[def.defName] = new ItemState
            {
                GenerateCommonality = def.generateCommonality,
                GenerateAllowChance = def.generateAllowChance,
                Recipes = def.recipes == null ? null : new List<RecipeDef>(def.recipes)
            };
        }
    }

    private static bool IsTargetWeapon(ThingDef def)
    {
        return def.defName != null && (def.defName.StartsWith("VFEM2_MeleeWeapon_")
            || def.defName == "VFEM2_ThrowingAxe" || def.defName == "VFEM2_Gun_Arquebus"
            || def.defName == "VFEM2_Gun_HandCannon" || def.defName == "VFEM2_Arbalest"
            || def.defName == "VFEM2_Warbow" || def.defName == "VFEM2_Gun_Musket" || def.defName == "VFEM2_Gun_Flintlock");
    }

    private static bool IsTargetApparel(ThingDef def)
    {
        return def.defName != null && (def.defName.StartsWith("VFEM2_Apparel_") || def.defName.StartsWith("VFEM2_Shield_"));
    }

    private static void ApplyItems(bool hideWeapons, bool hideApparel)
    {
        foreach ((string defName, ItemState state) in ItemStates)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            bool hide = IsTargetWeapon(def) ? hideWeapons : hideApparel;
            def.generateCommonality = hide ? 0f : state.GenerateCommonality;
            def.generateAllowChance = hide ? 0f : state.GenerateAllowChance;
            def.recipes = hide ? new List<RecipeDef>() : state.Recipes == null ? null : new List<RecipeDef>(state.Recipes);
        }
    }
}
