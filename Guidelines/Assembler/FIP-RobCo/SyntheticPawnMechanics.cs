using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FIP.RobCo;

public sealed class ThinkTankBiogelExtension : DefModExtension
{
    public Color color = Color.white;
}

public sealed class Gene_Gen2SynthAppearance : Gene
{
    public override void PostMake()
    {
        base.PostMake();
        SyntheticPawnUtility.RefreshGraphics(pawn);
    }

    public override void PostAdd()
    {
        base.PostAdd();
        SyntheticPawnUtility.RefreshGraphics(pawn);
    }
}

public sealed class Gene_ThinkTankCore : Gene
{
    public override void PostMake()
    {
        base.PostMake();
        SyntheticPawnUtility.EnsureThinkTankState(pawn);
    }

    public override void PostAdd()
    {
        base.PostAdd();
        SyntheticPawnUtility.EnsureThinkTankState(pawn);
    }

    public override void Tick()
    {
        base.Tick();

        if (pawn != null && pawn.IsHashIntervalTick(GenDate.TicksPerDay))
        {
            SyntheticPawnUtility.EnsureThinkTankState(pawn);
        }
    }
}

[StaticConstructorOnStartup]
internal static class RobCoSyntheticPawnBootstrap
{
    static RobCoSyntheticPawnBootstrap()
    {
        new Harmony("FIP.RobCo.SyntheticPawns").PatchAll();
        LongEventHandler.ExecuteWhenFinished(SyntheticPawnUtility.ExpandBiosphereImmunities);
    }
}

internal static class SyntheticPawnUtility
{
    private const string Gen1BodyPath = "FIP-RobCo/Robots/Synth/Gen1/SynthT1";
    private const string Gen2BodyPath = "FIP-RobCo/Robots/Synth/Gen2/SynthT2_body_Male";
    private const string Gen2HeadPath = "FIP-RobCo/Robots/Synth/Gen2/SynthT2_head_Male";
    private const string ThinkTankPath = "FIP-RobCo/Robots/ThinkTank/Thinktank";

    private static List<GeneDef> biogelColorDefs;

    public static bool HasActiveGene(Pawn pawn, GeneDef geneDef)
    {
        return pawn?.genes != null && geneDef != null && pawn.genes.HasActiveGene(geneDef);
    }

    public static bool IsThinkTank(Pawn pawn)
    {
        return HasActiveGene(pawn, RobCoDefOf.RobCo_Gene_ThinkTankCore);
    }

    public static bool IsGen2Synth(Pawn pawn)
    {
        return HasActiveGene(pawn, RobCoDefOf.WestTek_Gene_SynthBody);
    }

    public static bool IsGen1Synth(Pawn pawn)
    {
        return pawn?.def == RobCoDefOf.RobCo_Gen1Synth;
    }

    public static void ConvertToThinkTank(Pawn pawn)
    {
        if (pawn?.genes == null || RobCoDefOf.RobCo_ThinkTank == null)
        {
            return;
        }

        // SetXenotypeDirect changes only the displayed xenotype identity. Existing genes,
        // including genes supplied by optional mods, remain on the pawn.
        pawn.genes.SetXenotypeDirect(RobCoDefOf.RobCo_ThinkTank);
        AddGeneIfMissing(pawn, RobCoDefOf.RobCo_Gene_ThinkTankCore);
        AddGeneIfMissing(pawn, RobCoDefOf.RobCo_Gene_Biosphere);
        AddGeneIfMissing(pawn, RobCoDefOf.RobCo_Gene_ThinkTankEyes);
        EnsureBiogelColor(pawn);
        RemoveApparelAndImplants(pawn);
        EnsureThinkTankState(pawn);
    }

    public static void EnsureThinkTankState(Pawn pawn)
    {
        if (pawn?.genes == null || !IsThinkTank(pawn))
        {
            return;
        }

        bool graphicsChanged = false;

        if (pawn.story != null)
        {
            if (RobCoDefOf.RobCo_ThinkTankBodyType != null
                && pawn.story.bodyType != RobCoDefOf.RobCo_ThinkTankBodyType)
            {
                pawn.story.bodyType = RobCoDefOf.RobCo_ThinkTankBodyType;
                graphicsChanged = true;
            }

            if (RobCoDefOf.RobCo_ThinkTankHead != null
                && pawn.story.headType != RobCoDefOf.RobCo_ThinkTankHead)
            {
                pawn.story.headType = RobCoDefOf.RobCo_ThinkTankHead;
                graphicsChanged = true;
            }

            HairDef bald = DefDatabase<HairDef>.GetNamedSilentFail("Bald");
            if (bald != null && pawn.story.hairDef != bald)
            {
                pawn.story.hairDef = bald;
                graphicsChanged = true;
            }
        }

        if (EnsureBiogelColor(pawn))
        {
            graphicsChanged = true;
        }

        RemoveApparelAndImplants(pawn);

        if (graphicsChanged)
        {
            RefreshGraphics(pawn);
        }
    }

    public static Color BiogelColorFor(Pawn pawn)
    {
        if (pawn?.genes != null)
        {
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                ThinkTankBiogelExtension extension = gene.def.GetModExtension<ThinkTankBiogelExtension>();
                if (extension != null && gene.Active)
                {
                    return extension.color;
                }
            }
        }

        return new Color(0.55f, 0.28f, 0.82f);
    }

    public static Graphic Gen1BodyGraphic(Graphic original)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            Gen1BodyPath,
            ShaderDatabase.Cutout,
            original != null ? original.drawSize : Vector2.one,
            Color.white);
    }

    public static Graphic Gen2BodyGraphic(Graphic original)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            Gen2BodyPath,
            ShaderDatabase.Cutout,
            original != null ? original.drawSize : Vector2.one,
            Color.white);
    }

    public static Graphic Gen2HeadGraphic(Graphic original)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            Gen2HeadPath,
            ShaderDatabase.Cutout,
            original != null ? original.drawSize : Vector2.one,
            Color.white);
    }

    public static Graphic ThinkTankGraphic(Pawn pawn, Graphic original)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            ThinkTankPath,
            ShaderDatabase.CutoutComplex,
            new Vector2(1.5f, 1.5f),
            BiogelColorFor(pawn),
            Color.white);
    }

    public static Graphic InvisibleGraphic(Graphic original)
    {
        return GraphicDatabase.Get<Graphic_Multi>(
            Gen2BodyPath,
            ShaderDatabase.Invisible,
            original != null ? original.drawSize : Vector2.one,
            Color.white);
    }

    public static void ExpandBiosphereImmunities()
    {
        GeneDef biosphere = RobCoDefOf.RobCo_Gene_Biosphere;
        if (biosphere == null)
        {
            return;
        }

        biosphere.makeImmuneTo ??= new List<HediffDef>();
        biosphere.hediffGiversCannotGive ??= new List<HediffDef>();

        foreach (HediffDef hediffDef in DefDatabase<HediffDef>.AllDefsListForReading)
        {
            bool immunizable = hediffDef.comps?.Any(comp => comp is HediffCompProperties_Immunizable) == true;
            if (!immunizable)
            {
                continue;
            }

            if (!biosphere.makeImmuneTo.Contains(hediffDef))
            {
                biosphere.makeImmuneTo.Add(hediffDef);
            }

            if (!biosphere.hediffGiversCannotGive.Contains(hediffDef))
            {
                biosphere.hediffGiversCannotGive.Add(hediffDef);
            }
        }
    }

    public static void RefreshGraphics(Pawn pawn)
    {
        pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

        if (pawn != null)
        {
            PortraitsCache.SetDirty(pawn);
        }
    }

    private static void AddGeneIfMissing(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes != null
            && geneDef != null
            && !pawn.genes.GenesListForReading.Any(gene => gene.def == geneDef))
        {
            pawn.genes.AddGene(geneDef, xenogene: false);
        }
    }

    private static bool EnsureBiogelColor(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        if (pawn.genes.GenesListForReading.Any(gene => gene.def.GetModExtension<ThinkTankBiogelExtension>() != null))
        {
            return false;
        }

        biogelColorDefs ??= DefDatabase<GeneDef>.AllDefsListForReading
            .Where(def => def.GetModExtension<ThinkTankBiogelExtension>() != null)
            .ToList();

        if (biogelColorDefs.Count == 0)
        {
            return false;
        }

        pawn.genes.AddGene(biogelColorDefs.RandomElement(), xenogene: false);
        return true;
    }

    private static void RemoveApparelAndImplants(Pawn pawn)
    {
        if (pawn?.apparel?.AnyApparel == true)
        {
            pawn.apparel.DropAllOrMoveAllToInventory(null);
        }

        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            return;
        }

        List<Hediff> implants = pawn.health.hediffSet.hediffs
            .Where(hediff => hediff.def.countsAsAddedPartOrImplant)
            .ToList();

        foreach (Hediff implant in implants)
        {
            pawn.health.RemoveHediff(implant);
        }
    }
}

public sealed class Recipe_CreateThinkTank : Recipe_Surgery
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        return base.AvailableOnNow(thing, part)
            && thing is Pawn pawn
            && pawn.RaceProps.Humanlike
            && pawn.genes != null
            && !SyntheticPawnUtility.IsThinkTank(pawn);
    }

    protected override void OnSurgerySuccess(
        Pawn pawn,
        BodyPartRecord part,
        Pawn billDoer,
        List<Thing> ingredients,
        Bill bill)
    {
        SyntheticPawnUtility.ConvertToThinkTank(pawn);

        Messages.Message(
            $"{pawn.LabelShortCap}'s brain has been transferred into a Think Tank chassis.",
            pawn,
            MessageTypeDefOf.PositiveEvent);
    }
}

public sealed class Recipe_Lobotomize : Recipe_Surgery
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        return base.AvailableOnNow(thing, part)
            && thing is Pawn pawn
            && pawn.RaceProps.Humanlike
            && pawn.genes != null
            && !SyntheticPawnUtility.IsThinkTank(pawn)
            && !pawn.genes.GenesListForReading.Any(gene => gene.def == RobCoDefOf.RobCo_Gene_Lobotomized);
    }

    public override bool IsViolationOnPawn(Pawn pawn, BodyPartRecord part, Faction billDoerFaction)
    {
        return pawn?.IsPrisonerOfColony == true
            || base.IsViolationOnPawn(pawn, part, billDoerFaction);
    }

    protected override void OnSurgerySuccess(
        Pawn pawn,
        BodyPartRecord part,
        Pawn billDoer,
        List<Thing> ingredients,
        Bill bill)
    {
        if (pawn?.genes != null
            && RobCoDefOf.RobCo_Gene_Lobotomized != null
            && !pawn.genes.GenesListForReading.Any(gene => gene.def == RobCoDefOf.RobCo_Gene_Lobotomized))
        {
            pawn.genes.AddGene(RobCoDefOf.RobCo_Gene_Lobotomized, xenogene: false);
        }

        Messages.Message(
            $"{pawn.LabelShortCap} has been lobotomized and no longer experiences mood influences.",
            pawn,
            MessageTypeDefOf.NeutralEvent);
    }
}

[HarmonyPatch(typeof(PawnRenderNode_Body), nameof(PawnRenderNode_Body.GraphicFor))]
internal static class PawnRenderNodeBody_SyntheticGraphicPatch
{
    private static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (SyntheticPawnUtility.IsThinkTank(pawn))
        {
            __result = SyntheticPawnUtility.InvisibleGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen1Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen1BodyGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen2Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen2BodyGraphic(__result);
        }
    }
}

[HarmonyPatch(typeof(PawnRenderNode_Head), nameof(PawnRenderNode_Head.GraphicFor))]
internal static class PawnRenderNodeHead_SyntheticGraphicPatch
{
    private static void Postfix(Pawn pawn, ref Graphic __result)
    {
        if (SyntheticPawnUtility.IsThinkTank(pawn))
        {
            __result = SyntheticPawnUtility.ThinkTankGraphic(pawn, __result);
        }
        else if (SyntheticPawnUtility.IsGen1Synth(pawn))
        {
            __result = SyntheticPawnUtility.InvisibleGraphic(__result);
        }
        else if (SyntheticPawnUtility.IsGen2Synth(pawn))
        {
            __result = SyntheticPawnUtility.Gen2HeadGraphic(__result);
        }
    }
}

[HarmonyPatch(typeof(Recipe_Surgery), nameof(Recipe_Surgery.AvailableOnNow))]
internal static class RecipeSurgery_ThinkTankImplantPatch
{
    private static void Postfix(Recipe_Surgery __instance, Thing thing, ref bool __result)
    {
        if (!__result || thing is not Pawn pawn || !SyntheticPawnUtility.IsThinkTank(pawn))
        {
            return;
        }

        Type workerClass = __instance.recipe?.workerClass;
        bool installsAddedPart = __instance.recipe?.addsHediff?.countsAsAddedPartOrImplant == true;
        bool isInstallWorker = workerClass != null
            && (typeof(Recipe_InstallArtificialBodyPart).IsAssignableFrom(workerClass)
                || typeof(Recipe_InstallImplant).IsAssignableFrom(workerClass));

        if (installsAddedPart || isInstallWorker)
        {
            __result = false;
        }
    }
}

[HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear))]
internal static class ApparelUtility_ThinkTankPatch
{
	private static void Postfix(Pawn p, ref bool __result)
	{
		if (__result && SyntheticPawnUtility.IsThinkTank(p))
        {
            __result = false;
        }
    }
}
