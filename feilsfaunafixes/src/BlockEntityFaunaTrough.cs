using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class BlockEntityFaunaTrough : BlockEntityTrough
{
    private float waterUnits;
    private ContainedTextureSource? waterTextureSource;

    public float Capacity => Block?.Variant?["part"] == "small"
        ? FeilsFaunaFixesSystem.Config.SmallTroughCapacity
        : FeilsFaunaFixesSystem.Config.LargeTroughCapacity;
    public float WaterUnits => waterUnits;
    public bool HasWater => waterUnits > 0.001f;
    public bool CanAcceptWater => Inventory.Empty && waterUnits < Capacity - 0.001f;

    public float AddWater(float amount)
    {
        if (amount <= 0 || !Inventory.Empty) return 0;
        float accepted = Math.Min(amount, Capacity - waterUnits);
        if (accepted <= 0) return 0;
        waterUnits += accepted;
        MarkDirty(true);
        FeilsFaunaFixesSystem.Debug("Trough {0}: {1:0.##}/{2:0.##}", Pos, waterUnits, Capacity);
        return accepted;
    }

    public bool TryDrink(float amount)
    {
        if (waterUnits + 0.001f < amount) return false;
        waterUnits = Math.Max(0, waterUnits - amount);
        MarkDirty(true);
        FeilsFaunaFixesSystem.Debug("Trough {0}: drink -> {1:0.##}/{2:0.##}", Pos, waterUnits, Capacity);
        return true;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetFloat("faunaWaterUnits", waterUnits);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        waterUnits = Math.Clamp(tree.GetFloat("faunaWaterUnits"), 0, Capacity);
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        bool preventDefault = base.OnTesselation(mesher, tesselator);
        if (!HasWater || Api is not ICoreClientAPI capi || Block == null) return preventDefault;

        bool small = Block.Variant?["part"] == "small";
        string shapePath = small
            ? "feilsfaunafixes:shapes/block/trough/water-small.json"
            : "feilsfaunafixes:shapes/block/trough/water-large.json";
        Shape? shape = Shape.TryGet(Api, shapePath);
        if (shape == null) return preventDefault;

        waterTextureSource ??= new ContainedTextureSource(
            capi,
            capi.BlockTextureAtlas,
            new Dictionary<string, AssetLocation>
            {
                ["water"] = new AssetLocation("game:block/liquid/water")
            },
            "Feil's Fauna Fixes trough water"
        );

        int steps = small ? 8 : 16;
        int level = GameMath.Clamp((int)Math.Ceiling(WaterUnits / Capacity * steps), 1, steps);
        float fill = level / (float)steps;
        float minY = small ? 1.65f / 16f : 4.15f / 16f;
        float maxY = small ? 4.15f / 16f : 8.25f / 16f;
        float y = minY + (maxY - minY) * fill;
        Vec3f rotation = new(Block.Shape.rotateX, Block.Shape.rotateY, Block.Shape.rotateZ);

        tesselator.TesselateShape("fauna-trough-water", shape, out MeshData waterMesh, waterTextureSource, rotation);
        waterMesh.Translate(0, y, 0);

        if (!small && Block is BlockTroughDoubleBlock doubleTrough)
        {
            Vec3f otherRotation = new(Block.Shape.rotateX, Block.Shape.rotateY + 180, Block.Shape.rotateZ);
            tesselator.TesselateShape("fauna-trough-water-other", shape, out MeshData otherHalf, waterTextureSource, otherRotation);
            otherHalf.Translate(0, y, 0);
            otherHalf.Translate(doubleTrough.OtherPartFacing().Normalf);
            waterMesh.AddMeshData(otherHalf);
        }

        mesher.AddMeshData(waterMesh, 1);
        return preventDefault;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        if (HasWater)
        {
            dsc.AppendLine(Lang.Get("feilsfaunafixes:trough-water", Math.Round(waterUnits, 1), Math.Round(Capacity, 1)));
            return;
        }
        base.GetBlockInfo(forPlayer, dsc);
    }
}
