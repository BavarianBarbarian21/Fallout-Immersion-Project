using RimWorld;
using Verse;

namespace VanillaSocialInteractionsExpanded
{
    public class InspirationWorker_FlirtingFrenzy : InspirationWorker
	{
		public override bool InspirationCanOccur(Pawn pawn)
		{
			return base.InspirationCanOccur(pawn) && pawn.DevelopmentalStage == DevelopmentalStage.Adult;
		}
	}
}