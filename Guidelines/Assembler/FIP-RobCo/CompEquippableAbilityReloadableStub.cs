using RimWorld;
using Verse;

namespace FIP_RobCo;

public class CompEquippableAbilityReloadable : ThingComp
{
}

public class CompProperties_EquippableAbilityReloadable : CompProperties
{
    public string abilityDef;
    public int maxCharges;
    public SoundDef soundReload;
    public string chargeNoun;
    public ThingDef ammoDef;
    public int ammoCountPerCharge;
    public int baseReloadTicks;

    public CompProperties_EquippableAbilityReloadable()
    {
        compClass = typeof(CompEquippableAbilityReloadable);
    }
}