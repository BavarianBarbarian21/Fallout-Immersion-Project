using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace VanillaSocialInteractionsExpanded
{
	[HarmonyPatch(typeof(LordJob_Joinable_MarriageCeremony), "AddAttendedWeddingThoughts")]
	public class AddAttendedWeddingThoughts_Patch
	{
		private static void Postfix(LordJob_Joinable_MarriageCeremony __instance)
		{
			if (VanillaSocialInteractionsExpandedSettings.EnableMemories)
			{
				List<Pawn> attendedWedding = new List<Pawn>();
				List<Pawn> ownedPawns = __instance.lord.ownedPawns;
				for (int i = ownedPawns.Count - 1; i >= 0; i--)
				{
					if (__instance.firstPawn.Position.InHorDistOf(ownedPawns[i].Position, 18f)
						|| __instance.secondPawn.Position.InHorDistOf(ownedPawns[i].Position, 18f))
					{
						attendedWedding.Add(ownedPawns[i]);
					}
				}

				for (int i = attendedWedding.Count - 1; i >= 0; i--)
				{
					Pawn pawn = attendedWedding[i];
					if (pawn != __instance.firstPawn)
					{
						if (Rand.Chance(0.1f))
						{
							TaleRecorder.RecordTale(VSIE_DefOf.VSIE_AttendedMyWedding, __instance.firstPawn, pawn);
						}
					}
					else if (pawn != __instance.secondPawn)
					{
						if (Rand.Chance(0.1f))
						{
							TaleRecorder.RecordTale(VSIE_DefOf.VSIE_AttendedMyWedding, __instance.secondPawn, pawn);
						}
					}
				}

				List<Pawn> colonists = __instance.lord.Map.mapPawns.AllPawns.Where(x => x.IsColonist).ToList();
				for (int i = colonists.Count - 1; i >= 0; i--)
				{
					Pawn pawn = colonists[i];
					if (!attendedWedding.Contains(pawn))
					{
						if (attendedWedding.Contains(__instance.firstPawn))
						{
							if (Rand.Chance(0.1f))
							{
								TaleRecorder.RecordTale(VSIE_DefOf.VSIE_DidNotAttendWedding, __instance.firstPawn, pawn);
							}
						}
						if (attendedWedding.Contains(__instance.secondPawn))
						{
							if (Rand.Chance(0.1f))
							{
								TaleRecorder.RecordTale(VSIE_DefOf.VSIE_DidNotAttendWedding, __instance.secondPawn, pawn);
							}
						}
					}
				}
			}
		}
	}
}
