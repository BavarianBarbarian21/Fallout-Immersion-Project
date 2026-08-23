using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP.RobCo;

public class RobCoQuestGameComponent : GameComponent
{
    private static readonly HashSet<string> LegacyUniqueProductionRecipes = new()
    {
        "RobCo_LibertyPrime_Recipe",
        "RobCo_WarMachine_Recipe"
    };

    private bool offerScheduled;
    private bool offerResolved;
    private bool offerSent;
    private int offerFireTick = -1;
    private string selectedBranchDefName;
    private int selectedFactionId = -1;
    private string pendingVaultBranchDefName;
    private bool vaultPlacementFailureLogged;
    private bool selectionMigrationDone;
    private Corpse protectedQuestCorpse;

    public RobCoQuestGameComponent(Game game)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref offerScheduled, "offerScheduled");
        Scribe_Values.Look(ref offerResolved, "offerResolved");
        Scribe_Values.Look(ref offerSent, "offerSent");
        Scribe_Values.Look(ref offerFireTick, "offerFireTick", -1);
        Scribe_Values.Look(ref selectedBranchDefName, "selectedBranchDefName");
        Scribe_Values.Look(ref selectedFactionId, "selectedFactionId", -1);
        Scribe_Values.Look(ref pendingVaultBranchDefName, "pendingVaultBranchDefName");
        Scribe_Values.Look(ref vaultPlacementFailureLogged, "vaultPlacementFailureLogged");
        Scribe_Values.Look(ref selectionMigrationDone, "selectionMigrationDone");
        Scribe_References.Look(ref protectedQuestCorpse, "protectedQuestCorpse");
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        MigrateLegacyUniqueProductionBills();
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % 250 != 0)
        {
            return;
        }

        MaintainProtectedQuestCorpse();
        TryRevealPendingVaultSite();
        MigrateLegacyQuestSelection();

        if (offerResolved)
        {
            RecoverSelectedBranchFromActiveSite();
            EnsureSelectedBranchResearchUnlocked();
            return;
        }

        if (!offerScheduled)
        {
            if (RobCoQuestDefOf.UltraMechtech.IsFinished)
            {
                offerScheduled = true;
                offerFireTick = Find.TickManager.TicksGame + RobCoQuestUtility.OfferDelayTicks;
            }

            return;
        }

        if (!offerSent && Find.TickManager.TicksGame >= offerFireTick)
        {
            TrySendOfferLetter();
        }
    }

    public bool AcceptOffer(RobCoPlatinumQuestBranchDef branch, Faction faction)
    {
        if (offerResolved || branch?.unlockResearch == null || faction == null)
        {
            return false;
        }

        if (!RobCoQuestUtility.TryFindSiteTile(12, 24, out PlanetTile tile))
        {
            RobCoQuestUtility.SendLetter(
                "RobCoQuestTrailLostLabel".Translate().Resolve(),
                "RobCoQuestTrailLostText".Translate().Resolve(),
                LetterDefOf.NegativeEvent);
            return false;
        }

        RobCoCourierCamp camp = (RobCoCourierCamp)WorldObjectMaker.MakeWorldObject(RobCoQuestDefOf.RobCo_CourierCamp);
        camp.Tile = tile;
        camp.SetFaction(faction);
        camp.branchDefName = branch.defName;
        camp.expirationTick = Find.TickManager.TicksGame + RobCoQuestUtility.CourierExpiryTicks;
        camp.EnsureSitePart();
        Find.WorldObjects.Add(camp);

        // Do not resolve the one-shot offer until its map marker exists.  If tile
        // selection failed above, the choice letter remains usable and can retry.
        offerResolved = true;
        offerSent = true;
        selectedBranchDefName = branch.defName;
        selectedFactionId = faction.loadID;
        selectionMigrationDone = true;
        UnlockBranchResearch(branch);

        RobCoQuestUtility.SendLetter(
            "RobCoQuestCourierInterceptedLabel".Translate().Resolve(),
            "RobCoQuestCourierInterceptedText".Translate(RobCoQuestUtility.FormatOptionLabel(branch, faction)).Resolve(),
            LetterDefOf.PositiveEvent,
            new LookTargets(camp));
        return true;
    }

    public void DeclineOffer()
    {
        offerResolved = true;
        offerSent = true;
        selectionMigrationDone = true;
    }

    private static void UnlockBranchResearch(RobCoPlatinumQuestBranchDef branch)
    {
        if (branch?.unlockResearch?.requiredAnalyzed == null || Find.AnalysisManager == null)
        {
            return;
        }

        try
        {
            // Complete only the branch's hidden analysis gate.  The research
            // project itself remains at zero progress and must still be researched.
            foreach (ThingDef requirement in branch.unlockResearch.requiredAnalyzed)
            {
                CompProperties_CompAnalyzableUnlockResearch analyzable =
                    requirement?.GetCompProperties<CompProperties_CompAnalyzableUnlockResearch>();
                if (analyzable != null)
                {
                    Find.AnalysisManager.ForceCompleteAnalysisProgress(analyzable.analysisID);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"RobCo quest branch unlock failed for {branch.defName}: {ex.Message}");
        }
    }

    private void MigrateLegacyQuestSelection()
    {
        if (selectionMigrationDone || !offerResolved)
        {
            return;
        }

        RecoverSelectedBranchFromActiveSite();
        if (selectedBranchDefName.NullOrEmpty() && !pendingVaultBranchDefName.NullOrEmpty())
        {
            selectedBranchDefName = pendingVaultBranchDefName;
        }

        if (selectedBranchDefName.NullOrEmpty())
        {
            List<RobCoPlatinumQuestBranchDef> finishedBranches =
                DefDatabase<RobCoPlatinumQuestBranchDef>.AllDefsListForReading
                    .Where(branch => branch.unlockResearch?.IsFinished == true)
                    .ToList();
            if (finishedBranches.Count == 1)
            {
                selectedBranchDefName = finishedBranches[0].defName;
            }
        }

        if (selectedBranchDefName.NullOrEmpty())
        {
            // Old builds could record the offer as resolved without persisting a
            // usable branch. Re-offer once instead of leaving that save deadlocked.
            offerResolved = false;
            offerSent = false;
            offerScheduled = true;
            offerFireTick = Find.TickManager.TicksGame + 250;
        }

        selectionMigrationDone = true;
    }

    private void MigrateLegacyUniqueProductionBills()
    {
        int removedBills = 0;
        foreach (Map map in Find.Maps)
        {
            foreach (Building_MechGestator gestator in map.listerBuildings.allBuildingsColonist
                         .OfType<Building_MechGestator>())
            {
                foreach (Bill bill in gestator.BillStack.Bills.ToList())
                {
                    if (bill is Bill_ProductionMech
                        && LegacyUniqueProductionRecipes.Contains(bill.recipe?.defName))
                    {
                        // A saved Bill_ProductionMech may already be Formed, with
                        // its finished pawn waiting inside the gestator. Vanilla's
                        // Building_MechGestator.EjectContents destroys directly
                        // held pawns, so place every completed output on the map
                        // before deleting the obsolete bill.
                        bool heldPawnsSafe = true;
                        if (ReferenceEquals(gestator.ActiveBill, bill))
                        {
                            ThingOwner heldThings = gestator.GetDirectlyHeldThings();
                            foreach (Pawn heldPawn in heldThings.OfType<Pawn>().ToList())
                            {
                                if (!heldThings.TryDrop(
                                        heldPawn,
                                        gestator.InteractionCell,
                                        gestator.Map,
                                        ThingPlaceMode.Near,
                                        out Thing _))
                                {
                                    heldPawnsSafe = false;
                                    Log.Error($"RobCo could not safely eject a completed pawn from {gestator.LabelCap}; its obsolete production bill was left intact to prevent pawn loss.");
                                    break;
                                }
                            }
                        }

                        if (!heldPawnsSafe)
                        {
                            continue;
                        }

                        // BillStack.Delete notifies the gestator. Vanilla then
                        // ejects every remaining item ingredient, so the
                        // incompatible serialized bill cannot call
                        // Bill_ProductionMech.CreateProducts on a resurrection Def.
                        gestator.BillStack.Delete(bill);
                        removedBills++;
                    }
                }
            }
        }

        if (removedBills > 0)
        {
            Log.Warning($"RobCo removed {removedBills} obsolete Liberty Prime/War Machine production bill(s); active ingredients were ejected for recovery. The recovered prototype corpse and platinum chip are now required at a restoration gantry.");
        }
    }

    private void RecoverSelectedBranchFromActiveSite()
    {
        if (!selectedBranchDefName.NullOrEmpty())
        {
            return;
        }

        RobCoCourierCamp camp = Find.WorldObjects.AllWorldObjects.OfType<RobCoCourierCamp>().FirstOrDefault();
        if (camp != null)
        {
            selectedBranchDefName = camp.branchDefName;
            selectedFactionId = camp.Faction?.loadID ?? -1;
            return;
        }

        RobCoVaultSite vault = Find.WorldObjects.AllWorldObjects.OfType<RobCoVaultSite>().FirstOrDefault();
        if (vault != null)
        {
            selectedBranchDefName = vault.branchDefName;
        }
    }

    private void EnsureSelectedBranchResearchUnlocked()
    {
        if (selectedBranchDefName.NullOrEmpty())
        {
            return;
        }

        RobCoPlatinumQuestBranchDef branch = DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(selectedBranchDefName);
        UnlockBranchResearch(branch);
    }

    public void QueueVaultSite(RobCoPlatinumQuestBranchDef branch)
    {
        if (branch == null)
        {
            return;
        }

        pendingVaultBranchDefName = branch.defName;
        vaultPlacementFailureLogged = false;
        TryRevealPendingVaultSite();
    }

    public void RegisterProtectedQuestCorpse(Corpse corpse)
    {
        if (corpse == null)
        {
            return;
        }

        protectedQuestCorpse = corpse;
        ProtectCorpseFromRot(corpse);
    }

    private bool TryRevealPendingVaultSite()
    {
        if (pendingVaultBranchDefName.NullOrEmpty())
        {
            return true;
        }

        RobCoPlatinumQuestBranchDef branch = DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(pendingVaultBranchDefName);
        if (branch == null)
        {
            if (!vaultPlacementFailureLogged)
            {
                Log.Error($"RobCo quest cannot reveal vault: branch '{pendingVaultBranchDefName}' no longer exists.");
                vaultPlacementFailureLogged = true;
            }

            return false;
        }

        RobCoVaultSite existingSite = Find.WorldObjects.AllWorldObjects
            .OfType<RobCoVaultSite>()
            .FirstOrDefault(site => site.branchDefName == branch.defName);
        if (existingSite != null)
        {
            pendingVaultBranchDefName = null;
            vaultPlacementFailureLogged = false;
            return true;
        }

        if (!RobCoQuestUtility.TryFindSiteTile(30, 60, out PlanetTile tile))
        {
            if (!vaultPlacementFailureLogged)
            {
                Log.Warning($"RobCo quest could not place the {branch.defName} vault yet; it will retry automatically.");
                vaultPlacementFailureLogged = true;
            }

            return false;
        }

        RobCoVaultSite site = (RobCoVaultSite)WorldObjectMaker.MakeWorldObject(RobCoQuestDefOf.RobCo_VaultSite);
        site.Tile = tile;
        site.SetFaction(Faction.OfMechanoids);
        site.branchDefName = branch.defName;
        site.parts = new List<SitePart>
        {
            new(site, RobCoQuestDefOf.RobCo_AncientVault, new SitePartParams
            {
                threatPoints = branch.stage2ThreatPoints,
                points = branch.stage2ThreatPoints,
                interiorThreatPoints = branch.stage2ThreatPoints,
                exteriorThreatPoints = 0f
            })
        };
        Find.WorldObjects.Add(site);

        string text = RobCoQuestUtility.GetBranchStage2LetterText(branch);
        string label = RobCoQuestUtility.GetBranchStage2LetterLabel(branch);

        RobCoQuestUtility.SendLetter(label, text, LetterDefOf.PositiveEvent, new LookTargets(site));
        pendingVaultBranchDefName = null;
        vaultPlacementFailureLogged = false;
        return true;
    }

    private void MaintainProtectedQuestCorpse()
    {
        if (protectedQuestCorpse == null)
        {
            return;
        }

        if (protectedQuestCorpse.Destroyed)
        {
            protectedQuestCorpse = null;
            return;
        }

        ProtectCorpseFromRot(protectedQuestCorpse);
    }

    private static void ProtectCorpseFromRot(Corpse corpse)
    {
        if (corpse?.InnerPawn?.RaceProps.IsMechanoid == true && corpse.InnerPawn.Faction != Faction.OfPlayer)
        {
            // Save migration for quest wrecks generated by older builds. Direct
            // assignment avoids false joined-player/population notifications.
            corpse.InnerPawn.SetFactionDirect(Faction.OfPlayer);
        }

        corpse.debugRotLocked = true;
        CompRottable rottable = corpse.GetComp<CompRottable>();
        if (rottable != null)
        {
            // Unlike Corpse.debugRotLocked, this flag is scribed by
            // CompRottable and therefore survives maps, caravans, and reloads.
            rottable.disabled = true;
            if (rottable.RotProgress > 0f)
            {
                rottable.RotProgress = 0f;
            }
        }
    }

    private void TrySendOfferLetter()
    {
        List<RobCoPlatinumQuestBranchDef> branches = DefDatabase<RobCoPlatinumQuestBranchDef>.AllDefsListForReading
            .OrderBy(branch => branch.defName)
            .ToList();
        List<Faction> factions = RobCoQuestUtility.EligibleCourierFactions();

        if (branches.Count == 0 || factions.Count == 0)
        {
            return;
        }

        List<(RobCoPlatinumQuestBranchDef branch, Faction faction)> options = new();
        List<Faction> shuffledFactions = factions.InRandomOrder().ToList();
        for (int index = 0; index < branches.Count; index++)
        {
            // Offer every branch even in a sparse world.  A faction can carry
            // more than one competing contract, and pawn-generation failure has
            // a safe quest fallback when the player reaches the site.
            options.Add((branches[index], shuffledFactions[index % shuffledFactions.Count]));
        }

        if (options.Count == 0)
        {
            return;
        }

        ChoiceLetter_RobCoPlatinumChipOpportunity letter = (ChoiceLetter_RobCoPlatinumChipOpportunity)LetterMaker.MakeLetter(
            "RobCoQuestOfferLabel".Translate().Resolve(),
            RobCoQuestUtility.FormatOfferText(options),
            RobCoQuestDefOf.RobCo_PlatinumChipOpportunity);
        foreach ((RobCoPlatinumQuestBranchDef branch, Faction faction) in options)
        {
            letter.branchDefNames.Add(branch.defName);
            letter.factionIds.Add(faction.loadID);
        }

        Find.LetterStack.ReceiveLetter(letter);
        offerSent = true;
    }
}
