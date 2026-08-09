using HarmonyLib;
using Verse;

namespace FIP.WestTek;

[StaticConstructorOnStartup]
internal static class WestTekMod
{
    static WestTekMod()
    {
        new Harmony("FIP.WestTek").PatchAll();
    }
}
