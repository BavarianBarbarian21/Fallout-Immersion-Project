using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace FeilsFaunaFixes;

/// <summary>
/// Standard controlled physics for arboreal fauna, except that leaf blocks do not
/// contribute collision boxes. All other blocks retain their normal collision.
/// </summary>
public sealed class EntityBehaviorFoliagePhysics : EntityBehaviorControlledPhysics, IRemotePhysics
{
    private readonly FoliageCollisionTester foliageCollisionTester = new();

    public EntityBehaviorFoliagePhysics(Entity entity) : base(entity) { }

    public override void OnPhysicsTick(float dt)
    {
        CachingCollisionTester previous = collisionTester;
        collisionTester = foliageCollisionTester;
        try
        {
            base.OnPhysicsTick(dt);
        }
        finally
        {
            collisionTester = previous;
        }
    }

    void IRemotePhysics.HandleRemotePhysics(float dt, bool isTeleport)
    {
        CachingCollisionTester previous = collisionTester;
        collisionTester = foliageCollisionTester;
        try
        {
            base.HandleRemotePhysics(dt, isTeleport);
        }
        finally
        {
            collisionTester = previous;
        }
    }

    void IRemotePhysics.OnReceivedClientPos(int version)
    {
        CachingCollisionTester previous = collisionTester;
        collisionTester = foliageCollisionTester;
        try
        {
            base.OnReceivedClientPos(version);
        }
        finally
        {
            collisionTester = previous;
        }
    }

    public override string PropertyName() => "faunafoliagephysics";

    private sealed class FoliageCollisionTester : CachingCollisionTester
    {
        protected override void GenerateCollisionBoxList(
            IBlockAccessor blockAccessor,
            double motionX,
            double motionY,
            double motionZ,
            float stepHeight,
            float yExtra,
            int dimension)
        {
            Cuboidd box = entityBox;
            bool sameMin = minPos.SetAndEquals(
                (int)(box.X1 + Math.Min(0, motionX)),
                (int)(box.Y1 + Math.Min(0, motionY) - yExtra),
                (int)(box.Z1 + Math.Min(0, motionZ))
            );
            double collisionTop = Math.Max(box.Y1 + stepHeight, box.Y2);
            bool sameMax = maxPos.SetAndEquals(
                (int)(box.X2 + Math.Max(0, motionX)),
                (int)(collisionTop + Math.Max(0, motionY)),
                (int)(box.Z2 + Math.Max(0, motionZ))
            );
            if (sameMin && sameMax) return;

            CollisionBoxList.Clear();
            blockAccessor.WalkBlocks(minPos, maxPos, (block, x, y, z) =>
            {
                if (block.BlockMaterial == EnumBlockMaterial.Leaves) return;
                Cuboidf[]? boxes = block.GetCollisionBoxes(blockAccessor, tmpPos.Set(x, y, z));
                if (boxes != null) CollisionBoxList.Add(boxes, x, y, z, block);
            }, true);
        }
    }
}
