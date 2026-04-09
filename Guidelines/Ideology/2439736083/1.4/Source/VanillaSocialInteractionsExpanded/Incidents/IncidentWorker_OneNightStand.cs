using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace VanillaSocialInteractionsExpanded
{

	public class IncidentWorker_OneNightStand : IncidentWorker
	{
		protected override bool CanFireNowSub(IncidentParms parms)
		{
			if (!VanillaSocialInteractionsExpandedSettings.EnableOneNightStand)
			{
				return false;
			}
			if (!base.CanFireNowSub(parms))
			{
				return false;
			}
			Map map = (Map)parms.target;
			return TryFindNewLovers(map, out _, out _);
		}
		private bool TryFindNewLovers(Map map, out Pawn initiator, out Pawn partner)
		{
			IEnumerable<Pawn> candidates = map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).Where(x => x.RaceProps.Humanlike);
			IEnumerable<Pawn> lovers = GetNewLoversFrom(candidates);
			foreach (Pawn pawn in lovers.InRandomOrder())
			{
				foreach (Pawn otherPawn in lovers.InRandomOrder())
				{
					if (pawn != otherPawn)
					{
						Building_Bed bed1 = pawn.CurrentBed();
						if (bed1 != null && bed1.SleepingSlotsCount > 1)
						{
							initiator = otherPawn;
							partner = pawn;
							return true;
						}
						Building_Bed bed2 = otherPawn.CurrentBed();
						if (bed2 != null && bed2.SleepingSlotsCount > 1)
						{
							initiator = pawn;
							partner = otherPawn;
							return true;
						}
					}
				}
			}
			initiator = null;
			partner = null;
			return false;
		}

		private IEnumerable<Pawn> GetNewLoversFrom(IEnumerable<Pawn> candidates)
		{
			foreach (Pawn pawn in candidates)
			{

				if (pawn.DevelopmentalStage != DevelopmentalStage.Adult)
				{
					continue;
				}
				if (Find.TickManager.TicksGame < pawn.mindState.canLovinTick)
				{
					continue;
				}
				if (!pawn.health.capacities.CanBeAwake)
				{
					continue;
				}
				if (LovePartnerRelationUtility.HasAnyLovePartner(pawn))
				{
					continue;
				}

				{
					continue;
				}
				yield return pawn;
			}
		}
		protected override bool TryExecuteWorker(IncidentParms parms)
		{
			if (!VanillaSocialInteractionsExpandedSettings.EnableOneNightStand)
			{
				return false;
			}
			Map map = (Map)parms.target;
			if (TryFindNewLovers(map, out Pawn initiator, out Pawn partner))
			{
				Job job = JobMaker.MakeJob(VSIE_DefOf.VSIE_OneStandLovin, partner, partner.CurrentBed());
				initiator.jobs.TryTakeOrderedJob(job);
				SendStandardLetter("VSIE.OneNightStandTitle".Translate(), "VSIE.OneNightStandText".Translate(initiator.Named("INITIATOR"), partner.Named("PARTNER"))
					, LetterDefOf.PositiveEvent, parms, new List<Pawn> { initiator, partner });
				return true;
			}
			return false;
		}
	}
}