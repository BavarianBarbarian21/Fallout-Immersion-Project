using RimWorld;
using Verse;

namespace FIP_RobCo;

public class GenStep_RobCoVaultThreats : GenStep
{
    public override int SeedPart => 18042026;

    public override void Generate(Map map, GenStepParams parms)
    {
        if (map.Parent is RobCoVaultSite site)
        {
            site.EnsureMapInitialized(map);
        }
    }
}