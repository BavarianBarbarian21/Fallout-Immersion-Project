using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class AiTaskSquirrelForage : AiTaskBase
{
    private enum TraversePhase { None, Up, Over, Down }
    private readonly EntityBehaviorSquirrelClimber? climber;
    private BlockEntity? target;
    private Vec3d? climbApproach;
    private Vec3d? climbWall;
    private float moveSpeed;
    private float eatTime;
    private float elapsed;
    private float climbElapsed;
    private bool stuck;
    private bool climbing;
    private TraversePhase traversePhase;
    private Vec3d? obstacleCenter;
    private Vec3d? obstacleFarSide;
    private Vec3d? obstacleDirection;
    private Vec3d? obstacleDescentWall;
    private double obstacleClearY;
    private float traverseElapsed;
    private long lastSearch;
    private static readonly FieldInfo? HarvestedField = typeof(BlockEntityFruitTreePart).GetField("harvested", BindingFlags.Instance | BindingFlags.NonPublic);

    public AiTaskSquirrelForage(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        climber = entity.GetBehavior<EntityBehaviorSquirrelClimber>();
        moveSpeed = taskConfig["movespeed"].AsFloat(.018f);
        eatTime = taskConfig["eatTime"].AsFloat(2.2f);
    }

    public override bool ShouldExecute()
    {
        if (entity.WatchedAttributes.GetDouble("faunaSatedUntil") > entity.World.Calendar.TotalHours) return false;
        bool justLeftRefuge = climber?.IsClimbing == true && !double.IsNaN(climber.ReturnToGroundY);
        if (!justLeftRefuge && lastSearch + 12000 > entity.World.ElapsedMilliseconds) return false;
        lastSearch = entity.World.ElapsedMilliseconds;
        target = FindFood(16);
        if (target != null)
        {
            entity.WatchedAttributes.SetString("faunaAiGoal", "forage:" + target.Pos);
            FeilsFaunaFixesSystem.Debug("Squirrel {0}: forage target {1}", entity.EntityId, target.Pos);
        }
        return target != null;
    }

    private BlockEntity? FindFood(int radius)
    {
        BlockPos center = entity.Pos.AsBlockPos;
        BlockEntity? best = null;
        double bestDist = double.MaxValue;
        for (int x = -radius; x <= radius; x++)
        for (int z = -radius; z <= radius; z++)
        for (int y = -3; y <= 10; y++)
        {
            BlockEntity? be = entity.World.BlockAccessor.GetBlockEntity(center.AddCopy(x, y, z));
            bool ripe = be switch
            {
                BlockEntityFruitTreePart fruit => fruit.FoliageState == EnumFoliageState.Ripe && fruit.PartType != EnumTreePartType.Stem,
                BlockEntityBerryBush berry => berry.IsRipe,
                _ => be?.GetBehavior<BEBehaviorFruitingBush>()?.BState.Growthstate == EnumFruitingBushGrowthState.Ripe
            };
            if (!ripe || be == null) continue;
            double dist = entity.Pos.SquareDistanceTo(be.Pos.X + .5, be.Pos.Y + .5, be.Pos.Z + .5);
            if (dist < bestDist) { bestDist = dist; best = be; }
        }
        return best;
    }

    public override void StartExecute()
    {
        climber?.SetAutomaticTraversalSuppressed(true);
        climber?.StopClimbing();
        elapsed = 0;
        climbElapsed = 0;
        climbing = false;
        stuck = false;
        traversePhase = TraversePhase.None;
        climbApproach = target!.Pos.Y > entity.Pos.Y + 1.4 ? FindClimbApproach(target.Pos, 5) : null;
        Vec3d destination = climbApproach ?? target.Pos.ToVec3d().Add(.5, .2, .5);
        pathTraverser.NavigateTo_Async(destination, moveSpeed, 1f, () => { }, () => stuck = true);
    }

    public override bool ContinueExecute(float dt)
    {
        if (target == null) return false;
        if (traversePhase != TraversePhase.None) return ContinueObstacleTraversal(dt);
        if (stuck)
        {
            if (!PrepareObstacleTraversal(target.Pos)) return false;
            stuck = false;
            pathTraverser.Stop();
            traversePhase = TraversePhase.Up;
            traverseElapsed = 0;
            climber?.StartClimbing(1, obstacleCenter!);
            entity.WatchedAttributes.SetString("faunaAiGoal", "forage-cross-obstacle");
            return true;
        }
        if (climbApproach != null && entity.Pos.Y < target.Pos.Y - .7)
        {
            double approachDistance = entity.Pos.XYZ.HorizontalSquareDistanceTo(climbApproach.X, climbApproach.Z);
            if (approachDistance > 1.6) return pathTraverser.Active;
            pathTraverser.Stop();
            climbElapsed += dt;
            climbing = true;
            if (climbWall != null)
            {
                Vec3d towardWall = climbWall.SubCopy(entity.Pos.XYZ);
                entity.Pos.Yaw = (float)Math.Atan2(towardWall.X, towardWall.Z);
            }
            if (climbWall != null) climber?.StartClimbing(1, climbWall);
            entity.WatchedAttributes.SetString("faunaAiGoal", "forage-climb:" + target.Pos);
            return climbElapsed < 12f;
        }

        StopClimbing();
        double distance = entity.Pos.SquareDistanceTo(target.Pos.X + .5, target.Pos.Y + .5, target.Pos.Z + .5);
        if (distance > 9.0) return pathTraverser.Active;
        pathTraverser.Stop();
        elapsed += dt;
        if (elapsed < eatTime) return true;
        if (entity.World.Side == EnumAppSide.Server) ConsumeTarget(target);
        double minHours = Math.Max(0.1, FeilsFaunaFixesSystem.Config.SquirrelForageCooldownMinHours);
        double maxHours = Math.Max(minHours, FeilsFaunaFixesSystem.Config.SquirrelForageCooldownMaxHours);
        double nextHunger = entity.World.Calendar.TotalHours + minHours + entity.World.Rand.NextDouble() * (maxHours - minHours);
        entity.WatchedAttributes.SetDouble("faunaSatedUntil", nextHunger);
        entity.WatchedAttributes.MarkPathDirty("faunaSatedUntil");
        return false;
    }

    private bool ContinueObstacleTraversal(float dt)
    {
        if (target == null || obstacleCenter == null || obstacleFarSide == null || obstacleDirection == null) return false;
        traverseElapsed += dt;
        switch (traversePhase)
        {
            case TraversePhase.Up:
                climbing = true;
                climber?.StartClimbing(1, obstacleCenter);
                if (entity.Pos.Y < obstacleClearY && traverseElapsed < 12) return true;
                climber?.StopClimbing();
                climbing = false;
                traversePhase = TraversePhase.Over;
                traverseElapsed = 0;
                return true;
            case TraversePhase.Over:
                entity.Pos.Yaw = (float)Math.Atan2(obstacleDirection.X, obstacleDirection.Z);
                entity.Controls.Forward = false;
                entity.Pos.Motion.X = obstacleDirection.X * .06;
                entity.Pos.Motion.Z = obstacleDirection.Z * .06;
                entity.Pos.Motion.Y = Math.Max(entity.Pos.Motion.Y, .01);
                if (entity.Pos.XYZ.HorizontalSquareDistanceTo(obstacleFarSide.X, obstacleFarSide.Z) > .42 && traverseElapsed < 5) return true;
                traversePhase = TraversePhase.Down;
                traverseElapsed = 0;
                climbing = true;
                climber?.StartClimbing(-1, obstacleDescentWall ?? obstacleCenter);
                return true;
            case TraversePhase.Down:
                climber?.StartClimbing(-1, obstacleDescentWall ?? obstacleCenter);
                if (entity.Pos.Y > obstacleFarSide.Y + .12 && traverseElapsed < 12) return true;
                climber?.StopClimbing();
                climbing = false;
                traversePhase = TraversePhase.None;
                Vec3d destination = target.Pos.ToVec3d().Add(.5, .2, .5);
                pathTraverser.NavigateTo_Async(destination, moveSpeed, 1f, () => { }, () => stuck = true);
                return true;
        }
        return false;
    }

    private bool PrepareObstacleTraversal(BlockPos targetPos)
    {
        BlockPos center = entity.Pos.AsBlockPos;
        double towardX = targetPos.X + .5 - entity.Pos.X;
        double towardZ = targetPos.Z + .5 - entity.Pos.Z;
        double best = double.MinValue;
        obstacleCenter = obstacleFarSide = obstacleDirection = obstacleDescentWall = null;

        foreach (BlockFacing face in BlockFacing.HORIZONTALS)
        {
            double alignment = face.Normali.X * towardX + face.Normali.Z * towardZ;
            if (alignment <= best) continue;
            BlockPos first = center.AddCopy(face.Normali);
            if (!IsObstacle(first)) continue;

            int thickness = 1;
            while (thickness <= 3 && IsObstacle(first.AddCopy(face.Normali.X * thickness, 0, face.Normali.Z * thickness))) thickness++;
            if (thickness > 3) continue;
            BlockPos far = first.AddCopy(face.Normali.X * thickness, 0, face.Normali.Z * thickness);
            if (!CanOccupy(far) || !CanOccupy(far.UpCopy())) continue;

            int top = first.Y;
            while (top - first.Y < 5 && IsObstacle(new BlockPos(first.X, top + 1, first.Z, first.dimension))) top++;
            if (top - first.Y >= 5) continue;
            bool topRouteClear = true;
            for (int step = 0; step <= thickness; step++)
            {
                BlockPos above = new BlockPos(first.X + face.Normali.X * step, top + 1, first.Z + face.Normali.Z * step, first.dimension);
                if (!CanOccupy(above)) { topRouteClear = false; break; }
            }
            if (!topRouteClear) continue;

            best = alignment;
            obstacleCenter = first.ToVec3d().Add(.5, .5, .5);
            obstacleFarSide = far.ToVec3d().Add(.5, 0, .5);
            obstacleDirection = new Vec3d(face.Normali.X, 0, face.Normali.Z);
            BlockPos lastObstacle = far.AddCopy(face.Opposite.Normali);
            obstacleDescentWall = lastObstacle.ToVec3d().Add(.5, .5, .5);
            obstacleClearY = top + GetCollisionTop(new BlockPos(first.X, top, first.Z, first.dimension)) + .08;
        }
        return obstacleCenter != null;
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

    private bool CanOccupy(BlockPos pos)
    {
        Block block = entity.World.BlockAccessor.GetBlock(pos);
        if (block.BlockMaterial == EnumBlockMaterial.Leaves) return true;
        Cuboidf[]? boxes = block.GetCollisionBoxes(entity.World.BlockAccessor, pos);
        return boxes == null || boxes.Length == 0;
    }

    private static void ConsumeTarget(BlockEntity be)
    {
        if (be is BlockEntityFruitTreePart fruit && fruit.FoliageState == EnumFoliageState.Ripe)
        {
            fruit.FoliageState = EnumFoliageState.Plain;
            HarvestedField?.SetValue(fruit, true);
            fruit.MarkDirty(true);
            return;
        }
        if (be is BlockEntityBerryBush berry && berry.IsRipe)
        {
            AssetLocation emptyCode = berry.Block.CodeWithVariant("state", "empty");
            Block? empty = berry.Api.World.GetBlock(emptyCode);
            if (empty != null) berry.Api.World.BlockAccessor.ExchangeBlock(empty.BlockId, berry.Pos);
            return;
        }
        BEBehaviorFruitingBush? bush = be.GetBehavior<BEBehaviorFruitingBush>();
        if (bush?.BState.Growthstate == EnumFruitingBushGrowthState.Ripe)
        {
            bush.BState.Growthstate = EnumFruitingBushGrowthState.Mature;
            bush.BState.TransitionHoursLeft = bush.GetHoursForNextStage();
            be.MarkDirty(true);
        }
    }

    public override void FinishExecute(bool cancelled)
    {
        traversePhase = TraversePhase.None;
        StopClimbing();
        climber?.SetAutomaticTraversalSuppressed(false);
        pathTraverser.Stop();
        base.FinishExecute(cancelled);
    }

    private Vec3d? FindClimbApproach(BlockPos targetPos, int radius)
    {
        BlockPos center = entity.Pos.AsBlockPos;
        double best = double.MaxValue;
        Vec3d? result = null;
        climbWall = null;
        for (int x = targetPos.X - radius; x <= targetPos.X + radius; x++)
        for (int z = targetPos.Z - radius; z <= targetPos.Z + radius; z++)
        foreach (BlockFacing face in BlockFacing.HORIZONTALS)
        {
            BlockPos wall = new BlockPos(x, center.Y, z, center.dimension);
            Block block = entity.World.BlockAccessor.GetBlock(wall);
            BlockPos air = wall.AddCopy(face.Opposite.Normali);
            if (!block.SideSolid[face.Opposite.Index] || entity.World.BlockAccessor.GetBlock(air).Replaceable < 6000) continue;
            double targetHorizontal = targetPos.HorizontalManhattanDistance(wall);
            if (targetHorizontal > 3) continue;
            double distance = entity.Pos.SquareDistanceTo(air.X + .5, entity.Pos.Y, air.Z + .5);
            double score = targetHorizontal * 20 + distance;
            if (score >= best) continue;
            best = score;
            result = air.ToVec3d().Add(.5, 0, .5);
            climbWall = wall.ToVec3d().Add(.5, .5, .5);
        }
        return result;
    }

    private void StopClimbing()
    {
        if (!climbing) return;
        climbing = false;
        climber?.StopClimbing();
    }
}
