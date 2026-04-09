using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Grammar;

namespace VanillaSocialInteractionsExpanded
{
    public class JobGiver_Skygaze : ThinkNode_JobGiver
    {
		protected override Job TryGiveJob(Pawn pawn)
		{
			if (!JoyUtility.EnjoyableOutsideNow(pawn) || pawn.Map.weatherManager.curWeather.rainRate > 0.1f)
			{
				return null;
			}
			if (!TryFindSkygazeCell(pawn.Position, pawn, out IntVec3 result))
			{
				return null;
			}
			var companion = VSIE_Utils.GetCompanion(pawn);
			if (companion != null)
			{
                var job = JobMaker.MakeJob(VSIE_DefOf.VSIE_Skygaze, result, companion);
                return job;
            }
			return null;
		}

		private bool TryFindSkygazeCell(IntVec3 root, Pawn searcher, out IntVec3 result)
		{
            result = root;
            var companion = VSIE_Utils.GetCompanion(searcher);
			if (companion is null) return false;
			Predicate<IntVec3> cellValidator = (IntVec3 c) => !c.Roofed(searcher.Map) && !c.GetTerrain(searcher.Map).avoidWander 
				&& (companion.CurJobDef != VSIE_DefOf.VSIE_Skygaze || companion.CurJob.targetA.Cell.DistanceTo(c) < 5);
			IntVec3 result3;
			Predicate<Region> validator = (Region r) => r.Room.PsychologicallyOutdoors && !r.IsForbiddenEntirely(searcher) 
			&& r.TryFindRandomCellInRegionUnforbidden(searcher, cellValidator, out result3);
			TraverseParms traverseParms = TraverseParms.For(searcher);
			if (!CellFinder.TryFindClosestRegionWith(root.GetRegion(searcher.Map), traverseParms, validator, 300, out Region result2))
			{
				return false;
			}
			return CellFinder.RandomRegionNear(result2, 14, traverseParms, validator, searcher).TryFindRandomCellInRegionUnforbidden(searcher, cellValidator, out result);
		}
	}
}

