using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.RobCo;

public sealed class WestTekSynthGeneExtension : DefModExtension
{
    public bool isSynthEndogene;
}

public sealed class Gene_WestTekSynthComponents : Gene
{
    private const float DailyGeneLossChance = 0.02f;

    public override void Tick()
    {
        base.Tick();

        if (pawn == null || !pawn.IsHashIntervalTick(GenDate.TicksPerDay))
        {
            return;
        }

        if (!Rand.Chance(DailyGeneLossChance))
        {
            return;
        }

        WestTekSynthGeneUtility.ResolveSynthComponentsDailyDecay(pawn, this);
    }
}

internal static class WestTekSynthGeneUtility
{
    public static bool HasGene(Pawn pawn, GeneDef geneDef)
    {
        if (pawn?.genes == null || geneDef == null)
        {
            return false;
        }

        return pawn.genes.GenesListForReading.Any(gene => gene.def == geneDef);
    }

    public static bool IsSynthEndogeneCandidate(Gene gene)
    {
        if (gene?.def == null)
        {
            return false;
        }

        WestTekSynthGeneExtension extension = gene.def.GetModExtension<WestTekSynthGeneExtension>();
        if (extension?.isSynthEndogene != true)
        {
            return false;
        }

        return gene.def != RobCoDefOf.WestTek_Gene_SynthComponents
            && gene.def != RobCoDefOf.WestTek_Gene_Courser;
    }

    public static List<Gene> GetExistingSynthEndogeneCandidates(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return new List<Gene>();
        }

        return pawn.genes.Endogenes
            .Where(IsSynthEndogeneCandidate)
            .ToList();
    }

    public static void ResolveSynthComponentsDailyDecay(Pawn pawn, Gene synthComponentsGene)
    {
        if (pawn?.genes == null || synthComponentsGene == null)
        {
            return;
        }

        List<Gene> candidates = GetExistingSynthEndogeneCandidates(pawn);

        if (candidates.Count > 0)
        {
            Gene lostGene = candidates.RandomElement();
            pawn.genes.RemoveGene(lostGene);

            if (pawn.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    $"{pawn.LabelShortCap} loses the synth endogene {lostGene.def.LabelCap}.",
                    pawn,
                    MessageTypeDefOf.NeutralEvent
                );
            }

            RefreshGraphics(pawn);
            return;
        }

        ReplaceSynthComponentsWithCourser(pawn, synthComponentsGene);
    }

    private static void ReplaceSynthComponentsWithCourser(Pawn pawn, Gene synthComponentsGene)
    {
        if (pawn?.genes == null)
        {
            return;
        }

        pawn.genes.RemoveGene(synthComponentsGene);

        if (!HasGene(pawn, RobCoDefOf.WestTek_Gene_Courser))
        {
            pawn.genes.AddGene(RobCoDefOf.WestTek_Gene_Courser, xenogene: false);
        }

        if (pawn.Faction == Faction.OfPlayer)
        {
            Messages.Message(
                $"{pawn.LabelShortCap}'s unstable synth components collapse into a stable Courser-grade framework.",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
        }

        RefreshGraphics(pawn);
    }

    public static void RefreshGraphics(Pawn pawn)
    {
        pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

        if (pawn != null)
        {
            PortraitsCache.SetDirty(pawn);
        }
    }
}
