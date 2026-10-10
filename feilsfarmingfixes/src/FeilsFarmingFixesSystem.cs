using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace FeilsFarmingFixes;

public sealed class FeilsFarmingFixesSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("FeilsFarmingFarmland", typeof(BlockFarmingFarmland));
        api.RegisterBlockEntityClass("FeilsFarmingFarmland", typeof(BlockEntityFarmingFarmland));
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        api.Logger.Notification("[FeilsFarmingFixes] Loaded successfully.");
    }
}
