using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace FIP.HHTools;

public class HHToolsMotelMissionSite : Site
{
    public Faction sourceFaction;
    public Settlement sourceSettlement;
    public HHToolsPoliticalMissionType missionType;
    public HHToolsCrimeBoss eliminationTarget;
    public Quest quest;
    public float threatPoints;
    public int expirationTick = -1;
    public bool mapInitialized;
    public bool resolved;
    public string resolutionReason;
    public Thing objectiveThing;
    public List<Pawn> hostileTargets = [];
    public List<Pawn> protectedTargets = [];
    public List<Pawn> questHerd = [];

    private int nextMissionCheckTick;

    public void InitializeMissionMap(Map map)
    {
        if (mapInitialized || map == null)
        {
            return;
        }

        mapInitialized = true;
        hostileTargets ??= [];
        protectedTargets ??= [];
        questHerd ??= [];

        IntVec3 parkingCenter = new(70, 0, 49);
        IntVec3 serviceWing = new(38, 0, 54);
        IntVec3 guestWing = new(76, 0, 73);

        switch (missionType)
        {
            case HHToolsPoliticalMissionType.RangerBounty:
                SpawnHostileCombatGroup(map, guestWing, threatPoints, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.RangerRescue:
                SpawnProtectedGroup(map, serviceWing, 3, "ranger patrol");
                SpawnHostileCombatGroup(map, parkingCenter, threatPoints, defendPoint: false);
                break;
            case HHToolsPoliticalMissionType.BrahminRecoverHerd:
                SpawnQuestHerd(map, parkingCenter, playerOwned: true);
                SpawnHostileCombatGroup(map, guestWing, threatPoints, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.BrahminDefendHerd:
                SpawnQuestHerd(map, parkingCenter, playerOwned: false);
                protectedTargets.AddRange(questHerd);
                SpawnProtectedGroup(map, serviceWing, 2, "Baron guards");
                SpawnHostileCombatGroup(map, new IntVec3(89, 0, 47), threatPoints, defendPoint: false);
                break;
            case HHToolsPoliticalMissionType.CaravanEscort:
                SpawnProtectedGroup(map, serviceWing, 4, "caravan guards");
                SpawnHostileCombatGroup(map, new IntVec3(91, 0, 48), threatPoints, defendPoint: false);
                break;
            case HHToolsPoliticalMissionType.CaravanRecoverCargo:
                objectiveThing = SpawnObjective(
                    map,
                    "HHTools_CaravanCargo",
                    new IntVec3(84, 0, 76));
                SpawnHostileCombatGroup(map, guestWing, threatPoints, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.GunRunnerSchematics:
                objectiveThing = SpawnObjective(
                    map,
                    "HHTools_WeaponSchematics",
                    new IntVec3(41, 0, 61));
                SpawnHostileCombatGroup(map, guestWing, threatPoints * 1.1f, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.GunRunnerDefense:
                SpawnProtectedGroup(map, serviceWing, 3, "gun runner crew");
                SpawnHostileCombatGroup(map, parkingCenter, threatPoints * 1.1f, defendPoint: false);
                break;
            case HHToolsPoliticalMissionType.ArenaManhunters:
                SpawnManhunterPack(map, parkingCenter);
                break;
            case HHToolsPoliticalMissionType.ArenaRivals:
                SpawnHostileCombatGroup(map, guestWing, threatPoints * 1.15f, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.SlaverDefense:
                SpawnProtectedGroup(map, serviceWing, 3, "slaver crew");
                SpawnHostileCombatGroup(map, parkingCenter, threatPoints * 1.1f, defendPoint: false);
                break;
            case HHToolsPoliticalMissionType.SlaverRaid:
                SpawnHostileCombatGroup(map, guestWing, threatPoints * 1.2f, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.SaloonAlcohol:
                objectiveThing = SpawnObjective(
                    map,
                    "HHTools_AlcoholShipment",
                    new IntVec3(87, 0, 76));
                SpawnHostileCombatGroup(map, guestWing, threatPoints, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.SaloonDealers:
                SpawnHostileCombatGroup(map, guestWing, threatPoints * 1.1f, defendPoint: true);
                break;
            case HHToolsPoliticalMissionType.FamilyElimination:
                SpawnHostileCombatGroup(
                    map,
                    guestWing,
                    Math.Max(750f, threatPoints * 1.65f),
                    defendPoint: true,
                    includeFamilyBoss: true);
                break;
        }

        nextMissionCheckTick = Find.TickManager.TicksGame + 120;
    }

    public void ResolveMission(bool success, string reason)
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        resolutionReason = reason;
        HHToolsFactionPoliticsTracker.Instance?.NotifyPoliticalMissionResolved(this, success);

        if (quest is { State: QuestState.Ongoing })
        {
            quest.End(
                success ? QuestEndOutcome.Success : QuestEndOutcome.Fail,
                sendLetter: true,
                playSound: true);
        }
    }

    protected override void Tick()
    {
        base.Tick();
        if (resolved || Find.TickManager.TicksGame < nextMissionCheckTick)
        {
            return;
        }

        nextMissionCheckTick = Find.TickManager.TicksGame + 120;
        EvaluateMission();
    }

    public override bool ShouldRemoveMapNow(out bool alsoRemoveWorldObject)
    {
        bool shouldRemove = base.ShouldRemoveMapNow(out _);
        alsoRemoveWorldObject = resolved;
        return shouldRemove;
    }

    public override void Destroy()
    {
        if (!resolved)
        {
            ResolveMission(success: false, "The motel site was abandoned.");
        }

        base.Destroy();
    }

    public override string GetInspectString()
    {
        string inspect = base.GetInspectString();
        if (resolved)
        {
            return inspect;
        }

        string missionStatus = HHToolsFactionPoliticsUtility.RequiresReturnToSettlement(missionType)
            ? "Return the mission objective to " + (sourceSettlement?.LabelCap ?? "the issuing settlement") + "."
            : HHToolsFactionPoliticsUtility.GetMissionDescription(missionType);
        return inspect.NullOrEmpty() ? missionStatus : inspect + "\n" + missionStatus;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref sourceFaction, "sourceFaction");
        Scribe_References.Look(ref sourceSettlement, "sourceSettlement");
        Scribe_Values.Look(ref missionType, "missionType");
        Scribe_Values.Look(ref eliminationTarget, "eliminationTarget");
        Scribe_References.Look(ref quest, "quest");
        Scribe_Values.Look(ref threatPoints, "threatPoints");
        Scribe_Values.Look(ref expirationTick, "expirationTick", -1);
        Scribe_Values.Look(ref mapInitialized, "mapInitialized");
        Scribe_Values.Look(ref resolved, "resolved");
        Scribe_Values.Look(ref resolutionReason, "resolutionReason");
        Scribe_References.Look(ref objectiveThing, "objectiveThing");
        Scribe_Collections.Look(ref hostileTargets, "hostileTargets", LookMode.Reference);
        Scribe_Collections.Look(ref protectedTargets, "protectedTargets", LookMode.Reference);
        Scribe_Collections.Look(ref questHerd, "questHerd", LookMode.Reference);
        Scribe_Values.Look(ref nextMissionCheckTick, "nextMissionCheckTick");

        hostileTargets ??= [];
        protectedTargets ??= [];
        questHerd ??= [];
    }

    private void EvaluateMission()
    {
        if (sourceSettlement == null || sourceSettlement.Destroyed)
        {
            ResolveMission(success: false, "The settlement that issued the mission no longer exists.");
            return;
        }

        if (expirationTick > 0 && Find.TickManager.TicksGame >= expirationTick)
        {
            ResolveMission(success: false, "The mission expired after 15 days.");
            return;
        }

        if (!mapInitialized)
        {
            return;
        }

        switch (missionType)
        {
            case HHToolsPoliticalMissionType.BrahminRecoverHerd:
                EvaluateHerdRecovery();
                return;
            case HHToolsPoliticalMissionType.CaravanRecoverCargo:
            case HHToolsPoliticalMissionType.GunRunnerSchematics:
            case HHToolsPoliticalMissionType.SaloonAlcohol:
                EvaluateItemRecovery();
                return;
        }

        if (IsDefenseMission(missionType) && LivingProtectedCount() == 0)
        {
            ResolveMission(success: false, "Every protected target was lost.");
            return;
        }

        if (hostileTargets.Count > 0 && hostileTargets.All(IsNeutralized))
        {
            int requiredSurvivors = missionType == HHToolsPoliticalMissionType.BrahminDefendHerd ? 2 : 1;
            if (!IsDefenseMission(missionType) || LivingProtectedCount() >= requiredSurvivors)
            {
                ResolveMission(success: true, "The motel objective was secured.");
            }
        }
    }

    private void EvaluateHerdRecovery()
    {
        int livingAnimals = questHerd.Count(pawn => pawn != null && !pawn.Dead && !pawn.Destroyed);
        if (livingAnimals < 2)
        {
            ResolveMission(success: false, "Fewer than two herd animals survived.");
            return;
        }

        if (!hostileTargets.All(IsNeutralized))
        {
            return;
        }

        Caravan returningCaravan = Find.WorldObjects.Caravans.FirstOrDefault(caravan =>
            caravan.IsPlayerControlled
            && CaravanVisitUtility.SettlementVisitedNow(caravan) == sourceSettlement
            && questHerd.Count(caravan.PawnsListForReading.Contains) >= 2);
        if (returningCaravan != null)
        {
            foreach (Pawn animal in questHerd.Where(animal =>
                         animal != null
                         && !animal.Destroyed
                         && !animal.Dead))
            {
                Caravan holder = Find.WorldObjects.Caravans.FirstOrDefault(caravan =>
                    caravan.PawnsListForReading.Contains(animal));
                holder?.RemovePawn(animal);
                animal.Destroy();
            }

            ResolveMission(success: true, "The recovered herd was returned to the Barons.");
        }
    }

    private void EvaluateItemRecovery()
    {
        if (objectiveThing == null || objectiveThing.Destroyed)
        {
            ResolveMission(success: false, "The mission objective was destroyed.");
            return;
        }

        foreach (Caravan caravan in Find.WorldObjects.Caravans)
        {
            if (!caravan.IsPlayerControlled
                || CaravanVisitUtility.SettlementVisitedNow(caravan) != sourceSettlement)
            {
                continue;
            }

            List<Thing> contents = ThingOwnerUtility.GetAllThingsRecursively(caravan);
            if (contents.Contains(objectiveThing))
            {
                objectiveThing.Destroy();
                ResolveMission(success: true, "The recovered objective was delivered.");
                return;
            }
        }
    }

    private static bool IsDefenseMission(HHToolsPoliticalMissionType mission)
    {
        return mission is HHToolsPoliticalMissionType.RangerRescue
            or HHToolsPoliticalMissionType.BrahminDefendHerd
            or HHToolsPoliticalMissionType.CaravanEscort
            or HHToolsPoliticalMissionType.GunRunnerDefense
            or HHToolsPoliticalMissionType.SlaverDefense;
    }

    private static bool IsNeutralized(Pawn pawn)
    {
        return pawn == null
            || pawn.Destroyed
            || pawn.Dead
            || pawn.Downed
            || pawn.IsPrisonerOfColony
            || !pawn.Spawned;
    }

    private int LivingProtectedCount()
    {
        return protectedTargets.Count(pawn =>
            pawn != null
            && !pawn.Destroyed
            && !pawn.Dead);
    }

    private void SpawnQuestHerd(Map map, IntVec3 center, bool playerOwned)
    {
        PawnKindDef animalKind = HHToolsFactionPoliticsUtility.GetHerdAnimalKind();
        Faction animalFaction = playerOwned ? Faction.OfPlayer : sourceFaction;

        for (int index = 0; index < 3; index += 1)
        {
            Pawn animal = PawnGenerator.GeneratePawn(animalKind, animalFaction);
            SpawnPawnNear(animal, center, map, 7);
            questHerd.Add(animal);
        }
    }

    private void SpawnProtectedGroup(Map map, IntVec3 center, int desiredCount, string groupLabel)
    {
        List<Pawn> pawns = GenerateCombatPawns(
            sourceFaction,
            Math.Max(250f, threatPoints * 0.45f),
            desiredCount);

        List<Pawn> spawnedPawns = [];
        foreach (Pawn pawn in pawns.Take(desiredCount))
        {
            SpawnPawnNear(pawn, center, map, 8);
            protectedTargets.Add(pawn);
            spawnedPawns.Add(pawn);
        }

        if (spawnedPawns.Count == 0)
        {
            Log.Warning($"[FIP H&H Tools] Could not generate protected {groupLabel} for {missionType}.");
        }
        else
        {
            LordMaker.MakeNewLord(
                sourceFaction,
                new LordJob_DefendPoint(center, 12f, 18f),
                map,
                spawnedPawns.Where(pawn => pawn.Spawned));
        }
    }

    private void SpawnHostileCombatGroup(
        Map map,
        IntVec3 center,
        float points,
        bool defendPoint,
        bool includeFamilyBoss = false)
    {
        Faction hostileFaction = ResolveHostileFaction();
        List<Pawn> pawns = GenerateCombatPawns(
            hostileFaction,
            Math.Max(300f, points),
            includeFamilyBoss ? 8 : 5);

        if (pawns.Count == 0)
        {
            Pawn fallback = PawnGenerator.GeneratePawn(PawnKindDefOf.AncientSoldier, hostileFaction);
            pawns.Add(fallback);
        }

        if (includeFamilyBoss && pawns.Count > 0)
        {
            Pawn familyLeader = HHToolsFactionPoliticsTracker.Instance
                ?.GetOrCreateState(sourceFaction)
                ?.GetCrimeBoss(eliminationTarget)
                ?.leaderPawn;
            pawns[0].Name = familyLeader?.Name
                ?? new NameSingle(HHToolsFactionPoliticsUtility.GetFallbackLeaderName(eliminationTarget));
        }

        foreach (Pawn pawn in pawns)
        {
            SpawnPawnNear(pawn, center, map, 12);
            hostileTargets.Add(pawn);
        }

        if (hostileTargets.Count == 0)
        {
            return;
        }

        LordJob lordJob;
        if (defendPoint || protectedTargets.Count == 0)
        {
            lordJob = new LordJob_DefendPoint(center, 18f, 28f);
        }
        else
        {
            lordJob = new LordJob_AssaultThings(
                hostileFaction,
                protectedTargets.Cast<Thing>().ToList(),
                1f,
                useAvoidGridSmart: false);
        }

        LordMaker.MakeNewLord(
            hostileFaction,
            lordJob,
            map,
            hostileTargets.Where(pawn => pawn.Spawned));
    }

    private void SpawnManhunterPack(Map map, IntVec3 center)
    {
        string[] animalDefNames = ["Warg", "Cougar", "Bear_Grizzly"];
        int count = Mathf.Clamp(Mathf.RoundToInt(threatPoints / 120f), 4, 9);

        for (int index = 0; index < count; index += 1)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(
                animalDefNames[index % animalDefNames.Length]);
            if (kind == null)
            {
                continue;
            }

            Pawn animal = PawnGenerator.GeneratePawn(kind);
            SpawnPawnNear(animal, center, map, 14);
            animal.mindState.mentalStateHandler.TryStartMentalState(
                MentalStateDefOf.ManhunterPermanent,
                "Driven feral around the abandoned motel.",
                forceWake: true);
            hostileTargets.Add(animal);
        }
    }

    private List<Pawn> GenerateCombatPawns(Faction faction, float points, int desiredCount)
    {
        List<Pawn> pawns = [];
        if (faction == null)
        {
            return pawns;
        }

        try
        {
            PawnGroupMakerParms parms = new()
            {
                faction = faction,
                groupKind = PawnGroupKindDefOf.Combat,
                points = points,
                tile = Tile,
                inhabitants = false,
                generateFightersOnly = true
            };
            pawns.AddRange(PawnGroupMakerUtility.GeneratePawns(parms, warnOnZeroResults: false));
        }
        catch (Exception exception)
        {
            Log.Warning($"[FIP H&H Tools] Combat group generation failed for {faction}: {exception.Message}");
        }

        PawnKindDef fallbackKind = faction == sourceFaction
            ? DefDatabase<PawnKindDef>.GetNamedSilentFail(
                sourceFaction.GetPoliticsExtension()?.system == HHToolsFactionPoliticalSystem.Authoritarian
                    ? "HHTools_Raider_Fighter"
                    : "HHTools_Settlement_Fighter")
            : PawnKindDefOf.AncientSoldier;

        while (pawns.Count < desiredCount && fallbackKind != null)
        {
            pawns.Add(PawnGenerator.GeneratePawn(fallbackKind, faction));
        }

        return pawns;
    }

    private Faction ResolveHostileFaction()
    {
        List<Faction> candidates = Find.FactionManager.AllFactionsListForReading
            .Where(faction =>
                faction != null
                && !faction.defeated
                && !faction.Hidden
                && faction.HostileTo(Faction.OfPlayer)
                && faction != sourceFaction
                && (sourceFaction == null || faction.HostileTo(sourceFaction))
                && faction.def.humanlikeFaction)
            .ToList();

        return candidates.TryRandomElement(out Faction faction)
            ? faction
            : Faction.OfAncientsHostile;
    }

    private static Thing SpawnObjective(Map map, string defName, IntVec3 cell)
    {
        ThingDef def = DefDatabase<ThingDef>.GetNamed(defName);
        Thing objective = ThingMaker.MakeThing(def);
        return GenSpawn.Spawn(objective, cell, map);
    }

    private static void SpawnPawnNear(Pawn pawn, IntVec3 center, Map map, int radius)
    {
        if (!CellFinder.TryFindRandomSpawnCellForPawnNear(
                center,
                map,
                out IntVec3 cell,
                radius,
                candidate => candidate.Standable(map)))
        {
            cell = CellFinder.RandomClosewalkCellNear(center, map, radius);
        }

        GenSpawn.Spawn(pawn, cell, map);
    }
}
