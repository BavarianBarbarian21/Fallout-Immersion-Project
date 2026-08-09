using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace Anthrosonae
{
    public class JobDriver_ChangeFur : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        public override IEnumerable<Toil> MakeNewToils()
        {
            if (ModLister.CheckIdeology("Styling station"))
            {
                yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell).FailOnDespawnedOrNull(TargetIndex.A);
                yield return Toils_General.Do(delegate
                {
                    var furGene = pawn.GetFurGene();
                    Find.WindowStack.Add(new Window_ColorPicker(furGene));
                });
            }
        }
    }
}
