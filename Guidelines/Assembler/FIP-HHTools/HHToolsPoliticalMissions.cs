using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;
using Verse.Grammar;

namespace FIP.HHTools;

public partial class HHToolsFactionPoliticsTracker
{
    public bool CanRequestPoliticalMission(
        Settlement settlement,
        Caravan caravan,
        HHToolsPoliticalMissionType mission,
        HHToolsCrimeBoss? eliminationTarget,
        out string disabledReason)
    {
        disabledReason = null;
        if (settlement?.Faction == null
            || caravan == null
            || CaravanVisitUtility.SettlementVisitedNow(caravan) != settlement)
        {
            disabledReason = "A player caravan must be visiting this settlement.";
            return false;
        }

        HHToolsFactionPoliticalState state = GetOrCreateState(settlement.Faction);
        if (state == null)
        {
            disabledReason = "This faction has no political mission system.";
            return false;
        }

        ClearInvalidActiveMission(state);
        if (state.activePoliticalMissionSite != null)
        {
            disabledReason =
                $"Active mission: {HHToolsFactionPoliticsUtility.GetMissionTitle(state.activePoliticalMissionSite.missionType)}";
            return false;
        }

        int currentTick = Find.TickManager.TicksGame;
        if (state.nextPoliticalMissionTick > currentTick)
        {
            disabledReason =
                $"New missions available in {TicksToReadableTime(state.nextPoliticalMissionTick - currentTick)}.";
            return false;
        }

        HHToolsCivilizedParty? civilizedSponsor =
            HHToolsFactionPoliticsUtility.GetCivilizedSponsor(mission);
        HHToolsCrimeBoss? authoritarianSponsor =
            HHToolsFactionPoliticsUtility.GetAuthoritarianSponsor(mission);

        if (civilizedSponsor.HasValue)
        {
            if (state.system != HHToolsFactionPoliticalSystem.Civilized)
            {
                disabledReason = "This mission does not belong to this political system.";
                return false;
            }

            if (state.civilizedControlLocked)
            {
                disabledReason = "Parliamentary control is already consolidated.";
                return false;
            }
        }
        else if (authoritarianSponsor.HasValue)
        {
            if (state.system != HHToolsFactionPoliticalSystem.Authoritarian)
            {
                disabledReason = "This mission does not belong to this political system.";
                return false;
            }

            HHToolsCrimeBossState bossState = state.GetCrimeBoss(authoritarianSponsor.Value);
            if (bossState is not { eliminated: false })
            {
                disabledReason = "This family has been eliminated.";
                return false;
            }
        }
        else if (mission == HHToolsPoliticalMissionType.FamilyElimination)
        {
            if (!eliminationTarget.HasValue
                || !CanEliminateBoss(settlement.Faction, eliminationTarget.Value))
            {
                disabledReason = state.ActiveCrimeBossCount <= 1
                    ? "The last remaining family cannot be eliminated."
                    : "An elimination operation is not available.";
                return false;
            }
        }
        else
        {
            disabledReason = "Unknown political mission.";
            return false;
        }

        return true;
    }

    public bool TryRequestPoliticalMission(
        Settlement settlement,
        Caravan caravan,
        HHToolsPoliticalMissionType mission,
        HHToolsCrimeBoss? eliminationTarget,
        out string failureReason)
    {
        if (!CanRequestPoliticalMission(
                settlement,
                caravan,
                mission,
                eliminationTarget,
                out failureReason))
        {
            return false;
        }

        if (!TileFinder.TryFindNewSiteTile(
                out PlanetTile siteTile,
                settlement.Tile,
                minDist: 2,
                maxDist: 8,
                allowCaravans: false,
                allowedLandmarks: null,
                selectLandmarkChance: 0f,
                canSelectComboLandmarks: false,
                tileFinderMode: TileFinderMode.Near,
                exitOnFirstTileFound: false,
                canBeSpace: false,
                layer: settlement.Tile.Layer,
                validator: null))
        {
            failureReason = "No suitable motel site could be found near this settlement.";
            return false;
        }

        bool eliminationReserved = false;
        if (mission == HHToolsPoliticalMissionType.FamilyElimination)
        {
            eliminationReserved =
                eliminationTarget.HasValue
                && TryBeginBossElimination(settlement.Faction, eliminationTarget.Value);
            if (!eliminationReserved)
            {
                failureReason = "The elimination operation is no longer available.";
                return false;
            }
        }

        try
        {
            QuestScriptDef questDef =
                DefDatabase<QuestScriptDef>.GetNamed("HHTools_PoliticalMotelMission");
            Slate slate = new();
            slate.Set("sourceFaction", settlement.Faction);
            slate.Set("sourceSettlement", settlement);
            slate.Set("missionType", mission);
            slate.Set("eliminationTarget", eliminationTarget ?? default);
            slate.Set("siteTile", siteTile);
            slate.Set("points", Math.Max(350f, StorytellerUtility.DefaultSiteThreatPointsNow()));
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);

            if (quest == null)
            {
                throw new InvalidOperationException("Quest generation returned no quest.");
            }

            Messages.Message(
                $"{HHToolsFactionPoliticsUtility.GetMissionTitle(mission)} is now active near {settlement.LabelCap}.",
                MessageTypeDefOf.PositiveEvent,
                historical: true);
            failureReason = null;
            return true;
        }
        catch (Exception exception)
        {
            HHToolsFactionPoliticalState state = GetOrCreateState(settlement.Faction);
            if (state != null)
            {
                state.activePoliticalMissionSite = null;
            }

            if (eliminationReserved && eliminationTarget.HasValue)
            {
                CancelBossElimination(settlement.Faction, eliminationTarget.Value);
            }

            Log.Error($"[FIP H&H Tools] Could not start political motel mission: {exception}");
            failureReason = "The mission could not be generated. See the error log for details.";
            return false;
        }
    }

    public void RegisterPoliticalMission(HHToolsMotelMissionSite site)
    {
        if (site?.sourceFaction == null)
        {
            return;
        }

        HHToolsFactionPoliticalState state = GetOrCreateState(site.sourceFaction);
        if (state == null)
        {
            return;
        }

        state.activePoliticalMissionSite = site;
        state.lastPoliticalMission = site.missionType;
    }

    public void NotifyPoliticalMissionResolved(HHToolsMotelMissionSite site, bool success)
    {
        if (site?.sourceFaction == null)
        {
            return;
        }

        HHToolsFactionPoliticalState state = GetOrCreateState(site.sourceFaction);
        if (state == null)
        {
            return;
        }

        if (success)
        {
            HHToolsCivilizedParty? party =
                HHToolsFactionPoliticsUtility.GetCivilizedSponsor(site.missionType);
            HHToolsCrimeBoss? boss =
                HHToolsFactionPoliticsUtility.GetAuthoritarianSponsor(site.missionType);

            if (party.HasValue)
            {
                RecordCivilizedMissionSuccess(site.sourceFaction, party.Value);
            }
            else if (boss.HasValue)
            {
                RecordAuthoritarianMissionSuccess(site.sourceFaction, boss.Value);
            }
            else if (site.missionType == HHToolsPoliticalMissionType.FamilyElimination)
            {
                RecordBossEliminated(site.sourceFaction, site.eliminationTarget);
            }
        }
        else if (site.missionType == HHToolsPoliticalMissionType.FamilyElimination)
        {
            CancelBossElimination(site.sourceFaction, site.eliminationTarget);
        }

        if (state.activePoliticalMissionSite == site)
        {
            state.activePoliticalMissionSite = null;
        }

        state.lastPoliticalMission = site.missionType;
        state.nextPoliticalMissionTick =
            Find.TickManager.TicksGame + HHToolsFactionPoliticsUtility.PoliticalMissionCooldownTicks;
    }

    public string GetPoliticalMissionAvailability(HHToolsFactionPoliticalState state)
    {
        if (state == null)
        {
            return string.Empty;
        }

        ClearInvalidActiveMission(state);
        if (state.activePoliticalMissionSite != null)
        {
            return $"Active mission: {HHToolsFactionPoliticsUtility.GetMissionTitle(state.activePoliticalMissionSite.missionType)}";
        }

        int ticksRemaining = state.nextPoliticalMissionTick - Find.TickManager.TicksGame;
        return ticksRemaining > 0
            ? $"Next mission in {TicksToReadableTime(ticksRemaining)}"
            : "A new mission can be requested while visiting this settlement.";
    }

    private static string TicksToReadableTime(int ticks)
    {
        float hours = Math.Max(0f, ticks / 2500f);
        return hours >= 1f ? $"{hours:0.#} h" : $"{Math.Max(1, ticks / 42)} min";
    }

    private static void ClearInvalidActiveMission(HHToolsFactionPoliticalState state)
    {
        if (state?.activePoliticalMissionSite == null)
        {
            return;
        }

        HHToolsMotelMissionSite site = state.activePoliticalMissionSite;
        if (!site.Destroyed && !site.resolved)
        {
            return;
        }

        state.activePoliticalMissionSite = null;
        state.nextPoliticalMissionTick = Math.Max(
            state.nextPoliticalMissionTick,
            Find.TickManager.TicksGame + HHToolsFactionPoliticsUtility.PoliticalMissionCooldownTicks);
    }
}

public class QuestNode_Root_HHToolsPoliticalMotelMission : QuestNode
{
    protected override bool TestRunInt(Slate slate)
    {
        return slate.TryGet("sourceFaction", out Faction sourceFaction)
            && sourceFaction != null
            && slate.TryGet("sourceSettlement", out Settlement sourceSettlement)
            && sourceSettlement != null
            && slate.TryGet("missionType", out HHToolsPoliticalMissionType mission)
            && mission != HHToolsPoliticalMissionType.None
            && slate.TryGet("siteTile", out PlanetTile siteTile)
            && siteTile.Valid;
    }

    protected override void RunInt()
    {
        Slate slate = QuestGen.slate;
        Faction sourceFaction = slate.Get<Faction>("sourceFaction");
        Settlement sourceSettlement = slate.Get<Settlement>("sourceSettlement");
        HHToolsPoliticalMissionType mission =
            slate.Get<HHToolsPoliticalMissionType>("missionType");
        HHToolsCrimeBoss eliminationTarget =
            slate.Get("eliminationTarget", default(HHToolsCrimeBoss));
        PlanetTile siteTile = slate.Get<PlanetTile>("siteTile");
        float points = slate.Get("points", 350f);

        SitePartDef sitePartDef =
            DefDatabase<SitePartDef>.GetNamed("HHTools_AbandonedMotel");
        WorldObjectDef worldObjectDef =
            DefDatabase<WorldObjectDef>.GetNamed("HHTools_AbandonedMotelMissionSite");
        SitePartParams sitePartParams = new()
        {
            threatPoints = points,
            points = points,
            randomValue = (int)mission
        };

        HHToolsMotelMissionSite site = (HHToolsMotelMissionSite)QuestGen_Sites.GenerateSite(
            [new SitePartDefWithParams(sitePartDef, sitePartParams)],
            siteTile,
            faction: null,
            hiddenSitePartsPossible: false,
            singleSitePartRules: null,
            worldObjectDef: worldObjectDef);

        site.customLabel = "Abandoned roadside motel";
        site.sourceFaction = sourceFaction;
        site.sourceSettlement = sourceSettlement;
        site.missionType = mission;
        site.eliminationTarget = eliminationTarget;
        site.quest = QuestGen.quest;
        site.threatPoints = points;
        site.expirationTick = Find.TickManager.TicksGame + 15 * 60000;

        string missionTitle = mission == HHToolsPoliticalMissionType.FamilyElimination
            ? "Eliminate the " + HHToolsFactionPoliticsUtility.GetFamilyLabel(eliminationTarget)
            : HHToolsFactionPoliticsUtility.GetMissionTitle(mission);
        string missionDescription = mission == HHToolsPoliticalMissionType.FamilyElimination
            ? "The "
                + HHToolsFactionPoliticsUtility.GetFamilyLabel(eliminationTarget)
                + " boss and a hardened combat crew are meeting at the motel. "
                + "Eliminate or drive off the entire force to remove this family permanently."
            : HHToolsFactionPoliticsUtility.GetMissionDescription(mission);

        string fullDescription =
            missionDescription
            + "\n\n"
            + HHToolsFactionPoliticsTracker.Instance.GetQuestStatusSummary(sourceFaction)
            + "\n\nMission origin: "
            + sourceSettlement.LabelCap
            + ".";
        QuestGen.AddQuestNameRules([new Rule_String("questName", missionTitle)]);
        QuestGen.AddQuestDescriptionRules(
            [new Rule_String("questDescription", fullDescription)]);
        QuestGen.quest.name = missionTitle;
        QuestGen.quest.description = fullDescription;

        QuestGen_Sites.SpawnWorldObject(
            QuestGen.quest,
            site,
            defsToExcludeFromHyperlinks: null,
            QuestGen.quest.InitiateSignal);

        QuestGen.quest.AddPart(new QuestPart_HHToolsPoliticalMotelMission
        {
            site = site,
            sourceFaction = sourceFaction
        });

        HHToolsFactionPoliticsTracker.Instance.RegisterPoliticalMission(site);
    }
}

public class QuestPart_HHToolsPoliticalMotelMission : QuestPart
{
    public HHToolsMotelMissionSite site;
    public Faction sourceFaction;

    public override IEnumerable<Faction> InvolvedFactions
    {
        get
        {
            if (sourceFaction != null)
            {
                yield return sourceFaction;
            }
        }
    }

    public override void Cleanup()
    {
        if (site is { resolved: false, Destroyed: false })
        {
            site.ResolveMission(success: false, "The mission was abandoned.");
        }

        base.Cleanup();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref site, "site");
        Scribe_References.Look(ref sourceFaction, "sourceFaction");
    }
}
