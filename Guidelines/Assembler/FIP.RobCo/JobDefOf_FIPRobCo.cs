using RimWorld;
using Verse;

namespace FIP_RobCo;

[DefOf]
public static class JobDefOf_FIPRobCo
{
    public static JobDef ReloadAbilityFromMap;
    public static JobDef ReloadMechAbility;

    static JobDefOf_FIPRobCo()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf_FIPRobCo));
    }
}