using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

internal static class TroughWaterTransfer
{
    public static bool TryInteract(IWorldAccessor world, IPlayer player, BlockPos rootPos)
    {
        if (world.BlockAccessor.GetBlockEntity(rootPos) is not BlockEntityFaunaTrough trough) return false;
        ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;

        if (trough.HasWater && (slot.Empty || slot.Itemstack?.Collectible is not BlockLiquidContainerBase)) return true;
        if (slot.Empty || slot.Itemstack?.Collectible is not BlockLiquidContainerBase liquidContainer) return false;

        ItemStack? content = liquidContainer.GetContent(slot.Itemstack);
        if (content == null || !IsWater(content)) return trough.HasWater;
        if (!trough.CanAcceptWater) return trough.HasWater;

        float litresAvailable = liquidContainer.GetCurrentLitres(slot.Itemstack);
        float litresPerUnit = FeilsFaunaFixesSystem.Config.BucketLitres / FeilsFaunaFixesSystem.Config.SmallTroughCapacity;
        float desiredLitres = Math.Min(litresAvailable, (trough.Capacity - trough.WaterUnits) * litresPerUnit);
        if (desiredLitres <= 0) return true;

        if (world.Side == EnumAppSide.Server)
        {
            ItemStack? taken = liquidContainer.TryTakeLiquid(slot.Itemstack, desiredLitres);
            if (taken != null)
            {
                var props = BlockLiquidContainerBase.GetContainableProps(taken);
                float movedLitres = props == null ? desiredLitres : taken.StackSize / props.ItemsPerLitre;
                trough.AddWater(movedLitres / litresPerUnit);
                slot.MarkDirty();
            }
        }
        return true;
    }

    private static bool IsWater(ItemStack stack)
    {
        string path = stack.Collectible.Code.Path;
        return path == "waterportion" || path.StartsWith("water-") || path.Contains("freshwater");
    }
}

public sealed class BlockFaunaTroughSmall : BlockTrough
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        if (TroughWaterTransfer.TryInteract(world, byPlayer, blockSel.Position)) return true;
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}

public sealed class BlockFaunaTroughLarge : BlockTroughDoubleBlock
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        BlockPos root = blockSel.Position.AddCopy(RootOffset);
        if (TroughWaterTransfer.TryInteract(world, byPlayer, root)) return true;
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
