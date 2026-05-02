using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.QuestGen;
using Verse;

namespace FIP.WestTek;

public class QuestNode_Root_Loot_AncientWestTekLaboratory : QuestNode_Root_Loot_AncientComplex
{
    protected override LayoutDef LayoutDef => DefDatabase<LayoutDef>.GetNamed("WestTek_AncientWestTekLaboratory_Loot");

    protected override SitePartDef SitePartDef => DefDatabase<SitePartDef>.GetNamed("WestTek_AncientWestTekLaboratory");

    protected override bool BeforeRunInt()
    {
        return ModLister.CheckBiotech("Ancient West Tek laboratory");
    }

    protected override void RunInt()
    {
        Slate slate = QuestGen.slate;
        if (!slate.TryGet("discovered", out bool _))
        {
            slate.Set("discovered", false);
        }

        base.RunInt();
    }
}

public class SymbolResolver_AncientWestTekLaboratory : SymbolResolver_AncientComplex_Base
{
    protected override LayoutDef DefaultLayoutDef => DefDatabase<LayoutDef>.GetNamed("WestTek_AncientWestTekLaboratory_Loot");

    public override void Resolve(ResolveParams rp)
    {
        ResolveParams resolveParams = rp;
        resolveParams.floorDef = TerrainDefOf.PackedDirt;
        BaseGen.symbolStack.Push("outdoorsPath", resolveParams);
        BaseGen.symbolStack.Push("ensureCanReachMapEdge", rp);
        ResolveComplex(rp);
    }
}

public class GenStep_AncientWestTekLaboratory : GenStep_AncientComplex
{
    protected override void GenerateComplex(Map map, ResolveParams parms)
    {
        BaseGen.globalSettings.map = map;
        BaseGen.symbolStack.Push("ancientWestTekLaboratory", parms);
        BaseGen.Generate();
    }
}

public class LayoutWorkerComplex_AncientWestTekLaboratory : LayoutWorkerComplex
{
    private static readonly IntRange RandomSubjectCorpseAge = new(0, 360000000);

    private readonly List<CellRect> tmpAllRoomRects = new();

    public LayoutWorkerComplex_AncientWestTekLaboratory(LayoutDef def)
        : base(def)
    {
    }

    public override Faction GetFixedHostileFactionForThreats()
    {
        return Faction.OfMechanoids;
    }

    protected override void PreSpawnThreats(List<LayoutRoom> rooms, Map map, List<Thing> allSpawnedThings)
    {
        base.PreSpawnThreats(rooms, map, allSpawnedThings);

        tmpAllRoomRects.Clear();
        tmpAllRoomRects.AddRange(rooms.Where(room => room.requiredDef == null).SelectMany(room => room.rects));
        if (tmpAllRoomRects.Count == 0)
        {
            return;
        }

        CellRect bounds = tmpAllRoomRects[0];
        for (int index = 1; index < tmpAllRoomRects.Count; index++)
        {
            bounds = bounds.Encapsulate(tmpAllRoomRects[index]);
        }

        bool placed = false;
        foreach (CellRect room in tmpAllRoomRects.OrderBy(rect => rect.Contains(bounds.CenterCell) ? 0f : rect.CenterCell.DistanceTo(bounds.CenterCell)))
        {
            if (TryPlaceWestTekCache(room, map, out Building_AncientCryptosleepPod casket))
            {
                allSpawnedThings.Add(casket);
                placed = true;
                break;
            }
        }

        if (!placed)
        {
            Log.Error("Failed to place FEV cache in ancient West Tek laboratory.");
        }

        tmpAllRoomRects.Clear();
    }

    private bool TryPlaceWestTekCache(CellRect room, Map map, out Building_AncientCryptosleepPod casket)
    {
        foreach (IntVec3 cell in room.Cells.InRandomOrder())
        {
            if (!CanPlaceCasketAt(cell, map))
            {
                continue;
            }

            casket = (Building_AncientCryptosleepPod)GenSpawn.Spawn(ThingDefOf.AncientCryptosleepPod, cell, map);
            casket.openedSignal = "WestTekLaboratoryCacheOpened" + Find.UniqueIDsManager.GetNextSignalTagID();

            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.SpaceRefugee,
                Faction.OfAncients,
                PawnGenerationContext.NonPlayer,
                null,
                forceGenerateNewPawn: false,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 1f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: false,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: true,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false,
                biocodeWeaponChance: 0f,
                biocodeApparelChance: 0f,
                extraPawnForExtraRelationChance: null,
                relationWithExtraPawnChanceFactor: 1f,
                validatorPreGear: null,
                validatorPostGear: null,
                forcedTraits: null,
                prohibitedTraits: null,
                minChanceToRedressWorldPawn: null,
                fixedGender: null,
                fixedBirthName: null,
                fixedLastName: null,
                fixedIdeo: null,
                fixedBiologicalAge: null,
                fixedChronologicalAge: null,
                forceNoIdeo: false,
                forceNoBackstory: false,
                forbidAnyTitle: false,
                forceDead: true));

            HediffDef mechlinkImplant = DefDatabase<HediffDef>.GetNamedSilentFail("MechlinkImplant");
            Hediff mechlink = mechlinkImplant == null ? null : pawn.health?.hediffSet?.GetFirstHediffOfDef(mechlinkImplant);
            if (mechlink != null)
            {
                pawn.health.RemoveHediff(mechlink);
            }

            pawn.Corpse.Age = RandomSubjectCorpseAge.RandomInRange;
            pawn.relations.hidePawnRelations = true;
            CompRottable rottable = pawn.Corpse.GetComp<CompRottable>();
            if (rottable != null)
            {
                rottable.RotProgress += pawn.Corpse.Age;
            }

            casket.TryAcceptThing(pawn.Corpse, allowSpecialEffects: false);

            TriggerUnfogged triggerUnfogged = (TriggerUnfogged)ThingMaker.MakeThing(ThingDefOf.TriggerUnfogged);
            triggerUnfogged.signalTag = "WestTekLaboratoryCacheUnfogged" + Find.UniqueIDsManager.GetNextSignalTagID();
            GenSpawn.Spawn(triggerUnfogged, casket.Position, map);

            SignalAction_Message unfoggedMessage = (SignalAction_Message)ThingMaker.MakeThing(ThingDefOf.SignalAction_Message);
            unfoggedMessage.signalTag = triggerUnfogged.signalTag;
            unfoggedMessage.message = "You have located a sealed ancient West Tek cryopod. It likely contains FEV research material.";
            unfoggedMessage.messageType = MessageTypeDefOf.PositiveEvent;
            unfoggedMessage.lookTargets = casket;
            GenSpawn.Spawn(unfoggedMessage, casket.Position, map);

            SignalAction_Message openedMessage = (SignalAction_Message)ThingMaker.MakeThing(ThingDefOf.SignalAction_Message);
            openedMessage.signalTag = casket.openedSignal;
            openedMessage.message = "The ancient West Tek cache has been opened. Recover the FEV probes and unstable dosages inside.";
            openedMessage.messageType = MessageTypeDefOf.PositiveEvent;
            openedMessage.lookTargets = casket;
            GenSpawn.Spawn(openedMessage, casket.Position, map);

            ScatterDebrisUtility.ScatterFilthAroundThing(casket, map, ThingDefOf.Filth_MachineBits);
            SpawnLootNear(casket.Position, map, WestTekDefOf.WestTek_FEVProbe, Rand.RangeInclusive(2, 4));
            SpawnLootNear(casket.Position, map, WestTekDefOf.WestTek_UnrefinedFEVDosage, Rand.RangeInclusive(1, 2));
            return true;
        }

        casket = null;
        return false;
    }

    private static bool CanPlaceCasketAt(IntVec3 cell, Map map)
    {
        foreach (IntVec3 occupiedCell in GenAdj.OccupiedRect(cell, Rot4.North, ThingDefOf.AncientCryptosleepPod.Size).ExpandedBy(1))
        {
            if (occupiedCell.GetEdifice(map) != null)
            {
                return false;
            }
        }

        return true;
    }

    private static void SpawnLootNear(IntVec3 center, Map map, ThingDef def, int stackCount)
    {
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, 2.9f, useCenter: true))
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
            {
                continue;
            }

            Thing thing = ThingMaker.MakeThing(def);
            thing.stackCount = stackCount;
            GenSpawn.Spawn(thing, cell, map);
            return;
        }

        Thing fallbackThing = ThingMaker.MakeThing(def);
        fallbackThing.stackCount = stackCount;
        GenSpawn.Spawn(fallbackThing, center, map);
    }
}