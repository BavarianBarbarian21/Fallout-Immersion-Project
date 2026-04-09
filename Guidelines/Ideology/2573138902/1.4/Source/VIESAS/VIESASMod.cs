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
    class VIESASMod : Mod
    {
        public static VIESASSettings settings;
        public VIESASMod(ModContentPack pack) : base(pack)
        {
            settings = GetSettings<VIESASSettings>();
            new Harmony("VIESAS.Mod").PatchAll();
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            settings.DoSettingsWindowContents(inRect);
        }
        public override string SettingsCategory()
        {
            return "Vanilla Ideologies Expanded - Splits and Schisms";
        }
    }
}