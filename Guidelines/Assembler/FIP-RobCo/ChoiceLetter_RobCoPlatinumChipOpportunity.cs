using System.Linq;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FIP_RobCo;

public class ChoiceLetter_RobCoPlatinumChipOpportunity : ChoiceLetter
{
    public List<string> branchDefNames = new();
    public List<int> factionIds = new();

    public override IEnumerable<DiaOption> Choices
    {
        get
        {
            for (int index = 0; index < branchDefNames.Count && index < factionIds.Count; index++)
            {
                RobCoPlatinumQuestBranchDef branch = DefDatabase<RobCoPlatinumQuestBranchDef>.GetNamedSilentFail(branchDefNames[index]);
                Faction faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.loadID == factionIds[index]);
                if (branch == null || faction == null)
                {
                    continue;
                }

                DiaOption option = new(RobCoQuestUtility.FormatOptionLabel(branch, faction));
                option.action = delegate
                {
                    Current.Game.GetComponent<RobCoQuestGameComponent>()?.AcceptOffer(branch, faction);
                    Find.LetterStack.RemoveLetter(this);
                };
                yield return option;
            }

            DiaOption ignore = new("RobCoQuestIgnore".Translate().Resolve());
            ignore.action = delegate
            {
                Current.Game.GetComponent<RobCoQuestGameComponent>()?.DeclineOffer();
                Find.LetterStack.RemoveLetter(this);
            };
            yield return ignore;

            yield return Option_Close;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref branchDefNames, "branchDefNames", LookMode.Value);
        Scribe_Collections.Look(ref factionIds, "factionIds", LookMode.Value);
    }
}