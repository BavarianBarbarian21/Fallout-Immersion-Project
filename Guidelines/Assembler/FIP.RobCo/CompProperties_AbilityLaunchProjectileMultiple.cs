using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace FIP_RobCo;

public class CompProperties_AbilityLaunchProjectileMultiple : CompProperties_AbilityLaunchProjectile
{
    public new ThingDef projectileDef;
    public int burstShotCount = 1;
    public float forcedMissRadius = 2.9f;
    public int ticksBetweenBurstShots = 20;
    public int maxCharges = 1;
    public ThingDef ammoDef;
    public int ammoCountPerCharge = 1;
    public SoundDef soundReload;
    public string chargeNoun = "charge";

    public CompProperties_AbilityLaunchProjectileMultiple()
    {
        compClass = typeof(CompAbilityEffect_LaunchProjectileMultiple);
    }
}

public class CompAbilityEffect_LaunchProjectileMultiple : CompAbilityEffect
{
    private int currentCharges;

    public new CompProperties_AbilityLaunchProjectileMultiple Props => (CompProperties_AbilityLaunchProjectileMultiple)props;

    public int CurrentCharges => currentCharges;

    public override void Initialize(AbilityCompProperties props)
    {
        base.Initialize(props);
        currentCharges = Props.maxCharges;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref currentCharges, "currentCharges", Props.maxCharges);
    }

    public void AddCharges(int amount)
    {
        currentCharges = System.Math.Min(currentCharges + amount, Props.maxCharges);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        base.Apply(target, dest);
        if (currentCharges <= 0)
        {
            return;
        }

        currentCharges--;
        if (Props.projectileDef != null)
        {
            LaunchProjectile(target);
        }
    }

    public override bool GizmoDisabled(out string reason)
    {
        if (currentCharges > 0)
        {
            reason = null;
            return false;
        }

        reason = Props.ammoDef != null
            ? "CommandReload_NoAmmo".Translate(Props.ammoDef.label)
            : "NoCharges".Translate();
        return true;
    }

    public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
    {
        return Props.maxCharges > 1
            ? $"{Props.chargeNoun.CapitalizeFirst()}: {currentCharges} / {Props.maxCharges}"
            : null;
    }

    public void ReloadFromInventory(Pawn reloader = null)
    {
        Pawn inventoryPawn = reloader ?? parent.pawn;
        if (inventoryPawn?.inventory == null)
        {
            return;
        }

        int chargesToReload = Props.maxCharges - currentCharges;
        if (chargesToReload <= 0)
        {
            return;
        }

        if (Props.ammoDef == null)
        {
            currentCharges = Props.maxCharges;
            Props.soundReload?.PlayOneShot(new TargetInfo(parent.pawn.Position, parent.pawn.Map));
            return;
        }

        int ammoNeeded = chargesToReload * Props.ammoCountPerCharge;
        int ammoAvailable = inventoryPawn.inventory.innerContainer.TotalStackCountOfDef(Props.ammoDef);
        int ammoToConsume = System.Math.Min(ammoNeeded, ammoAvailable);
        if (ammoToConsume < Props.ammoCountPerCharge)
        {
            return;
        }

        Thing ammoThing = null;
        foreach (Thing thing in inventoryPawn.inventory.innerContainer)
        {
            if (thing.def == Props.ammoDef)
            {
                ammoThing = thing;
                break;
            }
        }

        if (ammoThing == null)
        {
            return;
        }

        Thing consumedAmmo = inventoryPawn.inventory.innerContainer.Take(ammoThing, ammoToConsume);
        consumedAmmo.Destroy();
        currentCharges += ammoToConsume / Props.ammoCountPerCharge;

        if (Props.soundReload != null)
        {
            Props.soundReload.PlayOneShot(new TargetInfo(parent.pawn.Position, parent.pawn.Map));
        }
    }

    private void LaunchProjectile(LocalTargetInfo target)
    {
        Pawn pawn = parent.pawn;
        if (Props.ticksBetweenBurstShots > 0 && Props.burstShotCount > 1)
        {
            BurstFireManager burstManager = pawn.Map.GetComponent<BurstFireManager>();
            if (burstManager == null)
            {
                burstManager = new BurstFireManager(pawn.Map);
                pawn.Map.components.Add(burstManager);
            }

            burstManager.QueueBurst(pawn, target, Props.projectileDef, Props.burstShotCount, Props.forcedMissRadius, Props.ticksBetweenBurstShots, parent.verb.preventFriendlyFire);
            return;
        }

        for (int i = 0; i < Props.burstShotCount; i++)
        {
            BurstFireManager.FireSingleProjectile(pawn, target, Props.projectileDef, Props.forcedMissRadius, parent.verb.preventFriendlyFire);
        }
    }
}

public class BurstFireManager : MapComponent
{
    private sealed class BurstData
    {
        public Pawn caster;
        public LocalTargetInfo target;
        public ThingDef projectileDef;
        public int shotsRemaining;
        public float forcedMissRadius;
        public int ticksBetweenShots;
        public bool preventFriendlyFire;
        public int ticksUntilNextShot;
    }

    private readonly List<BurstData> activeBursts = new();

    public BurstFireManager(Map map)
        : base(map)
    {
    }

    public void QueueBurst(Pawn caster, LocalTargetInfo target, ThingDef projectileDef, int burstShotCount, float forcedMissRadius, int ticksBetweenBurstShots, bool preventFriendlyFire)
    {
        activeBursts.Add(new BurstData
        {
            caster = caster,
            target = target,
            projectileDef = projectileDef,
            shotsRemaining = burstShotCount,
            forcedMissRadius = forcedMissRadius,
            ticksBetweenShots = ticksBetweenBurstShots,
            preventFriendlyFire = preventFriendlyFire,
            ticksUntilNextShot = 0
        });
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        for (int i = activeBursts.Count - 1; i >= 0; i--)
        {
            BurstData burst = activeBursts[i];
            if (burst.caster == null || burst.caster.Dead || burst.caster.Map != map)
            {
                activeBursts.RemoveAt(i);
                continue;
            }

            burst.ticksUntilNextShot--;
            if (burst.ticksUntilNextShot > 0)
            {
                continue;
            }

            FireSingleProjectile(burst.caster, burst.target, burst.projectileDef, burst.forcedMissRadius, burst.preventFriendlyFire);
            burst.shotsRemaining--;
            if (burst.shotsRemaining <= 0)
            {
                activeBursts.RemoveAt(i);
            }
            else
            {
                burst.ticksUntilNextShot = burst.ticksBetweenShots;
            }
        }
    }

    public static void FireSingleProjectile(Pawn pawn, LocalTargetInfo target, ThingDef projectileDef, float forcedMissRadius, bool preventFriendlyFire)
    {
        LocalTargetInfo missTarget = target;
        if (forcedMissRadius > 0.5f)
        {
            IntVec3 targetCell = target.Cell + GenRadial.RadialPattern[Rand.Range(0, GenRadial.NumCellsInRadius(forcedMissRadius))];
            missTarget = new LocalTargetInfo(targetCell);
        }

        ((Projectile)GenSpawn.Spawn(projectileDef, pawn.Position, pawn.Map)).Launch(
            pawn,
            pawn.DrawPos,
            missTarget,
            target,
            ProjectileHitFlags.IntendedTarget,
            preventFriendlyFire);
    }
}