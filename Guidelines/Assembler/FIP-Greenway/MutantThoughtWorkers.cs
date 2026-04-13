using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.Greenway;

internal static class MutantPolicyUtility
{
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
        if (pawn?.Faction != Faction.OfPlayer)
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