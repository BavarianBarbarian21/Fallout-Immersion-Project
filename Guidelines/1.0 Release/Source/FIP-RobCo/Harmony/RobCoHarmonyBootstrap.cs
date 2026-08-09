using HarmonyLib;
using Verse;

namespace FIP.RobCo;

[StaticConstructorOnStartup]
internal static class RobCoHarmonyBootstrap
{
    static RobCoHarmonyBootstrap()
    {
        new Harmony("FIP.RobCo.SyntheticPawns").PatchAll();
    }
}