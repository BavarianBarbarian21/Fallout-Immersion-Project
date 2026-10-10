using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;

namespace FeilsFaunaFixes;

/// <summary>
/// Keeps custom animal corpses in one exclusive pose. Task animations can otherwise
/// remain active briefly after death and continue moving limbs underneath "die".
/// </summary>
public sealed class EntityBehaviorDeathAnimation : EntityBehavior
{
    private static readonly string[] LivingAnimationCodes =
    [
        "idle", "walk", "run", "eat", "hurt", "climbup", "climbdown"
    ];

    private bool deathPoseActive;

    public EntityBehaviorDeathAnimation(Entity entity) : base(entity) { }

    public override void OnGameTick(float deltaTime)
    {
        base.OnGameTick(deltaTime);

        if (entity.Alive)
        {
            deathPoseActive = false;
            return;
        }

        foreach (string code in LivingAnimationCodes)
        {
            entity.AnimManager.StopAnimation(code);
        }

        if (!deathPoseActive || !entity.AnimManager.IsAnimationActive("die"))
        {
            entity.AnimManager.StartAnimation("die");
            deathPoseActive = true;
        }
    }

    public override string PropertyName() => "faunadeathanimation";
}
