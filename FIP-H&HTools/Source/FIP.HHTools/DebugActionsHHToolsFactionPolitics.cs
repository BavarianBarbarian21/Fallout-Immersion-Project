using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace FIP.HHTools;

public static class DebugActionsHHToolsFactionPolitics
{
    private const string CategoryName = "FIP: H&H Politics";

    [DebugAction(CategoryName, "Log Faction States", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void LogFactionStates()
    {
        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        if (tracker == null)
        {
            Log.Message("[FIP.HHTools] No faction politics tracker available.");
            return;
        }

        foreach (HHToolsFactionPoliticalState state in tracker.AllStates.OrderBy(entry => entry.factionName))
        {
            string summary = state.system switch
            {
                HHToolsFactionPoliticalSystem.Civilized => string.Join(", ", state.civilizedParties.Select(entry => $"{entry.party}={entry.influence}")),
                HHToolsFactionPoliticalSystem.Authoritarian => string.Join(", ", state.crimeBosses.Select(entry => $"{entry.boss}: missions={entry.completedMissions}, favor={entry.favorGranted}, dead={entry.eliminated}")),
                HHToolsFactionPoliticalSystem.Tribal => $"confederation={state.confederationFormed}, protectorate={state.protectorateFormed}",
                _ => "no dynamic state"
            };

            Log.Message($"[FIP.HHTools] {state.factionName} ({state.factionDefName}) [{state.system}] :: {summary}");
        }
    }

    [DebugAction(CategoryName, "Initialize H&H States", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
    private static void InitializeFactionStates()
    {
        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        if (tracker == null)
        {
            Log.Message("[FIP.HHTools] No faction politics tracker available.");
            return;
        }

        int initializedCount = 0;
        foreach (Faction faction in Find.FactionManager.AllFactionsListForReading)
        {
            if (!tracker.AppliesTo(faction))
            {
                continue;
            }

            if (tracker.GetOrCreateState(faction) != null)
            {
                initializedCount += 1;
            }
        }

        Log.Message($"[FIP.HHTools] Initialized or confirmed {initializedCount} H&H faction states.");
    }
}