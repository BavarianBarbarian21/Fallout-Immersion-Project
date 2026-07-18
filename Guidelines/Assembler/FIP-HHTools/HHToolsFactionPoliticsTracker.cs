using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.HHTools;

public partial class HHToolsFactionPoliticsTracker : WorldComponent
{
    public static HHToolsFactionPoliticsTracker Instance { get; private set; }

    private List<HHToolsFactionPoliticalState> factionStates = [];
    private readonly Dictionary<Faction, HHToolsFactionPoliticalState> factionStateByFaction = [];

    public HHToolsFactionPoliticsTracker(World world) : base(world)
    {
        Instance = this;
    }

    public IEnumerable<HHToolsFactionPoliticalState> AllStates => factionStates;

    public override void FinalizeInit(bool fromLoad)
    {
        base.FinalizeInit(fromLoad);
        Instance = this;
        RebuildCache();
        SyncWithWorldFactions();
    }

    public override void WorldComponentTick()
    {
        int currentTick = Find.TickManager.TicksGame;
        if (currentTick % 600 == 0)
        {
            SyncWithWorldFactions();
        }

        if (currentTick % 250 == 0)
        {
            ProcessPoliticalBenefits();
        }
    }

    public bool AppliesTo(Faction faction)
    {
        return faction is { def: not null } && faction.UsesFactionPolitics();
    }

    public HHToolsFactionPoliticalState GetOrCreateState(Faction faction)
    {
        if (!AppliesTo(faction))
        {
            return null;
        }

        if (factionStateByFaction.TryGetValue(faction, out HHToolsFactionPoliticalState existingState))
        {
            existingState.RefreshFactionMetadata();
            return existingState;
        }

        HHToolsFactionPoliticalState state = new(faction, faction.GetPoliticsExtension().system);
        EnsureStateShape(state);
        EnsureLeaderPawns(state);
        factionStates.Add(state);
        factionStateByFaction[faction] = state;
        return state;
    }

    public bool TryGetState(Faction faction, out HHToolsFactionPoliticalState state)
    {
        state = GetOrCreateState(faction);
        if (state != null)
        {
            EnsureLeaderPawns(state);
        }

        return state != null;
    }

    public int TotalFavors(HHToolsFactionPoliticalState state)
    {
        return state?.crimeBosses.Count(current => current is { favorGranted: true, eliminated: false }) ?? 0;
    }

    public string GetQuestStatusSummary(Faction faction)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state == null)
        {
            return string.Empty;
        }

        if (state.system == HHToolsFactionPoliticalSystem.Civilized)
        {
            return "Current support — "
                + string.Join(
                    "; ",
                    state.civilizedParties.Select(current =>
                        $"{HHToolsFactionPoliticsUtility.GetGroupLabel(current.party)}: {current.influence}%"));
        }

        if (state.system == HHToolsFactionPoliticalSystem.Authoritarian)
        {
            return "Current favors — "
                + string.Join(
                    "; ",
                    state.crimeBosses
                        .OrderBy(current => current.boss)
                        .Select(current =>
                            $"{HHToolsFactionPoliticsUtility.GetFamilyLabel(current.boss)}: "
                            + (current.eliminated
                                ? "eliminated"
                                : $"{current.completedMissions}/{HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily}")));
        }

        return string.Empty;
    }

    public float GetSettlementTradeBonus(Faction faction)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        return state is
        {
            system: HHToolsFactionPoliticalSystem.Civilized,
            civilizedControlLocked: true,
            civilizedController: HHToolsCivilizedParty.Caravans
        }
            ? HHToolsFactionPoliticsUtility.ConsolidatedTradeBonus
            : 0f;
    }

    public void RecordCivilizedMissionSuccess(Faction faction, HHToolsCivilizedParty winningParty)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system != HHToolsFactionPoliticalSystem.Civilized || state.civilizedControlLocked)
        {
            return;
        }

        foreach (HHToolsCivilizedPartyState partyState in state.civilizedParties)
        {
            partyState.influence += partyState.party == winningParty ? 2 : -1;
            partyState.influence = Math.Max(0, partyState.influence);
        }

        HHToolsCivilizedPartyState winner = state.GetCivilizedParty(winningParty);
        if (winner is { influence: > HHToolsFactionPoliticsUtility.CivilizedMajorityThreshold })
        {
            state.civilizedControlLocked = true;
            state.civilizedController = winningParty;
        }
    }

    public void RecordAuthoritarianMissionSuccess(Faction faction, HHToolsCrimeBoss boss)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system != HHToolsFactionPoliticalSystem.Authoritarian)
        {
            return;
        }

        HHToolsCrimeBossState bossState = state.GetCrimeBoss(boss);
        if (bossState == null || bossState.eliminated)
        {
            return;
        }

        bossState.completedMissions = Math.Min(
            HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily,
            bossState.completedMissions + 1);
        bossState.favorGranted =
            bossState.completedMissions >= HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily;
        RefreshAuthoritarianOutcome(state);
    }

    public bool CanEliminateBoss(Faction faction, HHToolsCrimeBoss boss)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system != HHToolsFactionPoliticalSystem.Authoritarian)
        {
            return false;
        }

        HHToolsCrimeBossState bossState = state.GetCrimeBoss(boss);
        return bossState is { eliminated: false }
            && state.ActiveCrimeBossCount > 1
            && !state.authoritarianControlLocked
            && !state.eliminationOperationActive;
    }

    public bool TryBeginBossElimination(Faction faction, HHToolsCrimeBoss boss)
    {
        if (!CanEliminateBoss(faction, boss))
        {
            return false;
        }

        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        state.eliminationOperationActive = true;
        state.eliminationTarget = boss;
        return true;
    }

    public void CancelBossElimination(Faction faction, HHToolsCrimeBoss boss)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system != HHToolsFactionPoliticalSystem.Authoritarian
            || !state.eliminationOperationActive
            || state.eliminationTarget != boss)
        {
            return;
        }

        state.eliminationOperationActive = false;
        state.eliminationTarget = default;
    }

    public void RecordBossEliminated(Faction faction, HHToolsCrimeBoss boss)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system != HHToolsFactionPoliticalSystem.Authoritarian)
        {
            return;
        }

        HHToolsCrimeBossState bossState = state.GetCrimeBoss(boss);
        if (bossState == null)
        {
            return;
        }

        if (bossState.eliminated || state.ActiveCrimeBossCount <= 1)
        {
            return;
        }

        bossState.eliminated = true;
        bossState.completedMissions = 0;
        bossState.favorGranted = false;
        HHToolsFamilyTrader.DestroyStock(bossState);

        if (state.eliminationOperationActive && state.eliminationTarget == boss)
        {
            state.eliminationOperationActive = false;
            state.eliminationTarget = default;
        }

        RefreshAuthoritarianOutcome(state);
    }

    public void SetConfederationFormed(Faction faction, bool enabled = true)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system == HHToolsFactionPoliticalSystem.Tribal)
        {
            state.confederationFormed = enabled;
            if (enabled)
            {
                state.protectorateFormed = false;
            }
        }
    }

    public void SetProtectorateFormed(Faction faction, bool enabled = true)
    {
        HHToolsFactionPoliticalState state = GetOrCreateState(faction);
        if (state?.system == HHToolsFactionPoliticalSystem.Tribal)
        {
            state.protectorateFormed = enabled;
            if (enabled)
            {
                state.confederationFormed = false;
            }
        }
    }

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref factionStates, "factionStates", LookMode.Deep);
        ExposeBenefitData();

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            factionStates ??= [];
            RebuildCache();
        }
    }

    private void RebuildCache()
    {
        factionStateByFaction.Clear();
        factionStates ??= [];

        foreach (HHToolsFactionPoliticalState state in factionStates)
        {
            if (state?.faction == null)
            {
                continue;
            }

            state.RefreshFactionMetadata();
            EnsureStateShape(state);
            EnsureLeaderPawns(state);
            factionStateByFaction[state.faction] = state;
        }
    }

    private void SyncWithWorldFactions()
    {
        List<Faction> allFactions = Find.FactionManager.AllFactionsListForReading;

        for (int index = factionStates.Count - 1; index >= 0; index -= 1)
        {
            HHToolsFactionPoliticalState state = factionStates[index];
            if (state?.faction == null || !allFactions.Contains(state.faction) || !AppliesTo(state.faction))
            {
                if (state?.crimeBosses != null)
                {
                    foreach (HHToolsCrimeBossState bossState in state.crimeBosses)
                    {
                        HHToolsFamilyTrader.DestroyStock(bossState);
                    }
                }

                factionStates.RemoveAt(index);
            }
        }

        RebuildCache();

        foreach (Faction faction in allFactions)
        {
            if (AppliesTo(faction))
            {
                GetOrCreateState(faction);
            }
        }

        EnsureSettlementTraderTrackers();
    }

    private void EnsureLeaderPawns(HHToolsFactionPoliticalState state)
    {
        if (state?.faction == null)
        {
            return;
        }

        PawnKindDef representativePawnKind = state.faction.ResolveRepresentativePawnKind(state.system);
        if (representativePawnKind == null)
        {
            return;
        }

        switch (state.system)
        {
            case HHToolsFactionPoliticalSystem.Civilized:
                foreach (HHToolsCivilizedPartyState partyState in state.civilizedParties)
                {
                    if (NeedsRepresentative(partyState.leaderPawn))
                    {
                        partyState.leaderPawn = GenerateRepresentative(state.faction, representativePawnKind);
                    }

                    EnsureRepresentativeOutfit(partyState.leaderPawn);
                }
                break;
            case HHToolsFactionPoliticalSystem.Authoritarian:
                foreach (HHToolsCrimeBossState bossState in state.crimeBosses)
                {
                    if (bossState.eliminated)
                    {
                        continue;
                    }

                    if (NeedsRepresentative(bossState.leaderPawn))
                    {
                        bossState.leaderPawn = GenerateRepresentative(state.faction, representativePawnKind);
                    }

                    EnsureRepresentativeOutfit(bossState.leaderPawn);
                }
                break;
        }
    }

    private static bool NeedsRepresentative(Pawn pawn)
    {
        return pawn == null || pawn.Discarded || pawn.Name == null;
    }

    private static Pawn GenerateRepresentative(Faction faction, PawnKindDef pawnKind)
    {
        PawnGenerationRequest request = new(pawnKind)
        {
            Faction = faction,
            ForceGenerateNewPawn = true
        };

        Pawn pawn = PawnGenerator.GeneratePawn(request);
        if (pawn?.Name == null)
        {
            pawn.Name = PawnBioAndNameGenerator.GeneratePawnName(pawn, NameStyle.Full)
                ?? new NameSingle(faction?.Name ?? pawnKind.label?.CapitalizeFirst() ?? "Leader");
        }

        EnsureRepresentativeOutfit(pawn);

        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        return pawn;
    }

    private static void EnsureRepresentativeOutfit(Pawn pawn)
    {
        if (pawn?.apparel == null)
        {
            return;
        }

        if (!HasOnSkinCoverage(pawn, BodyPartGroupDefOf.Legs))
        {
            TryWearRepresentativeApparel(pawn, "Apparel_Pants");
        }

        if (!HasOnSkinCoverage(pawn, BodyPartGroupDefOf.Torso))
        {
            TryWearRepresentativeApparel(pawn, "Apparel_CollarShirt");
        }
    }

    private static bool HasOnSkinCoverage(Pawn pawn, BodyPartGroupDef bodyPartGroup)
    {
        return pawn.apparel.WornApparel.Any(apparel =>
            apparel.def.apparel != null
            && apparel.def.apparel.layers.Contains(ApparelLayerDefOf.OnSkin)
            && apparel.def.apparel.bodyPartGroups.Contains(bodyPartGroup));
    }

    private static void TryWearRepresentativeApparel(Pawn pawn, string apparelDefName)
    {
        ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(apparelDefName);
        if (apparelDef == null)
        {
            return;
        }

        ThingDef stuffDef = GenStuff.DefaultStuffFor(apparelDef);
        Apparel apparel = (Apparel)ThingMaker.MakeThing(apparelDef, stuffDef);
        PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
        pawn.apparel.Wear(apparel, dropReplacedApparel: false);
    }

    private static void EnsureStateShape(HHToolsFactionPoliticalState state)
    {
        if (state == null)
        {
            return;
        }

        state.civilizedParties ??= [];
        state.crimeBosses ??= [];

        if (state.system == HHToolsFactionPoliticalSystem.Civilized)
        {
            foreach (HHToolsCivilizedParty party in Enum.GetValues(typeof(HHToolsCivilizedParty)))
            {
                if (state.GetCivilizedParty(party) == null)
                {
                    state.civilizedParties.Add(new HHToolsCivilizedPartyState(party, 0));
                }
            }
        }
        else if (state.system == HHToolsFactionPoliticalSystem.Authoritarian)
        {
            foreach (HHToolsCrimeBoss boss in Enum.GetValues(typeof(HHToolsCrimeBoss)))
            {
                if (state.GetCrimeBoss(boss) == null)
                {
                    state.crimeBosses.Add(new HHToolsCrimeBossState(boss));
                }
            }

            RefreshAuthoritarianOutcome(state);
        }
    }

    private static void RefreshAuthoritarianOutcome(HHToolsFactionPoliticalState state)
    {
        if (state?.system != HHToolsFactionPoliticalSystem.Authoritarian)
        {
            return;
        }

        List<HHToolsCrimeBossState> activeBosses = state.ActiveCrimeBosses.ToList();
        state.friendOfTheFamilies = activeBosses.Count == 4
            && state.crimeBosses.Count == 4
            && activeBosses.All(current => current.favorGranted);

        if (activeBosses.Count == 1)
        {
            state.authoritarianControlLocked = true;
            state.authoritarianController = activeBosses[0].boss;
            state.eliminationOperationActive = false;
            state.eliminationTarget = default;
        }
        else
        {
            state.authoritarianControlLocked = false;
            if (state.eliminationOperationActive
                && state.GetCrimeBoss(state.eliminationTarget) is not { eliminated: false })
            {
                state.eliminationOperationActive = false;
                state.eliminationTarget = default;
            }
        }
    }
}
