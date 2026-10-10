using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFarmingFixes;

public sealed class BlockFarmingFarmland : BlockFarmland
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityFarmingFarmland farmland
            && farmland.OnOllaInteract(byPlayer))
        {
            return true;
        }

        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1f)
    {
        if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityFarmingFarmland farmland)
        {
            farmland.DropOllas();
        }

        base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
    }
}
