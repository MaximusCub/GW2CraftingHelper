using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// (redesign,
    /// docs/gw2e-considerations.md): pure unit coverage of
    /// PillSubduingEvaluator, independent of PlanSolver/DecisionPillPlanner
    /// - see PlanSolverPillSubduingTests for real Solve()-path coverage of
    /// the breakdowns this class consumes, and
    /// DecisionPillPlannerSubduingTests for the pill-mapping consumer.
    /// </summary>
    public class PillSubduingEvaluatorTests
    {
        private static PillSourceCostBreakdown Available(
            long rawCoin = 0, long? decisionValue = null, params CostLine[] lines)
        {
            return new PillSourceCostBreakdown
            {
                IsAvailable = true,
                RawCoin = rawCoin,
                DecisionValue = decisionValue,
                CostLines = new List<CostLine>(lines),
            };
        }

        private static readonly PillSourceCostBreakdown Unavailable =
            new PillSourceCostBreakdown { IsAvailable = false };

        private static CostLine Item(int id, int count) => new CostLine { Type = "Item", Id = id, Count = count };

        private static CostLine Currency(int id, int count) => new CostLine { Type = "Currency", Id = id, Count = count };

        [Fact]
        public void EitherSideNull_None()
        {
            var a = Available(100, 100);
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(null, a).Rule);
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(a, null).Rule);
        }

        [Fact]
        public void EitherSideUnavailable_None()
        {
            var a = Available(100, 100);
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(Unavailable, a).Rule);
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(a, Unavailable).Rule);
        }

        // --- Weighted ---
        [Fact]
        public void Weighted_BothValued_LosingStrictlyMoreExpensive_Subdued()
        {
            // NOT a strict domination (losing needs LESS raw coin than
            // selected - a genuine tradeoff, priced out via a costly
            // currency valuation instead) - isolates the Weighted path
            // from StrictDomination, which is checked first and would
            // otherwise win on RawCoin alone if both sides were plain coin.
            var selected = Available(rawCoin: 500, decisionValue: 500);
            var losing = Available(rawCoin: 200, decisionValue: 800, Currency(23, 100));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
            Assert.Equal(300, result.ValueMarginCopper);
            Assert.Null(result.Deltas);
            Assert.True(result.HasNonCoinCost);
        }

        [Fact]
        public void Weighted_PureCoinBothSides_HasNonCoinCostFalse()
        {
            // TP selected at 500c, CRAFT
            // losing with DecisionValue 800c and no Currency/Item cost
            // line on either side (plain gold difference) -
            // StrictDomination cannot fire (losing's RawCoin, 0, is LOWER
            // than selected's, 500 - a genuine tradeoff priced via a
            // Currency ingredient elsewhere in the craft recipe, not shown
            // here as a CostLine since it is folded straight into
            // DecisionValue by the caller), so Weighted fires. The
            // resulting HasNonCoinCost must be false so the tooltip never
            // blames "your current currency values" for a pure-coin gap.
            var selected = Available(rawCoin: 500, decisionValue: 500);
            var losing = Available(rawCoin: 0, decisionValue: 800);

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
            Assert.Equal(300, result.ValueMarginCopper);
            Assert.False(result.HasNonCoinCost);
        }

        [Fact]
        public void Weighted_ItemLinesOnlyNoCurrencyLine_HasNonCoinCostFalse()
        {
            // Regression: BuildCraftCostBreakdown
            // emits an "Item" CostLine for EVERY Item ingredient regardless
            // of valuation (TP-priced, never user-valued) - so a craft
            // breakdown very commonly has non-empty CostLines with no
            // Currency line at all. The round-1 fix (any non-empty
            // CostLines) mis-fired here; only a Type == "Currency" line
            // (the only kind a CurrencyValuation can price) should count.
            var selected = Available(rawCoin: 400, decisionValue: 400);
            var losing = Available(rawCoin: 0, decisionValue: 500, Item(100, 5));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
            Assert.Equal(100, result.ValueMarginCopper);
            Assert.False(result.HasNonCoinCost);
        }

        [Fact]
        public void Weighted_CurrencyLinePresent_HasNonCoinCostTrue()
        {
            // Contrast case: a genuine Type == "Currency" line (the only
            // kind a CurrencyValuation prices) must still set the flag,
            // even alongside an Item line that does not count.
            var selected = Available(rawCoin: 400, decisionValue: 400);
            var losing = Available(rawCoin: 0, decisionValue: 500, Item(100, 5), Currency(23, 10));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
            Assert.True(result.HasNonCoinCost);
        }

        [Fact]
        public void Weighted_OneCopperMarginOnMultiGoldPurchase_NotDecisive_NotSubdued()
        {
            // Regression: the exact reported
            // shape - TP selected at 400c, craft losing at 401c (a
            // genuine 1-copper margin, clears neither the absolute nor
            // the relative floor on a value this size) - must stay None,
            // not render the losing pill subdued/muted over a margin no
            // reasonable person would call "decisive".
            // losing needs LESS raw coin than selected (a genuine tradeoff,
            // priced higher via a valued currency ingredient elsewhere -
            // not modeled as a CostLine here, same precedent
            // Weighted_BothValued_LosingStrictlyMoreExpensive_Subdued
            // above already establishes) so StrictDomination cannot fire
            // first and mask the margin gate this test targets.
            var selected = Available(rawCoin: 400, decisionValue: 400);
            var losing = Available(rawCoin: 0, decisionValue: 401);

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void Weighted_MarginClearsAbsoluteButNotRelativeFloor_NotSubdued()
        {
            // 101c margin on a 100000c (10g) purchase clears the 100c
            // absolute floor but is only 0.101% - well under the 1%
            // relative floor. Both floors must clear (AND, not OR).
            var selected = Available(rawCoin: 100000, decisionValue: 100000);
            var losing = Available(rawCoin: 0, decisionValue: 100101);

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void Weighted_MarginClearsRelativeButNotAbsoluteFloor_NotSubdued()
        {
            // 5c margin on a 50c purchase is a full 10% relative jump, but
            // still under the 100c absolute floor - both floors must
            // clear, so this stays None too (prevents a trivially cheap
            // item's tiny copper difference from reading as "decisive"
            // purely because it is a large percentage of a tiny number).
            var selected = Available(rawCoin: 50, decisionValue: 50);
            var losing = Available(rawCoin: 0, decisionValue: 55);

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void Weighted_MarginClearsBothFloors_Subdued()
        {
            var selected = Available(rawCoin: 10000, decisionValue: 10000);
            var losing = Available(rawCoin: 0, decisionValue: 10200); // +200c, 2%

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
            Assert.Equal(200, result.ValueMarginCopper);
        }

        [Fact]
        public void Weighted_SelectedValueZero_AnyPositiveMarginClearingAbsoluteFloorIsDecisive()
        {
            // A free/fully-owned selected source (DecisionValue 0) makes
            // any relative-percentage floor divide-by-zero/meaningless -
            // the absolute floor alone governs here. losing.RawCoin stays
            // equal to selected's (both 0, not negative) with no CostLines
            // on either side, so StrictDomination correctly finds nothing
            // to compare and falls through to Weighted.
            var selected = Available(rawCoin: 0, decisionValue: 0);
            var losing = Available(rawCoin: 0, decisionValue: 150);

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.Weighted, result.Rule);
        }

        [Fact]
        public void Weighted_ExactTie_NotSubdued()
        {
            var selected = Available(500, 500);
            var losing = Available(500, 500);

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void Weighted_LosingCheaper_NotSubdued()
        {
            // Defensive: the "losing" side should never actually be
            // cheaper in real usage (it lost PickCheapest), but the
            // evaluator must not flag it if it somehow were.
            var selected = Available(500, 500);
            var losing = Available(300, 300);

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void Weighted_OneSideUnvalued_None()
        {
            // Same non-dominated shape as the Weighted-detection test above
            // (losing needs less raw coin, more of a currency) so only the
            // Weighted path is in play - then unvalued on one side must
            // suppress it entirely.
            var selected = Available(rawCoin: 500, decisionValue: 500);
            var losing = Available(rawCoin: 200, decisionValue: null, Currency(23, 100)); // fallback-tier, unvalued
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);

            var selected2 = Available(rawCoin: 500, decisionValue: null);
            var losing2 = Available(rawCoin: 200, decisionValue: 800, Currency(23, 100));
            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected2, losing2).Rule);
        }

        // --- Strict domination ---
        [Fact]
        public void StrictDomination_AmalgamatedRiftEssenceShape_SameCoinTenMoreEcto()
        {
            // The maintainer's own canonical example: same currencies
            // (here, no coin either side - the "same" part), vendor needs
            // 10 more raw Globs of Ectoplasm (item id 100) than crafting
            // does. Needs no valuation at all - both breakdowns pass
            // decisionValue: null.
            var craft = Available(rawCoin: 0, decisionValue: null, Item(100, 5));
            var vendor = Available(rawCoin: 0, decisionValue: null, Item(100, 15));

            var result = PillSubduingEvaluator.Evaluate(craft, vendor);

            Assert.Equal(PillSubduingRule.StrictDomination, result.Rule);
            Assert.Null(result.ValueMarginCopper);
            Assert.Single(result.Deltas);
            Assert.Equal("Item", result.Deltas[0].Kind);
            Assert.Equal(100, result.Deltas[0].Id);
            Assert.Equal(10, result.Deltas[0].Amount);
        }

        [Fact]
        public void StrictDomination_TakesPriorityOverWeighted_WhenBothWouldApply()
        {
            var selected = Available(rawCoin: 0, decisionValue: 500, Item(100, 5));
            var losing = Available(rawCoin: 0, decisionValue: 800, Item(100, 15));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.StrictDomination, result.Rule);
        }

        [Fact]
        public void StrictDomination_ExtraCoinAlsoDominates()
        {
            var selected = Available(rawCoin: 100);
            var losing = Available(rawCoin: 250);

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.StrictDomination, result.Rule);
            Assert.Single(result.Deltas);
            Assert.Equal("Coin", result.Deltas[0].Kind);
            Assert.Equal(150, result.Deltas[0].Amount);
        }

        [Fact]
        public void StrictDomination_MixedTradeoff_LosingCheaperOnOneKind_NotDominated()
        {
            // Losing needs less Iron Ore (kind 200) than selected, even
            // though it needs more Ecto - a genuine tradeoff, not a strict
            // domination ("always more expensive" would be false here).
            var selected = Available(rawCoin: 0, decisionValue: null, Item(100, 5), Item(200, 10));
            var losing = Available(rawCoin: 0, decisionValue: null, Item(100, 15), Item(200, 2));

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void StrictDomination_ExactTieOnEveryKind_NotDominated()
        {
            // >= everything is satisfied, but nothing is strictly greater -
            // domination requires at least one strict inequality.
            var selected = Available(rawCoin: 50, decisionValue: null, Item(100, 5));
            var losing = Available(rawCoin: 50, decisionValue: null, Item(100, 5));

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void StrictDomination_KindPresentOnlyOnLosingSide_TreatedAsZeroOnSelected()
        {
            // Selected needs no currency at all; losing needs some, on top
            // of otherwise-identical costs - still a valid domination
            // (selected's implicit 0 <= losing's amount).
            var selected = Available(rawCoin: 100);
            var losing = Available(rawCoin: 100, decisionValue: null, Currency(23, 50));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.StrictDomination, result.Rule);
            Assert.Single(result.Deltas);
            Assert.Equal("Currency", result.Deltas[0].Kind);
            Assert.Equal(23, result.Deltas[0].Id);
            Assert.Equal(50, result.Deltas[0].Amount);
        }

        [Fact]
        public void UnvaluedAndNonDominated_BothPillsStayNormal()
        {
            // Neither side valued (no DecisionValue), and a genuine
            // tradeoff (not a domination) - per the spec, both pills must
            // stay normal (None), not just "not subdued for one reason".
            var selected = Available(rawCoin: 0, decisionValue: null, Item(100, 5), Item(200, 10));
            var losing = Available(rawCoin: 0, decisionValue: null, Item(100, 15), Item(200, 2));

            Assert.Equal(PillSubduingRule.None, PillSubduingEvaluator.Evaluate(selected, losing).Rule);
        }

        [Fact]
        public void DuplicateCostLinesOfSameKind_AreSummedBeforeComparison()
        {
            var selected = Available(rawCoin: 0, decisionValue: null, Item(100, 2), Item(100, 3)); // 5 total
            var losing = Available(rawCoin: 0, decisionValue: null, Item(100, 15));

            var result = PillSubduingEvaluator.Evaluate(selected, losing);

            Assert.Equal(PillSubduingRule.StrictDomination, result.Rule);
            Assert.Equal(10, result.Deltas[0].Amount);
        }
    }
}
