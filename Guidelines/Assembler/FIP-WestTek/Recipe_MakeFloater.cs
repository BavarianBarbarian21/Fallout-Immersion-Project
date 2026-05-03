using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class RecipeWorker_MakeFloater : RecipeWorker
{
    public override void Notify_IterationCompleted(Pawn billDoer, List<Thing> ingredients)
    {
        base.Notify_IterationCompleted(billDoer, ingredients);

        Map map = billDoer?.MapHeld;
        if (map == null)
        {
            return;
        }

        IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(billDoer.PositionHeld, map, 2);
        Pawn floater = PawnGenerator.GeneratePawn(WestTekDefOf.WestTek_Floater, Faction.OfPlayer);
        GenSpawn.Spawn(floater, spawnCell, map, WipeMode.Vanish);

        if (floater.training != null)
        {
            foreach (TrainableDef trainableDef in DefDatabase<TrainableDef>.AllDefsListForReading)
            {
                if (floater.training.CanAssignToTrain(trainableDef).Accepted)
                {
                    floater.training.Train(trainableDef, null, complete: true);
                }
            }
        }

        Messages.Message("A tame floater lurches out of the culture vat and recognizes the colony as its handler.", floater, MessageTypeDefOf.PositiveEvent);
    }
}