using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverVendorBatchingTests
    {
        // --- Mixed-currency vendor offer tests ---
        // Offers with non-coin currency lines must never win a coin-cost
        // comparison (their coin part alone is not their real price); they may
        // only be used when no coin-priceable option exists.

        [Fact]
        public void MixedCurrencyVendor_DoesNotBeatTpPrice()
        {
            // Regression: a karma-priced offer used to be rated by its coin part
            // (here 0) and always beat any TP price.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void MixedCurrencyVendor_DoesNotBeatPriceableCraft()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 50 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 10, 2, 50) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            // Craft (2 x 50 = 100 coin) wins over the incomparable mixed offer.
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void MixedCurrencyVendor_ZeroFilledCraft_BeatsFallbackVendor()
        {
            // M33 partial-pricing parity (superseded
            // "MixedCurrencyVendor_FallbackForUnpriceableCraftNode"): item
            // 1 has no TP price, an unpriceable-and-unrecipeable ingredient
            // (so its craft cost is zero-filled per the new rule, not
            // disqualified), and a fallback-only mixed vendor offer (25
            // coin + 50 unvalued currency). With no buy price at all, craft
            // (0, force-craftable) beats the fallback vendor outright -
            // craft is chosen over a real, priced vendor offer specifically
            // BECAUSE the craft total is an artificially cheap partial
            // total. This is intentional (gw2e's own behavior), not a
            // regression - see M33 spec item 2d.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 25, 2, 50) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var craftStep = plan.Steps.Single(s => s.ItemId == 1);
            Assert.Equal(AcquisitionSource.Craft, craftStep.Source);
            Assert.Equal(0, craftStep.TotalCost);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts); // the losing vendor offer never commits

            var unknownStep = plan.Steps.Single(s => s.ItemId == 2);
            Assert.Equal(AcquisitionSource.UnknownSource, unknownStep.Source);
        }

        [Fact]
        public void MixedVendorOffers_FallbackPicksLowerCoinPart()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        MixedVendorOffer(1, 100, 2, 50),
                        MixedVendorOffer(1, 50, 2, 500)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Equal(50, plan.TotalCoinCost);
            Assert.Equal(500, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void MixedVendorOffers_CoinTie_FewerCurrencyUnitsWins()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        MixedVendorOffer(1, 100, 2, 90),
                        MixedVendorOffer(1, 100, 2, 40)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Equal(40, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void MixedVendorOffers_CoinTie_DifferentCurrencies_FirstOfferKept()
        {
            // 500 units of currency 2 vs 20 units of currency 3 must NOT be
            // compared - unit counts of different currencies have no exchange
            // rate. On a coin-part tie across currencies the first-listed
            // offer wins deterministically.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        MixedVendorOffer(1, 0, 2, 500),
                        MixedVendorOffer(1, 0, 3, 20)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(500, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void MixedVendorOffer_ScaledCurrencyOverflowsInt_OfferSkippedNotCrash()
        {
            // 350,000 currency per unit x 10,000 units needed exceeds
            // int.MaxValue; the offer must be skipped gracefully, not abort
            // the whole solve with an OverflowException.
            var tree = Leaf(1, 10000);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 350000) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.UnknownSource, plan.Steps[0].Source);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void MixedOfferPresent_PureCoinOfferStillComparable()
        {
            // TP 150 vs pure-coin vendor 200 vs mixed offer with coin part 10:
            // the mixed offer must not hijack the comparison; TP wins.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 150 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        CoinVendorOffer(1, 200),
                        MixedVendorOffer(1, 10, 2, 50)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(150, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        // --- Aggregate-before-ceil tests (M34-B1 #1) ---
        // gw2efficiency merges same-id demand across the WHOLE tree first,
        // then ceils the purchase count exactly once (docs/gw2e-parity-spec.md
        // Section 6.5). Evaluating/ceiling per tree occurrence and only
        // summing afterward (the pre-fix shape) overstates the true cost for
        // any item needed via 2+ occurrences and bought via a bulk
        // (OutputCount > 1) offer.

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_CurrencyCost_AggregatesBeforeCeiling()
        {
            // Live repro (m34-m2-live-oddities.md): item 99 needed via 5
            // separate tree occurrences (qty 4, 4, 4, 83, 84 = 179 total),
            // all resolving to the same fallback-tier "3 units of item 99
            // for 3 units of currency 5" offer (no TP price, no recipe,
            // unvalued currency - exactly Obsidian Shard's real
            // 3-for-3-Laurels shape). Per-occurrence ceiling would charge
            // ceil(4/3)*3 x3 + ceil(83/3)*3 + ceil(84/3)*3 = 6+6+6+84+84 =
            // 186; merging demand first and ceiling once gives
            // ceil(179/3)*3 = 180 - not 186.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 83),
                    Leaf(99, 84)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { MixedVendorOffer(99, 0, 5, 3, outputCount: 3) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorStep.Source);
            Assert.Equal(179, vendorStep.Quantity);
            Assert.Equal(0, vendorStep.TotalCost);

            var currencyCost = Assert.Single(plan.CurrencyCosts, c => c.CurrencyId == 5);
            Assert.Equal(180, currencyCost.Amount);
        }

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_CoinCost_AggregatesBeforeCeiling()
        {
            // Sibling to the currency case above (M34-B1 #1's class-level
            // scope note): the identical bug shape applies to a bulk offer
            // priced in COIN, not just non-coin currency. Same 179-unit
            // demand, same 3-for-3 batch shape, coin instead of currency:
            // ceil(179/3)*3 = 180, not the per-occurrence sum of 186.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 83),
                    Leaf(99, 84)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 3, outputCount: 3) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorStep.Source);
            Assert.Equal(179, vendorStep.Quantity);
            Assert.Equal(180, vendorStep.TotalCost);
            Assert.Equal(180, plan.TotalCoinCost);

            // Critical review finding (PlanSolver.cs:1038): the root Craft
            // decision's own TotalCost - what CraftingTreeNode.SubtreeCost
            // shows for the Recipe Tree's root row - must agree with the
            // Total Cost summary above, not keep the stale per-occurrence
            // sum of 186 that FinalizeVendorBatches alone (which only fixes
            // the merged PlanStep/currencyMap view) left behind.
            Assert.Equal(180, result.Decisions[tree.NodeId].TotalCost);
        }

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_NoCurrencyValued_ComparisonValueMatchesTotalCostEverywhere()
        {
            // currency-ux-package review fix (finding 1, MEASURED): same
            // shape as MultiOccurrenceBulkVendorOffer_CoinCost_
            // AggregatesBeforeCeiling above (5 occurrences of item 99
            // totalling 179 units, one 3-for-3 coin-only vendor offer, no
            // currency anywhere in the plan) - the exact reproducer that
            // used to yield rootTotalCost=180 / rootComparisonValue=186
            // and a fabricated "Currencies: 0g 0s 6c" ValueDetailTooltipBuilder
            // line on a purely coin-priced plan. AllocateVendorNodeCosts
            // corrects each merged leaf's TotalCost but (by design,
            // DO-NOT-TOUCH: VendorBatchSolver's own merged-ceil batching
            // math) never touches ComparisonValue itself; PlanSolver's own
            // vendorComparisonDeltas/RecomputeComparisonValues passes must
            // keep the two in lockstep instead. Asserts ComparisonValue ==
            // TotalCost on every merged leaf AND the Craft root above it -
            // the leaf-level correction and the Craft-ancestor rollup are
            // two independently fixable bugs, both covered here.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 4),
                    Leaf(99, 83),
                    Leaf(99, 84)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 3, outputCount: 3) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var rootDecision = result.Decisions[tree.NodeId];

            Assert.Equal(180, rootDecision.TotalCost);
            Assert.Equal(rootDecision.TotalCost, rootDecision.ComparisonValue);

            foreach (var leaf in tree.Recipes[0].Ingredients)
            {
                var leafDecision = result.Decisions[leaf.NodeId];
                Assert.Equal(AcquisitionSource.BuyFromVendor, leafDecision.Source);
                Assert.Equal(leafDecision.TotalCost, leafDecision.ComparisonValue);
            }
        }

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_CoinUnitCost_UsesOfferRate_NotAggregateAverage()
        {
            // MustFix review finding (PlanSolver.cs:1062): the coin "Each"
            // cell (PlanStep.UnitCost) must show the winning offer's own
            // true per-unit rate (CoinCostPerBatch / OutputCount), not a
            // truncating average of the corrected aggregate TotalCost over
            // aggregate Quantity. A "2 for 5" offer merged to demand 3 needs
            // 2 batches (TotalCost = 10); the old average (10/3 = 3,
            // truncated) implied a per-unit price this offer never actually
            // charges - the true rate is 5/2 = 2.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 1),
                    Leaf(99, 2)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 5, outputCount: 2) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorStep.Source);
            Assert.Equal(3, vendorStep.Quantity);
            Assert.Equal(10, vendorStep.TotalCost);
            Assert.Equal(2, vendorStep.UnitCost);
            Assert.Equal(2, vendorStep.VendorOfferOutputCount);

            // The root Craft decision's TotalCost must also reflect the
            // corrected leaf allocations (5 + 5 = 10), same reconciliation
            // as the sibling test above.
            Assert.Equal(10, result.Decisions[tree.NodeId].TotalCost);
        }

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_CorrectionPropagatesThroughTwoCraftLevels()
        {
            // Critical review finding, deeper repro: the same 4/4/4/83/84
            // demand for the vendor-bought leaf (99), but split across TWO
            // separately-crafted intermediate items (2 and 3), each itself
            // an ingredient of the root craft - a 3-level-deep tree
            // (root -&gt; {item2, item3} -&gt; leaf99). RecomputeCraftCosts must
            // re-sum EVERY Craft ancestor bottom-up, not just a single
            // level, for the root's TotalCost to reach the corrected 180
            // rather than stopping at an intermediate level's stale value.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 1,
                        Option(20, 1, 1,
                            Leaf(99, 4),
                            Leaf(99, 4))),
                    Craftable(3, 1,
                        Option(30, 1, 1,
                            Leaf(99, 4),
                            Leaf(99, 83),
                            Leaf(99, 84)))));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 3, outputCount: 3) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(179, vendorStep.Quantity);
            Assert.Equal(180, vendorStep.TotalCost);
            Assert.Equal(180, plan.TotalCoinCost);
            Assert.Equal(180, result.Decisions[tree.NodeId].TotalCost);
        }

        [Fact]
        public void MultiOccurrenceBulkVendorOffer_CorrectionPropagatesThroughFourCraftLevelsAndBranches()
        {
            // Wave-validator regression: the same 4/4/4/83/84 = 179 demand
            // for the vendor-bought leaf (99) as the two-level sibling test
            // above, but now spread across FOUR Craft levels on one branch
            // AND multiple sibling branches at different depths - the exact
            // shape (root -> Exitare-like intermediate -> ... -> vendor
            // leaf, several levels deep, several branches merging into the
            // same vendor item) that hid the real gap: NOT a depth bound in
            // RecomputeCraftCosts/AllocateVendorNodeCosts (both already
            // walk the whole chosen-path tree and were verified correct at
            // this depth), but Collect()'s Craft-type PlanStep totals,
            // snapshotted BEFORE those correction passes ever run - see
            // PlanSolver.RefreshCraftStepCosts.
            //
            // Tree shape:
            //   root(1) -[recipe 10]-> craftA(2), craftD(5), craftE(6)
            //   craftA(2) -[recipe 20]-> craftB(3)
            //   craftB(3) -[recipe 30]-> craftC(4)
            //   craftC(4) -[recipe 40]-> leaf99 x3 occurrences @ qty 4 each
            //   craftD(5) -[recipe 50]-> leaf99 @ qty 83
            //   craftE(6) -[recipe 60]-> leaf99 @ qty 84
            //
            // A "3 for 3" vendor offer merges all five leaf99 occurrences
            // tree-wide: naive per-occurrence sum would be
            // 3*ceil(4/3)*3 + ceil(83/3)*3 + ceil(84/3)*3 = 18 + 84 + 84 = 186;
            // the corrected, ceil-once-on-aggregate-demand total is
            // ceil(179/3)*3 = 180 (matching the real Exordium 179 -> 180,
            // not 186, live repro this whole correction chain exists for).
            var craftC = Craftable(4, 1,
                Option(40, 1, 1, Leaf(99, 4), Leaf(99, 4), Leaf(99, 4)));
            var craftB = Craftable(3, 1, Option(30, 1, 1, craftC));
            var craftA = Craftable(2, 1, Option(20, 1, 1, craftB));
            var craftD = Craftable(5, 1, Option(50, 1, 1, Leaf(99, 83)));
            var craftE = Craftable(6, 1, Option(60, 1, 1, Leaf(99, 84)));
            var tree = Craftable(1, 1, Option(10, 1, 1, craftA, craftD, craftE));

            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 99, new List<VendorOffer> { CoinVendorOffer(99, 3, outputCount: 3) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(179, vendorStep.Quantity);
            Assert.Equal(180, vendorStep.TotalCost);
            Assert.Equal(180, plan.TotalCoinCost);

            // Decisions/Recipe-Tree side (memo, via RecomputeCraftCosts):
            // must reconcile bottom-up through all FOUR Craft levels on the
            // deep branch (craftC directly above the merged leaf, then
            // craftB, craftA, then root two levels further up), not just
            // the two the pre-existing sibling test covered.
            Assert.Equal(12, result.Decisions[craftC.NodeId].TotalCost);
            Assert.Equal(12, result.Decisions[craftB.NodeId].TotalCost);
            Assert.Equal(12, result.Decisions[craftA.NodeId].TotalCost);
            Assert.Equal(83, result.Decisions[craftD.NodeId].TotalCost);
            // craftE's leaf occurrence is last in DFS order, so
            // AllocateVendorNodeCosts' remainder-absorption lands its
            // corrected share here (180 - 12 - 83 = 85) rather than the
            // naively-corrected-in-isolation 84 - see
            // AllocateVendorNodeCosts' doc comment.
            Assert.Equal(85, result.Decisions[craftE.NodeId].TotalCost);
            Assert.Equal(180, result.Decisions[tree.NodeId].TotalCost);

            // Crafting Steps (shopping list) side: every Craft-type
            // PlanStep must show the SAME corrected totals as the
            // Decisions/tree side above - this is the half of the
            // correction fcbb277 left unfixed (RefreshCraftStepCosts).
            Assert.Equal(12, Assert.Single(plan.Steps, s => s.ItemId == 4).TotalCost);
            Assert.Equal(12, Assert.Single(plan.Steps, s => s.ItemId == 3).TotalCost);
            Assert.Equal(12, Assert.Single(plan.Steps, s => s.ItemId == 2).TotalCost);
            Assert.Equal(83, Assert.Single(plan.Steps, s => s.ItemId == 5).TotalCost);
            Assert.Equal(85, Assert.Single(plan.Steps, s => s.ItemId == 6).TotalCost);
            Assert.Equal(180, Assert.Single(plan.Steps, s => s.ItemId == 1).TotalCost);
        }

        [Fact]
        public void MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged()
        {
            // Two tree occurrences of the same item can, at their own local
            // quantity, legitimately prefer DIFFERENT vendor offers (a bulk
            // discount threshold effect: a small purchase favors a 1-for-2
            // offer, a large one favors a 100-for-150 offer). There is no
            // single "true" offer to merge these under, so the per-occurrence
            // sum (each individually correct) must be left alone rather than
            // forced through a single ceil - the Conflict ratchet in
            // PlanSolver.AggregateStep/FinalizeVendorBatches exists for
            // exactly this case.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(99, 1),
                    Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1),
                        CoinVendorOffer(99, 150, outputCount: 100)
                    }
                }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorStep.Source);
            Assert.Equal(101, vendorStep.Quantity);
            // qty=1 picks the 1-for-2 offer (2 coin); qty=100 picks the
            // 100-for-150 offer (150 coin) - two genuinely different real
            // purchases, correctly left summed (2 + 150 = 152) rather than
            // merged under either offer's own batch shape.
            Assert.Equal(152, vendorStep.TotalCost);
            Assert.Equal(152, plan.TotalCoinCost);
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);

            // Conflict case regression guard: AllocateVendorNodeCosts must
            // NOT redistribute a blended rate across occurrences that
            // genuinely used different offers - each occurrence's own memo
            // TotalCost (and therefore the root Craft decision's summed
            // TotalCost) must stay exactly the individually-correct 152.
            Assert.Equal(152, result.Decisions[tree.NodeId].TotalCost);
        }
    }
}
