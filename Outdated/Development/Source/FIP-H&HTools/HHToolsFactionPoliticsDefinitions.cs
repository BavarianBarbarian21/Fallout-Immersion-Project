using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP.HHTools;

public enum HHToolsFactionPoliticalSystem
{
    None,
    Civilized,
    Authoritarian,
    Raider,
    Tribal
}

public enum HHToolsCivilizedParty
{
    BrahminBarons,
    DesertRangers,
    Caravans
}

public enum HHToolsCrimeBoss
{
    Weapons,
    Arena,
    Drugs,
    Slaves
}

public enum HHToolsPoliticalMissionType
{
    None,
    RangerBounty,
    RangerRescue,
    BrahminRecoverHerd,
    BrahminDefendHerd,
    CaravanEscort,
    CaravanRecoverCargo,
    GunRunnerSchematics,
    GunRunnerDefense,
    ArenaManhunters,
    ArenaRivals,
    SlaverDefense,
    SlaverRaid,
    SaloonAlcohol,
    SaloonDealers,
    FamilyElimination
}

public class HHToolsFactionPoliticsExtension : DefModExtension
{
    public HHToolsFactionPoliticalSystem system = HHToolsFactionPoliticalSystem.None;
    public PawnKindDef representativePawnKind;
}

public class HHToolsPoliticalMissionSettingsDef : Def
{
    public PawnKindDef herdAnimalKind;
}

public static class HHToolsFactionPoliticsUtility
{
    public const int FavorsRequiredPerFamily = 5;
    public const int CivilizedMajorityThreshold = 50;
    public const int PoliticalMissionCooldownTicks = 60000;
    public const float ConsolidatedTradeBonus = 0.10f;
    public const float SoleFamilyTradeBonus = 0.30f;

    public static readonly Dictionary<HHToolsCivilizedParty, string> CivilizedGroupLabels = new()
    {
        { HHToolsCivilizedParty.BrahminBarons, "Brahmin Barons" },
        { HHToolsCivilizedParty.DesertRangers, "Desert Rangers" },
        { HHToolsCivilizedParty.Caravans, "Caravans" }
    };

    public static readonly Dictionary<HHToolsCrimeBoss, string> CrimeBossTitles = new()
    {
        { HHToolsCrimeBoss.Slaves, "Slave Master" },
        { HHToolsCrimeBoss.Drugs, "Saloon Owner" },
        { HHToolsCrimeBoss.Arena, "Arena Champion" },
        { HHToolsCrimeBoss.Weapons, "Gun Runner" }
    };

    public static readonly Dictionary<HHToolsCrimeBoss, string> CrimeFamilyLabels = new()
    {
        { HHToolsCrimeBoss.Slaves, "Slavers" },
        { HHToolsCrimeBoss.Drugs, "Saloon" },
        { HHToolsCrimeBoss.Arena, "Arena" },
        { HHToolsCrimeBoss.Weapons, "Gun Runners" }
    };

    public static readonly Dictionary<HHToolsCrimeBoss, string> CrimeFamilyTradeCategories = new()
    {
        { HHToolsCrimeBoss.Slaves, "slaves" },
        { HHToolsCrimeBoss.Drugs, "alcohol and chems" },
        { HHToolsCrimeBoss.Arena, "animals" },
        { HHToolsCrimeBoss.Weapons, "weapons" }
    };

    public static readonly Dictionary<HHToolsCivilizedParty, string> CivilizedBenefitDescriptions = new()
    {
        {
            HHToolsCivilizedParty.BrahminBarons,
            "A food and basic-resource shipment arrives every 30 days."
        },
        {
            HHToolsCivilizedParty.DesertRangers,
            "Ranger responders can reinforce a colony during a serious raid. Cooldown: 15 days."
        },
        {
            HHToolsCivilizedParty.Caravans,
            "Settlement trade prices with this faction improve by 10%."
        }
    };

    public static readonly Dictionary<HHToolsCivilizedParty, string> CivilizedLeaderFallbackNames = new()
    {
        { HHToolsCivilizedParty.BrahminBarons, "Baron Regent" },
        { HHToolsCivilizedParty.DesertRangers, "Chief Ranger" },
        { HHToolsCivilizedParty.Caravans, "Caravan Speaker" }
    };

    public static readonly Dictionary<HHToolsCrimeBoss, string> CrimeBossLeaderFallbackNames = new()
    {
        { HHToolsCrimeBoss.Weapons, "Gun Runner Boss" },
        { HHToolsCrimeBoss.Arena, "Arena Champion" },
        { HHToolsCrimeBoss.Drugs, "Saloon Owner" },
        { HHToolsCrimeBoss.Slaves, "Slave Master" }
    };

    public static HHToolsFactionPoliticsExtension GetPoliticsExtension(this Faction faction)
    {
        return faction?.def?.GetModExtension<HHToolsFactionPoliticsExtension>();
    }

    public static bool UsesFactionPolitics(this Faction faction)
    {
        HHToolsFactionPoliticsExtension extension = faction.GetPoliticsExtension();
        return extension is { system: not HHToolsFactionPoliticalSystem.None };
    }

    public static PawnKindDef ResolveRepresentativePawnKind(this Faction faction, HHToolsFactionPoliticalSystem system)
    {
        PawnKindDef explicitPawnKind = faction.GetPoliticsExtension()?.representativePawnKind;
        if (explicitPawnKind != null)
        {
            return explicitPawnKind;
        }

        string defName = system switch
        {
            HHToolsFactionPoliticalSystem.Civilized => "HHTools_Settlement_Leader",
            HHToolsFactionPoliticalSystem.Authoritarian => "HHTools_Raider_Leader",
            HHToolsFactionPoliticalSystem.Raider => "HHTools_Raider_Leader",
            HHToolsFactionPoliticalSystem.Tribal => "HHTools_Tribal_Leader",
            _ => null
        };

        return defName == null ? null : DefDatabase<PawnKindDef>.GetNamedSilentFail(defName);
    }

    public static string GetGroupLabel(HHToolsCivilizedParty party)
    {
        return CivilizedGroupLabels[party];
    }

    public static string GetBossTitle(HHToolsCrimeBoss boss)
    {
        return CrimeBossTitles[boss];
    }

    public static string GetFamilyLabel(HHToolsCrimeBoss boss)
    {
        return CrimeFamilyLabels[boss];
    }

    public static string GetFamilyTradeCategory(HHToolsCrimeBoss boss)
    {
        return CrimeFamilyTradeCategories[boss];
    }

    public static string GetCivilizedBenefitDescription(HHToolsCivilizedParty party)
    {
        return CivilizedBenefitDescriptions[party];
    }

    public static string GetFallbackLeaderName(HHToolsCivilizedParty party)
    {
        return CivilizedLeaderFallbackNames[party];
    }

    public static string GetFallbackLeaderName(HHToolsCrimeBoss boss)
    {
        return CrimeBossLeaderFallbackNames[boss];
    }

    public static string GetFamilyTraderKindDefName(HHToolsCrimeBoss boss, bool soleFamily)
    {
        string suffix = boss switch
        {
            HHToolsCrimeBoss.Weapons => "Weapons",
            HHToolsCrimeBoss.Arena => "Arena",
            HHToolsCrimeBoss.Drugs => "Saloon",
            HHToolsCrimeBoss.Slaves => "Slaves",
            _ => null
        };

        return suffix == null
            ? null
            : $"HHTools_FamilyTrader_{suffix}{(soleFamily ? "_Sole" : string.Empty)}";
    }

    public static IReadOnlyList<HHToolsPoliticalMissionType> GetMissions(HHToolsCivilizedParty party)
    {
        return party switch
        {
            HHToolsCivilizedParty.DesertRangers =>
            [
                HHToolsPoliticalMissionType.RangerBounty,
                HHToolsPoliticalMissionType.RangerRescue
            ],
            HHToolsCivilizedParty.BrahminBarons =>
            [
                HHToolsPoliticalMissionType.BrahminRecoverHerd,
                HHToolsPoliticalMissionType.BrahminDefendHerd
            ],
            HHToolsCivilizedParty.Caravans =>
            [
                HHToolsPoliticalMissionType.CaravanEscort,
                HHToolsPoliticalMissionType.CaravanRecoverCargo
            ],
            _ => []
        };
    }

    public static IReadOnlyList<HHToolsPoliticalMissionType> GetMissions(HHToolsCrimeBoss boss)
    {
        return boss switch
        {
            HHToolsCrimeBoss.Weapons =>
            [
                HHToolsPoliticalMissionType.GunRunnerSchematics,
                HHToolsPoliticalMissionType.GunRunnerDefense
            ],
            HHToolsCrimeBoss.Arena =>
            [
                HHToolsPoliticalMissionType.ArenaManhunters,
                HHToolsPoliticalMissionType.ArenaRivals
            ],
            HHToolsCrimeBoss.Slaves =>
            [
                HHToolsPoliticalMissionType.SlaverDefense,
                HHToolsPoliticalMissionType.SlaverRaid
            ],
            HHToolsCrimeBoss.Drugs =>
            [
                HHToolsPoliticalMissionType.SaloonAlcohol,
                HHToolsPoliticalMissionType.SaloonDealers
            ],
            _ => []
        };
    }

    public static string GetMissionTitle(HHToolsPoliticalMissionType mission)
    {
        return mission switch
        {
            HHToolsPoliticalMissionType.RangerBounty => "Ranger bounty",
            HHToolsPoliticalMissionType.RangerRescue => "Missing ranger patrol",
            HHToolsPoliticalMissionType.BrahminRecoverHerd => "Recover the herd",
            HHToolsPoliticalMissionType.BrahminDefendHerd => "Defend the herd",
            HHToolsPoliticalMissionType.CaravanEscort => "Escort through the ruins",
            HHToolsPoliticalMissionType.CaravanRecoverCargo => "Lost caravan cargo",
            HHToolsPoliticalMissionType.GunRunnerSchematics => "Steal the weapon schematics",
            HHToolsPoliticalMissionType.GunRunnerDefense => "Defend the gun runners",
            HHToolsPoliticalMissionType.ArenaManhunters => "Cull the manhunters",
            HHToolsPoliticalMissionType.ArenaRivals => "Break the rival arena crew",
            HHToolsPoliticalMissionType.SlaverDefense => "Defend the slaver camp",
            HHToolsPoliticalMissionType.SlaverRaid => "Clear the slave camp",
            HHToolsPoliticalMissionType.SaloonAlcohol => "Recover the liquor shipment",
            HHToolsPoliticalMissionType.SaloonDealers => "Remove the rival dealers",
            HHToolsPoliticalMissionType.FamilyElimination => "Eliminate the family",
            _ => "Political mission"
        };
    }

    public static string GetMissionDescription(HHToolsPoliticalMissionType mission)
    {
        return mission switch
        {
            HHToolsPoliticalMissionType.RangerBounty =>
                "A wanted gang is using an abandoned roadside motel as a hideout. Neutralize every member of the gang.",
            HHToolsPoliticalMissionType.RangerRescue =>
                "A ranger patrol disappeared while searching an abandoned motel. Find the patrol and eliminate its attackers before the surviving rangers are lost.",
            HHToolsPoliticalMissionType.BrahminRecoverHerd =>
                "Raiders drove a herd into the ruins around an abandoned motel. Defeat them and return at least two of the three animals to the settlement that issued the mission.",
            HHToolsPoliticalMissionType.BrahminDefendHerd =>
                "The Barons are holding a herd at an old motel when raiders strike. Defeat the attackers while keeping at least two animals alive.",
            HHToolsPoliticalMissionType.CaravanEscort =>
                "A caravan must pass the abandoned motel, but an ambush blocks the route. Break the ambush so the surviving caravan guards can leave the map.",
            HHToolsPoliticalMissionType.CaravanRecoverCargo =>
                "A caravan lost a marked cargo crate at an abandoned motel. Recover the crate and return it to the settlement that issued the mission.",
            HHToolsPoliticalMissionType.GunRunnerSchematics =>
                "A rival outfit has hidden valuable weapon schematics in the motel. Steal them and return them to the settlement that issued the mission.",
            HHToolsPoliticalMissionType.GunRunnerDefense =>
                "A gun runner crew is trapped at the motel. Defeat the attackers before the entire crew is wiped out.",
            HHToolsPoliticalMissionType.ArenaManhunters =>
                "A pack of manhunters nests around the motel. Kill every animal; the arena handlers will collect any young later.",
            HHToolsPoliticalMissionType.ArenaRivals =>
                "A rival arena crew occupies the motel. Kill, capture, or drive off every fighter still able to stand.",
            HHToolsPoliticalMissionType.SlaverDefense =>
                "A slaver crew at the motel is under attack. Defeat the attackers before the whole crew is destroyed.",
            HHToolsPoliticalMissionType.SlaverRaid =>
                "A slave camp has been established in the motel ruins. Kill, capture, down, or drive off every slaver still able to stand.",
            HHToolsPoliticalMissionType.SaloonAlcohol =>
                "A marked liquor shipment vanished at an abandoned motel. Recover it and return it to the settlement that issued the mission.",
            HHToolsPoliticalMissionType.SaloonDealers =>
                "Rival dealers are operating from the motel. Kill, capture, down, or drive off the entire crew.",
            HHToolsPoliticalMissionType.FamilyElimination =>
                "The family boss and a hardened combat crew are meeting at the motel. Eliminate or drive off the entire force to remove this family permanently.",
            _ => "Complete the mission at the abandoned motel."
        };
    }

    public static bool RequiresReturnToSettlement(HHToolsPoliticalMissionType mission)
    {
        return mission is HHToolsPoliticalMissionType.BrahminRecoverHerd
            or HHToolsPoliticalMissionType.CaravanRecoverCargo
            or HHToolsPoliticalMissionType.GunRunnerSchematics
            or HHToolsPoliticalMissionType.SaloonAlcohol;
    }

    public static HHToolsCivilizedParty? GetCivilizedSponsor(HHToolsPoliticalMissionType mission)
    {
        return mission switch
        {
            HHToolsPoliticalMissionType.RangerBounty or HHToolsPoliticalMissionType.RangerRescue =>
                HHToolsCivilizedParty.DesertRangers,
            HHToolsPoliticalMissionType.BrahminRecoverHerd or HHToolsPoliticalMissionType.BrahminDefendHerd =>
                HHToolsCivilizedParty.BrahminBarons,
            HHToolsPoliticalMissionType.CaravanEscort or HHToolsPoliticalMissionType.CaravanRecoverCargo =>
                HHToolsCivilizedParty.Caravans,
            _ => null
        };
    }

    public static HHToolsCrimeBoss? GetAuthoritarianSponsor(HHToolsPoliticalMissionType mission)
    {
        return mission switch
        {
            HHToolsPoliticalMissionType.GunRunnerSchematics or HHToolsPoliticalMissionType.GunRunnerDefense =>
                HHToolsCrimeBoss.Weapons,
            HHToolsPoliticalMissionType.ArenaManhunters or HHToolsPoliticalMissionType.ArenaRivals =>
                HHToolsCrimeBoss.Arena,
            HHToolsPoliticalMissionType.SlaverDefense or HHToolsPoliticalMissionType.SlaverRaid =>
                HHToolsCrimeBoss.Slaves,
            HHToolsPoliticalMissionType.SaloonAlcohol or HHToolsPoliticalMissionType.SaloonDealers =>
                HHToolsCrimeBoss.Drugs,
            _ => null
        };
    }

    public static PawnKindDef GetHerdAnimalKind()
    {
        return DefDatabase<HHToolsPoliticalMissionSettingsDef>
                   .GetNamedSilentFail("HHTools_PoliticalMissionSettings")
                   ?.herdAnimalKind
            ?? DefDatabase<PawnKindDef>.GetNamed("Cow");
    }
}
