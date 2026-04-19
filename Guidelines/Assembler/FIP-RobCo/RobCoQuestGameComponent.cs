using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace FIP_RobCo;

public class RobCoQuestGameComponent : GameComponent
{
    private bool offerScheduled;
    private bool offerResolved;
    private bool offerSent;
    private int offerFireTick = -1;

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
    }

    public override void GameComponentTick()
    {
        if (Find.TickManager.TicksGame % 250 != 0 || offerResolved)
        {
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

    public void AcceptOffer(RobCoPlatinumQuestBranchDef branch, Faction faction)
    {
        offerResolved = true;
        offerSent = true;

        if (!RobCoQuestUtility.TryFindSiteTile(12, 24, out int tile))
        {
            RobCoQuestUtility.SendLetter(
                "RobCoQuestTrailLostLabel".Translate().Resolve(),
                "RobCoQuestTrailLostText".Translate().Resolve(),
                LetterDefOf.NegativeEvent);
            return;
        }

        RobCoCourierCamp camp = (RobCoCourierCamp)WorldObjectMaker.MakeWorldObject(RobCoQuestDefOf.RobCo_CourierCamp);
        camp.Tile = tile;
        camp.SetFaction(faction);
        camp.branchDefName = branch.defName;
        camp.expirationTick = Find.TickManager.TicksGame + RobCoQuestUtility.CourierExpiryTicks;
        Find.WorldObjects.Add(camp);

        RobCoQuestUtility.SendLetter(
            "RobCoQuestCourierInterceptedLabel".Translate().Resolve(),
            "RobCoQuestCourierInterceptedText".Translate(RobCoQuestUtility.FormatOptionLabel(branch, faction)).Resolve(),
            LetterDefOf.PositiveEvent,
            new LookTargets(camp));
    }

    public void DeclineOffer()
    {
        offerResolved = true;
        offerSent = true;
    }

    public void RevealVaultSite(RobCoPlatinumQuestBranchDef branch)
    {
        if (!RobCoQuestUtility.TryFindSiteTile(30, 60, out int tile))
        {
            return;
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
        int count = branches.Count < shuffledFactions.Count ? branches.Count : shuffledFactions.Count;
        for (int index = 0; index < count; index++)
        {
            options.Add((branches[index], shuffledFactions[index]));
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