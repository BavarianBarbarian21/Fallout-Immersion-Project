using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class CompProperties_UseEffectBehemothMutagen : CompProperties_UseEffect
{
    public CompProperties_UseEffectBehemothMutagen()
    {
        compClass = typeof(CompUseEffect_BehemothMutagen);
    }
}

public sealed class CompUseEffect_BehemothMutagen : CompUseEffect
{
    public override AcceptanceReport CanBeUsedBy(Pawn p)
    {
        AcceptanceReport baseReport = base.CanBeUsedBy(p);
        if (!baseReport.Accepted)
        {
            return baseReport;
        }

        if (!WestTekMutationUtility.IsSuperMutant(p))
        {
            return "Only first- and second-generation super mutants can use this mutagen.";
        }

        return true;
    }

    public override void DoEffect(Pawn usedBy)
    {
        if (usedBy == null || !WestTekMutationUtility.IsSuperMutant(usedBy))
        {
            Messages.Message(
                "Only first- and second-generation super mutants can survive a behemoth mutagen.",
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return;
        }

        Map map = usedBy.MapHeld;
        if (map == null)
        {
            Messages.Message(
                "The mutagen must be injected on a map tile.",
                MessageTypeDefOf.RejectInput,
                historical: false
            );
            return;
        }

        base.DoEffect(usedBy);

        IntVec3 position = usedBy.PositionHeld;
        Name originalName = usedBy.Name;

        Pawn behemoth = PawnGenerator.GeneratePawn(WestTekDefOf.WestTek_TameBehemoth, Faction.OfPlayer);

        if (originalName != null)
        {
            behemoth.Name = originalName;
        }

        usedBy.DeSpawnOrDeselect();
        usedBy.Destroy(DestroyMode.Vanish);

        GenSpawn.Spawn(behemoth, position, map, WipeMode.Vanish);

        if (behemoth.training != null)
        {
            foreach (TrainableDef trainableDef in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                if (behemoth.training.CanAssignToTrain(trainableDef).Accepted)
                {
                    behemoth.training.Train(trainableDef, null, complete: true);
                }
            }
        }

        Messages.Message(
            "The mutagen finishes its work by collapsing the subject into a tame behemoth.",
            behemoth,
            MessageTypeDefOf.PositiveEvent
        );
    }
}