using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public class Window_HHToolsFactionPolitics : Window
{
    private readonly Settlement settlement;
    private readonly HHToolsFactionPoliticalState state;

    public override Vector2 InitialSize => new(1440f, 720f);

    public Window_HHToolsFactionPolitics(Settlement settlement)
    {
        this.settlement = settlement;
        draggable = true;
        doCloseX = true;
        doCloseButton = true;
        absorbInputAroundWindow = true;
        forcePause = true;

        HHToolsFactionPoliticsTracker.Instance?.TryGetState(settlement.Faction, out state);
    }

    public override void DoWindowContents(Rect inRect)
    {
        if (settlement?.Faction == null || state == null)
        {
            Widgets.Label(inRect, "No H&H politics data is available for this settlement.");
            return;
        }

        Rect headerRect = new(inRect.x, inRect.y, inRect.width, 122f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(headerRect.x, headerRect.y, headerRect.width, 34f), settlement.LabelCap);
        Text.Font = GameFont.Small;
        Widgets.Label(
            new Rect(headerRect.x, headerRect.y + 34f, headerRect.width, 26f),
            state.system == HHToolsFactionPoliticalSystem.Civilized
                ? "Parliament of Three Powers"
                : "Four-Family Network");
        Widgets.Label(
            new Rect(headerRect.x, headerRect.y + 62f, headerRect.width, 28f),
            HHToolsFactionPoliticsTracker.Instance?.GetQuestStatusSummary(settlement.Faction) ?? string.Empty);
        Widgets.Label(
            new Rect(headerRect.x, headerRect.y + 90f, headerRect.width, 28f),
            HHToolsFactionPoliticsTracker.Instance?.GetPoliticalMissionAvailability(state) ?? string.Empty);
        Text.Anchor = TextAnchor.UpperLeft;

        Rect contentRect = new(inRect.x, headerRect.yMax + 12f, inRect.width, inRect.height - headerRect.height - 12f);
        switch (state.system)
        {
            case HHToolsFactionPoliticalSystem.Civilized:
                DrawCivilized(contentRect);
                break;
            case HHToolsFactionPoliticalSystem.Authoritarian:
                DrawAuthoritarian(contentRect);
                break;
            default:
                Widgets.Label(contentRect, "This settlement does not use a supported politics screen.");
                break;
        }
    }

    private void DrawCivilized(Rect rect)
    {
        List<HHToolsCivilizedPartyState> parties = state.civilizedParties;
        float cardWidth = (rect.width - 24f) / 3f;
        for (int index = 0; index < parties.Count; index += 1)
        {
            Rect cardRect = new(rect.x + index * (cardWidth + 12f), rect.y, cardWidth, rect.height);
            DrawCardBackground(cardRect);

            HHToolsCivilizedPartyState partyState = parties[index];
            string details = state.civilizedControlLocked
                ? state.civilizedController == partyState.party
                    ? "Consolidated benefit active:\n"
                        + HHToolsFactionPoliticsUtility.GetCivilizedBenefitDescription(partyState.party)
                    : "This party is now opposition. Its benefit is inactive."
                : "Mission result: +2 support for this party and -1 for each rival.\n\n"
                    + $"A party consolidates control above {HHToolsFactionPoliticsUtility.CivilizedMajorityThreshold}%."
                    + "\n\nConsolidated benefit:\n"
                    + HHToolsFactionPoliticsUtility.GetCivilizedBenefitDescription(partyState.party);

            DrawLeaderSection(
                cardRect,
                partyState.leaderPawn,
                partyState.leaderPawn?.Name?.ToStringFull ?? HHToolsFactionPoliticsUtility.GetFallbackLeaderName(partyState.party),
                HHToolsFactionPoliticsUtility.GetGroupLabel(partyState.party),
                GetCivilizedStatusLabel(partyState),
                GetCivilizedStatusColor(partyState),
                details,
                reservedBottom: 52f);

            DrawCivilizedMissionButton(cardRect, partyState);
        }
    }

    private void DrawAuthoritarian(Rect rect)
    {
        List<HHToolsCrimeBossState> bosses = state.crimeBosses.OrderBy(entry => entry.boss).ToList();
        int totalFavors = HHToolsFactionPoliticsTracker.Instance?.TotalFavors(state) ?? 0;
        float cardWidth = (rect.width - 36f) / 4f;

        for (int index = 0; index < bosses.Count; index += 1)
        {
            Rect cardRect = new(rect.x + index * (cardWidth + 12f), rect.y, cardWidth, rect.height);
            DrawCardBackground(cardRect);

            HHToolsCrimeBossState bossState = bosses[index];
            string statusText = GetBossStatusLabel(bossState, totalFavors);
            Color statusColor = GetBossStatusColor(bossState, totalFavors);
            string details = GetBossDetails(bossState);

            DrawLeaderSection(
                cardRect,
                bossState.leaderPawn,
                bossState.leaderPawn?.Name?.ToStringFull ?? HHToolsFactionPoliticsUtility.GetFallbackLeaderName(bossState.boss),
                $"{HHToolsFactionPoliticsUtility.GetFamilyLabel(bossState.boss)} — "
                    + HHToolsFactionPoliticsUtility.GetBossTitle(bossState.boss),
                statusText,
                statusColor,
                details,
                reservedBottom: ShouldShowEliminationButton(bossState) ? 92f : 52f);

            DrawAuthoritarianMissionButtons(cardRect, bossState);
        }
    }

    private static void DrawLeaderSection(
        Rect cardRect,
        Pawn pawn,
        string leaderName,
        string subtitle,
        string statusLabel,
        Color statusColor,
        string details,
        float reservedBottom)
    {
        Rect nameRect = new(cardRect.x + 10f, cardRect.y + 10f, cardRect.width - 20f, 34f);
        Rect subtitleRect = new(cardRect.x + 10f, nameRect.yMax + 2f, cardRect.width - 20f, 28f);
        float portraitHeight = Mathf.Min(235f, cardRect.height * 0.40f);
        Rect portraitRect = new(
            cardRect.x + 35f,
            subtitleRect.yMax + 8f,
            cardRect.width - 70f,
            portraitHeight);
        Rect statusRect = new(cardRect.x + 10f, portraitRect.yMax + 12f, cardRect.width - 20f, 34f);
        Rect detailsRect = new(
            cardRect.x + 14f,
            statusRect.yMax + 12f,
            cardRect.width - 28f,
            cardRect.yMax - statusRect.yMax - 22f - reservedBottom);

        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(nameRect, leaderName);
        Text.Font = GameFont.Small;
        Widgets.Label(subtitleRect, subtitle);

        DrawPortrait(portraitRect, pawn);

        GUI.color = statusColor;
        Widgets.DrawHighlight(statusRect.ContractedBy(-4f));
        GUI.color = Color.white;
        Text.Font = GameFont.Small;
        Widgets.Label(statusRect, statusLabel);
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(detailsRect, details);
    }

    private void DrawCivilizedMissionButton(
        Rect cardRect,
        HHToolsCivilizedPartyState partyState)
    {
        Rect buttonRect = new(
            cardRect.x + 12f,
            cardRect.yMax - 44f,
            cardRect.width - 24f,
            34f);
        DrawMissionMenuButton(
            buttonRect,
            HHToolsFactionPoliticsUtility.GetMissions(partyState.party),
            eliminationTarget: null);
    }

    private void DrawAuthoritarianMissionButtons(
        Rect cardRect,
        HHToolsCrimeBossState bossState)
    {
        if (bossState.eliminated)
        {
            return;
        }

        bool showElimination = ShouldShowEliminationButton(bossState);
        Rect missionRect = new(
            cardRect.x + 12f,
            cardRect.yMax - (showElimination ? 84f : 44f),
            cardRect.width - 24f,
            34f);
        DrawMissionMenuButton(
            missionRect,
            HHToolsFactionPoliticsUtility.GetMissions(bossState.boss),
            eliminationTarget: null);

        if (!showElimination)
        {
            return;
        }

        Rect eliminationRect = new(
            cardRect.x + 12f,
            cardRect.yMax - 44f,
            cardRect.width - 24f,
            34f);
        DrawEliminationButton(eliminationRect, bossState.boss);
    }

    private void DrawMissionMenuButton(
        Rect buttonRect,
        IReadOnlyList<HHToolsPoliticalMissionType> missions,
        HHToolsCrimeBoss? eliminationTarget)
    {
        if (missions == null || missions.Count == 0)
        {
            return;
        }

        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        Caravan caravan = GetVisitingPlayerCaravan();
        string disabledReason = null;
        bool enabled = tracker != null
            && tracker.CanRequestPoliticalMission(
                settlement,
                caravan,
                missions[0],
                eliminationTarget,
                out disabledReason);

        string label = enabled ? "Request mission" : "Mission unavailable";
        if (Widgets.ButtonText(buttonRect, label, active: enabled))
        {
            List<FloatMenuOption> options = missions
                .Select(mission => new FloatMenuOption(
                    HHToolsFactionPoliticsUtility.GetMissionTitle(mission)
                        + " — "
                        + HHToolsFactionPoliticsUtility.GetMissionDescription(mission),
                    () => TryStartMission(mission, eliminationTarget)))
                .ToList();
            Find.WindowStack.Add(new FloatMenu(options));
        }

        if (!enabled && !disabledReason.NullOrEmpty())
        {
            TooltipHandler.TipRegion(buttonRect, disabledReason);
        }
    }

    private void DrawEliminationButton(Rect buttonRect, HHToolsCrimeBoss boss)
    {
        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        Caravan caravan = GetVisitingPlayerCaravan();
        string disabledReason = null;
        bool enabled = tracker != null
            && tracker.CanRequestPoliticalMission(
                settlement,
                caravan,
                HHToolsPoliticalMissionType.FamilyElimination,
                boss,
                out disabledReason);

        if (Widgets.ButtonText(buttonRect, "Eliminate family", active: enabled))
        {
            string familyLabel = HHToolsFactionPoliticsUtility.GetFamilyLabel(boss);
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                $"Begin an elimination operation against the {familyLabel}? "
                + "Their boss and a combat group stronger than an ordinary raid will occupy the motel. "
                + "Success permanently removes their trader, missions, favors, and bonuses.",
                () => TryStartMission(HHToolsPoliticalMissionType.FamilyElimination, boss),
                destructive: true));
        }

        if (!enabled && !disabledReason.NullOrEmpty())
        {
            TooltipHandler.TipRegion(buttonRect, disabledReason);
        }
    }

    private void TryStartMission(
        HHToolsPoliticalMissionType mission,
        HHToolsCrimeBoss? eliminationTarget)
    {
        Caravan caravan = GetVisitingPlayerCaravan();
        if (HHToolsFactionPoliticsTracker.Instance.TryRequestPoliticalMission(
                settlement,
                caravan,
                mission,
                eliminationTarget,
                out string failureReason))
        {
            Close();
            return;
        }

        Messages.Message(
            failureReason ?? "The mission could not be started.",
            MessageTypeDefOf.RejectInput,
            historical: false);
    }

    private Caravan GetVisitingPlayerCaravan()
    {
        return Find.WorldObjects.Caravans.FirstOrDefault(caravan =>
            caravan.IsPlayerControlled
            && CaravanVisitUtility.SettlementVisitedNow(caravan) == settlement);
    }

    private bool ShouldShowEliminationButton(HHToolsCrimeBossState bossState)
    {
        return bossState is { eliminated: false }
            && state.ActiveCrimeBossCount > 1
            && !state.authoritarianControlLocked;
    }

    private static void DrawPortrait(Rect portraitRect, Pawn pawn)
    {
        Widgets.DrawMenuSection(portraitRect);
        if (pawn == null)
        {
            return;
        }

        RenderTexture texture = PortraitsCache.Get(
            pawn,
            portraitRect.size,
            Rot4.South,
            new Vector3(0f, 0f, 0.1f),
            1.3f,
            healthStateOverride: PawnHealthState.Mobile);

        GUI.DrawTexture(portraitRect.ContractedBy(8f), texture, ScaleMode.ScaleToFit);
    }

    private static void DrawCardBackground(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        Widgets.DrawBoxSolid(rect.ContractedBy(2f), new Color(0.12f, 0.12f, 0.12f, 0.9f));
    }

    private string GetCivilizedStatusLabel(HHToolsCivilizedPartyState partyState)
    {
        if (state.civilizedControlLocked)
        {
            return state.civilizedController == partyState.party
                ? $"Ruling Party — {partyState.influence}% Support"
                : $"Opposition — {partyState.influence}% Support";
        }

        return $"{partyState.influence}% Support";
    }

    private Color GetCivilizedStatusColor(HHToolsCivilizedPartyState partyState)
    {
        if (!state.civilizedControlLocked)
        {
            return new Color(0.18f, 0.24f, 0.34f, 0.9f);
        }

        return state.civilizedController == partyState.party
            ? new Color(0.2f, 0.45f, 0.2f, 0.95f)
            : new Color(0.45f, 0.18f, 0.18f, 0.95f);
    }

    private string GetBossStatusLabel(HHToolsCrimeBossState bossState, int totalFavors)
    {
        if (bossState.eliminated)
        {
            return "Eliminated";
        }

        if (state.authoritarianControlLocked && state.authoritarianController == bossState.boss)
        {
            return "Absolute Control";
        }

        if (state.friendOfTheFamilies && totalFavors >= 4)
        {
            return "Family Friend";
        }

        if (bossState.favorGranted)
        {
            return "Favor Secured";
        }

        return $"Favors {bossState.completedMissions}/{HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily}";
    }

    private Color GetBossStatusColor(HHToolsCrimeBossState bossState, int totalFavors)
    {
        if (bossState.eliminated)
        {
            return new Color(0.45f, 0.18f, 0.18f, 0.95f);
        }

        if (state.authoritarianControlLocked && state.authoritarianController == bossState.boss)
        {
            return new Color(0.55f, 0.4f, 0.12f, 0.95f);
        }

        if (state.friendOfTheFamilies && totalFavors >= 4)
        {
            return new Color(0.2f, 0.45f, 0.2f, 0.95f);
        }

        if (bossState.favorGranted)
        {
            return new Color(0.18f, 0.35f, 0.2f, 0.95f);
        }

        return new Color(0.32f, 0.22f, 0.12f, 0.95f);
    }

    private string GetBossDetails(HHToolsCrimeBossState bossState)
    {
        if (bossState.eliminated)
        {
            return "This family is eliminated. Its trader, favors, missions, and bonuses are permanently unavailable.";
        }

        string category = HHToolsFactionPoliticsUtility.GetFamilyTradeCategory(bossState.boss);
        string details = $"Specialist market: {category}.\n\n"
            + "Full family favor: 10% better prices at this family's market.";

        if (state.authoritarianControlLocked
            && state.authoritarianController == bossState.boss)
        {
            details += bossState.favorGranted
                ? "\n\nSole-family bonus active: 30% better prices and expanded specialist stock."
                : "\n\nSole-family bonus unlocks when this family reaches full favor.";
        }
        else if (state.friendOfTheFamilies)
        {
            details += "\n\nAll-family bonus active: coalition reinforcements can support attacks on other settlements.";
        }
        else
        {
            details += "\n\nFull favor with all four families unlocks coalition support for settlement attacks.";
        }

        if (state.eliminationOperationActive)
        {
            details += state.eliminationTarget == bossState.boss
                ? "\n\nAn elimination operation against this family is active."
                : "\n\nAnother family's elimination operation is already active.";
        }

        return details;
    }
}
