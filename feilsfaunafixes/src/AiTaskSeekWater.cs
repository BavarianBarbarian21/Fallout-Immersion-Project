using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class AiTaskSeekWater : AiTaskBase
{
    private readonly EntityBehaviorHydration? hydration;
    private BlockEntityFaunaTrough? target;
    private float moveSpeed;
    private float drinkTime;
    private float elapsed;
    private long lastSearchMs;
    private bool stuck;

    public AiTaskSeekWater(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        hydration = entity.GetBehavior<EntityBehaviorHydration>();
        moveSpeed = taskConfig["movespeed"].AsFloat(0.012f);
        drinkTime = taskConfig["drinkTime"].AsFloat(2f);
    }

    public override bool ShouldExecute()
    {
        if (hydration == null || !hydration.NeedsWater || lastSearchMs + 10000 > entity.World.ElapsedMilliseconds) return false;
        lastSearchMs = entity.World.ElapsedMilliseconds;
        target = FindNearest(24);
        return target != null;
    }

    private BlockEntityFaunaTrough? FindNearest(int radius)
    {
        BlockPos center = entity.Pos.AsBlockPos;
        BlockEntityFaunaTrough? best = null;
        double bestDist = double.MaxValue;
        for (int x = -radius; x <= radius; x++)
        for (int z = -radius; z <= radius; z++)
        for (int y = -4; y <= 4; y++)
        {
            var pos = center.AddCopy(x, y, z);
            if (entity.World.BlockAccessor.GetBlockEntity(pos) is BlockEntityFaunaTrough be && be.HasWater)
            {
                double dist = entity.Pos.SquareDistanceTo(be.Pos.X + .5, be.Pos.Y + .5, be.Pos.Z + .5);
                if (dist < bestDist) { bestDist = dist; best = be; }
            }
        }
        return best;
    }

    public override void StartExecute()
    {
        base.StartExecute();
        elapsed = 0;
        stuck = false;
        pathTraverser.NavigateTo_Async(target!.Pos.ToVec3d().Add(.5, .2, .5), moveSpeed, .7f, () => { }, () => stuck = true);
    }

    public override bool CanContinueExecute()
    {
        // NavigateTo_Async does not make the traverser active until its pathfinder
        // job has completed.  Running ContinueExecute before then would see
        // Active == false, finish this task, and cancel that pending job again.
        return pathTraverser.Ready;
    }

    public override bool ContinueExecute(float dt)
    {
        if (target == null || !target.HasWater || stuck) return false;
        if (entity.Pos.SquareDistanceTo(target.Pos.X + .5, target.Pos.Y + .5, target.Pos.Z + .5) > 2.0) return pathTraverser.Active;
        pathTraverser.Stop();
        elapsed += dt;
        if (elapsed < drinkTime) return true;
        hydration!.DrinkFrom(target);
        return false;
    }

    public override void FinishExecute(bool cancelled)
    {
        pathTraverser.Stop();
        base.FinishExecute(cancelled);
    }
}
