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

public class HHToolsFactionPoliticsExtension : DefModExtension
{
    public HHToolsFactionPoliticalSystem system = HHToolsFactionPoliticalSystem.None;
    public PawnKindDef representativePawnKind;
}

public static class HHToolsFactionPoliticsUtility
{
    public static readonly Dictionary<HHToolsCivilizedParty, string> CivilizedGroupLabels = new()
    {
        { HHToolsCivilizedParty.BrahminBarons, "Brahmin Barons" },
        { HHToolsCivilizedParty.DesertRangers, "Desert Rangers" },
        { HHToolsCivilizedParty.Caravans, "Caravans" }
    };

    public static readonly Dictionary<HHToolsCrimeBoss, string> CrimeBossTitles = new()
    {
        { HHToolsCrimeBoss.Slaves, "Slave Trader" },
        { HHToolsCrimeBoss.Drugs, "Saloon Owner" },
        { HHToolsCrimeBoss.Arena, "Arena Champion" },
        { HHToolsCrimeBoss.Weapons, "Gun Runner" }
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
}