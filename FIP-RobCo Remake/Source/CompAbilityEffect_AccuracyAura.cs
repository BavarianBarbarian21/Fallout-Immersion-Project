using System.Linq;
using RimWorld;
using Verse;

namespace FIP_RobCo
{
    public class CompAbilityEffect_AccuracyAura : CompAbilityEffect
    {
        public int Radius => (props.GetType().GetProperty("radius")?.GetValue(props) as int?) ?? 5;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            var map = parent.pawn.Map;
            var pawns = GenRadial.RadialDistinctThingsAround(target.Cell, map, Radius, true)
                .OfType<Pawn>()
                .Where(p => p.Faction == parent.pawn.Faction && p != parent.pawn);

            foreach (var pawn in pawns)
            {
                if (!pawn.health.hediffSet.HasHediff(HediffDef.Named("Eyebot_AccuracyBuff")))
                {
                    var hediff = HediffMaker.MakeHediff(HediffDef.Named("Eyebot_AccuracyBuff"), pawn);
                    hediff.Severity = 1f;
                    pawn.health.AddHediff(hediff);
                }
            }
        }
    }
}
