using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class AiTaskHydratedLayEgg : AiTaskSeekBlockAndLay
{
    private readonly EntityBehaviorHydration? hydration;
    private bool waterEgg;

    public AiTaskHydratedLayEgg(EntityAgent entity, JsonObject taskConfig, JsonObject aiConfig) : base(entity, taskConfig, aiConfig)
    {
        hydration = entity.GetBehavior<EntityBehaviorHydration>();
    }

    public override bool ShouldExecute()
    {
        ITreeAttribute? hunger = entity.WatchedAttributes.GetTreeAttribute("hunger");
        float actual = hunger?.GetFloat("saturation") ?? 0;
        waterEgg = actual < 3f;
        if (!waterEgg) return base.ShouldExecute();
        double now = entity.World.Calendar.TotalHours;
        if (hydration?.IsHydrated != true || hunger == null || entity.WatchedAttributes.GetDouble("faunaNextWaterEggHours") > now) return false;
        hunger.SetFloat("saturation", 3f);
        bool result = base.ShouldExecute();
        hunger.SetFloat("saturation", actual);
        if (result)
        {
            double multiplier = Math.Max(0.05, FeilsFaunaFixesSystem.Config.WaterEggProductionMultiplier);
            entity.WatchedAttributes.SetDouble("faunaNextWaterEggHours", now + (1 + entity.World.Rand.NextDouble()) / multiplier);
            entity.WatchedAttributes.MarkPathDirty("faunaNextWaterEggHours");
            FeilsFaunaFixesSystem.Debug("Chicken {0}: water-only unfertilized egg", entity.EntityId);
        }
        return result;
    }

    protected override ItemStack MakeEggItem(string chickCode)
    {
        ItemStack egg = base.MakeEggItem(waterEgg ? null! : chickCode);
        if (waterEgg) egg.Attributes.RemoveAttribute("chick");
        return egg;
    }
}
