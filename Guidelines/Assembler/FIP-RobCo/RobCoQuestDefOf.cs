using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP_RobCo;

[DefOf]
public static class RobCoQuestDefOf
{
    public static ThingDef RobCo_PlatinumChip;
    public static ResearchProjectDef UltraMechtech;
    public static WorldObjectDef RobCo_CourierCamp;
    public static WorldObjectDef RobCo_VaultSite;
    public static SitePartDef RobCo_AncientVault;
    public static LetterDef RobCo_PlatinumChipOpportunity;

    static RobCoQuestDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(RobCoQuestDefOf));
    }
}