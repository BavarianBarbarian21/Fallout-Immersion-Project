using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FIP.WestTek;

public sealed class WestTekPlantRegrowthExtension : DefModExtension
{
    public List<BodyPartDef> appliesToParts;
}

public sealed class Gene_WestTekSaplingGrowth : Gene
{
    private Dictionary<string, int> trackedMissingParts = new();

    public override void Tick()
    {
        base.Tick();

        if (!pawn.IsHashIntervalTick(250))
        {
            return;
        }

        UpdateMissingPartTimers(250);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref trackedMissingParts, "trackedMissingParts", LookMode.Value, LookMode.Value);
        trackedMissingParts ??= new Dictionary<string, int>();
    }

    private void UpdateMissingPartTimers(int tickDelta)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
        {
            trackedMissingParts.Clear();
            return;
        }

        List<Hediff_MissingPart> missingParts = pawn.health.hediffSet.hediffs
            .OfType<Hediff_MissingPart>()
            .Where(hediff => hediff.Part?.def != null)
            .Where(hediff => WestTekFloraMutationUtility.GetRegrowthOptionsForPart(hediff.Part.def).Count > 0)
            .ToList();

        HashSet<string> currentKeys = missingParts
            .Select(hediff => BodyPartKey(hediff.Part))
            .ToHashSet();

        foreach (string oldKey in trackedMissingParts.Keys.Where(key => !currentKeys.Contains(key)).ToList())
        {
            trackedMissingParts.Remove(oldKey);
        }

        foreach (Hediff_MissingPart missingPart in missingParts)
        {
            string key = BodyPartKey(missingPart.Part);
            trackedMissingParts.TryGetValue(key, out int ticksMissing);
            ticksMissing += tickDelta;

            if (ticksMissing < WestTekFloraMutationUtility.PlantRegrowthDelayTicks)
            {
                trackedMissingParts[key] = ticksMissing;
                continue;
            }

            if (WestTekFloraMutationUtility.TryRegrowSymbiotePart(pawn, missingPart.Part))
            {
                trackedMissingParts.Remove(key);
                Messages.Message(
                    $"{pawn.LabelShortCap}'s sapling growth replaces a missing {missingPart.Part.Label}.",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            }
            else
            {
                trackedMissingParts[key] = ticksMissing;
            }
        }
    }

    private static string BodyPartKey(BodyPartRecord part)
    {
        List<int> path = new();
        BodyPartRecord current = part;

        while (current != null)
        {
            BodyPartRecord parent = current.parent;
            path.Add(parent?.parts?.IndexOf(current) ?? 0);
            current = parent;
        }

        path.Reverse();
        return string.Join(".", path);
    }
}

public sealed class Recipe_InstallFloraSymbiote : Recipe_InstallArtificialBodyPart
{
    public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
    {
        if (!base.AvailableOnNow(thing, part))
        {
            return false;
        }

        return thing is Pawn pawn && pawn.RaceProps.Humanlike && pawn.RaceProps.IsFlesh;
    }

    public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, List<Thing> ingredients, Bill bill)
    {
        base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);

        if (WestTekFloraMutationUtility.TryApplyOvergrownMutation(pawn))
        {
            Messages.Message(
                $"{pawn.LabelShortCap} is overtaken by symbiotic flora and becomes Overgrown.",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );
        }
    }
}
