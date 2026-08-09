using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class Gene_WestTekSuperMutantAppearance : Gene
{
    public override void PostMake()
    {
        base.PostMake();
        ApplySuperMutantBodyType();
    }

    public override void PostAdd()
    {
        base.PostAdd();
        ApplySuperMutantBodyType();
    }

    public override void Tick()
    {
        base.Tick();
        ApplySuperMutantBodyType();
    }

    private void ApplySuperMutantBodyType()
    {
        if (!Active
            || pawn?.story == null
            || pawn.story.bodyType == BodyTypeDefOf.Hulk)
        {
            return;
        }

        // Keep the vanilla Hulk body type so apparel can use its normal Hulk
        // graphics. The custom naked body is supplied by the render patch below.
        pawn.story.bodyType = BodyTypeDefOf.Hulk;
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        PortraitsCache.SetDirty(pawn);
    }
}
