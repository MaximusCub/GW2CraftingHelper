using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;
using static GW2CraftingHelper.Tests.Helpers.RecipeNodeBuilders;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverTests
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
            // Item 1: craft from 2x item 2 + 50x currency 99
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 2),
                    Leaf(99, 50, "Currency")));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10000 } },
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
        public void CurrencyIngredient_Unvalued_ContributesZeroToDecisionAndCost()
        {
            // No CurrencyValuation supplied (null -> CurrencyValuation.None
            // internally): the currency ingredient must be inert - same
            // behavior as before this fix, never inventing an exchange
            // rate. Craft (50 real coin, decision value 50) beats the 1000
            // buy price regardless of the unvalued 3-unit currency cost.
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

            var plan = solver.Solve(tree, prices).Plan;

            var craftStep = plan.Steps.Single(s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(50, craftStep.TotalCost);
        }

        // --- VendorCurrencyCosts threading tests (M33 spec item 5) ---

        [Fact]
        public void VendorCurrencyCosts_ThreadedOntoSolverDecisionAndPlanStep()
        {
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-currency-thread",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 10 },
                    new CostLine { Type = "Currency", Id = 23, Count = 50 }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(AcquisitionSource.BuyFromVendor, result.Decisions[0].Source);
            Assert.NotNull(result.Decisions[0].VendorCurrencyCosts);
            Assert.Single(result.Decisions[0].VendorCurrencyCosts);
            Assert.Equal(23, result.Decisions[0].VendorCurrencyCosts[0].Id);
            Assert.Equal(100, result.Decisions[0].VendorCurrencyCosts[0].Count); // 50/unit * qty 2

            var step = result.Plan.Steps.Single(s => s.ItemId == 1);
            Assert.NotNull(step.VendorCurrencyCosts);
            Assert.Single(step.VendorCurrencyCosts);
            Assert.Equal(23, step.VendorCurrencyCosts[0].Id);
            Assert.Equal(100, step.VendorCurrencyCosts[0].Count);
        }

        [Fact]
        public void VendorCurrencyCosts_MergedAcrossDeduplicatedOccurrences()
        {
            // Same vendor-sourced item reached via two tree branches must
            // sum its currency cost into the single aggregated PlanStep row,
            // not just the last-seen occurrence's amount.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 1)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-dedup-currency",
                OutputItemId = 2,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 23, Count = 10 }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var item2Steps = plan.Steps.Where(s => s.ItemId == 2).ToList();
            Assert.Single(item2Steps); // deduplicated into one row
            Assert.NotNull(item2Steps[0].VendorCurrencyCosts);
            Assert.Single(item2Steps[0].VendorCurrencyCosts);
            Assert.Equal(23, item2Steps[0].VendorCurrencyCosts[0].Id);
            Assert.Equal(20, item2Steps[0].VendorCurrencyCosts[0].Count); // 10 + 10 across both occurrences
        }

        [Fact]
        public void VendorCurrencyCosts_MergeOverflow_ClampsRatherThanWraps()
        {
            // Two occurrences of the same vendor-sourced item, each with a
            // currency count near int.MaxValue, sum past int.MaxValue -
            // must clamp, not silently wrap to a negative/garbage count.
            const int nearMax = 1_200_000_000;
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1),
                    Craftable(3, 1,
                        Option(20, 1, 1,
                            Leaf(2, 1)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 100000 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-overflow-currency",
                OutputItemId = 2,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 23, Count = nearMax }
                },
                MerchantName = "Miyani",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            var item2Step = plan.Steps.Single(s => s.ItemId == 2);
            Assert.Equal(int.MaxValue, item2Step.VendorCurrencyCosts[0].Count);
        }

        // --- Backward-compat regression tests ---

        [Fact]
        public void ExistingLeafBuyFromTp_WithNullVendorOffers_Unchanged()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, null).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
        }

        [Fact]
        public void ExistingCraftCheaper_WithEmptyVendorOffers_Unchanged()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>();
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Equal(2, plan.Steps.Count);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        // --- Vendor offer tests ---

        private static VendorOffer CoinVendorOffer(
            int outputItemId, int coinCost, int outputCount = 1, int? dailyCap = null, int? weeklyCap = null,
            int? seasonalCap = null)
        {
            return new VendorOffer
            {
                OfferId = $"test-{outputItemId}-{coinCost}",
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = coinCost }
                },
                MerchantName = "TestMerchant",
                Locations = new List<string> { "TestLoc" },
                DailyCap = dailyCap,
                WeeklyCap = weeklyCap,
                SeasonalCap = seasonalCap
            };
        }

        [Fact]
        public void VendorCheaperThanTpAndCraft_ChoosesVendor()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 400 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 200) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(200, plan.Steps[0].TotalCost);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorMoreExpensiveThanTp_ChoosesTp()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 500) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorWithCurrencyCost_TracksCurrencyInPlan()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-mixed",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 },
                    new CostLine { Type = "Currency", Id = 2, Count = 50 }
                },
                MerchantName = "Karma Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.Steps[0].TotalCost);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void VendorOnlyOption_NoTpNoCraft_ChoosesVendor()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 300) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(300, plan.TotalCoinCost);
        }

        [Fact]
        public void MultipleVendorOffers_PicksCheapest()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        CoinVendorOffer(1, 500),
                        CoinVendorOffer(1, 100)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorOfferWithItemCosts_PricesViaTP()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 42, new ItemPrice { ItemId = 42, BuyInstant = 10 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-item-cost",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = 42, Count = 5 }
                },
                MerchantName = "Barter Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            // Vendor cost = 5 * 10 = 50, TP buy = 200 -> vendor wins
            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
        }

        [Fact]
        public void VendorOfferWithOutputCountGreaterThanOne_ScalesCorrectly()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>();
            // Vendor sells 2 for 100 coin each batch -> need ceil(5/2)=3 batches = 300
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 100, outputCount: 2) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(300, plan.TotalCoinCost);
        }

        // --- Mixed-currency vendor offer tests ---
        // Offers with non-coin currency lines must never win a coin-cost
        // comparison (their coin part alone is not their real price); they may
        // only be used when no coin-priceable option exists.

        private static VendorOffer MixedVendorOffer(
            int outputItemId, int coinCost, int currencyId, int currencyCount, int outputCount = 1,
            int? dailyCap = null, int? weeklyCap = null, int? seasonalCap = null)
        {
            var costLines = new List<CostLine>();
            if (coinCost > 0)
            {
                costLines.Add(new CostLine
                {
                    Type = "Currency",
                    Id = Gw2Constants.CoinCurrencyId,
                    Count = coinCost
                });
            }
            costLines.Add(new CostLine { Type = "Currency", Id = currencyId, Count = currencyCount });

            return new VendorOffer
            {
                OfferId = "test-mixed-" + outputItemId + "-" + currencyId + "-" + currencyCount,
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = costLines,
                MerchantName = "Mixed Vendor",
                Locations = new List<string>(),
                DailyCap = dailyCap,
                WeeklyCap = weeklyCap,
                SeasonalCap = seasonalCap
            };
        }

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

        // --- Vendor purchase-cap tests ---
        // V2 semantics (M34-B1 #3, gw2efficiency parity): a DailyCap/WeeklyCap
        // NEVER excludes an offer or re-routes the solver to a different
        // source - gw2efficiency itself only ever surfaces a cap as a
        // post-solve "this is timegated" notice, never a tree change. A
        // cap-exceeding offer is still used exactly like an uncapped one;
        // the only observable effect is a CraftingPlan.TimegatedItems entry.

        [Fact]
        public void CappedOffer_NeededExceedsCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Vendor sells for 1 coin each but only 25/day; node needs 50,
            // exceeding one day's cap. The far cheaper vendor offer (50
            // coin) is still used over the expensive TP price (500 coin) -
            // caps never re-route the solver - and the plan surfaces a
            // timegated notice instead of silently falling back.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 25) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(25, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void CappedOffer_NeededWithinCap_StillUsedAsVendor()
        {
            // Needed (20) is within the cap (25); the far cheaper vendor
            // offer must still be picked over the expensive TP price, and
            // no timegated notice is raised since the cap is not exceeded.
            var tree = Leaf(1, 20);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, dailyCap: 25) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedBatchOffer_CapTimesOutputCountArithmetic()
        {
            // Offer sells batches of 10 with a cap of 3 purchases/day (max
            // 30 units/day). Needing 25 units requires only 3 purchases
            // (ceil(25/10)), which fits the cap even though 25 itself is far
            // greater than the raw DailyCap of 3 - proving OutputCount is
            // correctly folded into the cap check (no timegated notice)
            // rather than comparing the node's raw quantity against the cap.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, outputCount: 10, dailyCap: 3) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(15, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedBatchOffer_OneMoreUnitPushesPastCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Same batch/cap shape as above (10/batch, cap 3 => 30/day), but
            // needing 31 units requires 4 purchases (ceil(31/10)), exceeding
            // the cap. With no TP price and no recipe, the offer is still
            // the only (and therefore chosen) source - caps never exclude -
            // and the plan surfaces a timegated notice for it.
            var tree = Leaf(1, 31);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, outputCount: 10, dailyCap: 3) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(20, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(3, notice.CapValue);
            Assert.Equal(4, notice.NeededCount);
        }

        [Fact]
        public void ZeroCap_TreatedAsUncapped()
        {
            // An explicit DailyCap of 0 (not merely absent) must still mean
            // uncapped, not "zero purchases allowed" - no timegated notice.
            var tree = Leaf(1, 500);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 0) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void WeeklyCapUsed_WhenDailyCapAbsent()
        {
            // No DailyCap set; WeeklyCap of 25 cannot cover the 50 needed.
            // The offer is still used (far cheaper than TP) and surfaces a
            // Weekly-typed timegated notice.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 10 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, weeklyCap: 25) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(TimegatedCapType.Weekly, notice.CapType);
            Assert.Equal(25, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void DailyCapTakesPrecedenceOverWeeklyCap()
        {
            // DailyCap (100) alone covers the 50 needed, so no notice is
            // raised even though the WeeklyCap (1) alone would have been
            // exceeded - DailyCap wins whenever it is positive.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, dailyCap: 100, weeklyCap: 1) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededExceedsCap_StillUsedAsFallback_SurfacesTimegatedNotice()
        {
            // A mixed-currency offer only ever competes in the fallback
            // tier (its non-coin currency line is unvalued). With no TP
            // price and no recipe, it remains the only source even though
            // needing 50 exceeds the cap of 10 - caps never exclude - and a
            // timegated notice is raised for it.
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50, dailyCap: 10) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(2500, plan.CurrencyCosts[0].Amount);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Daily, notice.CapType);
            Assert.Equal(10, notice.CapValue);
            Assert.Equal(50, notice.NeededCount);
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededWithinCap_StillUsedAsFallback()
        {
            // Needed (5) is within the cap (10); the mixed-currency offer
            // remains the fallback acquisition (no TP price, no recipe), and
            // no timegated notice is raised.
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50, dailyCap: 10) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(250, plan.CurrencyCosts[0].Amount);
            Assert.Empty(plan.TimegatedItems);
        }

        // --- Seasonal (Astral Acclaim package, KNOWN-ISSUES #33) vendor
        // purchase-cap tests ---
        // Same warn-only semantics as Daily/Weekly above (a cap never gates
        // offer eligibility or re-routes the solver), but checked
        // INDEPENDENTLY of Daily/Weekly rather than folded into the same
        // "pick one" precedence - see the SeasonalAndWeeklyCap test below.

        [Fact]
        public void SeasonalCap_NeededExceedsCap_StillUsedAsVendor_SurfacesTimegatedNotice()
        {
            // Vendor sells for 1 coin each but only 20/season; node needs
            // 25, exceeding the season's cap. The far cheaper vendor offer
            // (25 coin) is still used over the expensive TP price (10000
            // coin) - caps never re-route the solver - and the plan
            // surfaces a Seasonal-typed timegated notice instead of
            // silently falling back.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 400 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, seasonalCap: 20) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(25, plan.TotalCoinCost);

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Seasonal, notice.CapType);
            Assert.Equal(20, notice.CapValue);
            Assert.Equal(25, notice.NeededCount);
        }

        [Fact]
        public void SeasonalCap_NeededWithinCap_StillUsedAsVendor_NoNotice()
        {
            // Needed (10) is within the season cap (20); the far cheaper
            // vendor offer must still be picked over the expensive TP
            // price, and no timegated notice is raised since the cap is
            // not exceeded.
            var tree = Leaf(1, 10);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, seasonalCap: 20) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalCapZero_TreatedAsUncapped()
        {
            // An explicit SeasonalCap of 0 (not merely absent) must still
            // mean uncapped, not "zero purchases allowed" - matching the
            // DailyCap/WeeklyCap zero-cap convention exactly.
            var tree = Leaf(1, 500);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, seasonalCap: 0) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalAndWeeklyCap_BothExceeded_BothNoticesReported()
        {
            // A single offer carrying BOTH a WeeklyCap and a SeasonalCap
            // must surface BOTH notices when both are exceeded - Seasonal
            // is checked independently of Daily/Weekly (a separate,
            // unrelated real-world limit), unlike Daily's precedence over
            // Weekly which suppresses one notice in favor of the other on
            // that SAME axis (see DailyCapTakesPrecedenceOverWeeklyCap).
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 1, weeklyCap: 10, seasonalCap: 20) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(50, plan.TotalCoinCost);

            Assert.Equal(2, plan.TimegatedItems.Count);
            var weeklyNotice = Assert.Single(plan.TimegatedItems, t => t.CapType == TimegatedCapType.Weekly);
            Assert.Equal(1, weeklyNotice.ItemId);
            Assert.Equal(10, weeklyNotice.CapValue);
            Assert.Equal(50, weeklyNotice.NeededCount);
            var seasonalNotice = Assert.Single(plan.TimegatedItems, t => t.CapType == TimegatedCapType.Seasonal);
            Assert.Equal(1, seasonalNotice.ItemId);
            Assert.Equal(20, seasonalNotice.CapValue);
            Assert.Equal(50, seasonalNotice.NeededCount);
        }

        [Fact]
        public void SeasonalCap_NeverChangesDecisionOrTotalCost_Regression()
        {
            // Regression guard (mirrors the existing Daily/Weekly cap-
            // never-reroutes tests): an exceeded SeasonalCap must not alter
            // the solver's Source choice, TotalCost, or the per-node
            // Decision - purely an informational notice layered on top of
            // an otherwise-unchanged solve.
            var tree = Leaf(1, 30);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 2, seasonalCap: 5) } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);
            var plan = result.Plan;

            // The vendor offer (2 coin each = 60 total) is still far
            // cheaper than TP (1000 each) and remains the chosen source
            // despite the exceeded SeasonalCap (need 30, cap 5).
            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(60, plan.Steps[0].TotalCost);
            Assert.Equal(60, plan.TotalCoinCost);
            Assert.Equal(60, result.Decisions[tree.NodeId].TotalCost);
            Assert.NotEmpty(plan.TimegatedItems);
        }

        [Fact]
        public void SeasonalCappedCurrencyOffer_ValuedCurrency_BeatsExpensiveTp_SurfacesTimegatedNotice()
        {
            // HONESTY NOTE: in live data, Wizard's Vault offers are priced
            // in unvalued Astral Acclaim, so the solver only ever selects
            // one (and therefore only ever fires this notice) when the
            // user has supplied a CurrencyValuation for that currency.
            // This exercises that real path through the actual comparable-
            // tier pipeline: a currency-priced (not coin-priced) offer,
            // chosen over TP because the user values the currency, whose
            // SeasonalCap the merged demand exceeds.
            var tree = Leaf(1, 25);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 9, seasonalCap: 20) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(225, plan.CurrencyCosts[0].Amount); // real currency amount: 9 * 25, unaffected by valuation

            var notice = Assert.Single(plan.TimegatedItems);
            Assert.Equal(1, notice.ItemId);
            Assert.Equal(TimegatedCapType.Seasonal, notice.CapType);
            Assert.Equal(20, notice.CapValue);
            Assert.Equal(25, notice.NeededCount);
        }

        // --- Price basis tests ---

        [Fact]
        public void BuyOrderBasis_UsesBuyOrderPrice()
        {
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100, SellInstant = 60 } }
            };
            var solver = new PlanSolver();

            var instant = solver.Solve(Leaf(1, 2), prices, null, PriceBasis.InstantBuy).Plan;
            var order = solver.Solve(Leaf(1, 2), prices, null, PriceBasis.BuyOrder).Plan;

            Assert.Equal(200, instant.TotalCoinCost);
            Assert.Equal(120, order.TotalCoinCost);
        }

        [Fact]
        public void BuyOrderBasis_NoBuyOrders_ItemNotPriceable()
        {
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100, SellInstant = 0 } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(Leaf(1, 1), prices, null, PriceBasis.BuyOrder).Plan;

            Assert.Equal(AcquisitionSource.UnknownSource, plan.Steps[0].Source);
        }

        [Fact]
        public void BuyOrderBasis_CanFlipBuyVsCraftDecision()
        {
            // Output: instant 100 / order 90. Craft from 2x ingredient:
            // instant 2x60=120 (buy wins), order 2x30=60 (craft wins).
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100, SellInstant = 90 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 60, SellInstant = 30 } }
            };
            var solver = new PlanSolver();

            var instant = solver.Solve(
                Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2))), prices, null,
                PriceBasis.InstantBuy).Plan;
            var order = solver.Solve(
                Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2))), prices, null,
                PriceBasis.BuyOrder).Plan;

            Assert.Single(instant.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, instant.Steps[0].Source);
            Assert.Contains(order.Steps, s => s.Source == AcquisitionSource.Craft);
            Assert.Equal(60, order.TotalCoinCost);
        }

        [Fact]
        public void BuyOrderBasis_VendorItemBarter_PricedAtBasis()
        {
            // Offer: 5x item 42. Instant 10 -> 50; order 4 -> 20.
            var offer = new VendorOffer
            {
                OfferId = "test-barter-basis",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = 42, Count = 5 }
                },
                MerchantName = "Barter Vendor",
                Locations = new List<string>()
            };
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200, SellInstant = 100 } },
                { 42, new ItemPrice { ItemId = 42, BuyInstant = 10, SellInstant = 4 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var solver = new PlanSolver();

            var order = solver.Solve(Leaf(1, 1), prices, vendorOffers, PriceBasis.BuyOrder).Plan;

            Assert.Equal(AcquisitionSource.BuyFromVendor, order.Steps[0].Source);
            Assert.Equal(20, order.TotalCoinCost);
        }

        // --- Per-node override tests ---

        [Fact]
        public void Override_ForcesBuyOverCheaperCraft()
        {
            // Craft = 60 beats buy = 100; user forces buy on the root.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            int rootNodeId = 0; // DFS pre-pass: root is always node 0
            Assert.Equal(AcquisitionSource.Craft, baseline.Decisions[rootNodeId].Source);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { rootNodeId, AcquisitionSource.BuyFromTp }
            };
            var forced = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, overrides);

            Assert.Single(forced.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, forced.Plan.Steps[0].Source);
            Assert.Equal(100, forced.Plan.TotalCoinCost);
        }

        [Fact]
        public void Override_ForcesCraftOverCheaperBuy()
        {
            // Buy = 50 beats craft = 200; user forces craft.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 50 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft }
            };
            var forced = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, overrides);

            Assert.Contains(forced.Plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Equal(200, forced.Plan.TotalCoinCost);
        }

        [Fact]
        public void Override_Infeasible_IgnoredAndBestPathApplies()
        {
            // Leaf with no recipes: forcing Craft is infeasible.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft }
            };
            var plan = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, overrides).Plan;

            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
        }

        [Fact]
        public void Override_OnChildNode_ParentCraftCostUsesForcedChildCost()
        {
            // Child 2: craft (20) beats buy (100). Forcing child to buy makes
            // the parent's craft cost 100, so the parent flips to buying at 90.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 1,
                        Option(20, 1, 1, Leaf(3, 2)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 90 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 10 } }
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            // Baseline: craft chain, total 20
            Assert.Equal(20, baseline.Plan.TotalCoinCost);

            // Child 2 is NodeId 1 (DFS: root=0, first child=1)
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 1, AcquisitionSource.BuyFromTp }
            };
            var forced = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, overrides);

            // Parent now prefers its own buy at 90 over craft-with-forced-child at 100
            Assert.Equal(AcquisitionSource.BuyFromTp, forced.Decisions[0].Source);
            Assert.Equal(90, forced.Plan.TotalCoinCost);
        }

        [Fact]
        public void UnpriceableRecipe_CanCraftIsTrue_ForceCraftSucceedsWithZeroFilledCost()
        {
            // M33 partial-pricing parity (superseded
            // "Override_ForcedCraftOnUnpriceableRecipe_IgnoredKeepsBuy"):
            // CanCraft now means "has a recipe" (gw2e's hasComponents), not
            // "recipe is fully priceable" - a recipe with an unpriceable
            // ingredient is always force-craftable (the ingredient just
            // zero-fills the craft cost). Item 1 is TP-priced (100) AND has
            // a recipe whose ingredient has no price; without any override
            // at all, craft (0, zero-filled) already strictly beats buy
            // (100), so this also demonstrates the natural (non-forced)
            // pick, not just the override path.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var natural = solver.Solve(tree, prices, null);
            Assert.Equal(AcquisitionSource.Craft, natural.Decisions[0].Source);
            Assert.True(natural.Decisions[0].CanCraft);
            Assert.True(natural.Decisions[0].CanBuyTp);
            Assert.Equal(0, natural.Plan.TotalCoinCost);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft }
            };
            var forced = solver.Solve(tree, prices, null, PriceBasis.InstantBuy, overrides);

            Assert.Equal(AcquisitionSource.Craft, forced.Decisions[0].Source);
            Assert.Equal(0, forced.Plan.TotalCoinCost);
        }

        [Fact]
        public void AvailabilityFlags_ReflectFeasiblePaths()
        {
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { CoinVendorOffer(2, 500) } }
            };
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers);

            // Root: craftable, no TP price, no vendor offer
            Assert.True(result.Decisions[0].CanCraft);
            Assert.False(result.Decisions[0].CanBuyTp);
            Assert.False(result.Decisions[0].CanBuyVendor);
            // Child: leaf with TP price and vendor offer
            Assert.False(result.Decisions[1].CanCraft);
            Assert.True(result.Decisions[1].CanBuyTp);
            Assert.True(result.Decisions[1].CanBuyVendor);
        }

        // --- Currency valuation tests ---
        // A user-provided CurrencyValuation makes an offer's non-coin
        // currency lines comparable, but ONLY when every line on the offer
        // has a valuation; the valuation affects comparison only, never the
        // currency amounts reported on the plan.

        [Fact]
        public void ValuedCurrencyOffer_BeatsExpensiveTp_AndPlanListsCurrencyCost()
        {
            // Karma-priced offer (0 coin, 50 karma) with a user valuation of
            // 5 copper/karma (= 250 total) beats a 1000-copper TP price. The
            // plan must still report the real karma amount to pay, not a
            // coin-converted figure.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost); // coin part only - offer has no coin cost
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        [Fact]
        public void UnvaluedCurrencyOffer_WithoutValuation_StaysFallbackOnly()
        {
            // Same offer and prices as the valued-wins test above, but with
            // no valuation supplied at all: pins the existing fallback-only
            // behavior (TP wins; the offer never even enters the comparison).
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(1000, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void ValuedCurrencyOffer_LosesWhenValuedCostExceedsTp()
        {
            // Same karma offer, but its valued cost (250) now exceeds the TP
            // price (100): TP must win outright, and the losing offer must
            // not leak into CurrencyCosts.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void MixedValuedAndUnvaluedCurrencyOffer_StaysFallbackTier()
        {
            // Offer costs both a valued currency (karma, id 2) and an
            // unvalued one (laurels, id 3). Any unvalued line must keep the
            // WHOLE offer in the fallback tier - it must not become
            // partially comparable. No TP price exists, so the fallback
            // offer is the only acquisition; both currency lines (valued and
            // unvalued alike) must appear in full on the plan.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>();
            var offer = new VendorOffer
            {
                OfferId = "test-mixed-valued-and-unvalued",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 2, Count = 10 },
                    new CostLine { Type = "Currency", Id = 3, Count = 1000 }
                },
                MerchantName = "Mixed Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost);
            Assert.Equal(2, plan.CurrencyCosts.Count);
            Assert.Contains(plan.CurrencyCosts, c => c.CurrencyId == 2 && c.Amount == 10);
            Assert.Contains(plan.CurrencyCosts, c => c.CurrencyId == 3 && c.Amount == 1000);
        }

        [Fact]
        public void MixedValuedAndUnvaluedCurrencyOffer_DoesNotBeatTp()
        {
            // Even a trivially cheap valued line must not make the offer
            // comparable while any other line stays unvalued.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var offer = new VendorOffer
            {
                OfferId = "test-mixed-valued-cheap",
                OutputItemId = 1,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Currency", Id = 2, Count = 1 },
                    new CostLine { Type = "Currency", Id = 3, Count = 1 }
                },
                MerchantName = "Mixed Vendor",
                Locations = new List<string>()
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { offer } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 1 } });
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation).Plan;

            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(100, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void ValuedCurrencyOffer_ForcedOverride_CarriesCurrencyCostsIntoPlan()
        {
            // A per-node override forcing BuyFromVendor on a fully-valued
            // offer must commit the same real coin part + currency lines as
            // the automatic comparison path.
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.BuyFromVendor }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, overrides, valuation).Plan;

            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(0, plan.Steps[0].TotalCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(2, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        // --- Comparison-value laundering regression tests ---
        // A valued vendor offer's coin-equivalent (coin + valued currency)
        // must survive being summed into an ANCESTOR's craft cost. Before
        // the fix, the craft loop summed each ingredient's returned REAL
        // coin cost (e.g. 0 for a karma-only vendor offer) instead of its
        // comparison value (coin + valued currency), so the karma cost was
        // laundered away and an ancestor could wrongly choose to craft
        // through a valued vendor offer that was actually more expensive.

        [Fact]
        public void ValuedVendorDescendant_DoesNotLaunderIntoCraftComparison_TpWinsForAncestor()
        {
            // B (item 2): TP buy 1000, or vendor offer 0 coin + 50 karma
            // (currency 3) valued at 5 copper/unit = 250 comparison value.
            // A (item 1): TP buy 200, or craft from 1x B.
            // Craft-A's true comparison cost is B's comparison value (250),
            // not B's real coin part (0), so TP-buy-A (200) must beat craft.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 200 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { MixedVendorOffer(2, 0, 3, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 3, 5 } });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(1, result.Plan.Steps[0].ItemId);
            Assert.Equal(200, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void ValuedVendorDescendant_CraftStillWinsWhenGenuinelyCheaper_PlanShowsRealCoinAndCurrency()
        {
            // Same B options as above, but A's TP price (2000) is expensive
            // enough that craft (comparison cost 250) genuinely wins. The
            // committed plan must show the REAL coin cost (0, B's vendor
            // coin part) and the real karma amount (50) - the valuation used
            // to pick this path must never leak into the displayed coin.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 2000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 2, new List<VendorOffer> { MixedVendorOffer(2, 0, 3, 50) } }
            };
            var valuation = new CurrencyValuation(new Dictionary<int, long> { { 3, 5 } });
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy, null, valuation);
            var plan = result.Plan;

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.BuyFromVendor && s.ItemId == 2);

            // Real coin cost only (B's vendor coin part is 0) - the 250
            // comparison value used to pick this path must not appear here.
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Single(plan.CurrencyCosts);
            Assert.Equal(3, plan.CurrencyCosts[0].CurrencyId);
            Assert.Equal(50, plan.CurrencyCosts[0].Amount);
        }

        // --- M34-B2a #3: cost diagnostics + force-buy-only exclusion ---

        [Fact]
        public void CostDiagnostics_PopulatedForEveryItemNode_RegardlessOfDecision()
        {
            // Item 1: buy 1000, craft from item 2 (2x30=60) - craft wins.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();
            var diagnostics = new Dictionary<int, (long? BuyCost, long? CraftCost)>();

            solver.Solve(tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: null, costDiagnostics: diagnostics);

            // Root (NodeId 0): buy=1000, craft=60 - present even though craft won.
            Assert.True(diagnostics.TryGetValue(0, out var rootDiag));
            Assert.Equal(1000, rootDiag.BuyCost);
            Assert.Equal(60, rootDiag.CraftCost);

            // Leaf ingredient (item 2, NodeId 1): buy=60 (2x30), no recipe -> craft null.
            Assert.True(diagnostics.TryGetValue(1, out var leafDiag));
            Assert.Equal(60, leafDiag.BuyCost);
            Assert.Null(leafDiag.CraftCost);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_ExcludesCraftFromAutomaticPick()
        {
            // Craft (60) would normally beat buy (100); force-buy-only
            // excludes craft for the root node, so buy wins instead even
            // though nothing else about the tree/prices changed.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            Assert.Equal(AcquisitionSource.Craft, baseline.Decisions[0].Source);

            var forceBuyOnly = new HashSet<int> { 0 };
            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: forceBuyOnly);

            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[0].Source);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(100, result.Plan.TotalCoinCost);
            // CanCraft still reflects true feasibility (a recipe exists) -
            // only the AUTOMATIC pick is affected, not the reported flag.
            Assert.True(result.Decisions[0].CanCraft);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_ManualOverrideStillWinsOverForceBuy()
        {
            // Same setup as above, but the user ALSO manually forces Craft
            // on the root - matching gw2e's own "manual pill always beats
            // the automatic pre-pass" rule (Section 3.2 of the R2 report).
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var forceBuyOnly = new HashSet<int> { 0 };
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { 0, AcquisitionSource.Craft }
            };

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, overrides, null,
                forceBuyOnlyNodeIds: forceBuyOnly);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void ForceBuyOnlyNodeIds_Null_BehavesExactlyAsBefore()
        {
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 30 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                forceBuyOnlyNodeIds: null);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(60, result.Plan.TotalCoinCost);
        }

        // --- M34-B2b: "Ignore" pill (ignoredItemIds) ---

        [Fact]
        public void IgnoredItemIds_LeafBuyNode_ZeroCostNoStep()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 1 });

            Assert.Empty(result.Plan.Steps);
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void IgnoredItemIds_CraftIngredient_ParentCostExcludesIgnoredIngredient()
        {
            // Item 1 crafts from 2x item 2 (would normally cost 2*100=200).
            // Ignoring item 2 must make the WHOLE craft's cost 0, not just
            // hide item 2's own row - matching gw2e's "owned materials are
            // free" rule (Section 2.1 of the r2 report) applied via Ignore.
            var tree = Craftable(1, 1, Option(10, 1, 1, Leaf(2, 2)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } }, // buying finished item is far pricier
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var baseline = solver.Solve(tree, prices, null);
            Assert.Equal(AcquisitionSource.Craft, baseline.Decisions[0].Source);
            Assert.Equal(200, baseline.Plan.TotalCoinCost);

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[0].Source);
            Assert.Equal(0, result.Plan.TotalCoinCost);
            // Item 2's own row is gone entirely - not a "0 cost" leftover row.
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
        }

        [Fact]
        public void IgnoredItemIds_DoesNotAffectUnrelatedItem()
        {
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 2), Leaf(3, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 50 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
            var item3Step = result.Plan.Steps.Single(s => s.ItemId == 3);
            Assert.Equal(AcquisitionSource.BuyFromTp, item3Step.Source);
            Assert.Equal(50, item3Step.TotalCost);
            Assert.Equal(50, result.Plan.TotalCoinCost); // only item 3's real cost remains
        }

        [Fact]
        public void IgnoredItemIds_DoesNotRecurseIntoIgnoredNodesOwnIngredients()
        {
            // Item 2 (ignored) itself crafts from item 3 - since item 2 is
            // treated as fully in-hand, its own recipe must never be
            // evaluated/collected (matching gw2e's "an un-crafted branch
            // never asks for its ingredients" rule), so item 3 must not
            // appear anywhere in the plan even though it has a real price.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Craftable(2, 5, Option(20, 1, 1, Leaf(3, 10)))));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 5 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: new HashSet<int> { 2 });

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2);
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 3);
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        [Fact]
        public void IgnoredItemIds_Null_BehavesExactlyAsBefore()
        {
            var tree = Leaf(1, 5);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, null, null,
                ignoredItemIds: null);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(500, result.Plan.TotalCoinCost);
        }

        // M38 WP-18 (tests T6, KNOWN-ISSUES 20.4 "Conservative reading"):
        // the Ignore x owned-materials interaction is NOT pinned at this
        // layer. RecipeNode (the type Solve consumes) has no ownership
        // field at all - only Id/NodeId/Quantity/achievement fields - so
        // "a node already reduced by partial ownership" cannot be
        // represented here beyond just choosing a smaller Quantity, which
        // collapses to the exact same Evaluate/Collect code path as
        // IgnoredItemIds_LeafBuyNode_ZeroCostNoStep above and proves
        // nothing extra about the interaction. Ownership only exists on the
        // downstream CraftingTreeNode built after Solve returns, so the
        // real pin lives one and two layers up:
        // CraftingPlanPipelineTests (GenerateStructuredAsync Ignore x owned-
        // materials coverage) and DecisionPillPlannerTests
        // (Have_IgnoredAndPartiallyOwned_ShowsIgnoredNotOwnedInfo).

        // --- M35-B1: synthetic multi-item wrapper root (gw2e parity) ---
        // WrapperOf lives in Helpers/RecipeNodeBuilders.cs.

        [Fact]
        public void WrapperRoot_NeverAppearsAsItsOwnStep_OnlyItemRootsDo()
        {
            var itemA = Leaf(100, 5);
            var itemB = Leaf(200, 3);
            var wrapper = WrapperOf(itemA, itemB);

            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 10 } },
                { 200, new ItemPrice { ItemId = 200, BuyInstant = 20 } }
            };

            var result = new PlanSolver().Solve(wrapper, prices, null, PriceBasis.InstantBuy);
            var plan = result.Plan;

            Assert.Equal(2, plan.Steps.Count);
            Assert.DoesNotContain(plan.Steps, s => s.ItemId == Gw2Constants.MultiItemWrapperItemId);
            Assert.Contains(plan.Steps, s => s.ItemId == 100 && s.Quantity == 5 && s.TotalCost == 50);
            Assert.Contains(plan.Steps, s => s.ItemId == 200 && s.Quantity == 3 && s.TotalCost == 60);
            Assert.Equal(110, plan.TotalCoinCost);

            // The wrapper's own memo entry exists (Evaluate always visits
            // it) but is never surfaced via a step; it also never appears
            // as a decision a caller would look up (NodeId 0, pre-order
            // DFS root).
            Assert.True(result.Decisions.ContainsKey(wrapper.NodeId));
        }

        [Fact]
        public void WrapperRoot_EachItemRoot_GetsIndependentCraftVsBuyDecision()
        {
            // Item A: crafting (2 x 10 = 20) beats its own buy price (100).
            var ingredientA = Leaf(101, 2);
            var itemA = Craftable(100, 1, Option(110, 1, 1, ingredientA));
            // Item B: no recipe, always bought.
            var itemB = Leaf(200, 4);

            var wrapper = WrapperOf(itemA, itemB);

            var prices = new Dictionary<int, ItemPrice>
            {
                { 100, new ItemPrice { ItemId = 100, BuyInstant = 100 } },
                { 101, new ItemPrice { ItemId = 101, BuyInstant = 10 } },
                { 200, new ItemPrice { ItemId = 200, BuyInstant = 5 } }
            };

            var result = new PlanSolver().Solve(wrapper, prices, null, PriceBasis.InstantBuy);

            Assert.Equal(AcquisitionSource.Craft, result.Decisions[itemA.NodeId].Source);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Decisions[itemB.NodeId].Source);
        }

        // --- M37: Homestead Refinement efficiency tier gating (gw2e parity,
        // KNOWN-ISSUES #24) - the live defect fix: our seed already carries
        // all tier rows untagged, so before this gate the solver silently
        // assumed every account had every efficiency upgrade. ---

        private static VendorOffer HomesteadOffer(
            int outputItemId, int inputCount, int outputCount, int homesteadTier,
            int? weeklyCap = null, string merchantName = "Homestead Refinement\u2014Metal Forge")
        {
            return new VendorOffer
            {
                OfferId = $"homestead-{outputItemId}-{homesteadTier}-{inputCount}",
                OutputItemId = outputItemId,
                OutputCount = outputCount,
                CostLines = new List<CostLine>
                {
                    // Item id is arbitrary/unique per test; only its buy
                    // price (set by the caller) matters to the solver.
                    new CostLine { Type = "Item", Id = 900 + homesteadTier, Count = inputCount }
                },
                MerchantName = merchantName,
                Locations = new List<string> { "Hearth's Glow" },
                WeeklyCap = weeklyCap,
                HomesteadTier = homesteadTier
            };
        }

        [Fact]
        public void HomesteadOffer_DefaultTierZero_ExcludesHigherTierOffers()
        {
            // Metal Forge Iron Ore, matching the wiki-verified conversion
            // table exactly: tier0 4->2, tier1 2->2, tier2 1->1 (docs/
            // research/m37-r1-homestead.md Section 2.2). Iron ore costs 1
            // coin each; tier2's 1-ore rate is far cheaper per unit of
            // output than tier0's 4-ore rate. Default (no homesteadTiers
            // argument -> HomesteadEfficiencyTiers.Default, tier 0 for
            // every material) must still pick the tier-0 row.
            var tree = Leaf(102205, 2); // Refined Homestead Metal, need 2
            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 1 } }, // tier0 input (900+0)
                { 901, new ItemPrice { ItemId = 901, BuyInstant = 1 } }, // tier1 input (900+1)
                { 902, new ItemPrice { ItemId = 902, BuyInstant = 1 } }  // tier2 input (900+2)
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    102205, new List<VendorOffer>
                    {
                        HomesteadOffer(102205, inputCount: 4, outputCount: 2, homesteadTier: 0),
                        HomesteadOffer(102205, inputCount: 2, outputCount: 2, homesteadTier: 1),
                        HomesteadOffer(102205, inputCount: 1, outputCount: 1, homesteadTier: 2)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;

            var step = Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, step.Source);
            // Tier0 offer: ceil(2/2)=1 purchase of 4 ore = 4 coin.
            Assert.Equal(4, step.TotalCost);
        }

        [Fact]
        public void HomesteadOffer_TierTwoConfigured_AdmitsCheaperHigherTierOffer()
        {
            var tree = Leaf(102205, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 1 } },
                { 901, new ItemPrice { ItemId = 901, BuyInstant = 1 } },
                { 902, new ItemPrice { ItemId = 902, BuyInstant = 1 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    102205, new List<VendorOffer>
                    {
                        HomesteadOffer(102205, inputCount: 4, outputCount: 2, homesteadTier: 0),
                        HomesteadOffer(102205, inputCount: 2, outputCount: 2, homesteadTier: 1),
                        HomesteadOffer(102205, inputCount: 1, outputCount: 1, homesteadTier: 2)
                    }
                }
            };
            var tiers = new HomesteadEfficiencyTiers(new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadMetalItemId, 2 }
            });
            var solver = new PlanSolver();

            var plan = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                homesteadTiers: tiers).Plan;

            var step = Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, step.Source);
            // Tier2 offer: ceil(2/1)=2 purchases of 1 ore = 2 coin - cheaper
            // than tier0's 4 coin, and only reachable once tier 2 is
            // configured for Metal.
            Assert.Equal(2, step.TotalCost);
        }

        [Fact]
        public void HomesteadOffer_TierOneConfigured_AdmitsTierOneButNotTierTwo()
        {
            var tree = Leaf(102205, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 1 } },
                { 901, new ItemPrice { ItemId = 901, BuyInstant = 1 } },
                { 902, new ItemPrice { ItemId = 902, BuyInstant = 1 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    102205, new List<VendorOffer>
                    {
                        HomesteadOffer(102205, inputCount: 4, outputCount: 2, homesteadTier: 0),
                        HomesteadOffer(102205, inputCount: 2, outputCount: 2, homesteadTier: 1),
                        HomesteadOffer(102205, inputCount: 1, outputCount: 1, homesteadTier: 2)
                    }
                }
            };
            var tiers = new HomesteadEfficiencyTiers(new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadMetalItemId, 1 }
            });
            var solver = new PlanSolver();

            var plan = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                homesteadTiers: tiers).Plan;

            var step = Assert.Single(plan.Steps);
            // Cheapest of {tier0, tier1} (tier2 excluded): tier1's 2-ore
            // rate (ceil(2/2)=1 purchase = 2 coin) beats tier0's 4 coin.
            Assert.Equal(2, step.TotalCost);
        }

        [Fact]
        public void HomesteadTierConfigured_ForDifferentMaterial_DoesNotAffectThisOne()
        {
            // Configuring Fiber's tier to 2 must not admit a higher-tier
            // Metal offer - the gate is per-material, not global.
            var tree = Leaf(102205, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 1 } },
                { 902, new ItemPrice { ItemId = 902, BuyInstant = 1 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    102205, new List<VendorOffer>
                    {
                        HomesteadOffer(102205, inputCount: 4, outputCount: 2, homesteadTier: 0),
                        HomesteadOffer(102205, inputCount: 1, outputCount: 1, homesteadTier: 2)
                    }
                }
            };
            var tiers = new HomesteadEfficiencyTiers(new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadFiberItemId, 2 }
            });
            var solver = new PlanSolver();

            var plan = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                homesteadTiers: tiers).Plan;

            var step = Assert.Single(plan.Steps);
            Assert.Equal(4, step.TotalCost);
        }

        [Fact]
        public void NonHomesteadVendorOffer_UnaffectedByHomesteadTierSetting()
        {
            // A plain vendor offer with HomesteadTier == null (every
            // existing non-Homestead offer in the seed) must be completely
            // unaffected by any homesteadTiers configuration, at default or
            // otherwise - byte-identical to before this feature existed.
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } }
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5) } }
            };
            var tiers = HomesteadEfficiencyTiers.Default;
            var solver = new PlanSolver();

            var planDefault = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;
            var planExplicit = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy, homesteadTiers: tiers).Plan;

            Assert.Equal(AcquisitionSource.BuyFromVendor, planDefault.Steps[0].Source);
            Assert.Equal(10, planDefault.TotalCoinCost);
            Assert.Equal(planDefault.TotalCoinCost, planExplicit.TotalCoinCost);
        }

        [Fact]
        public void ExordiumStyleTree_NoHomesteadOffersReachable_ByteIdenticalAtAnyTier()
        {
            // Regression guard mirroring the research report's own
            // BFS-verified finding: Exordium's tree reaches zero Homestead
            // Refinement materials, so a plan for a tree with NO homestead
            // offers at all must be byte-identical regardless of the
            // configured tier setting. A small synthetic tree stands in for
            // the real (14k-recipe) Exordium tree here; the real tree is
            // checked via the offline Harness per this milestone's manual
            // verification step.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(2, 3), Leaf(3, 5)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } },
                { 3, new ItemPrice { ItemId = 3, BuyInstant = 20 } }
            };
            var solver = new PlanSolver();

            var planTier0 = solver.Solve(tree, prices, null, PriceBasis.InstantBuy).Plan;
            var maxTiers = new HomesteadEfficiencyTiers(new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadFiberItemId, 2 },
                { Gw2Constants.RefinedHomesteadMetalItemId, 2 },
                { Gw2Constants.RefinedHomesteadWoodItemId, 2 }
            });
            var planTier2 = solver.Solve(
                tree, prices, null, PriceBasis.InstantBuy, homesteadTiers: maxTiers).Plan;

            Assert.Equal(planTier0.TotalCoinCost, planTier2.TotalCoinCost);
            Assert.Equal(planTier0.Steps.Count, planTier2.Steps.Count);
            for (int i = 0; i < planTier0.Steps.Count; i++)
            {
                Assert.Equal(planTier0.Steps[i].Source, planTier2.Steps[i].Source);
                Assert.Equal(planTier0.Steps[i].TotalCost, planTier2.Steps[i].TotalCost);
            }
        }

        [Fact]
        public void NullHomesteadTier_OnMaterialOutput_IsAdmittedRegardlessOfConfiguredTier()
        {
            // Documents CURRENT, by-design behavior (not a bug to fix
            // here): EvaluateVendorOffers only gates on
            // `offer.HomesteadTier.HasValue` - a null tier is NEVER
            // excluded, even when OutputItemId is one of the three real
            // Homestead Refinement materials and even at the most
            // restrictive tier (0). Null is meant for the 21 one-time
            // "Upgrade" purchase rows the same merchant pages also sell
            // (tier-independent by design), NOT for a material-conversion
            // row - if a future wiki re-scrape ever mistagged a material
            // row with a null tier, this is exactly the runtime behavior
            // that would silently readmit it at every tier, reintroducing
            // the always-max-tier defect PR #57 fixed. The solver itself
            // has no way to catch that mistake; the data-integrity test
            // ShippedSeedFile_HomesteadRefinementMaterialRows_AllHaveNonNullTierInRange
            // (VendorOfferStoreTests) exists precisely because of the
            // runtime behavior pinned here.
            var tree = Leaf(Gw2Constants.RefinedHomesteadMetalItemId, 2);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 1 } }
            };
            var untaggedMaterialOffer = new VendorOffer
            {
                OfferId = "untagged-material-offer",
                OutputItemId = Gw2Constants.RefinedHomesteadMetalItemId,
                OutputCount = 1,
                CostLines = new List<CostLine>
                {
                    new CostLine { Type = "Item", Id = 900, Count = 1 }
                },
                MerchantName = "Homestead Refinement\u2014Metal Forge",
                Locations = new List<string> { "Hearth's Glow" },
                HomesteadTier = null
            };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    Gw2Constants.RefinedHomesteadMetalItemId,
                    new List<VendorOffer> { untaggedMaterialOffer }
                }
            };
            // Tier 0 is the most restrictive setting - if the gate applied
            // to this offer, it would still be excluded here.
            var tierZero = new HomesteadEfficiencyTiers(new Dictionary<int, int>
            {
                { Gw2Constants.RefinedHomesteadMetalItemId, 0 }
            });
            var solver = new PlanSolver();

            var plan = solver.Solve(
                tree, prices, vendorOffers, PriceBasis.InstantBuy,
                homesteadTiers: tierZero).Plan;

            var step = Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, step.Source);
            Assert.Equal(2, step.TotalCost);
        }

        // --- M37: Homestead mixed-offer cap-notice gap (KNOWN-ISSUES
        // #24/#25 Section 3.3) - a fix was attempted here (summing each
        // occurrence's own true purchase count when occurrences disagreed
        // on the winning offer but agreed on the raw (DailyCap, WeeklyCap)
        // tuple) but reverted after adversarial review: the wiki's per-row
        // WeeklyCap the Homestead seed data carries is a template
        // parameter, not a confirmed per-station aggregate (see
        // KNOWN-ISSUES #24's "Cap data" note), so two occurrences agreeing
        // on that raw number does not mean they agree on a real shared
        // limit worth summing against - and every Homestead row within one
        // station shares that same number, so the summing branch fired for
        // the ordinary case, not a rare edge case. The pre-existing
        // suppress-on-Conflict behavior is kept; both tests below document
        // that as an intentional, narrower limitation rather than a silent
        // regression risk. ---

        [Fact]
        public void MixedOfferSameWeeklyCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged
            // (qty=1 deterministically favors the 1-for-2 offer, qty=100
            // deterministically favors the 100-for-150 offer - genuine
            // disagreement, not a tie). Both offers happen to share the
            // identical WeeklyCap=1 (the normal Homestead shape - every
            // offer at one station carries the same wiki-scraped per-row
            // number), but Conflict (the offer-shape ratchet) alone still
            // suppresses the notice: there is no verified single cap to
            // check the mixed-offer total against.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(99, 1), Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, weeklyCap: 1),
                        CoinVendorOffer(99, 150, outputCount: 100, weeklyCap: 1)
                    }
                }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing sibling test's own proof for this exact shape),
            // so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);
            Assert.Equal(152, vendorStep.TotalCost);

            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void MixedOfferDifferentWeeklyCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MultiOccurrenceDifferentWinningOffers_LeavesPerOccurrenceSumUnmerged
            // (qty=1 favors the 1-for-2 offer, qty=100 favors the
            // 100-for-150 offer - genuine, deterministic disagreement, not
            // a tie), but this time the two offers ALSO carry different
            // WeeklyCap values. Whether or not the raw cap number happens to
            // match across occurrences, Conflict alone suppresses the
            // notice - same as before this milestone. This documents that
            // limitation as intentional rather than a silent regression
            // risk.
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
                        CoinVendorOffer(99, 2, outputCount: 1, weeklyCap: 5),
                        CoinVendorOffer(99, 150, outputCount: 100, weeklyCap: 999)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;

            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing sibling test's own proof for this exact shape),
            // so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);

            Assert.Empty(plan.TimegatedItems);
        }

        // --- Adversarial review of the M37 mixed-offer Weekly pair above
        // found the Conflict-suppression parity claim for the KNOWN-ISSUES
        // #33 SeasonalCap package unverified: FinalizeVendorBatches checks
        // Seasonal inside the exact same "!state.Conflict" guard as Daily/
        // Weekly (an implementation coincidence, not a pinned contract), so
        // nothing failed if that guard were ever hoisted apart for Seasonal
        // specifically. These two tests mirror the Weekly pair exactly,
        // substituting seasonalCap for weeklyCap, to pin the same suppress-
        // on-Conflict behavior for Seasonal. ---

        [Fact]
        public void MixedOfferSameSeasonalCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MixedOfferSameWeeklyCap_NoticeStillSuppressed_DocumentedLimitation
            // (qty=1 deterministically favors the 1-for-2 offer, qty=100
            // deterministically favors the 100-for-150 offer - genuine
            // disagreement, not a tie). Both offers happen to share the
            // identical SeasonalCap=1, but Conflict (the offer-shape
            // ratchet) alone still suppresses the notice - same as Weekly.
            var tree = Craftable(1, 1,
                Option(10, 1, 1, Leaf(99, 1), Leaf(99, 100)));
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    99, new List<VendorOffer>
                    {
                        CoinVendorOffer(99, 2, outputCount: 1, seasonalCap: 1),
                        CoinVendorOffer(99, 150, outputCount: 100, seasonalCap: 1)
                    }
                }
            };
            var solver = new PlanSolver();

            var result = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy);
            var plan = result.Plan;

            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing Weekly sibling test's own proof for this exact
            // shape), so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);
            Assert.Equal(152, vendorStep.TotalCost);

            Assert.Empty(plan.TimegatedItems);
        }

        [Fact]
        public void MixedOfferDifferentSeasonalCap_NoticeStillSuppressed_DocumentedLimitation()
        {
            // Same bulk-discount-threshold shape as
            // MixedOfferDifferentWeeklyCap_NoticeStillSuppressed_DocumentedLimitation
            // (qty=1 favors the 1-for-2 offer, qty=100 favors the
            // 100-for-150 offer - genuine, deterministic disagreement, not
            // a tie), but this time the two offers ALSO carry different
            // SeasonalCap values. Whether or not the raw cap number happens
            // to match across occurrences, Conflict alone suppresses the
            // notice - same as Weekly.
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
                        CoinVendorOffer(99, 2, outputCount: 1, seasonalCap: 5),
                        CoinVendorOffer(99, 150, outputCount: 100, seasonalCap: 999)
                    }
                }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers, PriceBasis.InstantBuy).Plan;

            // Confirms Conflict actually ratcheted true here (matching the
            // pre-existing Weekly sibling test's own proof for this exact
            // shape), so the empty-notice assertion below is testing genuine
            // Conflict suppression, not merely "no cap was ever exceeded".
            var vendorStep = Assert.Single(plan.Steps, s => s.ItemId == 99);
            Assert.Equal(0, vendorStep.VendorOfferOutputCount);

            Assert.Empty(plan.TimegatedItems);
        }

        // --- M37 (KNOWN-ISSUES #26 fix-pass finding): a Quantity == 0 node
        // must never leave a standalone "ghost" step, even when its own
        // resolved Source/stepKey does not match any other occurrence's ---

        [Fact]
        public void QuantityZeroNode_NestedUnderCraftedParent_MismatchedStepKey_NoGhostStep()
        {
            // Item 900 occurs twice under root 999's chosen recipe:
            // - branchA: Quantity == 0 (simulating either genuine full
            //   ownership or an AchievementBitDedupPrePass zeroing - both
            //   collapse the same way: Recipes cleared, forced onto a
            //   Buy-only path).
            // - branchB: Quantity == 1, has its OWN recipe (crafting from
            //   901 at 1 coin beats buying 900 at 100), so it resolves to
            //   Craft - a DIFFERENT stepKey than branchA's forced Buy.
            // Before the M37 fix-pass, branchA (Quantity 0, Source
            // BuyFromTp) would still call AggregateStep and - since nothing
            // else shares its (900, BuyFromTp, 0) stepKey - leave a
            // standalone "buy 0 units of 900, 0 cost" row in Plan.Steps.
            var branchA = Leaf(900, 0);
            var branchB = Craftable(900, 1, Option(50, 1, 1, Leaf(901, 5)));
            var root = Craftable(999, 1, Option(10, 1, 1, branchA, branchB));

            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 100 } },
                { 901, new ItemPrice { ItemId = 901, BuyInstant = 1 } }
            };

            var result = new PlanSolver().Solve(root, prices, null, PriceBasis.InstantBuy);

            // branchB genuinely crafts (5*1=5 beats buying at 100).
            Assert.Contains(result.Plan.Steps, s => s.ItemId == 900 && s.Source == AcquisitionSource.Craft && s.Quantity == 1);
            // branchA contributes NOTHING - no standalone zero-quantity row
            // of any Source for item 900.
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 900 && s.Source == AcquisitionSource.BuyFromTp);
            Assert.DoesNotContain(result.Plan.Steps, s => s.Quantity == 0);
        }

        [Fact]
        public void QuantityZeroNode_MatchingStepKeyElsewhere_MergesWithoutInflatingQuantityOrCost()
        {
            // Same shape, but branchB has NO recipe of its own (a plain
            // buy) - both occurrences now share the SAME stepKey
            // (900, BuyFromTp, 0). Confirms the M37 Quantity == 0 guard does
            // not merely avoid a ghost row but also does not change the
            // ordinary merge-by-stepKey outcome for the real occurrence.
            var branchA = Leaf(900, 0);
            var branchB = Leaf(900, 1);
            var root = Craftable(999, 1, Option(10, 1, 1, branchA, branchB));

            var prices = new Dictionary<int, ItemPrice>
            {
                { 900, new ItemPrice { ItemId = 900, BuyInstant = 100 } }
            };

            var result = new PlanSolver().Solve(root, prices, null, PriceBasis.InstantBuy);

            var step = Assert.Single(result.Plan.Steps.Where(s => s.ItemId == 900));
            Assert.Equal(AcquisitionSource.BuyFromTp, step.Source);
            Assert.Equal(1, step.Quantity);
            Assert.Equal(100, step.TotalCost);
        }
    }
}
