using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class EntityBehaviorSquirrelClimber : EntityBehavior
{
    private enum AutoTraversePhase { None, Up, Over, Down }

    private const string DirectionAttribute = "faunaClimbDirection";
    private int direction;
    private Vec3d? wallCenter;
    private int displayedDirection;
    private long nextGapJumpMs;
    private AutoTraversePhase autoPhase;
    private Vec3d? autoWall;
    private Vec3d? autoDescentWall;
    private Vec3d? autoFarSide;
    private Vec3d? autoDirection;
    private double autoClearY;
    private float autoElapsed;
    private bool automaticTraversalSuppressed;
    private double returnToGroundY = double.NaN;

    public bool IsClimbing => direction != 0;
    public Vec3d? ClimbSurface => wallCenter?.Clone();
    public double ReturnToGroundY => returnToGroundY;

    public EntityBehaviorSquirrelClimber(Entity entity) : base(entity) { }

    public void SetAutomaticTraversalSuppressed(bool suppressed)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        if (suppressed && autoPhase != AutoTraversePhase.None) EndAutomaticTraversal();
        automaticTraversalSuppressed = suppressed;
    }

    public void StartClimbing(int climbDirection, Vec3d wall)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        direction = Math.Sign(climbDirection);
        wallCenter = wall.Clone();
        SetSyncedDirection(direction);
    }

    public void StopClimbing()
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        direction = 0;
        wallCenter = null;
        returnToGroundY = double.NaN;
        if (entity is EntityAgent agent) agent.Controls.IsClimbing = false;
        SetSyncedDirection(0);
    }

    public void HoldOnTree(Vec3d trunk, double descentY = double.NaN)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        direction = 2;
        wallCenter = trunk.Clone();
        if (!double.IsNaN(descentY)) returnToGroundY = descentY;
        SetSyncedDirection(1);
    }

    public override void OnGameTick(float deltaTime)
    {
        base.OnGameTick(deltaTime);
        if (!entity.Alive)
        {
            if (entity.World.Side == EnumAppSide.Server) StopClimbing();
            UpdateDisplayedAnimation(0);
            return;
        }

        if (entity.World.Side == EnumAppSide.Server)
        {
            if (autoPhase != AutoTraversePhase.None)
            {
                ContinueAutomaticTraversal(deltaTime);
                if (direction != 0) ApplyVerticalMovement();
            }
            else if (direction != 0) ApplyVerticalMovement();
            else
            {
                TryBeginAutomaticTraversal();
                if (autoPhase == AutoTraversePhase.None) TryJumpGap();
            }
        }
        UpdateDisplayedAnimation(entity.WatchedAttributes.GetInt(DirectionAttribute));
    }

    private void ApplyVerticalMovement()
    {
        if (entity is EntityAgent agent)
        {
            agent.Controls.Forward = false;
            agent.Controls.Backward = false;
            agent.Controls.Left = false;
            agent.Controls.Right = false;
            agent.Controls.IsClimbing = true;
        }

        if (wallCenter != null)
        {
            double dx = wallCenter.X - entity.Pos.X;
            double dz = wallCenter.Z - entity.Pos.Z;
            double length = Math.Sqrt(dx * dx + dz * dz);
            if (length > 0.001)
            {
                entity.Pos.Yaw = (float)Math.Atan2(dx, dz);
                entity.Pos.Motion.X = dx / length * 0.004;
                entity.Pos.Motion.Z = dz / length * 0.004;
            }
        }

        entity.Pos.Motion.Y = direction == 2 ? 0 : FeilsFaunaFixesSystem.Config.SquirrelClimbSpeed * direction;
        entity.OnGround = true;
        entity.WatchedAttributes.SetString("faunaAiGoal", direction == 2 ? "tree-refuge" : direction > 0 ? "climb-up" : "climb-down");
    }

    private void TryJumpGap()
    {
        if (entity is not EntityAgent agent || !entity.OnGround || entity.World.ElapsedMilliseconds < nextGapJumpMs) return;
        Vec3d walk = agent.Controls.WalkVector;
        double length = Math.Sqrt(walk.X * walk.X + walk.Z * walk.Z);
        if (length < .01) return;

        double dx = walk.X / length;
        double dz = walk.Z / length;
        BlockPos origin = entity.Pos.AsBlockPos;
        int maxGap = Math.Clamp(FeilsFaunaFixesSystem.Config.SquirrelMaximumJumpGap, 1, 2);
        bool sawGap = false;
        int landingDistance = 0;
        for (int distance = 1; distance <= maxGap + 1; distance++)
        {
            int x = (int)Math.Floor(entity.Pos.X + dx * distance);
            int z = (int)Math.Floor(entity.Pos.Z + dz * distance);
            BlockPos feet = new BlockPos(x, origin.Y, z, origin.dimension);
            bool bodyOpen = IsPassable(feet) && IsPassable(feet.UpCopy());
            bool supported = HasCollision(feet.DownCopy());
            if (!bodyOpen) return;
            if (!supported) { sawGap = true; continue; }
            if (sawGap) landingDistance = distance;
            break;
        }
        if (landingDistance < 2 || landingDistance > maxGap + 1) return;

        entity.Pos.Motion.X = dx * (.075 + .025 * landingDistance);
        entity.Pos.Motion.Z = dz * (.075 + .025 * landingDistance);
        entity.Pos.Motion.Y = .155;
        entity.OnGround = false;
        nextGapJumpMs = entity.World.ElapsedMilliseconds + 1100;
        entity.WatchedAttributes.SetString("faunaAiGoal", "gap-jump");
    }

    private void TryBeginAutomaticTraversal()
    {
        if (automaticTraversalSuppressed || entity is not EntityAgent agent || !entity.CollidedHorizontally || HasNearbyThreat()) return;
        EntityBehaviorTaskAI? taskAi = entity.GetBehavior<EntityBehaviorTaskAI>();
        PathTraverserBase? traverser = taskAi?.PathTraverser;
        if (traverser == null || !traverser.Active) return;

        Vec3d target = traverser.CurrentTarget;
        double towardX = target.X - entity.Pos.X;
        double towardZ = target.Z - entity.Pos.Z;
        if (towardX * towardX + towardZ * towardZ < 2.25) return;
        BlockPos center = entity.Pos.AsBlockPos;
        double bestAlignment = double.MinValue;

        foreach (BlockFacing face in BlockFacing.HORIZONTALS)
        {
            double alignment = face.Normali.X * towardX + face.Normali.Z * towardZ;
            if (alignment <= bestAlignment) continue;
            BlockPos first = center.AddCopy(face.Normali);
            if (!IsObstacle(first)) continue;

            int thickness = 1;
            while (thickness <= 3 && IsObstacle(first.AddCopy(face.Normali.X * thickness, 0, face.Normali.Z * thickness))) thickness++;
            if (thickness > 3) continue;
            BlockPos far = first.AddCopy(face.Normali.X * thickness, 0, face.Normali.Z * thickness);
            if (!IsPassable(far) || !IsPassable(far.UpCopy())) continue;
            double farDistanceSq = far.ToVec3d().Add(.5, 0, .5).SquareDistanceTo(target);
            if (farDistanceSq >= entity.Pos.XYZ.SquareDistanceTo(target)) continue;

            int top = first.Y;
            while (top - first.Y < 5 && IsObstacle(new BlockPos(first.X, top + 1, first.Z, first.dimension))) top++;
            if (top - first.Y >= 5) continue;
            bool topClear = true;
            for (int step = 0; step <= thickness; step++)
            {
                BlockPos above = new BlockPos(first.X + face.Normali.X * step, top + 1, first.Z + face.Normali.Z * step, first.dimension);
                if (!IsPassable(above)) { topClear = false; break; }
            }
            if (!topClear) continue;

            bestAlignment = alignment;
            autoWall = first.ToVec3d().Add(.5, .5, .5);
            BlockPos lastObstacle = far.AddCopy(face.Opposite.Normali);
            autoDescentWall = lastObstacle.ToVec3d().Add(.5, .5, .5);
            autoFarSide = far.ToVec3d().Add(.5, 0, .5);
            autoDirection = new Vec3d(face.Normali.X, 0, face.Normali.Z);
            autoClearY = top + GetCollisionTop(new BlockPos(first.X, top, first.Z, first.dimension)) + .08;
        }

        if (autoWall == null) return;
        autoPhase = AutoTraversePhase.Up;
        autoElapsed = 0;
        StartClimbing(1, autoWall);
        entity.WatchedAttributes.SetString("faunaAiGoal", "path-cross-obstacle");
    }

    private void ContinueAutomaticTraversal(float dt)
    {
        if (HasNearbyThreat())
        {
            EndAutomaticTraversal();
            return;
        }
        if (autoWall == null || autoFarSide == null || autoDirection == null)
        {
            EndAutomaticTraversal();
            return;
        }
        autoElapsed += dt;
        switch (autoPhase)
        {
            case AutoTraversePhase.Up:
                StartClimbing(1, autoWall);
                if (entity.Pos.Y < autoClearY && autoElapsed < 12) return;
                StopClimbing();
                autoPhase = AutoTraversePhase.Over;
                autoElapsed = 0;
                break;
            case AutoTraversePhase.Over:
                entity.Pos.Yaw = (float)Math.Atan2(autoDirection.X, autoDirection.Z);
                entity.Pos.Motion.X = autoDirection.X * .06;
                entity.Pos.Motion.Z = autoDirection.Z * .06;
                entity.Pos.Motion.Y = Math.Max(entity.Pos.Motion.Y, .01);
                if (entity.Pos.XYZ.HorizontalSquareDistanceTo(autoFarSide.X, autoFarSide.Z) > .42 && autoElapsed < 5) return;
                autoPhase = AutoTraversePhase.Down;
                autoElapsed = 0;
                StartClimbing(-1, autoDescentWall ?? autoWall);
                break;
            case AutoTraversePhase.Down:
                StartClimbing(-1, autoDescentWall ?? autoWall);
                if (entity.Pos.Y > autoFarSide.Y + .12 && autoElapsed < 12) return;
                EndAutomaticTraversal();
                break;
        }
    }

    private void EndAutomaticTraversal()
    {
        StopClimbing();
        autoPhase = AutoTraversePhase.None;
        autoWall = autoDescentWall = autoFarSide = autoDirection = null;
        autoElapsed = 0;
    }

    private bool HasNearbyThreat()
    {
        float range = FeilsFaunaFixesSystem.Config.SquirrelFleeDistance;
        return entity.World.GetNearestEntity(entity.Pos.XYZ, range, range,
            e => e.Alive && e != entity && (e is EntityPlayer || IsPredator(e.Code.Path))) != null;
    }

    private bool IsObstacle(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        if (block.BlockMaterial == EnumBlockMaterial.Leaves) return false;
        Cuboidf[]? boxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
        return boxes != null && boxes.Length > 0;
    }

    private double GetCollisionTop(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        Cuboidf[]? boxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
        if (boxes == null || boxes.Length == 0) return 0;
        double top = 0;
        foreach (Cuboidf box in boxes) top = Math.Max(top, box.Y2);
        return top;
    }

    private static bool IsPredator(string path) => path.StartsWith("wolf-") || path.StartsWith("fox-") || path.StartsWith("bear-") || path.StartsWith("hyena-");

    private bool IsPassable(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        return block.BlockMaterial == EnumBlockMaterial.Leaves || block.GetCollisionBoxes(entity.World.BlockAccessor, pos) == null;
    }

    private bool HasCollision(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        Cuboidf[]? boxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
        return boxes != null && boxes.Length > 0;
    }

    private void SetSyncedDirection(int value)
    {
        if (entity.WatchedAttributes.GetInt(DirectionAttribute) == value) return;
        entity.WatchedAttributes.SetInt(DirectionAttribute, value);
        entity.WatchedAttributes.MarkPathDirty(DirectionAttribute);
        UpdateDisplayedAnimation(value);
    }

    private void UpdateDisplayedAnimation(int value)
    {
        if (displayedDirection == value) return;
        if (displayedDirection > 0) entity.AnimManager.StopAnimation("climbup");
        if (displayedDirection < 0) entity.AnimManager.StopAnimation("climbdown");
        displayedDirection = value;
        if (value > 0) entity.AnimManager.StartAnimation("climbup");
        if (value < 0) entity.AnimManager.StartAnimation("climbdown");
    }

    public override string PropertyName() => "faunasquirrelclimber";
}
