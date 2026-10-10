using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

/// <summary>Lowest useful squirrel action: leave a refuge only when neither fleeing nor foraging.</summary>
public sealed class AiTaskSquirrelDescend : AiTaskBase
{
    private readonly EntityBehaviorSquirrelClimber? climber;
    private Vec3d? surface;
    private double groundY;
    private float elapsed;
    private bool reachedGround;

    public AiTaskSquirrelDescend(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        climber = entity.GetBehavior<EntityBehaviorSquirrelClimber>();
    }

    public override bool ShouldExecute()
    {
        if (climber == null || !climber.IsClimbing || double.IsNaN(climber.ReturnToGroundY)) return false;
        surface = climber.ClimbSurface;
        groundY = climber.ReturnToGroundY;
        return surface != null && FindThreat() == null;
    }

    public override void StartExecute()
    {
        elapsed = 0;
        reachedGround = false;
        climber?.SetAutomaticTraversalSuppressed(true);
        climber?.StartClimbing(-1, surface!);
        entity.WatchedAttributes.SetString("faunaAiGoal", "leave-tree-refuge");
    }

    public override bool ContinueExecute(float dt)
    {
        if (climber == null || surface == null) return false;
        elapsed += dt;
        if (FindThreat() != null)
        {
            climber.HoldOnTree(surface, groundY);
            return false;
        }
        climber.StartClimbing(-1, surface);
        if (entity.Pos.Y > groundY + .12 && elapsed < 24) return true;
        reachedGround = true;
        return false;
    }

    public override void FinishExecute(bool cancelled)
    {
        if (reachedGround) climber?.StopClimbing();
        else if (surface != null) climber?.HoldOnTree(surface, groundY);
        climber?.SetAutomaticTraversalSuppressed(false);
        base.FinishExecute(cancelled);
    }

    private Entity? FindThreat()
    {
        float range = FeilsFaunaFixesSystem.Config.SquirrelFleeDistance;
        return entity.World.GetNearestEntity(entity.Pos.XYZ, range, range,
            e => e.Alive && e != entity && (e is EntityPlayer || IsPredator(e.Code.Path)));
    }

    private static bool IsPredator(string path) => path.StartsWith("wolf-") || path.StartsWith("fox-") || path.StartsWith("bear-") || path.StartsWith("hyena-");
}
