using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

public sealed class EntityBehaviorHydration : EntityBehavior
{
    private const string TreeKey = "faunaHydration";
    private ITreeAttribute state = null!;
    private double lastUpdateHours;
    private float usePerDay;
    private bool sheep;
    private bool chicken;
    private bool dungProducer;
    private bool largeDungProducer;
    private bool participates;
    private bool shearing;
    private static WorldInteraction[]? shearHelp;

    public float Hydration
    {
        get => state.GetFloat("value");
        private set
        {
            state.SetFloat("value", Math.Clamp(value, 0, FeilsFaunaFixesSystem.Config.AnimalHydrationReserve));
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    public float WoolGrowth
    {
        get => state.GetFloat("woolGrowth");
        private set
        {
            state.SetFloat("woolGrowth", Math.Clamp(value, 0, 1));
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    public bool NeedsWater => participates && Hydration < 0.55f;
    public bool IsHydrated => Hydration > 0.02f;

    public EntityBehaviorHydration(Entity entity) : base(entity) { }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
        chicken = entity.Code.Path.StartsWith("chicken-");
        sheep = entity.Code.Path.StartsWith("sheep-");
        dungProducer = entity.Code.Path.StartsWith("goat-");
        largeDungProducer = entity.Code.Path.StartsWith("goat-muskox-")
            || entity.Code.Path.StartsWith("goat-takingold-");
        participates = chicken || sheep || dungProducer;
        usePerDay = chicken
            ? FeilsFaunaFixesSystem.Config.ChickenHydrationUsePerDay
            : sheep
                ? FeilsFaunaFixesSystem.Config.SheepHydrationUsePerDay
                : FeilsFaunaFixesSystem.Config.DungAnimalHydrationUsePerDay;
        state = entity.WatchedAttributes.GetTreeAttribute(TreeKey)!;
        if (state == null)
        {
            state = new TreeAttribute();
            entity.WatchedAttributes.SetAttribute(TreeKey, state);
            if (entity.World.Side == EnumAppSide.Server && participates) Hydration = FeilsFaunaFixesSystem.Config.AnimalHydrationReserve * 0.5f;
        }
        lastUpdateHours = state.GetDouble("lastUpdateHours", entity.World.Calendar.TotalHours);
    }

    public override void OnGameTick(float deltaTime)
    {
        base.OnGameTick(deltaTime);
        if (entity.World.Side != EnumAppSide.Server || !entity.Alive || !participates) return;
        double now = entity.World.Calendar.TotalHours;
        double elapsed = Math.Clamp(now - lastUpdateHours, 0, entity.World.Calendar.HoursPerDay * 7);
        if (elapsed < 0.05) return;
        lastUpdateHours = now;
        state.SetDouble("lastUpdateHours", now);
        float before = Hydration;
        Hydration -= (float)(elapsed / entity.World.Calendar.HoursPerDay) * usePerDay;
        if (sheep && before > 0 && elapsed > 0)
        {
            double daysPerYear = entity.World.Calendar.DaysPerYear;
            WoolGrowth += (float)(elapsed / entity.World.Calendar.HoursPerDay / daysPerYear * FeilsFaunaFixesSystem.Config.ShearingsPerYear);
        }
        if (dungProducer && before > 0 && elapsed > 0)
        {
            float progress = state.GetFloat("dungProgress");
            progress += (float)(elapsed / entity.World.Calendar.HoursPerDay);
            float interval = Math.Max(0.1f, FeilsFaunaFixesSystem.Config.DungProductionIntervalDays);
            while (progress >= interval)
            {
                SpawnDung();
                progress -= interval;
            }
            state.SetFloat("dungProgress", progress);
            entity.WatchedAttributes.MarkPathDirty(TreeKey);
        }
    }

    private void SpawnDung()
    {
        Item? dung = entity.World.GetItem(new AssetLocation("feilsfaunafixes:dung"));
        if (dung == null) return;
        int configuredCount = largeDungProducer
            ? FeilsFaunaFixesSystem.Config.LargeGoatDungPerProduction
            : FeilsFaunaFixesSystem.Config.DungPerProduction;
        int count = Math.Max(1, configuredCount);
        if (TryPlaceDung(new ItemStack(dung, count)))
        {
            FeilsFaunaFixesSystem.Debug("{0} placed {1} dung in ground storage", entity.Code, count);
        }
        else
        {
            FeilsFaunaFixesSystem.Debug("{0} could not place dung: no free supported block in the surrounding 3x3 area", entity.Code);
        }
    }

    private bool TryPlaceDung(ItemStack stack)
    {
        Block? groundStorageBlock = entity.World.GetBlock(new AssetLocation("game:groundstorage"));
        if (groundStorageBlock == null) return false;

        BlockPos origin = entity.Pos.AsBlockPos;
        int rotation = entity.World.Rand.Next(4);

        if (TryPlaceDungAt(origin, groundStorageBlock, stack)) return true;

        (int X, int Z)[] cardinalOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];
        for (int i = 0; i < cardinalOffsets.Length; i++)
        {
            (int x, int z) = cardinalOffsets[(i + rotation) % cardinalOffsets.Length];
            if (TryPlaceDungAt(origin.AddCopy(x, 0, z), groundStorageBlock, stack)) return true;
        }

        (int X, int Z)[] diagonalOffsets = [(1, -1), (1, 1), (-1, 1), (-1, -1)];
        for (int i = 0; i < diagonalOffsets.Length; i++)
        {
            (int x, int z) = diagonalOffsets[(i + rotation) % diagonalOffsets.Length];
            if (TryPlaceDungAt(origin.AddCopy(x, 0, z), groundStorageBlock, stack)) return true;
        }

        return false;
    }

    private bool TryPlaceDungAt(BlockPos targetPos, Block groundStorageBlock, ItemStack stack)
    {
        IBlockAccessor accessor = entity.World.BlockAccessor;

        // Default returns air only if neither the solid nor fluid layer contains anything.
        // Requiring actual air prevents grass, crops, loose stones, water, and other blocks from being replaced.
        if (accessor.GetBlock(targetPos, BlockLayersAccess.Default).Id != 0) return false;

        BlockPos supportPos = targetPos.DownCopy();
        Block support = accessor.GetBlock(supportPos, BlockLayersAccess.MostSolid);
        if (!support.CanAttachBlockAt(accessor, groundStorageBlock, supportPos, BlockFacing.UP)) return false;

        accessor.SetBlock(groundStorageBlock.BlockId, targetPos);
        if (accessor.GetBlockEntity(targetPos) is not BlockEntityGroundStorage storage)
        {
            accessor.SetBlock(0, targetPos);
            return false;
        }

        ItemSlot slot = storage.Inventory[0];
        slot.Itemstack = stack.Clone();
        storage.MeshAngle = (float)(entity.World.Rand.NextDouble() * GameMath.TWOPI);
        storage.DetermineStorageProperties(slot);
        slot.MarkDirty();
        storage.MarkDirty(true);
        accessor.TriggerNeighbourBlockUpdate(targetPos);
        return true;
    }

    public bool DrinkFrom(BlockEntityFaunaTrough trough)
    {
        if (!participates) return false;
        float wanted = FeilsFaunaFixesSystem.Config.AnimalHydrationReserve - Hydration;
        float portions = Math.Min(trough.WaterUnits, Math.Max(1f, wanted));
        if (!trough.TryDrink(portions)) return false;
        Hydration += portions;
        FeilsFaunaFixesSystem.Debug("{0} hydration {1:0.##}; trough {2:0.##}", entity.Code, Hydration, trough.WaterUnits);
        return true;
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode, ref EnumHandling handled)
    {
        if (!sheep || mode == EnumInteractMode.Attack || !entity.Alive || WoolGrowth < 0.999f || byEntity.Controls?.Sneak != true || itemslot?.Itemstack?.Collectible.Tool != EnumTool.Shears)
        {
            return;
        }
        handled = EnumHandling.PreventSubsequent;
        shearing = true;
        byEntity.World.RegisterCallback(_ =>
        {
            if (!shearing || WoolGrowth < 0.999f || byEntity.Pos.DistanceTo(entity.Pos) > 4.5) return;
            if (byEntity.World.Side == EnumAppSide.Server)
            {
                WoolGrowth = 0;
                int count = Math.Max(1, FeilsFaunaFixesSystem.Config.WoolPerShear);
                Item? wool = byEntity.World.GetItem(new AssetLocation("feilsfaunafixes:rawwool"));
                if (wool != null) byEntity.World.SpawnItemEntity(new ItemStack(wool, count), entity.Pos.XYZ.Add(0, 0.5, 0));
                itemslot.Itemstack.Collectible.DamageItem(byEntity.World, byEntity, itemslot, 1);
                itemslot.MarkDirty();
            }
            shearing = false;
        }, 1500);
    }

    public override WorldInteraction[]? GetInteractionHelp(IClientWorldAccessor world, EntitySelection es, IClientPlayer player, ref EnumHandling handled)
    {
        if (!sheep || WoolGrowth < 0.999f) return null;
        shearHelp ??= new[]
        {
            new WorldInteraction
            {
                ActionLangCode = "feilsfaunafixes:interaction-shear",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "shift",
                Itemstacks = ObjectCacheUtil.GetToolStacks(world.Api, EnumTool.Shears)
            }
        };
        return shearHelp;
    }

    public override void GetInfoText(StringBuilder infotext)
    {
        if (!entity.Alive || !participates) return;
        infotext.AppendLine(Lang.Get("feilsfaunafixes:hydration-info", Math.Round(Hydration, 2), FeilsFaunaFixesSystem.Config.AnimalHydrationReserve));
        if (sheep) infotext.AppendLine(Lang.Get("feilsfaunafixes:wool-info", Math.Round(WoolGrowth * 100)));
        if (dungProducer)
        {
            float interval = Math.Max(0.1f, FeilsFaunaFixesSystem.Config.DungProductionIntervalDays);
            infotext.AppendLine(Lang.Get("feilsfaunafixes:dung-progress-info", Math.Round(state.GetFloat("dungProgress") / interval * 100)));
        }
    }

    public override string PropertyName() => "faunahydration";
}
