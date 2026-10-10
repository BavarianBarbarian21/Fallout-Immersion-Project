using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFarmingFixes;

public sealed class BlockEntityFarmingFarmland : BlockEntityFarmland
{
    private const int InstalledOllaCount = 3;
    private const float FillLitres = 10f;

    private bool hasOllas;
    private bool ollasFilled;
    private string ollaColor = "blue";
    private ContainedTextureSource? ollaTextureSource;
    private string? textureSourceColor;

    public bool HasFilledOllas => hasOllas && ollasFilled;

    public bool OnOllaInteract(IPlayer byPlayer)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;
        ItemStack? stack = slot.Itemstack;

        if (!hasOllas)
        {
            if (byPlayer.Entity.Controls?.Sneak != true || stack?.Collectible?.Code?.Domain != "feilsfarmingfixes"
                || !stack.Collectible.Code.Path.StartsWith("olla-fired-", StringComparison.Ordinal))
            {
                return false;
            }

            if (stack.StackSize < InstalledOllaCount)
            {
                return true;
            }

            if (Api.Side == EnumAppSide.Server)
            {
                ollaColor = stack.Collectible.Code.Path["olla-fired-".Length..];
                hasOllas = true;
                ollasFilled = false;
                slot.TakeOut(InstalledOllaCount);
                slot.MarkDirty();
                MarkDirty(true);
                RefreshOllaArea();
                Api.World.PlaySoundAt(new AssetLocation("game:sounds/block/ceramicplace"), Pos.X + 0.5, Pos.Y + 0.9, Pos.Z + 0.5, byPlayer);
            }

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            return true;
        }

        if (stack?.Block is not BlockBucket bucket)
        {
            return false;
        }

        ItemStack? content = bucket.GetContent(stack);
        bool containsWater = content?.Collectible?.Code?.Domain == "game" && content.Collectible.Code.Path == "waterportion";
        if (!containsWater || bucket.GetCurrentLitres(stack) + 0.001f < FillLitres)
        {
            return true;
        }

        if (ollasFilled)
        {
            return true;
        }

        if (Api.Side == EnumAppSide.Server)
        {
            bucket.TryTakeLiquid(stack, FillLitres);
            slot.MarkDirty();
            ollasFilled = true;
            ApplyOllaMoistureFloor();
            RefreshOllaArea();
            MarkDirty(true);
            Api.World.PlaySoundAt(new AssetLocation("game:sounds/effect/water-pour"), Pos.X + 0.5, Pos.Y + 0.9, Pos.Z + 0.5, byPlayer);
        }

        (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
        return true;
    }

    public void DropOllas()
    {
        if (!hasOllas || Api.Side != EnumAppSide.Server)
        {
            return;
        }

        Item? olla = Api.World.GetItem(new AssetLocation($"feilsfarmingfixes:olla-fired-{ollaColor}"));
        if (olla != null)
        {
            Api.World.SpawnItemEntity(new ItemStack(olla, InstalledOllaCount), Pos.ToVec3d().Add(0.5, 0.9, 0.5));
        }

        hasOllas = false;
        ollasFilled = false;
        RefreshOllaArea();
    }

    protected override void ShortUpdate(float dt)
    {
        base.ShortUpdate(dt);
        ApplyOllaMoistureFloor();
    }

    protected override void beginIntervalledUpdate(out FarmlandFastForwardUpdate onInterval, out FarmlandUpdateEnd onEnd)
    {
        base.beginIntervalledUpdate(out FarmlandFastForwardUpdate baseInterval, out FarmlandUpdateEnd baseEnd);

        onInterval = (hours, conditions, lightFactor, growthPaused) =>
        {
            ApplyOllaMoistureFloor(false);
            baseInterval(hours, conditions, lightFactor, growthPaused);
            ApplyOllaMoistureFloor(false);
        };

        onEnd = () =>
        {
            baseEnd();
            ApplyOllaMoistureFloor();
        };
    }

    private void RefreshOllaArea()
    {
        for (int dx = -4; dx <= 4; dx++)
        {
            for (int dz = -4; dz <= 4; dz++)
            {
                BlockPos nearbyPos = Pos.AddCopy(dx, 0, dz);
                if (Api.World.BlockAccessor.GetBlockEntity(nearbyPos) is BlockEntityFarmingFarmland nearby)
                {
                    nearby.ApplyOllaMoistureFloor();
                }
            }
        }
    }

    private void ApplyOllaMoistureFloor(bool markDirty = true)
    {
        float floor = FindOllaMoistureFloor();
        if (floor <= 0f || moistureLevel + 0.0001f >= floor)
        {
            return;
        }

        moistureLevel = floor;
        UpdateFarmlandBlock();
        if (markDirty)
        {
            MarkDirty(true);
        }
    }

    private float FindOllaMoistureFloor()
    {
        float best = 0f;

        for (int dx = -4; dx <= 4; dx++)
        {
            for (int dz = -4; dz <= 4; dz++)
            {
                int distance = Math.Max(Math.Abs(dx), Math.Abs(dz));
                if (distance > 4)
                {
                    continue;
                }

                BlockPos sourcePos = Pos.AddCopy(dx, 0, dz);
                if (Api.World.BlockAccessor.GetBlockEntity(sourcePos) is not BlockEntityFarmingFarmland source || !source.HasFilledOllas)
                {
                    continue;
                }

                float candidate = distance == 0 ? 1f : Math.Max(0f, 0.80f - 0.25f * (distance - 1));
                best = Math.Max(best, candidate);
            }
        }

        return best;
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        hasOllas = tree.GetBool("farmingHasOllas");
        ollasFilled = tree.GetBool("farmingOllasFilled");
        ollaColor = tree.GetString("farmingOllaColor", "blue");
        ollaTextureSource = null;
        textureSourceColor = null;
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        tree.SetBool("farmingHasOllas", hasOllas);
        tree.SetBool("farmingOllasFilled", ollasFilled);
        tree.SetString("farmingOllaColor", ollaColor);
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
    {
        base.GetBlockInfo(forPlayer, dsc);
        if (hasOllas)
        {
            dsc.AppendLine(Lang.Get(ollasFilled ? "feilsfarmingfixes:ollas-filled" : "feilsfarmingfixes:ollas-empty"));
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        bool preventDefault = base.OnTesselation(mesher, tesselator);
        if (!hasOllas || Api is not ICoreClientAPI capi)
        {
            return preventDefault;
        }

        string shapePath = "feilsfarmingfixes:shapes/block/farmland/ollas-empty.json";
        Shape? shape = Shape.TryGet(Api, shapePath);
        if (shape == null)
        {
            return preventDefault;
        }

        if (ollaTextureSource == null || textureSourceColor != ollaColor)
        {
            textureSourceColor = ollaColor;
            ollaTextureSource = new ContainedTextureSource(
                capi,
                capi.BlockTextureAtlas,
                new Dictionary<string, AssetLocation>
                {
                    ["clay"] = new AssetLocation($"game:block/clay/hardened/{ollaColor}"),
                    ["water"] = new AssetLocation("game:block/liquid/water")
                },
                "Feil's Farming Fixes farmland ollas"
            );
        }

        tesselator.TesselateShape("farmland-ollas", shape, out MeshData mesh, ollaTextureSource);
        mesher.AddMeshData(mesh, 1);

        if (ollasFilled)
        {
            Shape? waterShape = Shape.TryGet(Api, "feilsfarmingfixes:shapes/block/farmland/ollas-water.json");
            if (waterShape != null)
            {
                tesselator.TesselateShape("farmland-olla-water", waterShape, out MeshData waterMesh, ollaTextureSource);
                mesher.AddMeshData(waterMesh, 1);
            }
        }

        return preventDefault;
    }
}
