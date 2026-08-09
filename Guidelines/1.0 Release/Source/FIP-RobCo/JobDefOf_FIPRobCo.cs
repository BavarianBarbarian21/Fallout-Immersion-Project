using RimWorld;
using Verse;

namespace FIP.RobCo;

[DefOf]
public static class JobDefOf_FIPRobCo
{
    public static JobDef RobCo_ReloadAbilityFromMap;
    public static JobDef RobCo_ReloadMechAbility;

    static JobDefOf_FIPRobCo()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf_FIPRobCo));
    }
}