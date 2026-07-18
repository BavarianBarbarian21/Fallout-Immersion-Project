using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FIP.HHTools;

public class HHToolsSettlementTraderTracker : Settlement_TraderTracker
{
    public HHToolsSettlementTraderTracker(Settlement settlement) : base(settlement)
    {
    }

    public override float TradePriceImprovementOffsetForPlayer =>
        base.TradePriceImprovementOffsetForPlayer
        + (HHToolsFactionPoliticsTracker.Instance?.GetSettlementTradeBonus(settlement.Faction) ?? 0f);
}

public sealed class HHToolsFamilyTrader : ITrader
{
    private const int FamilyStockRefreshTicks = 30 * 60000;
    private const float BaseSettlementTradeBonus = 0.02f;

    private readonly Settlement settlement;
    private readonly HHToolsFactionPoliticalState politicalState;
    private readonly HHToolsCrimeBossState bossState;

    public TraderKindDef TraderKind
    {
        get
        {
            bool soleFamily = politicalState.HasSoleFavoredFamily
                && politicalState.authoritarianController == bossState.boss;
            string defName = HHToolsFactionPoliticsUtility.GetFamilyTraderKindDefName(bossState.boss, soleFamily);
            return DefDatabase<TraderKindDef>.GetNamedSilentFail(defName);
        }
    }

    public IEnumerable<Thing> Goods
    {
        get
        {
            EnsureStock();
            return bossState.tradeStock.InnerListForReading;
        }
    }

    public int RandomPriceFactorSeed =>
        Gen.HashCombineInt(settlement.ID, 1933327354 + (int)bossState.boss * 7919);

    public string TraderName =>
        $"{HHToolsFactionPoliticsUtility.GetFamilyLabel(bossState.boss)} market at {settlement.LabelCap}";

    public bool CanTradeNow => !bossState.eliminated && TraderKind != null;

    public float TradePriceImprovementOffsetForPlayer
    {
        get
        {
            if (!bossState.favorGranted)
            {
                return BaseSettlementTradeBonus;
            }

            bool soleFamily = politicalState.HasSoleFavoredFamily
                && politicalState.authoritarianController == bossState.boss;
            return BaseSettlementTradeBonus
                + (soleFamily
                    ? HHToolsFactionPoliticsUtility.SoleFamilyTradeBonus
                    : HHToolsFactionPoliticsUtility.ConsolidatedTradeBonus);
        }
    }

    public Faction Faction => settlement.Faction;

    public TradeCurrency TradeCurrency => TraderKind.tradeCurrency;

    public HHToolsFamilyTrader(
        Settlement settlement,
        HHToolsFactionPoliticalState politicalState,
        HHToolsCrimeBossState bossState)
    {
        this.settlement = settlement;
        this.politicalState = politicalState;
        this.bossState = bossState;
    }

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator)
    {
        Caravan caravan = playerNegotiator.GetCaravan();
        foreach (Thing item in CaravanInventoryUtility.AllInventoryItems(caravan))
        {
            yield return item;
        }

        foreach (Pawn pawn in caravan.PawnsListForReading)
        {
            if (!caravan.IsOwner(pawn))
            {
                yield return pawn;
            }
        }
    }

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        EnsureStock();
        Caravan caravan = playerNegotiator.GetCaravan();
        Thing tradedThing = toGive.SplitOff(countToGive);
        tradedThing.PreTraded(TradeAction.PlayerSells, playerNegotiator, settlement);

        if (toGive is Pawn pawn)
        {
            CaravanInventoryUtility.MoveAllInventoryToSomeoneElse(pawn, caravan.PawnsListForReading);
            if (!pawn.RaceProps.Humanlike
                && !bossState.tradeStock.TryAdd(pawn, canMergeWithExistingStacks: false))
            {
                pawn.Destroy();
            }

            return;
        }

        if (!bossState.tradeStock.TryAdd(tradedThing, canMergeWithExistingStacks: false))
        {
            tradedThing.Destroy();
        }
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        Caravan caravan = playerNegotiator.GetCaravan();
        Thing tradedThing = toGive.SplitOff(countToGive);
        tradedThing.PreTraded(TradeAction.PlayerBuys, playerNegotiator, settlement);

        if (tradedThing is Pawn pawn)
        {
            caravan.AddPawn(pawn, addCarriedPawnToWorldPawnsIfAny: true);
            return;
        }

        Pawn inventoryPawn = CaravanInventoryUtility.FindPawnToMoveInventoryTo(
            tradedThing,
            caravan.PawnsListForReading,
            null);

        if (inventoryPawn == null)
        {
            Log.Error($"Could not find a caravan pawn to receive {tradedThing} from a family trader.");
            tradedThing.Destroy();
        }
        else if (!inventoryPawn.inventory.innerContainer.TryAdd(tradedThing))
        {
            Log.Error($"Could not add {tradedThing} to the caravan after a family trade.");
            tradedThing.Destroy();
        }
    }

    public static Command_Action CreateTradeCommand(
        Caravan caravan,
        Settlement settlement,
        HHToolsFactionPoliticalState politicalState,
        HHToolsCrimeBossState bossState)
    {
        HHToolsFamilyTrader familyTrader = new(settlement, politicalState, bossState);
        TraderKindDef traderKind = familyTrader.TraderKind;
        if (traderKind == null)
        {
            return null;
        }

        Pawn bestNegotiator = BestCaravanPawnUtility.FindBestNegotiator(
            caravan,
            settlement.Faction,
            traderKind);

        Command_Action command = new()
        {
            defaultLabel = $"Trade: {HHToolsFactionPoliticsUtility.GetFamilyLabel(bossState.boss)}",
            defaultDesc =
                $"Trade {HHToolsFactionPoliticsUtility.GetFamilyTradeCategory(bossState.boss)} with this family."
                + GetFavorTradeDescription(politicalState, bossState),
            icon = ContentFinder<Texture2D>.Get("UI/Commands/Trade"),
            action = () => Find.WindowStack.Add(new Dialog_Trade(bestNegotiator, familyTrader))
        };

        if (bestNegotiator == null)
        {
            command.Disable("CommandTradeFailNoNegotiator".Translate());
        }
        else if (bestNegotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
        {
            command.Disable("CommandTradeFailSocialDisabled".Translate());
        }

        return command;
    }

    public static void DestroyStock(HHToolsCrimeBossState bossState)
    {
        if (bossState?.tradeStock == null)
        {
            return;
        }

        for (int index = bossState.tradeStock.Count - 1; index >= 0; index -= 1)
        {
            Thing thing = bossState.tradeStock[index];
            bossState.tradeStock.Remove(thing);
            if (thing is not Pawn && !thing.Destroyed)
            {
                thing.Destroy();
            }
        }

        bossState.lastTradeStockGenerationTick = -1;
        bossState.tradeStockTraderKindDefName = null;
    }

    private static string GetFavorTradeDescription(
        HHToolsFactionPoliticalState politicalState,
        HHToolsCrimeBossState bossState)
    {
        if (!bossState.favorGranted)
        {
            return $"\n\nFavor: {bossState.completedMissions}/{HHToolsFactionPoliticsUtility.FavorsRequiredPerFamily}."
                + " Full favor unlocks 10% better prices.";
        }

        bool soleFamily = politicalState.HasSoleFavoredFamily
            && politicalState.authoritarianController == bossState.boss;
        return soleFamily
            ? "\n\nAbsolute favor: 30% better prices and an expanded specialist stock."
            : "\n\nFavor secured: 10% better prices.";
    }

    private void EnsureStock()
    {
        TraderKindDef traderKind = TraderKind;
        if (traderKind == null)
        {
            return;
        }

        int currentTick = Find.TickManager?.TicksGame ?? 0;
        bool traderKindChanged = bossState.tradeStockTraderKindDefName != traderKind.defName;
        bool refreshDue = bossState.lastTradeStockGenerationTick < 0
            || currentTick - bossState.lastTradeStockGenerationTick > FamilyStockRefreshTicks;

        if (!traderKindChanged
            && !refreshDue
            && bossState.tradeStock is { Count: > 0 })
        {
            return;
        }

        DestroyStock(bossState);
        bossState.tradeStock ??= new ThingOwner<Thing>(bossState);

        ThingSetMakerParams parameters = new()
        {
            traderDef = traderKind,
            tile = settlement.Tile,
            makingFaction = settlement.Faction
        };

        bossState.tradeStock.TryAddRangeOrTransfer(
            ThingSetMakerDefOf.TraderStock.root.Generate(parameters));

        foreach (Pawn pawn in bossState.tradeStock.InnerListForReading.OfType<Pawn>())
        {
            Find.WorldPawns.PassToWorld(pawn);
        }

        bossState.lastTradeStockGenerationTick = currentTick;
        bossState.tradeStockTraderKindDefName = traderKind.defName;
    }
}

public class StockGenerator_HHToolsSlaves : StockGenerator
{
    public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
    {
        PawnKindDef slaveKind = DefDatabase<PawnKindDef>.GetNamedSilentFail("Slave")
            ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("HHTools_Settlement_Civilian");
        if (slaveKind == null)
        {
            yield break;
        }

        List<Faction> origins = Find.FactionManager.AllFactionsVisible
            .Where(candidate =>
                candidate != Faction.OfPlayer
                && candidate != faction
                && candidate.def.humanlikeFaction
                && !candidate.temporary)
            .ToList();

        for (int index = 0; index < countRange.RandomInRange; index += 1)
        {
            if (!origins.TryRandomElement(out Faction origin))
            {
                yield break;
            }

            PawnGenerationRequest request = new(
                slaveKind,
                origin,
                PawnGenerationContext.NonPlayer,
                forTile);
            yield return PawnGenerator.GeneratePawn(request);
        }
    }

    public override bool HandlesThingDef(ThingDef thingDef)
    {
        return thingDef.category == ThingCategory.Pawn
            && thingDef.race.Humanlike
            && thingDef.tradeability != Tradeability.None;
    }
}
