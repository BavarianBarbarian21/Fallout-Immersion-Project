using System;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.RobCo;

public class RobCoVaultSite : Site
{
    public string branchDefName;
    public bool mapInitialized;
    public bool defensesInitialized;
    public bool objectiveInitialized;
    public int defenseGenerationFailureCount;
    public bool completed;
    public int rewardCorpseThingId = -1;

    private Corpse rewardCorpse;

    public RobCoPlatinumQuestBranchDef Branch => DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(branchDefName);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref branchDefName, "branchDefName");
        Scribe_Values.Look(ref mapInitialized, "mapInitialized");
        Scribe_Values.Look(ref defensesInitialized, "defensesInitialized");
        Scribe_Values.Look(ref objectiveInitialized, "objectiveInitialized");
        Scribe_Values.Look(ref defenseGenerationFailureCount, "defenseGenerationFailureCount");
        Scribe_Values.Look(ref completed, "completed");
        Scribe_Values.Look(ref rewardCorpseThingId, "rewardCorpseThingId", -1);
        Scribe_References.Look(ref rewardCorpse, "rewardCorpse");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && mapInitialized)
        {
            // Saves made before the two-phase initializer already contain the
            // defenders and normally contain an unreferenced objective on-map.
            // Let ResolveRewardCorpse adopt that body before any regeneration.
            defensesInitialized = true;
            objectiveInitialized = true;
        }
    }

    protected override void Tick()
    {
        base.Tick();
        if (!mapInitialized && HasMap && Find.TickManager.TicksGame % 250 == 0)
        {
            EnsureMapInitialized(Map);
        }

        if (HasMap && mapInitialized && Find.TickManager.TicksGame % 250 == 0)
        {
            ResolveRewardCorpse(Map);
            if (rewardCorpse != null && !rewardCorpse.Destroyed)
            {
                Current.Game.GetComponent<RobCoQuestGameComponent>()?.RegisterProtectedQuestCorpse(rewardCorpse);
            }

            TryMarkCompleted(Map);
        }
    }

    public override void Notify_MyMapAboutToBeRemoved()
    {
        // Game.DeinitAndRemoveMap clears the map listers before MyMapRemoved.
        // Evaluate while hostiles and the recovery objective are still visible.
        TryMarkCompleted(Map);
        base.Notify_MyMapAboutToBeRemoved();
    }

    public override void Notify_MyMapRemoved(Map map)
    {
        base.Notify_MyMapRemoved(map);
        RobCoQuestUtility.DestroyWorldObject(this);
    }

    public void EnsureMapInitialized(Map map)
    {
        if (mapInitialized || Branch == null || map == null)
        {
            return;
        }

        if (!defensesInitialized)
        {
            float targetCombatPower = Branch.stage2ThreatPoints;
            float currentCombatPower = map.mapPawns.AllPawnsSpawned
                .Where(pawn => !pawn.Dead && pawn.Faction == Faction.OfMechanoids)
                .Sum(pawn => Math.Max(1f, pawn.kindDef?.combatPower ?? 0f));
            int attempts = 0;
            while (currentCombatPower < targetCombatPower && attempts < 256)
            {
                attempts++;
                Pawn pawn = null;
                try
                {
                    pawn = PawnGenerator.GeneratePawn(Branch.stage2EnemyPawnKind, Faction.OfMechanoids);
                    IntVec3 cell = CellFinder.RandomClosewalkCellNear(map.Center, map, 45);
                    GenSpawn.Spawn(pawn, cell, map);
                    currentCombatPower += Math.Max(1f, pawn.kindDef?.combatPower ?? 0f);
                }
                catch (Exception ex)
                {
                    defenseGenerationFailureCount++;
                    Log.ErrorOnce($"RobCo vault enemy generation failed for {Branch.defName}; it will retry: {ex}",
                        Gen.HashCombineInt(ID, 18042027));
                    if (pawn != null && !pawn.Destroyed)
                    {
                        pawn.Discard(true);
                    }

                    break;
                }
            }

            defensesInitialized = currentCombatPower >= targetCombatPower || attempts >= 256;
            if (!defensesInitialized && defenseGenerationFailureCount >= 3)
            {
                // A permanently broken third-party pawn generator must not make
                // the unique quest impossible. Keep any defenders already made
                // and allow the recovery objective after three separate retries.
                Log.WarningOnce($"RobCo vault defense generation failed repeatedly for {Branch.defName}; continuing with the defenders that were created.",
                    Gen.HashCombineInt(ID, 18042028));
                defensesInitialized = true;
            }
        }

        if (!objectiveInitialized)
        {
            // Adopt an older or partially spawned objective before creating one.
            ResolveRewardCorpse(map);
        }

        if (!objectiveInitialized && TryGenerateRecoveryCorpse(map, null, out Corpse generatedCorpse))
        {
            rewardCorpse = generatedCorpse;
            rewardCorpseThingId = rewardCorpse.thingIDNumber;
            objectiveInitialized = true;
        }

        mapInitialized = defensesInitialized && objectiveInitialized;
    }

    private bool TryGenerateRecoveryCorpse(Map map, IntVec3? preferredCell, out Corpse generatedCorpse)
    {
        generatedCorpse = null;
        Pawn corpsePawn = null;
        Corpse corpse = null;
        try
        {
            // Kill mechanical objectives while hostile so their creation cannot
            // emit a false player-colonist death. Assign player ownership only
            // after death, as required by vanilla Bill_ResurrectMech.
            Faction corpseFaction = Branch.stage2CorpsePawnKind.RaceProps.IsMechanoid
                ? Faction.OfMechanoids
                : null;
            corpsePawn = PawnGenerator.GeneratePawn(Branch.stage2CorpsePawnKind, corpseFaction);
            corpsePawn.Kill(null);
            if (corpsePawn.RaceProps.IsMechanoid)
            {
                // Direct assignment satisfies Bill_ResurrectMech without firing
                // Pawn.SetFaction's joined-player/population notifications.
                corpsePawn.SetFactionDirect(Faction.OfPlayer);
            }

            corpse = corpsePawn.Corpse;
            if (corpse == null)
            {
                Log.ErrorOnce($"RobCo vault could not create a corpse for {Branch.stage2CorpsePawnKind.defName}; it will retry.",
                    Gen.HashCombineInt(ID, 18042029));
                if (!corpsePawn.Destroyed)
                {
                    corpsePawn.Discard(true);
                }

                return false;
            }

            PrepareRecoveryCorpse(corpse);
            IntVec3 corpseCell = preferredCell.HasValue && preferredCell.Value.InBounds(map)
                ? preferredCell.Value
                : map.Center;
            GenSpawn.Spawn(corpse, corpseCell, map);
            generatedCorpse = corpse;
            return true;
        }
        catch (Exception ex)
        {
            Log.ErrorOnce($"RobCo vault recovery-target generation failed for {Branch.defName}: {ex}",
                Gen.HashCombineInt(ID, 18042026));
            corpse ??= corpsePawn?.Corpse;
            if (corpse != null && corpse.Spawned && corpse.MapHeld == map)
            {
                // SpawnSetup can throw after registering the Thing. Adopt that
                // exact corpse so the retry path cannot create a duplicate.
                PrepareRecoveryCorpse(corpse);
                generatedCorpse = corpse;
                return true;
            }

            if (corpse != null && !corpse.Destroyed && !corpse.Spawned)
            {
                corpse.Discard(true);
            }
            else if (corpsePawn != null && !corpsePawn.Destroyed && corpsePawn.Corpse == null)
            {
                corpsePawn.Discard(true);
            }

            return false;
        }
    }

    private void ResolveRewardCorpse(Map map)
    {
        if (map == null)
        {
            return;
        }

        if (rewardCorpse != null)
        {
            if (IsLegacyUniversityObjective(rewardCorpse) && rewardCorpse.MapHeld == map)
            {
                ReplaceLegacyUniversityObjective(map, rewardCorpse);
            }

            if (rewardCorpse != null)
            {
                PrepareRecoveryCorpse(rewardCorpse);
            }

            return;
        }

        var corpses = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse).OfType<Corpse>();
        if (rewardCorpseThingId >= 0)
        {
            rewardCorpse = corpses.FirstOrDefault(corpse => corpse.thingIDNumber == rewardCorpseThingId);
        }
        else if (Branch?.stage2CorpsePawnKind != null)
        {
            // Save migration for vaults generated before the dedicated reference
            // was persisted.  There is only one recovery corpse of this kind.
            rewardCorpse = corpses.FirstOrDefault(corpse => corpse.InnerPawn?.kindDef == Branch.stage2CorpsePawnKind);
            if (rewardCorpse == null && Branch.defName == "RobCo_PlatinumBranch_University")
            {
                // Legacy University vaults incorrectly contained a Pacificator;
                // replace it with the Courser which the Synth branch can restore.
                PawnKindDef legacyKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("RobCo_Pacificator");
                Corpse legacyCorpse = corpses.FirstOrDefault(corpse => corpse.InnerPawn?.kindDef == legacyKind);
                if (legacyCorpse != null)
                {
                    ReplaceLegacyUniversityObjective(map, legacyCorpse);
                }
            }

        }

        if (rewardCorpse != null && IsLegacyUniversityObjective(rewardCorpse))
        {
            ReplaceLegacyUniversityObjective(map, rewardCorpse);
        }

        if (rewardCorpse != null)
        {
            rewardCorpseThingId = rewardCorpse.thingIDNumber;
            objectiveInitialized = true;
            mapInitialized = defensesInitialized;
            PrepareRecoveryCorpse(rewardCorpse);
            return;
        }

        if (objectiveInitialized || rewardCorpseThingId >= 0)
        {
            // An older build assigned the ID before spawning the corpse. If that
            // spawn failed, no referenced Thing exists and the objective must be
            // regenerated instead of leaving the vault permanently unwinnable.
            rewardCorpseThingId = -1;
            objectiveInitialized = false;
            mapInitialized = false;
        }
    }

    private bool IsLegacyUniversityObjective(Corpse corpse)
    {
        return Branch?.defName == "RobCo_PlatinumBranch_University"
            && corpse?.InnerPawn?.kindDef?.defName == "RobCo_Pacificator";
    }

    private void ReplaceLegacyUniversityObjective(Map map, Corpse legacyCorpse)
    {
        IntVec3 cell = legacyCorpse.PositionHeld.IsValid ? legacyCorpse.PositionHeld : map.Center;
        if (!TryGenerateRecoveryCorpse(map, cell, out Corpse replacement))
        {
            objectiveInitialized = false;
            mapInitialized = false;
            return;
        }

        rewardCorpse = replacement;
        rewardCorpseThingId = replacement.thingIDNumber;
        objectiveInitialized = true;
        mapInitialized = defensesInitialized;
        if (!legacyCorpse.Destroyed)
        {
            legacyCorpse.Destroy(DestroyMode.Vanish);
        }
    }

    private static void PrepareRecoveryCorpse(Corpse corpse)
    {
        if (corpse?.InnerPawn?.RaceProps.IsMechanoid == true && corpse.InnerPawn.Faction != Faction.OfPlayer)
        {
            corpse.InnerPawn.SetFactionDirect(Faction.OfPlayer);
        }

        int oldDeathTick = Math.Max(0, Find.TickManager.TicksGame - RobCoQuestUtility.TicksPerDay);
        if (corpse != null && corpse.timeOfDeath > oldDeathTick)
        {
            // Older than the resurrection-mech ability's short corpse-age limit,
            // while the dedicated restoration bill deliberately has no age cap.
            corpse.timeOfDeath = oldDeathTick;
        }

        Current.Game.GetComponent<RobCoQuestGameComponent>()?.RegisterProtectedQuestCorpse(corpse);
    }

    private void TryMarkCompleted(Map map)
    {
        if (completed || map == null || !mapInitialized)
        {
            return;
        }

        ResolveRewardCorpse(map);
        if (rewardCorpse != null && !rewardCorpse.Destroyed)
        {
            Current.Game.GetComponent<RobCoQuestGameComponent>()?.RegisterProtectedQuestCorpse(rewardCorpse);
        }

        bool hostilePawnRemains = map.mapPawns.AllPawnsSpawned.Any(
            pawn => !pawn.Dead && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer));
        bool objectiveRecovered = rewardCorpse != null
            && !rewardCorpse.Destroyed
            && rewardCorpse.MapHeld != map;

        if (hostilePawnRemains || !objectiveRecovered)
        {
            return;
        }

        completed = true;
        RobCoQuestUtility.SendLetter(
            "RobCoQuestVaultClearedLabel".Translate().Resolve(),
            "RobCoQuestVaultClearedText".Translate().Resolve(),
            LetterDefOf.PositiveEvent);
    }
}
