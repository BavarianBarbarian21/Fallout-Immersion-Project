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

        Rect headerRect = new(inRect.x, inRect.y, inRect.width, 70f);
        Text.Anchor = TextAnchor.MiddleCenter;
        Text.Font = GameFont.Medium;
        Widgets.Label(headerRect.TopHalf(), settlement.LabelCap);
        Text.Font = GameFont.Small;
        Widgets.Label(headerRect.BottomHalf(), state.system == HHToolsFactionPoliticalSystem.Civilized ? "Parliament of Three Powers" : "Boss Network");
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
            DrawLeaderSection(
                cardRect,
                partyState.leaderPawn,
                partyState.leaderPawn?.Name?.ToStringFull ?? HHToolsFactionPoliticsUtility.GetFallbackLeaderName(partyState.party),
                HHToolsFactionPoliticsUtility.GetGroupLabel(partyState.party),
                GetCivilizedStatusLabel(partyState),
                GetCivilizedStatusColor(partyState));
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

            DrawLeaderSection(
                cardRect,
                bossState.leaderPawn,
                bossState.leaderPawn?.Name?.ToStringFull ?? HHToolsFactionPoliticsUtility.GetFallbackLeaderName(bossState.boss),
                HHToolsFactionPoliticsUtility.GetBossTitle(bossState.boss),
                statusText,
                statusColor);
        }
    }

    private static void DrawLeaderSection(Rect cardRect, Pawn pawn, string leaderName, string subtitle, string statusLabel, Color statusColor)
    {
        Rect nameRect = new(cardRect.x + 10f, cardRect.y + 10f, cardRect.width - 20f, 34f);
        Rect subtitleRect = new(cardRect.x + 10f, nameRect.yMax + 2f, cardRect.width - 20f, 28f);
        Rect portraitRect = new(cardRect.x + 35f, subtitleRect.yMax + 8f, cardRect.width - 70f, cardRect.height - 170f);
        Rect statusRect = new(cardRect.x + 10f, portraitRect.yMax + 12f, cardRect.width - 20f, 34f);

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
            return state.civilizedController == partyState.party ? "Ruling Party" : "Opposition";
        }

        return $"{partyState.influence}% Seats";
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

        return $"Favor {bossState.completedMissions}/5";
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
}