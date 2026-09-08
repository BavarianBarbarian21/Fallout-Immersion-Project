using System.Collections.Generic;
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
    }

    private static readonly Dictionary<string, FactionState> FactionStates = new();
    private static readonly Dictionary<string, bool> ScenarioStates = new();
    private static readonly Dictionary<string, float> QuestStates = new();
    private static readonly Dictionary<string, bool> StorytellerStates = new();
    private static readonly Dictionary<string, BuildingState> WallStructureStates = new();
    private static readonly Dictionary<string, BuildingState> FurnitureStates = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized)
        {
            return;
        }

        if (DefDatabase<FactionDef>.GetNamedSilentFail("OutlanderCivil") == null)
        {
            return;
        }

        CaptureFactions();
        CaptureScenarios();
        CaptureQuests();
        CaptureStorytellers();
        CaptureBuildings();
        initialized = true;
    }

    public static void Apply(HHToolsModSettings settings)
    {
        Initialize();
        if (!initialized)
        {
            return;
        }

        ApplyFactions(settings.onlyImmersiveFactions);
        ApplyScenarios(settings.onlyImmersiveScenarios);
        ApplyBuildings(WallStructureStates, settings.onlyImmersiveWallStructures);
        ApplyBuildings(FurnitureStates, settings.onlyImmersiveFurniture);
        ApplyQuests(settings.onlyImmersiveQuests);
        ApplyStorytellers(settings.onlyImmersiveStorytellers);
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
            if (def == null || def.category != ThingCategory.Building || def.defName.NullOrEmpty())
            {
                continue;
            }

            Dictionary<string, BuildingState> target = IsWallStructure(def.defName)
                ? WallStructureStates
                : IsFurniture(def.defName) ? FurnitureStates : null;
            if (target == null)
            {
                continue;
            }

            target[def.defName] = new BuildingState
            {
                DesignationCategory = def.designationCategory
            };
        }
    }

    private static bool IsWallStructure(string defName)
    {
        return defName == "VFEM2_Turret_WallMountedArbalest"
            || defName == "VFEM2_Turret_WallMountedArquebus"
            || defName == "VFEM2_2x1CastleDoor"
            || defName == "VFEM2_3x1CastleDoor"
            || defName == "VFEM2_4x1CastleDoor"
            || defName == "VFEM2_2x1CastleGate"
            || defName == "VFEM2_3x1CastleGate"
            || defName == "VFEM2_4x1CastleGate"
            || defName == "VFEM2_CastleWall"
            || defName == "VFEM2_LowCastleWall"
            || defName == "VFEM2_ClothWall"
            || defName == "VFEM2_TimberedWall"
            || defName.StartsWith("VFEM2_CobblestoneWall_");
    }

    private static bool IsFurniture(string defName)
    {
        return defName == "VFEM2_FurBed"
            || defName == "VFEM2_DoubleFurBed"
            || defName == "VFEM2_Hearth"
            || defName == "VFEM2_StandingBanner"
            || defName == "VFEM2_HeraldicRugNarrow"
            || defName == "VFEM2_HeraldicRugBroad"
            || defName == "VFEM2_HeraldicRugGrand";
    }

    private static void ApplyBuildings(Dictionary<string, BuildingState> states, bool hide)
    {
        foreach ((string defName, BuildingState state) in states)
        {
            ThingDef def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null)
            {
                continue;
            }

            def.designationCategory = hide ? null : state.DesignationCategory;
        }
    }
}
