using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public class WorldObjectCompProperties_FactionPolitics : WorldObjectCompProperties
{
    public WorldObjectCompProperties_FactionPolitics()
    {
        compClass = typeof(WorldObjectCompFactionPolitics);
    }
}

public class WorldObjectCompFactionPolitics : WorldObjectComp
{
    public override IEnumerable<Gizmo> GetGizmos()
    {
        Command_Action command = CreatePoliticsCommand();
        if (command != null)
        {
            yield return command;
        }
    }

    public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
    {
        if (caravan == null)
        {
            yield break;
        }

        Command_Action command = CreatePoliticsCommand();
        if (command != null)
        {
            yield return command;
        }

        if (parent is not Settlement settlement)
        {
            yield break;
        }

        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        if (tracker != null
            && CaravanVisitUtility.SettlementVisitedNow(caravan) == settlement
            && tracker.TryGetState(settlement.Faction, out HHToolsFactionPoliticalState state)
            && state.system == HHToolsFactionPoliticalSystem.Authoritarian)
        {
            foreach (HHToolsCrimeBossState bossState in state.crimeBosses)
            {
                if (bossState.eliminated)
                {
                    continue;
                }

                Command_Action tradeCommand = HHToolsFamilyTrader.CreateTradeCommand(
                    caravan,
                    settlement,
                    state,
                    bossState);
                if (tradeCommand != null)
                {
                    yield return tradeCommand;
                }
            }
        }

        Command_Action coalitionCommand = CreateCoalitionAttackCommand(caravan, settlement);
        if (coalitionCommand != null)
        {
            yield return coalitionCommand;
        }
    }

    private Command_Action CreatePoliticsCommand()
    {
        Faction faction = parent?.Faction;
        if (faction == null || parent is not Settlement settlement)
        {
            return null;
        }

        HHToolsFactionPoliticsExtension extension = faction.GetPoliticsExtension();
        if (extension == null || (extension.system != HHToolsFactionPoliticalSystem.Civilized && extension.system != HHToolsFactionPoliticalSystem.Authoritarian))
        {
            return null;
        }

        Texture2D icon = null;
        if (!faction.def.factionIconPath.NullOrEmpty())
        {
            icon = ContentFinder<Texture2D>.Get(faction.def.factionIconPath, false);
        }

        return new Command_Action
        {
            defaultLabel = "Open politics",
            defaultDesc = "View the internal power structure of this faction.",
            icon = icon,
            action = () => Find.WindowStack.Add(new Window_HHToolsFactionPolitics(settlement))
        };
    }

    private static Command_Action CreateCoalitionAttackCommand(
        Caravan caravan,
        Settlement targetSettlement)
    {
        if (targetSettlement?.Faction == null
            || targetSettlement.Faction == Faction.OfPlayer
            || !targetSettlement.Attackable)
        {
            return null;
        }

        HHToolsFactionPoliticsTracker tracker = HHToolsFactionPoliticsTracker.Instance;
        HHToolsFactionPoliticalState supportState =
            tracker?.GetAvailableCoalitionSupport(targetSettlement.Faction);
        if (supportState?.faction == null)
        {
            return null;
        }

        Faction supportFaction = supportState.faction;
        return new Command_Action
        {
            defaultLabel = "Attack with family support",
            defaultDesc =
                $"Attack this settlement with a reinforcement group from {supportFaction.Name}. "
                + "Requires favor with all four families and has a 30-day cooldown.",
            icon = Settlement.AttackCommand,
            action = () =>
            {
                if (!tracker.TryReserveCoalitionSupport(
                        targetSettlement.Faction,
                        out Faction reservedSupportFaction))
                {
                    Messages.Message(
                        "Family support is no longer available.",
                        MessageTypeDefOf.RejectInput,
                        historical: false);
                    return;
                }

                tracker.SetPendingCoalitionTargetTile(targetSettlement.Tile);
                Messages.Message(
                    $"{reservedSupportFaction.Name} has committed a family coalition force to the attack.",
                    MessageTypeDefOf.PositiveEvent,
                    historical: true);
                SettlementUtility.Attack(caravan, targetSettlement);
            }
        };
    }
}
