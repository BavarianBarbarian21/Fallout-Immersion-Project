using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.HHTools;

public class HHToolsCivilizedPartyState : IExposable
{
    public HHToolsCivilizedParty party;
    public int influence = 33;
    public Pawn leaderPawn;

    public HHToolsCivilizedPartyState()
    {
    }

    public HHToolsCivilizedPartyState(HHToolsCivilizedParty party, int influence)
    {
        this.party = party;
        this.influence = influence;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref party, "party");
        Scribe_Values.Look(ref influence, "influence", 33);
        Scribe_References.Look(ref leaderPawn, "leaderPawn");
    }
}

public class HHToolsCrimeBossState : IExposable, IThingHolder
{
    public HHToolsCrimeBoss boss;
    public int completedMissions;
    public bool favorGranted;
    public bool eliminated;
    public Pawn leaderPawn;
    public ThingOwner<Thing> tradeStock;
    public int lastTradeStockGenerationTick = -1;
    public string tradeStockTraderKindDefName;

    private List<Pawn> tmpSavedTradePawns = [];

    public IThingHolder ParentHolder => null;

    public HHToolsCrimeBossState()
    {
        tradeStock = new ThingOwner<Thing>(this);
    }

    public HHToolsCrimeBossState(HHToolsCrimeBoss boss)
    {
        this.boss = boss;
        tradeStock = new ThingOwner<Thing>(this);
    }

    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            tmpSavedTradePawns.Clear();
            if (tradeStock != null)
            {
                for (int index = tradeStock.Count - 1; index >= 0; index -= 1)
                {
                    if (tradeStock[index] is Pawn pawn)
                    {
                        tradeStock.Remove(pawn);
                        tmpSavedTradePawns.Add(pawn);
                    }
                }
            }
        }

        Scribe_Values.Look(ref boss, "boss");
        Scribe_Values.Look(ref completedMissions, "completedMissions");
        Scribe_Values.Look(ref favorGranted, "favorGranted");
        Scribe_Values.Look(ref eliminated, "eliminated");
        Scribe_References.Look(ref leaderPawn, "leaderPawn");
        Scribe_Collections.Look(ref tmpSavedTradePawns, "tmpSavedTradePawns", LookMode.Reference);
        Scribe_Deep.Look(ref tradeStock, "tradeStock", this);
        Scribe_Values.Look(ref lastTradeStockGenerationTick, "lastTradeStockGenerationTick", -1);
        Scribe_Values.Look(ref tradeStockTraderKindDefName, "tradeStockTraderKindDefName");

        completedMissions = System.Math.Max(
            0,
            System.Math.Min(
                completedMissions,
                HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily));
        favorGranted = favorGranted || completedMissions >= HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily;
        tradeStock ??= new ThingOwner<Thing>(this);
        tmpSavedTradePawns ??= [];

        if (Scribe.mode == LoadSaveMode.PostLoadInit || Scribe.mode == LoadSaveMode.Saving)
        {
            foreach (Pawn pawn in tmpSavedTradePawns)
            {
                tradeStock.TryAdd(pawn, canMergeWithExistingStacks: false);
            }

            tmpSavedTradePawns.Clear();
        }
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return tradeStock;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, tradeStock);
    }
}

public class HHToolsFactionPoliticalState : IExposable
{
    public Faction faction;
    public string factionDefName;
    public string factionName;
    public HHToolsFactionPoliticalSystem system;
    public List<HHToolsCivilizedPartyState> civilizedParties = [];
    public List<HHToolsCrimeBossState> crimeBosses = [];
    public bool civilizedControlLocked;
    public HHToolsCivilizedParty civilizedController;
    public bool authoritarianControlLocked;
    public HHToolsCrimeBoss authoritarianController;
    public bool friendOfTheFamilies;
    public bool confederationFormed;
    public bool protectorateFormed;
    public bool eliminationOperationActive;
    public HHToolsCrimeBoss eliminationTarget;
    public HHToolsMotelMissionSite activePoliticalMissionSite;
    public HHToolsPoliticalMissionType lastPoliticalMission;
    public int nextPoliticalMissionTick = -1;
    public int nextBrahminDeliveryTick = -1;
    public int nextRangerAidTick = -1;
    public int nextCoalitionAidTick = -1;

    public HHToolsFactionPoliticalState()
    {
    }

    public HHToolsFactionPoliticalState(Faction faction, HHToolsFactionPoliticalSystem system)
    {
        this.faction = faction;
        factionDefName = faction?.def?.defName;
        factionName = faction?.Name;
        this.system = system;

        switch (system)
        {
            case HHToolsFactionPoliticalSystem.Civilized:
                civilizedParties =
                [
                    new HHToolsCivilizedPartyState(HHToolsCivilizedParty.BrahminBarons, 34),
                    new HHToolsCivilizedPartyState(HHToolsCivilizedParty.DesertRangers, 33),
                    new HHToolsCivilizedPartyState(HHToolsCivilizedParty.Caravans, 33)
                ];
                break;
            case HHToolsFactionPoliticalSystem.Authoritarian:
                crimeBosses =
                [
                    new HHToolsCrimeBossState(HHToolsCrimeBoss.Weapons),
                    new HHToolsCrimeBossState(HHToolsCrimeBoss.Arena),
                    new HHToolsCrimeBossState(HHToolsCrimeBoss.Drugs),
                    new HHToolsCrimeBossState(HHToolsCrimeBoss.Slaves)
                ];
                break;
        }
    }

    public HHToolsCivilizedPartyState GetCivilizedParty(HHToolsCivilizedParty party)
    {
        return civilizedParties.FirstOrDefault(state => state.party == party);
    }

    public HHToolsCrimeBossState GetCrimeBoss(HHToolsCrimeBoss boss)
    {
        return crimeBosses.FirstOrDefault(state => state.boss == boss);
    }

    public IEnumerable<HHToolsCrimeBossState> ActiveCrimeBosses =>
        crimeBosses.Where(current => current is { eliminated: false });

    public int ActiveCrimeBossCount => ActiveCrimeBosses.Count();

    public bool HasSoleFavoredFamily =>
        authoritarianControlLocked
        && ActiveCrimeBossCount == 1
        && GetCrimeBoss(authoritarianController) is { eliminated: false, favorGranted: true };

    public void RefreshFactionMetadata()
    {
        factionDefName = faction?.def?.defName ?? factionDefName;
        factionName = faction?.Name ?? factionName;
    }

    public void ExposeData()
    {
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref factionDefName, "factionDefName");
        Scribe_Values.Look(ref factionName, "factionName");
        Scribe_Values.Look(ref system, "system");
        Scribe_Collections.Look(ref civilizedParties, "civilizedParties", LookMode.Deep);
        Scribe_Collections.Look(ref crimeBosses, "crimeBosses", LookMode.Deep);
        Scribe_Values.Look(ref civilizedControlLocked, "civilizedControlLocked");
        Scribe_Values.Look(ref civilizedController, "civilizedController");
        Scribe_Values.Look(ref authoritarianControlLocked, "authoritarianControlLocked");
        Scribe_Values.Look(ref authoritarianController, "authoritarianController");
        Scribe_Values.Look(ref friendOfTheFamilies, "friendOfTheFamilies");
        Scribe_Values.Look(ref confederationFormed, "confederationFormed");
        Scribe_Values.Look(ref protectorateFormed, "protectorateFormed");
        Scribe_Values.Look(ref eliminationOperationActive, "eliminationOperationActive");
        Scribe_Values.Look(ref eliminationTarget, "eliminationTarget");
        Scribe_References.Look(ref activePoliticalMissionSite, "activePoliticalMissionSite");
        Scribe_Values.Look(ref lastPoliticalMission, "lastPoliticalMission");
        Scribe_Values.Look(ref nextPoliticalMissionTick, "nextPoliticalMissionTick", -1);
        Scribe_Values.Look(ref nextBrahminDeliveryTick, "nextBrahminDeliveryTick", -1);
        Scribe_Values.Look(ref nextRangerAidTick, "nextRangerAidTick", -1);
        Scribe_Values.Look(ref nextCoalitionAidTick, "nextCoalitionAidTick", -1);

        civilizedParties ??= [];
        crimeBosses ??= [];
    }
}
