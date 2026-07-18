using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class Gene_WestTekSuperMutantAppearance : Gene
{
    private static BodyTypeDef SuperMutantBodyType =>
        DefDatabase<BodyTypeDef>.GetNamedSilentFail("WestTek_SuperMutant");

    public override void PostMake()
    {
        base.PostMake();
        ApplySuperMutantBody();
    }

    public override void PostAdd()
    {
        base.PostAdd();
        ApplySuperMutantBody();
    }

    public override void Tick()
    {
        base.Tick();
        ApplySuperMutantBody();
    }

    private void ApplySuperMutantBody()
    {
        BodyTypeDef bodyType = SuperMutantBodyType;
        if (!Active
            || bodyType == null
            || pawn?.story == null
            || pawn.story.bodyType == bodyType)
        {
            return;
        }

        pawn.story.bodyType = bodyType;
        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
    }
}
