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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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

            var plan = solver.Solve(tree, prices);

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
            // Item 1 crafts from: 3x item 2 + 5x item 2 (same item, two ingredients — simulating
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

            var plan = solver.Solve(tree, prices);

            // Item 2 appears twice as BuyFromTp, should be deduplicated: 3 + 5 = 8
            var item2Steps = plan.Steps.Where(s => s.ItemId == 2).ToList();
            Assert.Single(item2Steps);
            Assert.Equal(8, item2Steps[0].Quantity);
            Assert.Equal(80, item2Steps[0].TotalCost);
            Assert.Equal(10, item2Steps[0].UnitCost);
        }

        [Fact]
        public void UnpriceableCraftIngredients_BuyAvailable_FallsBackToBuy()
        {
            // Item 1: buy = 500. Craft needs item 2 which has no TP price.
            var tree = Craftable(1, 1,
                Option(10, 1, 1,
                    Leaf(2, 1)));
            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } }
                // Item 2 has no price
            };
            var solver = new PlanSolver();

            var plan = solver.Solve(tree, prices);

            // Should buy item 1 since craft is unpriceable
            Assert.Single(plan.Steps);
            var step = plan.Steps[0];
            Assert.Equal(1, step.ItemId);
            Assert.Equal(AcquisitionSource.BuyFromTp, step.Source);
            Assert.Equal(500, step.TotalCost);
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

            var plan = solver.Solve(tree, prices, null);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

            Assert.Equal(2, plan.Steps.Count);
            Assert.Contains(plan.Steps, s => s.Source == AcquisitionSource.Craft && s.ItemId == 1);
            Assert.Equal(200, plan.TotalCoinCost);
        }

        // --- Vendor offer tests ---

        private static VendorOffer CoinVendorOffer(int outputItemId, int coinCost, int outputCount = 1)
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
                Locations = new List<string> { "TestLoc" }
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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

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

            var plan = solver.Solve(tree, prices, vendorOffers);

            Assert.Single(plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromVendor, plan.Steps[0].Source);
            Assert.Equal(300, plan.TotalCoinCost);
        }
    }
}
