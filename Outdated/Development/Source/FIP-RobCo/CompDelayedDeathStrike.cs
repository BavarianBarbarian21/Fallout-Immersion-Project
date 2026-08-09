using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FIP.RobCo;

public class CompDelayedDeathStrike : ThingComp
{
    public CompProperties_DelayedDeathStrike Props => (CompProperties_DelayedDeathStrike)props;

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        IntVec3 strikeCell = parent.PositionHeld;
        base.PostDestroy(mode, previousMap);

        if (mode != DestroyMode.KillFinalize || previousMap == null || !strikeCell.IsValid)
        {
            return;
        }

        RobCoDelayedDeathStrikeManager strikeManager = previousMap.GetComponent<RobCoDelayedDeathStrikeManager>();
        if (strikeManager == null)
        {
            strikeManager = new RobCoDelayedDeathStrikeManager(previousMap);
            previousMap.components.Add(strikeManager);
        }

        strikeManager.QueueStrike(strikeCell, Props.delayTicks, Props.projectileDef, Props.orbitalStrikeDef);
    }
}

public class CompProperties_DelayedDeathStrike : CompProperties
{
    public int delayTicks = 180;
    public ThingDef projectileDef;
    public ThingDef orbitalStrikeDef;

    public CompProperties_DelayedDeathStrike()
    {
        compClass = typeof(CompDelayedDeathStrike);
    }
}

public class RobCoDelayedDeathStrikeManager : MapComponent
{
    private sealed class PendingStrike : IExposable
    {
        public IntVec3 cell = IntVec3.Invalid;
        public int triggerTick = -1;
        public ThingDef projectileDef;
        public ThingDef orbitalStrikeDef;

        public void ExposeData()
        {
            Scribe_Values.Look(ref cell, "cell", IntVec3.Invalid);
            Scribe_Values.Look(ref triggerTick, "triggerTick", -1);
            Scribe_Defs.Look(ref projectileDef, "projectileDef");
            Scribe_Defs.Look(ref orbitalStrikeDef, "orbitalStrikeDef");
        }
    }

    private List<PendingStrike> pendingStrikes = new();

    public RobCoDelayedDeathStrikeManager(Map map)
        : base(map)
    {
    }

    public void QueueStrike(IntVec3 cell, int delayTicks, ThingDef projectileDef, ThingDef orbitalStrikeDef)
    {
        pendingStrikes.Add(new PendingStrike
        {
            cell = cell,
            triggerTick = Find.TickManager.TicksGame + delayTicks,
            projectileDef = projectileDef,
            orbitalStrikeDef = orbitalStrikeDef
        });
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref pendingStrikes, "pendingStrikes", LookMode.Deep);
        if (pendingStrikes == null)
        {
            pendingStrikes = new List<PendingStrike>();
        }
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        int currentTick = Find.TickManager.TicksGame;
        for (int index = pendingStrikes.Count - 1; index >= 0; index--)
        {
            PendingStrike strike = pendingStrikes[index];
            if (strike == null)
            {
                pendingStrikes.RemoveAt(index);
                continue;
            }

            if (currentTick < strike.triggerTick)
            {
                continue;
            }

            TriggerStrike(strike);
            pendingStrikes.RemoveAt(index);
        }
    }

    private void TriggerStrike(PendingStrike strike)
    {
        if (!strike.cell.IsValid || !strike.cell.InBounds(map))
        {
            return;
        }

        if (strike.projectileDef?.projectile != null)
        {
            TriggerProjectileExplosion(strike.cell, strike.projectileDef);
            return;
        }

        if (strike.orbitalStrikeDef != null)
        {
            TriggerOrbitalStrike(strike.cell, strike.orbitalStrikeDef);
        }
    }

    private void TriggerProjectileExplosion(IntVec3 cell, ThingDef projectileDef)
    {
        ProjectileProperties projectile = projectileDef.projectile;
        if (projectile == null)
        {
            return;
        }

        if (projectile.explosionEffect != null)
        {
            Effecter effecter = projectile.explosionEffect.Spawn();
            if (projectile.explosionEffectLifetimeTicks != 0)
            {
                map.effecterMaintainer.AddEffecterToMaintain(effecter, cell, projectile.explosionEffectLifetimeTicks);
            }
            else
            {
                effecter.Trigger(new TargetInfo(cell, map), new TargetInfo(cell, map));
                effecter.Cleanup();
            }
        }

        GenExplosion.DoExplosion(cell, map, projectile.explosionRadius, projectile.damageDef, null,
            damAmount: projectile.GetDamageAmount((Thing)null, null),
            armorPenetration: projectile.GetArmorPenetration((Thing)null, null),
            explosionSound: projectile.soundExplode,
            projectile: projectileDef,
            postExplosionSpawnThingDef: projectile.postExplosionSpawnThingDef ?? (projectile.explosionSpawnsSingleFilth ? null : projectile.filth),
            postExplosionSpawnThingDefWater: projectile.postExplosionSpawnThingDefWater,
            postExplosionSpawnChance: projectile.postExplosionSpawnChance,
            postExplosionSpawnThingCount: projectile.postExplosionSpawnThingCount,
            postExplosionGasType: projectile.postExplosionGasType,
            preExplosionSpawnThingDef: projectile.preExplosionSpawnThingDef,
            preExplosionSpawnChance: projectile.preExplosionSpawnChance,
            preExplosionSpawnThingCount: projectile.preExplosionSpawnThingCount,
            applyDamageToExplosionCellsNeighbors: projectile.applyDamageToExplosionCellsNeighbors,
            chanceToStartFire: projectile.explosionChanceToStartFire,
            damageFalloff: projectile.explosionDamageFalloff,
            propagationSpeed: projectile.damageDef.expolosionPropagationSpeed,
            screenShakeFactor: projectile.screenShakeFactor,
            doVisualEffects: projectile.doExplosionVFX,
            preExplosionSpawnSingleThingDef: projectile.preExplosionSpawnSingleThingDef,
            postExplosionSpawnSingleThingDef: projectile.postExplosionSpawnSingleThingDef);

        if (projectile.explosionSpawnsSingleFilth && projectile.filth != null && projectile.filthCount.TrueMax > 0 && Rand.Chance(projectile.filthChance) && !cell.Filled(map))
        {
            FilthMaker.TryMakeFilth(cell, map, projectile.filth, projectile.filthCount.RandomInRange);
        }
    }

    private void TriggerOrbitalStrike(IntVec3 cell, ThingDef orbitalStrikeDef)
    {
        Thing strikeThing = ThingMaker.MakeThing(orbitalStrikeDef);
        Thing spawnedThing = GenSpawn.Spawn(strikeThing, cell, map);

        if (spawnedThing is Bombardment bombardment)
        {
            bombardment.StartStrike();
            return;
        }

        if (spawnedThing is PowerBeam powerBeam)
        {
            powerBeam.StartStrike();
        }
    }
}