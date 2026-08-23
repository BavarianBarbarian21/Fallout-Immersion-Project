using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.RobCo;

/// <summary>
/// A gestator bill for the University branch's human Courser. Vanilla's
/// Bill_ResurrectMech assumes every restored pawn has a mech energy need and
/// an overseer relation, so using it for a human pawn fails at completion.
/// </summary>
public sealed class Bill_RestoreCourser : Bill_Mech
{
    public override float BandwidthCost => 0f;

    public Bill_RestoreCourser()
    {
    }

    public Bill_RestoreCourser(RecipeDef recipe, Precept_ThingStyle precept = null)
        : base(recipe, precept)
    {
    }

    public override bool IsFixedOrAllowedIngredient(Thing thing)
    {
        if (!base.IsFixedOrAllowedIngredient(thing))
        {
            return false;
        }

        if (thing is not Corpse corpse)
        {
            return true;
        }

        Pawn pawn = corpse.InnerPawn;
        return pawn?.kindDef?.defName == "RobCo_QuestCourser"
            && pawn.genes?.GenesListForReading.Any(gene => gene.def == RobCoDefOf.RobCo_Gene_Courser) == true;
    }

    public override Thing CreateProducts()
    {
        Corpse corpse = Gestator?.ResurrectingMechCorpse;
        Pawn courser = corpse?.InnerPawn;
        if (courser == null || !IsCourser(courser))
        {
            throw new InvalidOperationException("RobCo Courser restoration completed without its quest Courser corpse.");
        }

        // The quest generates the corpse factionless to avoid a fake colonist-
        // death event. Set the faction before resurrection so vanilla initializes
        // colonist work settings and population adaptation correctly.
        if (courser.Faction != Faction.OfPlayer)
        {
            courser.SetFaction(Faction.OfPlayer);
        }

        if (!ResurrectionUtility.TryResurrect(courser, new ResurrectionParams
        {
            dontSpawn = true,
            removeDiedThoughts = true
        }))
        {
            throw new InvalidOperationException("RimWorld rejected the RobCo Courser resurrection.");
        }

        if (courser.IsWorldPawn())
        {
            Find.WorldPawns.RemovePawn(courser);
        }

        return courser;
    }

    private static bool IsCourser(Pawn pawn)
    {
        return pawn?.kindDef?.defName == "RobCo_QuestCourser"
            && pawn.genes?.GenesListForReading.Any(gene => gene.def == RobCoDefOf.RobCo_Gene_Courser) == true;
    }
}
