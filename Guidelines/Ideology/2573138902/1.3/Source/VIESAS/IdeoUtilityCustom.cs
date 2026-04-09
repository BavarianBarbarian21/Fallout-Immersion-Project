using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace VIESAS
{
	public static class IdeoUtilityCustom
	{
		[DebugAction("General", "Trigger ideo schism")]
		public static void TriggerIdeoSchism()
		{
			Current.Game.GetComponent<IdeologyTracker>().TrySplitIdeo();
		}
		public static bool CanAdd(MemeDef meme, List<MemeDef> memes, FactionDef forFaction = null)
		{
			if (memes.Contains(meme))
			{
				return false;
			}
			if (forFaction != null && !IsMemeAllowedFor(meme, forFaction))
			{
				return false;
			}
			for (int i = 0; i < memes.Count; i++)
			{
				for (int j = 0; j < meme.exclusionTags.Count; j++)
				{
					if (memes[i].exclusionTags.Contains(meme.exclusionTags[j]))
					{
						return false;
					}
				}
			}
			return true;
		}

		public static List<MemeDef> GenerateRandomMemes(IdeoGenerationParms parms)
		{
			FactionDef forFaction = parms.forFaction;
			bool forPlayerFaction = forFaction != null && forFaction.isPlayer;
			List<MemeDef> memes = new List<MemeDef>();
			bool flag = false;
			if (forFaction != null && forFaction.requiredMemes != null)
			{
				for (int i = 0; i < forFaction.requiredMemes.Count; i++)
				{
					if (forFaction.requiredMemes[i].category == MemeCategory.Normal)
					{
						memes.Add(forFaction.requiredMemes[i]);
					}
				}
			}

			if (forFaction != null && forFaction.structureMemeWeights != null && !flag)
			{
				MemeWeight result2;
				if (forFaction.structureMemeWeights.Where((MemeWeight x) => CanAdd(x.meme, memes, forFaction) && (!AnyIdeoHas(x.meme) || forPlayerFaction)).TryRandomElementByWeight((MemeWeight x) => x.selectionWeight * x.meme.randomizationSelectionWeightFactor, out var result))
				{
					memes.Add(result.meme);
					flag = true;
				}
				else if (forFaction.structureMemeWeights.Where((MemeWeight x) => CanAdd(x.meme, memes, forFaction)).TryRandomElementByWeight((MemeWeight x) => x.selectionWeight * x.meme.randomizationSelectionWeightFactor, out result2))
				{
					memes.Add(result2.meme);
					flag = true;
				}
			}

			foreach (var meme in DefDatabase<MemeDef>.AllDefs.Where((MemeDef x) => x.category == MemeCategory.Normal && CanAdd(x, memes, forFaction)))
			{
				if (!memes.Contains(meme))
                {
					memes.Add(meme);
				}
			}

			return memes.Distinct().ToList();
		}
		private static bool AnyIdeoHas(MemeDef meme)
		{
			if (Find.World == null)
			{
				return false;
			}
			List<Ideo> ideosListForReading = Find.IdeoManager.IdeosListForReading;
			for (int i = 0; i < ideosListForReading.Count; i++)
			{
				if (ideosListForReading[i].memes.Contains(meme))
				{
					return true;
				}
			}
			return false;
		}

		public static bool CanUseIdeo(FactionDef factionDef, Ideo ideo, List<PreceptDef> disallowedPrecepts)
		{
			if (factionDef.allowedCultures != null && !factionDef.allowedCultures.Contains(ideo.culture))
			{
				return false;
			}
			if (factionDef.requiredMemes != null)
			{
				for (int i = 0; i < factionDef.requiredMemes.Count; i++)
				{
					if (!ideo.memes.Contains(factionDef.requiredMemes[i]))
					{
						return false;
					}
				}
			}
			for (int j = 0; j < ideo.memes.Count; j++)
			{
				if (!IsMemeAllowedFor(ideo.memes[j], factionDef))
				{
					return false;
				}
			}
			if (!factionDef.isPlayer)
			{
				List<Precept> preceptsListForReading = ideo.PreceptsListForReading;
				for (int k = 0; k < preceptsListForReading.Count; k++)
				{
					if (!preceptsListForReading[k].def.allowedForNPCFactions)
					{
						return false;
					}
				}
			}
			if (disallowedPrecepts != null && ideo.PreceptsListForReading.Any((Precept p) => disallowedPrecepts.Contains(p.def)))
			{
				return false;
			}
			if (ideo.PreceptsListForReading.OfType<Precept_Ritual>().Any((Precept_Ritual p) => !RitualPatternDef.CanUseWithTechLevel(factionDef.techLevel, p.minTechLevel, p.maxTechLevel)))
			{
				return false;
			}
			if (ideo.PreceptsListForReading.OfType<Precept_Role>().Any((Precept_Role p) => !p.apparelRequirements.NullOrEmpty() && p.apparelRequirements.Any((PreceptApparelRequirement req) => !req.Compatible(ideo, factionDef))))
			{
				return false;
			}
			return true;
		}

		public static bool IsMemeAllowedFor(MemeDef meme, FactionDef faction)
		{
			if (faction.structureMemeWeights != null && meme.category == MemeCategory.Structure && faction.structureMemeWeights.Any((MemeWeight x) => x.meme == meme && x.selectionWeight != 0f))
			{
				return true;
			}
			if (meme.category == MemeCategory.Normal && !meme.allowDuringTutorial && faction.classicIdeo)
			{
				return false;
			}
			if (faction.disallowedMemes != null && faction.disallowedMemes.Contains(meme))
			{
				return false;
			}
			if (faction.requiredMemes != null && faction.requiredMemes.Contains(meme))
			{
				return true;
			}
			if (faction.allowedMemes != null && !faction.allowedMemes.Contains(meme))
			{
				return false;
			}
			return true;
		}
	}
}
