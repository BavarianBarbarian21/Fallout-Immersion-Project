using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

/// <summary>Escapes onto real, sufficiently tall trees. Ordinary walls are never selected as refuges.</summary>
public sealed class AiTaskSquirrelClimbEscape : AiTaskBase
{
    private enum EscapePhase { Approach, Up, Hold }

    private readonly EntityBehaviorSquirrelClimber? climber;
    private Entity? threat;
    private Vec3d? approach;
    private Vec3d? trunkCenter;
    private float moveSpeed;
    private float phaseTime;
    private double refugeY;
    private bool stuck;
    private bool pathSearchPending;
    private long retryTreeSearchAfterMs;
    private bool preserveClimbForHandoff;
    private bool resumeAttached;
    private EscapePhase phase;

    public AiTaskSquirrelClimbEscape(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        climber = entity.GetBehavior<EntityBehaviorSquirrelClimber>();
        moveSpeed = taskConfig["movespeed"].AsFloat(.055f);
    }

    public override bool ShouldExecute()
    {
        if (climber == null) return false;
        threat = FindThreat();
        if (threat == null) return false;
        Vec3d? attachedSurface = climber.ClimbSurface;
        if (climber.IsClimbing && attachedSurface != null && !double.IsNaN(climber.ReturnToGroundY))
        {
            trunkCenter = attachedSurface;
            approach = new Vec3d(entity.Pos.X, climber.ReturnToGroundY, entity.Pos.Z);
            refugeY = entity.Pos.Y;
            resumeAttached = true;
            return true;
        }
        resumeAttached = false;
        if (entity.World.ElapsedMilliseconds < retryTreeSearchAfterMs) return false;
        bool found = FindEscapeTree(entity.Pos.AsBlockPos, threat, 12);
        if (found) FeilsFaunaFixesSystem.Debug("Squirrel {0}: escape tree {1}, refuge y {2}", entity.EntityId, trunkCenter!, refugeY);
        return found;
    }

    private Entity? FindThreat() => entity.World.GetNearestEntity(
        entity.Pos.XYZ,
        FeilsFaunaFixesSystem.Config.SquirrelFleeDistance,
        FeilsFaunaFixesSystem.Config.SquirrelFleeDistance,
        e => e.Alive && e != entity && (e is EntityPlayer || IsPredator(e.Code.Path))
    );

    private bool FindEscapeTree(BlockPos center, Entity danger, int radius)
    {
        approach = trunkCenter = null;
        double awayX = center.X + .5 - danger.Pos.X;
        double awayZ = center.Z + .5 - danger.Pos.Z;
        double awayLength = Math.Max(.001, Math.Sqrt(awayX * awayX + awayZ * awayZ));
        awayX /= awayLength;
        awayZ /= awayLength;
        double bestScore = double.MaxValue;
        int minimumHeight = Math.Max(6, FeilsFaunaFixesSystem.Config.SquirrelMinimumEscapeTreeHeight);

        for (int x = center.X - radius; x <= center.X + radius; x++)
        for (int z = center.Z - radius; z <= center.Z + radius; z++)
        for (int yOffset = -2; yOffset <= 2; yOffset++)
        {
            BlockPos log = new BlockPos(x, center.Y + yOffset, z, center.dimension);
            if (!IsNaturalVerticalLog(log)) continue;
            while (log.Y > center.Y - 5 && IsNaturalVerticalLog(log.DownCopy())) log.Down();

            int height = 0;
            while (height < FeilsFaunaFixesSystem.Config.SquirrelMaxClimbHeight && IsNaturalVerticalLog(log.UpCopy(height))) height++;
            if (height < minimumHeight) continue;

            foreach (BlockFacing face in BlockFacing.HORIZONTALS)
            {
                BlockPos near = log.AddCopy(face.Normali);
                if (!CanOccupy(near) || !CanOccupy(near.UpCopy())) continue;
                bool laneClear = true;
                for (int dy = 1; dy < height; dy++)
                {
                    BlockPos lane = new BlockPos(near.X, log.Y + dy, near.Z, near.dimension);
                    if (!CanOccupy(lane)) { laneClear = false; break; }
                }
                if (!laneClear) continue;

                double treeOffsetX = near.X + .5 - (center.X + .5);
                double treeOffsetZ = near.Z + .5 - (center.Z + .5);
                double awayProgress = treeOffsetX * awayX + treeOffsetZ * awayZ;
                double distanceSq = entity.Pos.SquareDistanceTo(near.X + .5, entity.Pos.Y, near.Z + .5);
                // The nearest usable tree is normally safest. Direction only influences the
                // choice between similarly close trees; it must never exclude a nearby refuge.
                double score = distanceSq - Math.Max(0, awayProgress) * 2 + Math.Max(0, -awayProgress) * 2;
                if (score >= bestScore) continue;

                bestScore = score;
                approach = near.ToVec3d().Add(.5, 0, .5);
                trunkCenter = log.ToVec3d().Add(.5, .5, .5);
                // Stop beside the highest continuous trunk block, never on top of it.
                refugeY = log.Y + height - .15;
            }
        }
        return approach != null;
    }

    public override void StartExecute()
    {
        climber?.SetAutomaticTraversalSuppressed(true);
        phase = EscapePhase.Approach;
        phaseTime = 0;
        stuck = false;
        pathSearchPending = false;
        preserveClimbForHandoff = false;
        if (resumeAttached)
        {
            phase = EscapePhase.Hold;
            climber?.HoldOnTree(trunkCenter!, approach!.Y);
            return;
        }
        pathSearchPending = pathTraverser.NavigateTo_Async(
            approach!, moveSpeed, .35f,
            () => pathSearchPending = false,
            FailApproach,
            FailApproach,
            800,
            1
        );
        if (!pathSearchPending) FailApproach();
    }

    public override bool ContinueExecute(float dt)
    {
        if (approach == null || trunkCenter == null || stuck) return false;
        phaseTime += dt;

        switch (phase)
        {
            case EscapePhase.Approach:
                if (pathSearchPending)
                {
                    if (!pathTraverser.Ready) return true;
                    pathSearchPending = false;
                }
                if (entity.Pos.XYZ.HorizontalSquareDistanceTo(approach.X, approach.Z) > .75) return pathTraverser.Active;
                pathTraverser.Stop();
                phase = EscapePhase.Up;
                phaseTime = 0;
                climber!.StartClimbing(1, trunkCenter);
                return true;
            case EscapePhase.Up:
                climber!.StartClimbing(1, trunkCenter);
                if (entity.Pos.Y < refugeY && phaseTime < 22) return true;
                phase = EscapePhase.Hold;
                phaseTime = 0;
                climber.HoldOnTree(trunkCenter, approach.Y);
                return true;
            case EscapePhase.Hold:
                climber!.HoldOnTree(trunkCenter, approach.Y);
                if (ThreatStillClose()) return true;
                // Leave the squirrel attached. Food-seeking gets first refusal; the lower-priority
                // descent task only takes over if no forage task wants to run.
                preserveClimbForHandoff = true;
                entity.WatchedAttributes.SetString("faunaAiGoal", "tree-refuge-ended");
                return false;
        }
        return false;
    }

    private void FailApproach()
    {
        pathSearchPending = false;
        stuck = true;
        retryTreeSearchAfterMs = entity.World.ElapsedMilliseconds + 2000;
    }

    private bool ThreatStillClose()
    {
        // Do not descend while any relevant pursuer is still inside the flee radius.
        threat = FindThreat();
        return threat != null;
    }

    private bool IsNaturalVerticalLog(BlockPos pos)
    {
        string path = entity.World.BlockAccessor.GetBlock(pos).Code?.Path ?? "";
        return path.StartsWith("log-grown-") && (path.EndsWith("-ud") || !path.EndsWith("-we") && !path.EndsWith("-ns"));
    }

    private bool CanOccupy(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        if (block.BlockMaterial == EnumBlockMaterial.Leaves) return true;
        Cuboidf[]? boxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
        return boxes == null || boxes.Length == 0;
    }

    public override void FinishExecute(bool cancelled)
    {
        pathSearchPending = false;
        if (!preserveClimbForHandoff) climber?.StopClimbing();
        climber?.SetAutomaticTraversalSuppressed(false);
        entity.Controls.Forward = false;
        pathTraverser.Stop();
        base.FinishExecute(cancelled);
    }

    private static bool IsPredator(string path) => path.StartsWith("wolf-") || path.StartsWith("fox-") || path.StartsWith("bear-") || path.StartsWith("hyena-");
}
