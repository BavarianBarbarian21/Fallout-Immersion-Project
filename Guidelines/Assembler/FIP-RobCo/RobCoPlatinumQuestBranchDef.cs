using RimWorld;
using Verse;

namespace FIP_RobCo;

public class RobCoPlatinumQuestBranchDef : Def
{
    public int courierNumber;
    public string chipTitle = string.Empty;
    public PawnKindDef stage2EnemyPawnKind;
    public PawnKindDef stage2CorpsePawnKind;
    public float stage1TargetPoints = 600f;
    public float stage2ThreatPoints = 2400f;
    public string stage2LetterLabel = string.Empty;
    public string stage2LetterText = string.Empty;
}