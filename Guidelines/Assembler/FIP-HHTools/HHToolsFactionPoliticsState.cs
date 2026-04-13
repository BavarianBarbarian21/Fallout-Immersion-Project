using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

public class HHToolsCrimeBossState : IExposable
{
    public HHToolsCrimeBoss boss;
    public int completedMissions;
    public bool favorGranted;
    public bool eliminated;
    public Pawn leaderPawn;

    public HHToolsCrimeBossState()
    {
    }

    public HHToolsCrimeBossState(HHToolsCrimeBoss boss)
    {
        this.boss = boss;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref boss, "boss");
        Scribe_Values.Look(ref completedMissions, "completedMissions");
        Scribe_Values.Look(ref favorGranted, "favorGranted");
        Scribe_Values.Look(ref eliminated, "eliminated");
        Scribe_References.Look(ref leaderPawn, "leaderPawn");
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
                    new HHToolsCivilizedPartyState(HHToolsCivilizedParty.BrahminBarons, 33),
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

        civilizedParties ??= [];
        crimeBosses ??= [];
    }
}