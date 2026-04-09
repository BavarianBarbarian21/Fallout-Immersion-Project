using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace VIESAS
{
    public class IdeologyTracker : GameComponent
    {
        public Ideo originIdeo;
        public Ideo splittedIdeo;
        public int nextConversionTickCheck;
        public static IdeologyTracker Instance;

        private int lastBelieverCheckTick;
        private List<Pawn> cachedPawnList;
        public IdeologyTracker(Game game)
        {
            Instance = this;
        }
        private IEnumerable<Pawn> GetConvertablePawns(Ideo ideo, List<Pawn> pawns)
        {
            foreach (var pawn in pawns)
            {
                if (ideo.GetRole(pawn) is null)
                {
                    yield return pawn;
                }
            }
        }
        private IEnumerable<Pawn> GetBelievers(Ideo ideo)
        {
            foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravelingTransportPods_Alive_FreeColonists)
            {
                if (pawn.IsColonist && pawn.Ideo == ideo)
                {
                    yield return pawn;
                }
            }
        }
        private int GetNextConversionTickCheck()
        {
            return (int)(Find.TickManager.TicksGame + (GenDate.TicksPerDay * (VIESASMod.settings.ideologyConversionCheckDaysInterval + Rand.Range(-0.5f, 0.5f))));
        }
        public override void LoadedGame()
        {
            base.LoadedGame();
            Instance = this;
            if (nextConversionTickCheck == 0)
            {
                nextConversionTickCheck = GetNextConversionTickCheck();
            }
        }
        public override void StartedNewGame()
        {
            base.StartedNewGame();
            Instance = this;
            if (nextConversionTickCheck == 0)
            {
                nextConversionTickCheck = GetNextConversionTickCheck();
            }
        }
        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Find.TickManager.TicksGame >= nextConversionTickCheck && Find.TickManager.TicksGame % 60 == 0)
            {
                if (IdeoSplitCanOccur())
                {
                    TrySplitIdeo();
                }
                else
                {
                    nextConversionTickCheck = GetNextConversionTickCheck();
                }
            }
        }

        public void TrySplitIdeo()
        {
            if (Find.TickManager.TicksGame > lastBelieverCheckTick + 60 || cachedPawnList is null)
            {
                cachedPawnList = GetBelievers(Faction.OfPlayer.ideos.PrimaryIdeo).ToList();
                lastBelieverCheckTick = Find.TickManager.TicksGame;
            }
            var believers = cachedPawnList;
            if (believers.Count >= VIESASMod.settings.minimumColonistCountForSchismToOccur)
            {
                var convertablePawns = GetConvertablePawns(Faction.OfPlayer.ideos.PrimaryIdeo, believers).ToList();
                int minPopValue;
                if (convertablePawns.Count > 3)
                {
                    minPopValue = 2;
                }
                else
                {
                    minPopValue = 1;
                }
                var countToConvert = Rand.RangeInclusive(minPopValue, (int)(convertablePawns.Count * VIESASMod.settings.pctOfColonistsToTurnToNewIdeology));
                var colonistsToConvert = convertablePawns.InRandomOrder().Take(countToConvert).ToList();
                StringBuilder ideoChanges = new StringBuilder();
                var newIdeo = GenerateNewSplittedIdeoFrom(Faction.OfPlayer.ideos.PrimaryIdeo, ideoChanges);
                Find.IdeoManager.Add(newIdeo);
                StringBuilder joinerDesc = new StringBuilder();

                foreach (var pawn in colonistsToConvert)
                {
                    var oldCertainty = pawn.ideo.Certainty;
                    pawn.ideo.SetIdeo(newIdeo);
                    Traverse.Create(pawn.ideo).Field("certainty").SetValue(oldCertainty);
                    joinerDesc.AppendLine("  - " + pawn.LabelShort);
                }
                Find.LetterStack.ReceiveLetter("VIESAS.IdeoSplit".Translate(newIdeo.name), "VIESAS.IdeoSplitDesc".Translate(newIdeo.name,
                    ideoChanges.ToString().TrimEndNewlines(), joinerDesc.ToString().TrimEndNewlines()), LetterDefOf.NegativeEvent, colonistsToConvert);

                originIdeo = Faction.OfPlayer.ideos.PrimaryIdeo;
                splittedIdeo = newIdeo;
                nextConversionTickCheck = GetNextConversionTickCheck();
                Faction.OfPlayer.ideos.Notify_ColonistChangedIdeo();
            }
        }
        private bool IdeoSplitCanOccur()
        {
            if (originIdeo != null)
            {
                if (NoBelieversIn(originIdeo))
                {
                    originIdeo = null;
                    return true;
                }
            }
            else
            {
                return true;
            }
            if (splittedIdeo != null)
            {
                if (NoBelieversIn(splittedIdeo))
                {
                    splittedIdeo = null;
                    return true;
                }
            }
            else
            {
                return true;
            }
            return false;
        }


        public void RecheckIdeos()
        {
            if (originIdeo != null)
            {
                if (NoBelieversIn(originIdeo))
                {
                    originIdeo = null;
                    nextConversionTickCheck = GetNextConversionTickCheck();
                }
            }
            if (splittedIdeo != null)
            {
                if (NoBelieversIn(splittedIdeo))
                {
                    splittedIdeo = null;
                    nextConversionTickCheck = GetNextConversionTickCheck();
                }
            }
        }
        private bool NoBelieversIn(Ideo ideo)
        {
            if (!GetBelievers(ideo).Any())
            {
                if (Faction.OfPlayer.ideos.IdeosMinorListForReading.Contains(ideo))
                {
                    Faction.OfPlayer.ideos.IdeosMinorListForReading.Remove(ideo);
                }
                if (CanRemoveIdeo(ideo))
                {
                    Find.LetterStack.ReceiveLetter("VIESAS.OldIdeoForgotten".Translate(ideo.name), "VIESAS.OldIdeoForgottenDesc".Translate(ideo.name), LetterDefOf.NeutralEvent);
                    Find.IdeoManager.Remove(ideo);
                }
                return true;
            }
            return false;
        }

        private bool CanRemoveIdeo(Ideo ideo)
        {
            if (!Find.IdeoManager.IdeosListForReading.Contains(ideo)) // it's removed already
            {
                return false;
            }
            foreach (Faction allFaction in Find.FactionManager.AllFactions)
            {
                if (allFaction.ideos != null && allFaction.ideos.AllIdeos.Contains(ideo))
                {
                    return false;
                }
            }
            foreach (Pawn allMap in PawnsFinder.AllMaps)
            {
                if (allMap.ideo != null && allMap.ideo.Ideo == ideo)
                {
                    return false;
                }
            }

            return true;
        }
        private Ideo GenerateNewSplittedIdeoFrom(Ideo oldIdeo, StringBuilder ideoChanges)
        {
            var newIdeo = IdeoGenerator.MakeIdeo(oldIdeo.foundation.def);
            var parms = new IdeoGenerationParms(Faction.OfPlayer.def);
            RandomizeMemes(newIdeo, oldIdeo, parms, ideoChanges);
            newIdeo.culture = oldIdeo.culture;
            newIdeo.foundation.place = oldIdeo.foundation.place;
            if (newIdeo.foundation is IdeoFoundation_Deity ideoFoundation_Deity)
            {
                ideoFoundation_Deity.GenerateDeities();
            }
            newIdeo.foundation.GenerateTextSymbols();
            newIdeo.foundation.RandomizePrecepts(init: false, parms);
            newIdeo.foundation.GenerateLeaderTitle();
            newIdeo.foundation.RandomizeIcon();
            newIdeo.foundation.InitPrecepts(parms);
            newIdeo.RecachePrecepts();
            newIdeo.foundation.ideo.RegenerateDescription(force: true);

            newIdeo.thingStyleCategories = new List<ThingStyleCategoryWithPriority>();
            foreach (var cat in oldIdeo.thingStyleCategories)
            {
                newIdeo.thingStyleCategories.Add(cat);
            }
            newIdeo.style.ResetStylesForThingDef();

            newIdeo.primaryFactionColor = oldIdeo.primaryFactionColor;
            return newIdeo;
        }

        private void RandomizeMemes(Ideo newIdeo, Ideo oldIdeo, IdeoGenerationParms parms, StringBuilder ideoChanges)
        {
            var memesCount = VIESASMod.settings.amountOfMemesChangedDuringSchism;
            var memesToAdd = IdeoUtilityCustom.GenerateRandomMemes(parms).Where(x => !oldIdeo.HasMeme(x)).Distinct();
            var normalMemes = memesToAdd.Where(x => x.category == MemeCategory.Normal).ToList();

            newIdeo.memes.AddRange(oldIdeo.memes);
            List<MemeDef> removedMemes = new List<MemeDef>();
            List<MemeDef> addedMemes = new List<MemeDef>();

            Predicate<MemeDef> oldMemesToRemoveValidator = delegate (MemeDef x)
            {
                if (x.category == MemeCategory.Structure)
                {
                    return false;
                }
                if (!newIdeo.memes.Contains(x))
                {
                    return false;
                }
                if (addedMemes.Contains(x))
                {
                    return false;
                }
                return true;
            };

            Predicate<MemeDef> newMemeToAddValidator = delegate (MemeDef x)
            {
                if (removedMemes.Contains(x))
                {
                    return false;
                }
                if (!IdeoUtilityCustom.CanAdd(x, newIdeo.memes, Faction.OfPlayer.def))
                {
                    return false;
                }
                if (x.category == MemeCategory.Structure)
                {
                    return false;
                }
                if (addedMemes.Contains(x))
                {
                    return false;
                }
                return true;
            };
            int maxImpact = memesToAdd.Max(x => x.impact);

            for (var i = 0; i < memesCount; i++)
            {
                if (oldIdeo.memes.Count(x => x.category == MemeCategory.Normal) >= 3 || newIdeo.memes.Count(x => x.category == MemeCategory.Normal) >= 3)
                {
                    if (oldIdeo.memes.Where(x => oldMemesToRemoveValidator(x)).TryRandomElement(out var memeToRemove))
                    {
                        newIdeo.memes.Remove(memeToRemove);
                        if (memesToAdd.Where(x => newMemeToAddValidator(x)).TryRandomElementByWeight(x => (maxImpact + 1) - x.impact, out var newMeme))
                        {
                            removedMemes.Add(memeToRemove);

                            newIdeo.memes.Add(newMeme);
                            addedMemes.Add(newMeme);
                            ideoChanges.AppendLine("VIESAS.MemeChanged".Translate() + ": " + memeToRemove.LabelCap + " -> " + newMeme.LabelCap);
                        }
                        else
                        {
                            newIdeo.memes.Add(memeToRemove);
                        }
                    }
                }

                else if (memesToAdd.Where(x => newMemeToAddValidator(x)).TryRandomElementByWeight(x => (maxImpact + 1) - x.impact, out var newMeme))
                {
                    newIdeo.memes.Add(newMeme);
                    addedMemes.Add(newMeme);
                    ideoChanges.AppendLine("VIESAS.NewMeme".Translate() + ": " + newMeme.LabelCap);
                }
            }
            newIdeo.SortMemesInDisplayOrder();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Instance = this;
            Scribe_Values.Look(ref nextConversionTickCheck, "nextConversionTickCheck");
            Scribe_References.Look(ref originIdeo, "originIdeo");
            Scribe_References.Look(ref splittedIdeo, "splittedIdeo");
        }
    }
}
