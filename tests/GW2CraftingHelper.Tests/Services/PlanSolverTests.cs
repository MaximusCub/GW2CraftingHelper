using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class PlanSolverTests
    {
        private static RecipeNode Leaf(int id, int quantity, string type = "Item")
        {
            return new RecipeNode
            {
                Id = id,
                IngredientType = type,
                Quantity = quantity,
                Recipes = new List<RecipeOption>()
            };
        }

        private static RecipeNode Craftable(int id, int quantity, params RecipeOption[] recipes)
        {
            var node = new RecipeNode
            {
                Id = id,
                IngredientType = "Item",
                Quantity = quantity,
                Recipes = new List<RecipeOption>()
            };
            if (recipes != null)
            {
                node.Recipes.AddRange(recipes);
            }
            return node;
        }

        private static RecipeOption Option(int recipeId, int outputCount, int craftsNeeded, params RecipeNode[] ingredients)
        {
            var opt = new RecipeOption
            {
                RecipeId = recipeId,
                OutputCount = outputCount,
                CraftsNeeded = craftsNeeded,
                Ingredients = new List<RecipeNode>()
            };
            if (ingredients != null)
            {
                opt.Ingredients.AddRange(ingredients);
            }
            return opt;
        }

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

        // --- Mystic Clover-style EV pricing tests (M33 spec item 7) ---

        [Fact]
        public void FractionalExpectedOutput_AmortizesCraftCost_FlipsDecisionToBuy()
        {
            // Raw ingredient cost = 100 (1x item 2 @ 100). Nominal
            // OutputCount=1 but ExpectedOutputCount=0.5 (Mystic
            // Clover-style EV) means the true cost is amortized:
            // 100 / 0.5 = 200 - which now loses to the 150 buy price (the
            // raw, un-adjusted 100 would have won craft outright).
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode> { Leaf(2, 1) }
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
        public void FractionalExpectedOutput_CraftStillWinsWhenExpectedCostCheaper()
        {
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 0.5,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode> { Leaf(2, 1) }
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
            Assert.Equal(1, craftStep.ItemId);
            // 100 (raw ingredient cost) / 0.5 (EV ratio) = 200 - the real
            // committed coin cost is EV-adjusted too, not just the
            // comparison value used to pick the winner. plan.TotalCoinCost
            // stays 100 (only BuyFromTp/BuyFromVendor steps are summed
            // into it - item 2's 100 - matching the pre-existing
            // no-double-counting design; the Craft step's own 200 is a
            // derived total, not additional coin spent).
            Assert.Equal(200, craftStep.TotalCost);
            Assert.Equal(100, plan.TotalCoinCost);
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

        [Fact]
        public void FractionalExpectedOutput_AbsurdlyTinyValue_OverflowFallsBackGracefully()
        {
            // A corrupt/malicious seed could set an ExpectedOutputCount so
            // small that dividing by it overflows long - must fall back to
            // the un-adjusted cost rather than crash the whole solve or
            // silently wrap to a garbage (possibly negative) total.
            var evOption = new RecipeOption
            {
                RecipeId = 10,
                OutputCount = 1,
                ExpectedOutputCount = 1e-15,
                CraftsNeeded = 1,
                Ingredients = new List<RecipeNode> { Leaf(2, 1) }
            };
            var tree = Craftable(1, 1, evOption);
            var prices = new Dictionary<int, ItemPrice>
            {
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 100 } }
            };
            var solver = new PlanSolver();

            var exception = Record.Exception(() => solver.Solve(tree, prices));

            Assert.Null(exception);
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
            int outputItemId, int coinCost, int outputCount = 1, int? dailyCap = null, int? weeklyCap = null)
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
                WeeklyCap = weeklyCap
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
            int? dailyCap = null, int? weeklyCap = null)
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
                WeeklyCap = weeklyCap
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

        // --- Vendor purchase-cap tests ---
        // V1 semantics: an offer whose DailyCap (or WeeklyCap when DailyCap is
        // absent/zero) cannot cover the node's needed purchases in a single
        // cap period is excluded entirely - it never competes in the
        // comparable tier or the fallback tier. Partial cap-split sourcing
        // (buy up to the cap, then take the rest from a second source) is a
        // deliberate non-goal; a node is still sourced from exactly one
        // acquisition.

        [Fact]
        public void CappedOffer_NeededExceedsCap_ExcludedFallsBackToTp()
        {
            // Vendor sells for 1 coin each but only 25/day; node needs 50, so
            // one day's cap cannot cover it and the offer must be excluded,
            // leaving the (much pricier) TP buy as the only option.
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
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
        }

        [Fact]
        public void CappedOffer_NeededWithinCap_StillUsedAsVendor()
        {
            // Needed (20) is within the cap (25); the far cheaper vendor
            // offer must still be picked over the expensive TP price.
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
        }

        [Fact]
        public void CappedBatchOffer_CapTimesOutputCountArithmetic()
        {
            // Offer sells batches of 10 with a cap of 3 purchases/day (max
            // 30 units/day). Needing 25 units requires only 3 purchases
            // (ceil(25/10)), which fits the cap even though 25 itself is far
            // greater than the raw DailyCap of 3 - proving OutputCount is
            // correctly folded into the cap check rather than comparing the
            // node's raw quantity against the cap.
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
        }

        [Fact]
        public void CappedBatchOffer_OneMoreUnitPushesPastCap_Excluded()
        {
            // Same batch/cap shape as above (10/batch, cap 3 => 30/day), but
            // needing 31 units requires 4 purchases (ceil(31/10)), which
            // exceeds the cap even though the cap*OutputCount ceiling (30) is
            // barely below 31.
            var tree = Leaf(1, 31);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { CoinVendorOffer(1, 5, outputCount: 10, dailyCap: 3) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.UnknownSource, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
        }

        [Fact]
        public void ZeroCap_TreatedAsUncapped()
        {
            // An explicit DailyCap of 0 (not merely absent) must still mean
            // uncapped per the V1 decision, not "zero purchases allowed".
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
        }

        [Fact]
        public void WeeklyCapUsed_WhenDailyCapAbsent()
        {
            // No DailyCap set; WeeklyCap of 25 cannot cover the 50 needed, so
            // the offer is excluded and TP is used instead.
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
            Assert.Equal(AcquisitionSource.BuyFromTp, plan.Steps[0].Source);
            Assert.Equal(500, plan.TotalCoinCost);
        }

        [Fact]
        public void DailyCapTakesPrecedenceOverWeeklyCap()
        {
            // DailyCap (100) alone covers the 50 needed, so the offer is used
            // even though its WeeklyCap (1) alone would have excluded it -
            // DailyCap wins whenever it is positive.
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
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededExceedsCap_ExcludedFromFallbackTier()
        {
            // A mixed-currency offer only ever competes in the fallback
            // tier (its non-coin currency line is unvalued). The cap check
            // must still apply there: needing 50 against a cap of 10 excludes
            // it, leaving no acquisition at all (no TP price, no recipe).
            var tree = Leaf(1, 50);
            var prices = new Dictionary<int, ItemPrice>();
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                { 1, new List<VendorOffer> { MixedVendorOffer(1, 0, 2, 50, dailyCap: 10) } }
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices, vendorOffers).Plan;

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.UnknownSource, plan.Steps[0].Source);
            Assert.Equal(0, plan.TotalCoinCost);
            Assert.Empty(plan.CurrencyCosts);
        }

        [Fact]
        public void CappedMixedCurrencyOffer_NeededWithinCap_StillUsedAsFallback()
        {
            // Needed (5) is within the cap (10); the mixed-currency offer
            // remains the fallback acquisition (no TP price, no recipe).
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
    }
}
