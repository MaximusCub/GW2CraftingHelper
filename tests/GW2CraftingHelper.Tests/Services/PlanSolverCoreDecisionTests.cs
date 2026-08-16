using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;
using static GW2CraftingHelper.Tests.Helpers.VendorOfferBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverCoreDecisionTests
    {
        [Fact]
        public void LeafItem_HasTpPrice_ReturnsBuyFromTp()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);
            var plan = result.Plan;

            Assert.Single(plan.Steps);
            var step = plan.Steps[0];
            Assert.Equal(1, step.ItemId);
            Assert.Equal(5, step.Quantity);
            Assert.Equal(AcquisitionSource.BuyFromTp, step.Source);
            Assert.Equal(100, step.UnitCost);
            Assert.Equal(500, step.TotalCost);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void LeafItem_NoTpPrice_ReturnsUnknownSource()
        {
            var tree = Leaf(1, 3);
            var prices = new Dictionary<int, ItemPrice>();
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            Assert.Single(plan.Steps);
            var step = plan.Steps[0];
            Assert.Equal(AcquisitionSource.UnknownSource, step.Source);
            Assert.Equal(3, step.Quantity);
            Assert.Equal(0, step.TotalCost);
            Assert.Equal(0, plan.TotalCoinCost);
        }

        [Fact]
        public void CraftCheaperThanBuy_ChoosesCraft()
        {
            // Item 1: buy = 1000 each. Craft from 2x item 2 (100 each) = 200 total vs 1000 buy
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // Should have: Buy 2x item 2, then Craft 1x item 1
            Assert.Equal(2, plan.Steps.Count);

            var buyStep = plan.Steps.First(s => s.Source == AcquisitionSource.BuyFromTp);
            Assert.Equal(2, buyStep.ItemId);
            Assert.Equal(2, buyStep.Quantity);
            Assert.Equal(200, buyStep.TotalCost);

            var craftStep = plan.Steps.First(s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(1, craftStep.ItemId);
            Assert.Equal(10, craftStep.RecipeId);

            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void BuyCheaperThanCraft_ChoosesBuy()
        {
            // Item 1: buy = 100 each. Craft from 2x item 2 (200 each) = 400 total vs 100 buy
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 200 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // Should just buy item 1, no ingredient steps
            Assert.Single(plan.Steps);
            var step = plan.Steps[0];
            Assert.Equal(1, step.ItemId);
            Assert.Equal(AcquisitionSource.BuyFromTp, step.Source);
            Assert.Equal(100, step.TotalCost);
            Assert.Equal(100, plan.TotalCoinCost);
        }

        [Fact]
        public void MultipleRecipeOptions_PicksCheapest()
        {
            // Item 1 has two recipes:
            //   Recipe 10: 3x item 2 (100 each) = 300
            //   Recipe 11: 1x item 3 (50 each) = 50 (cheaper)
            // Buy item 1 = 500
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 3)),
                Option(11, 1, 1, Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            var craftStep = plan.Steps.First(s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(11, craftStep.RecipeId); // chose cheaper recipe
            Assert.Equal(50, plan.TotalCoinCost);
        }

        [Fact]
        public void MultiLevelTree_CorrectBottomUpOrdering()
        {
            // A(1) -> craft from B(2) -> craft from C(3, leaf)
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 1,
                        Option(20, 1, 1,
                            Leaf(3, 2)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 5000 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 10 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // Buys first, then crafts bottom-up: craft B before craft A
            var craftSteps = plan.Steps.Where(s => s.Source == AcquisitionSource.Craft).ToList();
            Assert.Equal(2, craftSteps.Count);
            Assert.Equal(2, craftSteps[0].ItemId); // B crafted first
            Assert.Equal(1, craftSteps[1].ItemId); // A crafted second
        }

        [Fact]
        public void CurrencyIngredient_AppearsInCurrencyCostsNotSteps()
        {
            // Item 1: craft from 2x item 2 + 50x currency 99. Craft-
            // comparability-parity fix: an unvalued currency ingredient
            // makes this recipe fallback-tier (see
            // PlanSolverCraftVendorComparabilityTests), so item 1
            // intentionally has NO buy price here - nothing comparable
            // exists, so the fallback craft is used as the last resort and
            // this test's real subject (currency surfaces in CurrencyCosts,
            // never as a step; TotalCoinCost excludes it) still applies.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 2),
                    Leaf(99, 50, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // No Currency steps
            Assert.DoesNotContain(plan.Steps, s => s.Source == AcquisitionSource.Currency);
            // Currency in CurrencyCosts
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(99, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
            // TotalCoinCost excludes currency
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void QuantityPropagation_OutputCountGreaterThanOne()
        {
            // Need 3 of item 1. Recipe makes 2 per craft -> 2 crafts.
            // Each craft needs 4x item 2. Total: 2 * 4 = 8x item 2.
            var tree = Craftable(1, 3,
                Option(10, 2, 2,
                    Leaf(2, 8)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // Buy cost: 3 * 500 = 1500. Craft cost: 8 * 10 = 80. Craft wins.
            var buyStep = plan.Steps.First(s => s.Source == AcquisitionSource.BuyFromTp);
            Assert.Equal(2, buyStep.ItemId);
            Assert.Equal(8, buyStep.Quantity);
            Assert.Equal(80, buyStep.TotalCost);
            Assert.Equal(80, plan.TotalCoinCost);
        }

        [Fact]
        public void SameItemInMultipleBranches_DeduplicatedStep()
        {
            // Item 1 crafts from: 3x item 2 + 5x item 2 (same item, two ingredients - simulating
            // what happens when item 2 appears via two branches in a real tree)
            // Actually, let's make it: item 1 -> recipe with 2 ingredients that are both item 2
            // More realistically: item 1 -> (item 2, item 3), item 3 -> item 2
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 3),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 5)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            // Item 2 appears twice as BuyFromTp, should be deduplicated: 3 + 5 = 8
            var item2Steps = plan.Steps.Where(s => s.ItemId == 2).ToList();
            Assert.Single(item2Steps);
            Assert.Equal(8, item2Steps[0].Quantity);
            Assert.Equal(80, item2Steps[0].TotalCost);
            Assert.Equal(10, item2Steps[0].UnitCost);
        }

        [Fact]
        public void UnpriceableCraftIngredient_ZeroFilled_CraftWinsWithPartialCost()
        {
            // M33 partial-pricing parity (superseded
            // "UnpriceableCraftIngredients_BuyAvailable_FallsBackToBuy"):
            // an unpriceable-and-unrecipeable ingredient no longer
            // disqualifies the recipe - it contributes ZERO to the craft
            // cost instead (echoing gw2e's craftPrice = sum(component
            // .craftResultPrice || 0)). Item 1: buy = 500. Craft needs item
            // 2, which has no TP price and no recipe, so craft "costs" 0
            // and strictly beats the 500 buy price - Craft wins, and item
            // 2 still surfaces as its own UnknownSource node/step (the
            // partial total is intentional, not a display bug).
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } }
                // Item 2 has no price
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);
            var plan = result.Plan;

            Assert.Equal(2, plan.Steps.Count);
            var craftStep = plan.Steps.Single(s => s.ItemId == 1);
            Assert.Equal(AcquisitionSource.Craft, craftStep.Source);
            Assert.Equal(0, craftStep.TotalCost);

            var unknownStep = plan.Steps.Single(s => s.ItemId == 2);
            Assert.Equal(AcquisitionSource.UnknownSource, unknownStep.Source);

            // Finding-1 fix: item 2 still gets its own decision entry even
            // though it contributed nothing to item 1's craft cost.
            Assert.True(result.Decisions.ContainsKey(1)); // item 2 is NodeId 1 (DFS)
            Assert.Equal(AcquisitionSource.UnknownSource, result.Decisions[1].Source);
        }

        [Fact]
        public void SiblingIngredients_AfterUnpriceableFirstIngredient_AreStillEvaluated()
        {
            // M33 Finding 1 (m5 report): the ingredient loop used to `break`
            // on the first unpriceable ingredient, so every LATER sibling in
            // that same recipe never got evaluated at all - no memo entry,
            // indistinguishable from a genuine no-data gap. Recipe 10's
            // ingredients: item 2 (unpriceable, no recipe) first, then item
            // 3 and item 4 (both TP-priced) - both must still resolve to
            // BuyFromTp with a real decision entry, not just silently
            // vanish because item 2 came first.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1),
                    Leaf(3, 1),
                    Leaf(4, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                // Item 1 and item 2 intentionally have no price.
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 40 } },
                { 4, new ItemPrice { ItemId = 4, BuyInstant = 60 } }
            };
            var solver = new PlanSolver();

            // DFS NodeIds: root=0, item2=1, item3=2, item4=3.
            var result = solver.Solve(tree, prices);

            Assert.True(result.Decisions.ContainsKey(2), "item 3 (sibling after the unpriceable item 2) must have a decision entry");
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[2].Source);
            Assert.Equal(40, result.Decisions[2].TotalCost);

            Assert.True(result.Decisions.ContainsKey(3), "item 4 (sibling after the unpriceable item 2) must have a decision entry");
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[3].Source);
            Assert.Equal(60, result.Decisions[3].TotalCost);

            var plan = result.Plan;
            Assert.Contains(plan.Steps, s => s.ItemId == 3 && s.Source == AcquisitionSource.BuyFromTp);
            Assert.Contains(plan.Steps, s => s.ItemId == 4 && s.Source == AcquisitionSource.BuyFromTp);
        }

        [Fact]
        public void RecipeWithNoBuyPriceAtAll_AlwaysForceCrafts_NeverUnknown()
        {
            // M33 spec item 2a (gw2e: isCheaperToCraft = craftPrice-defined
            // && (!buyPrice || decisionPrice < buyPrice)): a node with a
            // recipe but NO buy price (no TP price, no comparable vendor
            // offer) is force-crafted - Craft, never Unknown - regardless
            // of the recipe's own priceability.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 3)));
            var prices = new Dictionary<int, ItemPrice>
            {
                // Item 1 has NO price at all; item 2 is normally priced.
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.True(result.Decisions[0].CanCraft);
            Assert.False(result.Decisions[0].CanBuyTp);
            Assert.Equal(30, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void NoRecipeAndNoPrice_IsUnknownSource_WithAllFlagsFalse()
        {
            // M33 spec item 2c (gw2e's "Not sold or crafted"): a node with
            // NO recipe and NO price gets UnknownSource with every
            // feasibility flag false - never silently defaults to Craft.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);

            Assert.Equal(AcquisitionSource.UnknownSource, result.Decisions[0].Source);
            Assert.False(result.Decisions[0].CanCraft);
            Assert.False(result.Decisions[0].CanBuyTp);
            Assert.False(result.Decisions[0].CanBuyVendor);
        }

        // --- Tie-break parity tests (M33 spec item 3) ---
        // gw2e: craft/vendor must be STRICTLY cheaper than buy to win; an
        // exact tie resolves to buy at every level ("Vendor beats TP beats
        // Craft" is superseded).

        [Fact]
        public void CraftCostTiesBuyPrice_BuyWins()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorCostTiesBuyPrice_BuyWins()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 200) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorAndCraftBothBeatBuy_VendorWinsTieBetweenThem()
        {
            // Both craft (200) and vendor (200) strictly beat buy (500);
            // an exact craft/vendor tie keeps vendor (this engine's
            // pre-existing precedent - not itself part of the gw2e spec,
            // which never separates vendor from craft as its own arm).
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 200) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        // --- Mystic Clover-style EV pricing tests (M33 spec item 7,
        // CORRECTED per the M33 fix-pass Critical finding: quantity
        // propagation - not a second cost adjustment inside PlanSolver -
        // is where ExpectedOutputCount now takes effect. RecipeService (and
        // InventoryReducer, when a snapshot is present) compute
        // CraftsNeeded = ceil(quantity / ExpectedOutputCount) and scale
        // every ingredient's Quantity by that many attempts BEFORE the tree
        // ever reaches PlanSolver. By the time Evaluate sees an EV recipe's
        // ingredients, their quantities already reflect the full expected
        // cost - PlanSolver must simply sum them, never amortize again.) ---

        [Fact]
        public void FractionalExpectedOutput_PreScaledIngredients_CraftCostReconcilesWithBuySteps()
        {
            // Simulates what RecipeService now produces for a Mystic
            // Clover-style recipe: needing 1 successful output at EV=0.5
            // means ceil(1/0.5)=2 forge attempts, so the (1-per-attempt)
            // ingredient already carries Quantity=2 by the time it reaches
            // PlanSolver - NOT Quantity=1 with a solver-side /0.5 fixup.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 2,
                Ingredients = new List<RecipeNode> { Leaf(2, 2) }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            var craftStep = plan.Steps.Single(s => s.Source == AcquisitionSource.Craft);
            var buyStep = plan.Steps.Single(s => s.Source == AcquisitionSource.BuyFromTp);

            // 2 units of item 2 @ 100 = 200 - PlanSolver must NOT divide
            // this by the EV ratio again (that would double-amortize to
            // 400). The Craft step's own TotalCost must reconcile EXACTLY
            // with the Buy step(s) it recursively spawns - the M33 Critical
            // finding's "two different coin figures for the same subtree"
            // bug is fixed when this holds.
            Assert.Equal(200, buyStep.TotalCost);
            Assert.Equal(200, craftStep.TotalCost);
            Assert.Equal(craftStep.TotalCost, plan.TotalCoinCost);
        }

        [Fact]
        public void FractionalExpectedOutput_PreScaledIngredients_StillLosesToCheaperBuy()
        {
            // Same pre-scaled tree (2 units of item 2 @ 100 = 200 real
            // cost), but the item's own buy price (150) is now cheaper than
            // the (correctly, non-amortized) craft cost - buy must win.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 2,
                Ingredients = new List<RecipeNode> { Leaf(2, 2) }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 150 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Equal(150, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void OrdinaryRecipe_ExpectedOutputDefaultsToOutputCount_NoOpOnPricing()
        {
            // A recipe that never sets ExpectedOutputCount (the common
            // case - only Mystic Clover-style recipes do) must price
            // identically to before this feature existed.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices).Plan;

            var craftStep = plan.Steps.Single(s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(200, craftStep.TotalCost);
        }

        // --- Currency-ingredient decision valuation (M33 fix-pass MustFix:
        // a recipe's Currency-type ingredient must feed the craft-vs-buy
        // DECISION value via a caller-supplied valuation, while always
        // contributing zero to the displayed real coin cost - r1 4.2/4.3) ---

        [Fact]
        public void CurrencyIngredient_ValuedAndExpensive_TipsDecisionToBuy()
        {
            // Craft option: 1x item2(@50)=50 real coin + 3x currency 23
            // (spirit shard) valued at 3600 copper/unit by the caller for
            // COMPARISON only = 10800. Comparison total 10850 loses to the
            // 200 buy price, even though the real coin ingredient (50)
            // looks cheap in isolation.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode>
                {
                    Leaf(2, 1),
                    Leaf(23, 3, "Currency")
                }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 50 } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 23, 3600 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void CurrencyIngredient_ValuedButCraftStillWins_RealCostExcludesCurrencyValue()
        {
            // Currency valuation (10 copper x 3 = 30) is small enough that
            // craft still wins overall, but the COMMITTED real coin cost
            // must be just the coin ingredient (50) - never inflated by the
            // currency's decision-only valuation.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode>
                {
                    Leaf(2, 1),
                    Leaf(23, 3, "Currency")
                }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 50 } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 23, 10 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, null, valuation).Plan;

            var craftStep = plan.Steps.Single(s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(50, craftStep.TotalCost);
        }

        [Fact]
        public void CurrencyIngredient_Unvalued_ComparableBuyWins_RegardlessOfFakeZeroCost()
        {
            // Craft-comparability-parity fix (supersedes the old
            // "CurrencyIngredient_Unvalued_ContributesZeroToDecisionAndCost"
            // name/assertion, which encoded the bug this fixes): No
            // CurrencyValuation supplied, so the currency ingredient is
            // unvalued - this now makes the recipe FALLBACK-tier (mirrors
            // VendorBatchSolver.EvaluateVendorOffers' comparable/fallback
            // split; see PlanSolverCraftVendorComparabilityTests for the
            // dedicated coverage). A fallback-tier craft must never beat a
            // real, comparable buy price, even though its own priced
            // ingredients alone (ignoring the unknown currency) look far
            // cheaper (50) than the 1000 buy price - that illusory
            // cheapness is exactly the hidden-cost bug this fix closes.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode>
                {
                    Leaf(2, 1),
                    Leaf(23, 3, "Currency")
                }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 50 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices);
            var plan = result.Plan;

            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(1000, plan.TotalCoinCost);
            // M33 guarantee preserved: the CRAFT pill still shows even
            // though the automatic decision picked buy instead.
            Assert.True(result.Decisions[0].CanCraft);
        }
    }
}
