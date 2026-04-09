using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace VIESAS
{
    class VIESASSettings : ModSettings
    {
        public int minimumColonistCountForSchismToOccur = 12;
        public int amountOfMemesChangedDuringSchism = 1;
        public float pctOfColonistsToTurnToNewIdeology = 0.5f;
        public float oddsOfSchismOccuring = 0.5f;
        public int ideologyConversionCheckDaysInterval = 15;
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minimumColonistCountForSchismToOccur, "minimumColonistCountForSchismToOccur", 12);
            Scribe_Values.Look(ref pctOfColonistsToTurnToNewIdeology, "pctOfColonistsToTurnToNewIdeology", 0.5f);
            Scribe_Values.Look(ref amountOfMemesChangedDuringSchism, "amountOfMemesChangedDuringSchism", 1);
            Scribe_Values.Look(ref oddsOfSchismOccuring, "oddsOfSchismOccuring", 0.5f);
            Scribe_Values.Look(ref ideologyConversionCheckDaysInterval, "ideologyConversionCheckDaysInterval", 15);
        }
        public void DoSettingsWindowContents(Rect inRect)
        {
            Widgets.BeginScrollView(inRect, ref scrollPosition, inRect, true);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);
            listingStandard.SliderLabeled("VIESAS.OddsOfSchismToOccur".Translate(), ref oddsOfSchismOccuring, (oddsOfSchismOccuring * 100f).ToStringDecimalIfSmall() + "%", 0.001f, 1f);
            listingStandard.SliderLabeled("VIESAS.PctOfColonistsToTurnToNewIdeology".Translate(), ref pctOfColonistsToTurnToNewIdeology, 
                (pctOfColonistsToTurnToNewIdeology * 100f).ToStringDecimalIfSmall() + "%", 0.001f, 1f);
            listingStandard.SliderLabeled("VIESAS.MinimumColonistCountForSchismToOccur".Translate(), ref minimumColonistCountForSchismToOccur, minimumColonistCountForSchismToOccur.ToString(), 2, 100);
            listingStandard.SliderLabeled("VIESAS.AmountOfMemesChangedDuringSchism".Translate(), ref amountOfMemesChangedDuringSchism, amountOfMemesChangedDuringSchism.ToString(), 1, 3);
            listingStandard.SliderLabeled("VIESAS.IdeologyConversionCheckDaysInterval".Translate(), ref ideologyConversionCheckDaysInterval, ideologyConversionCheckDaysInterval.ToString(), 1, 30);
            if (listingStandard.ButtonText("Reset".Translate()))
            {
                minimumColonistCountForSchismToOccur = 12;
                amountOfMemesChangedDuringSchism = 1;
                pctOfColonistsToTurnToNewIdeology = 0.5f;
                oddsOfSchismOccuring = 0.5f;
                ideologyConversionCheckDaysInterval = 15;
            }
            listingStandard.End();
            Widgets.EndScrollView();
        }
        private static Vector2 scrollPosition = Vector2.zero;

    }
}

