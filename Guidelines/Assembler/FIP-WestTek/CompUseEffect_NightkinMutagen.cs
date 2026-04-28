using System.Linq;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public class CompProperties_UseEffectNightkinMutagen : CompProperties_UseEffect
{
    public CompProperties_UseEffectNightkinMutagen()
    {
        compClass = typeof(CompUseEffect_NightkinMutagen);
    }
}

public class CompUseEffect_NightkinMutagen : CompUseEffect
{
    public override void DoEffect(Pawn usedBy)
    {
        base.DoEffect(usedBy);

        if (usedBy == null)
        {
            return;
        }

        if (WestTekDefOf.WestTek_Nightkin != null && !usedBy.health.hediffSet.HasHediff(WestTekDefOf.WestTek_Nightkin))
        {
            usedBy.health.AddHediff(WestTekDefOf.WestTek_Nightkin);
        }

        if (usedBy.story?.traits != null && WestTekDefOf.WestTek_NightkinSchizophrenia != null && usedBy.story.traits.GetTrait(WestTekDefOf.WestTek_NightkinSchizophrenia) == null)
        {
            usedBy.story.traits.GainTrait(new Trait(WestTekDefOf.WestTek_NightkinSchizophrenia));
        }

        if (usedBy.abilities != null && WestTekDefOf.WestTek_NightkinInvisibility != null && !usedBy.abilities.abilities.Any(ability => ability.def == WestTekDefOf.WestTek_NightkinInvisibility))
        {
            usedBy.abilities.GainAbility(WestTekDefOf.WestTek_NightkinInvisibility);
        }

        usedBy.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(usedBy);
    }
}