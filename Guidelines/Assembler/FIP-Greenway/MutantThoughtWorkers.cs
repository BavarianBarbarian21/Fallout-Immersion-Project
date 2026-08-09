using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.Greenway;

internal static class MutantPolicyUtility
{
    private static readonly Dictionary<string, string> RequiredPreceptDefNamesByThoughtDefName = new()
    {
        ["Greenway_MutantCitizenship_ColonistPresent"] = "Greenway_Precept_MutantCitizenship",
        ["Greenway_SecondClassMutants_MutantSlavePresent"] = "Greenway_Precept_SecondClassMutants",
        ["Greenway_MutantSlaves_SlaveStatus"] = "Greenway_Precept_MutantSlaves",
        ["Greenway_MutantSlaves_MutantColonistPresent"] = "Greenway_Precept_MutantSlaves",
        ["Greenway_MutantPurge_MutantColonistPresent"] = "Greenway_Precept_MutantPurge",
        ["Greenway_MutantPurge_MutantSlavePresent"] = "Greenway_Precept_MutantPurge",
        ["Greenway_MutantMasters_MutantColonistPresent"] = "Greenway_Precept_MutantMasters",
        ["Greenway_MutantMasters_NonMutantSlavePresent"] = "Greenway_Precept_MutantMasters",
        ["Greenway_Utobitha_MutantColonistPresent"] = "Greenway_Precept_Utobitha",
        ["Greenway_Utobitha_NonMutantPresent"] = "Greenway_Precept_Utobitha"
    };

    private static readonly HashSet<string> MutantXenotypeDefNames =
    [
        "WestTek_Xenotype_SuperMutant",
        "WestTek_Xenotype_Numen",
        "WestTek_Xenotype_Ghoul",
        "FCP_Xenotype_Ghoul",
        "WestTek_Xenotype_SLanter"
    ];

    public static IEnumerable<Pawn> PlayerFactionPawns()
    {
        if (Faction.OfPlayer is null)
        {
            yield break;
        }

        foreach (Map map in Find.Maps)
        {
            foreach (Pawn pawn in map.mapPawns.PawnsInFaction(Faction.OfPlayer))
            {
                if (pawn.RaceProps?.Humanlike == true)
                {
                    yield return pawn;
                }
            }
        }
    }

    public static bool HasMutantColonist() => PlayerFactionPawns().Any(IsMutantColonist);

    public static bool HasMutantSlave() => PlayerFactionPawns().Any(IsMutantSlave);

    public static bool HasNonMutantSlave() => PlayerFactionPawns().Any(IsNonMutantSlave);

    public static bool HasNonMutantColonistOrSlave() => PlayerFactionPawns().Any(IsNonMutantColonistOrSlave);

    public static bool PawnHasRequiredPolicy(Pawn pawn, ThoughtDef thoughtDef)
    {
        if (!ModsConfig.IdeologyActive
            || pawn?.Ideo is null
            || thoughtDef is null
            || !RequiredPreceptDefNamesByThoughtDefName.TryGetValue(thoughtDef.defName, out string preceptDefName))
        {
            return false;
        }

        PreceptDef preceptDef = DefDatabase<PreceptDef>.GetNamedSilentFail(preceptDefName);
        return preceptDef != null && pawn.Ideo.HasPrecept(preceptDef);
    }

    private static bool IsMutantColonist(Pawn pawn) => pawn.IsColonist && !pawn.IsSlave && IsMutant(pawn);

    private static bool IsMutantSlave(Pawn pawn) => pawn.IsSlave && IsMutant(pawn);

    private static bool IsNonMutantSlave(Pawn pawn) => pawn.IsSlave && !IsMutant(pawn);

    private static bool IsNonMutantColonistOrSlave(Pawn pawn)
    {
        if (!pawn.IsColonist && !pawn.IsSlave)
        {
            return false;
        }

        return !IsMutant(pawn);
    }

    private static bool IsMutant(Pawn pawn)
    {
        if (!ModsConfig.BiotechActive)
        {
            return false;
        }

        string xenotypeDefName = pawn.genes?.Xenotype?.defName;
        return xenotypeDefName != null && MutantXenotypeDefNames.Contains(xenotypeDefName);
    }
}

public abstract class ThoughtWorker_MutantPolicyBase : ThoughtWorker
{
    protected sealed override ThoughtState CurrentStateInternal(Pawn pawn)
    {
        if (pawn?.Faction != Faction.OfPlayer || !MutantPolicyUtility.PawnHasRequiredPolicy(pawn, def))
        {
            return ThoughtState.Inactive;
        }

        return CurrentStateForPlayerColony();
    }

    protected abstract ThoughtState CurrentStateForPlayerColony();
}

public class ThoughtWorker_Greenway_MutantColonistPresent : ThoughtWorker_MutantPolicyBase
{
    protected override ThoughtState CurrentStateForPlayerColony() => MutantPolicyUtility.HasMutantColonist();
}

public class ThoughtWorker_Greenway_MutantSlavePresent : ThoughtWorker_MutantPolicyBase
{
    protected override ThoughtState CurrentStateForPlayerColony() => MutantPolicyUtility.HasMutantSlave();
}

public class ThoughtWorker_Greenway_MutantSlaveStatus : ThoughtWorker_MutantPolicyBase
{
    protected override ThoughtState CurrentStateForPlayerColony() =>
        MutantPolicyUtility.HasMutantSlave() ? ThoughtState.ActiveAtStage(0) : ThoughtState.ActiveAtStage(1);
}

public class ThoughtWorker_Greenway_NonMutantSlavePresent : ThoughtWorker_MutantPolicyBase
{
    protected override ThoughtState CurrentStateForPlayerColony() => MutantPolicyUtility.HasNonMutantSlave();
}

public class ThoughtWorker_Greenway_NonMutantColonistOrSlavePresent : ThoughtWorker_MutantPolicyBase
{
    protected override ThoughtState CurrentStateForPlayerColony() => MutantPolicyUtility.HasNonMutantColonistOrSlave();
}
