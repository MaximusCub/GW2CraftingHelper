using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// M35-B1 (gw2efficiency parity - multi-item plans): the synthetic
    /// wrapper pipeline (RecipeService.BuildMultiItemTreeAsync ->
    /// CraftingPlanPipeline.GenerateStructuredAsync(IReadOnlyList
    /// &lt;PlanRequestItem&gt;, ...) -> PlanSolver -> CraftingTreeBuilder),
    /// exercised end to end via the same InMemory fakes the rest of the
    /// pipeline test suite uses - real production code paths throughout,
    /// no Blish references.
    /// </summary>
    public class MultiItemPlanTests
    {
        [Fact]
        public async Task GenerateStructuredAsync_SingleEntryList_MatchesLegacySingleItemCall()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 5000, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var legacy = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            var viaList = await pipeline.GenerateStructuredAsync(
                new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } },
                null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // Same plan-level totals
            Assert.Equal(legacy.Plan.TotalCoinCost, viaList.Plan.TotalCoinCost);
            Assert.Equal(legacy.Plan.Steps.Count, viaList.Plan.Steps.Count);
            for (int i = 0; i < legacy.Plan.Steps.Count; i++)
            {
                Assert.Equal(legacy.Plan.Steps[i].ItemId, viaList.Plan.Steps[i].ItemId);
                Assert.Equal(legacy.Plan.Steps[i].Source, viaList.Plan.Steps[i].Source);
                Assert.Equal(legacy.Plan.Steps[i].Quantity, viaList.Plan.Steps[i].Quantity);
                Assert.Equal(legacy.Plan.Steps[i].TotalCost, viaList.Plan.Steps[i].TotalCost);
                Assert.Equal(legacy.Plan.Steps[i].RecipeId, viaList.Plan.Steps[i].RecipeId);
            }

            // Same result-model tree, one node at a time
            AssertTreesEqual(legacy.CraftingTree, viaList.CraftingTree);

            // Same derived required-disciplines/recipes
            Assert.Equal(legacy.RequiredDisciplines.Count, viaList.RequiredDisciplines.Count);
            Assert.Equal(legacy.RequiredRecipes.Count, viaList.RequiredRecipes.Count);

            // No wrapper metadata leaks into a single-item result reached
            // through the list overload - it short-circuited straight to
            // the untouched single-item method, exactly like gw2e's own
            // `if (r.length === 1) return r[0]`.
            Assert.Null(viaList.MultiItemRoots);
            Assert.Null(viaList.RequestedItems);
            Assert.NotNull(viaList.CraftingTree);
        }

        private static void AssertTreesEqual(CraftingTreeNode expected, CraftingTreeNode actual)
        {
            Assert.Equal(expected.ItemId, actual.ItemId);
            Assert.Equal(expected.NodeId, actual.NodeId);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Quantity, actual.Quantity);
            Assert.Equal(expected.Decision, actual.Decision);
            Assert.Equal(expected.SubtreeCost, actual.SubtreeCost);
            Assert.Equal(expected.UnitCost, actual.UnitCost);
            Assert.Equal(expected.RecipeId, actual.RecipeId);
            Assert.Equal(expected.Children.Count, actual.Children.Count);
            for (int i = 0; i < expected.Children.Count; i++)
            {
                AssertTreesEqual(expected.Children[i], actual.Children[i]);
            }
        }

        /// <summary>
        /// Two items each need 2 of a shared item (500) that is buyable
        /// ONLY from a bulk vendor offer (5 units for 20 coin, no TP
        /// price). Solved independently, each item's own ceil(2/5)=1
        /// purchase costs 20 coin, 40 total. Merged under the wrapper, the
        /// combined demand is 4 (2+2), ceil(4/5)=1 purchase - 20 coin total,
        /// not 40 - proving the M34 merge-then-ceil path
        /// (FinalizeVendorBatches) applies across item roots, not just
        /// within one item's own tree.
        /// </summary>
        [Fact]
        public async Task GenerateStructuredAsync_TwoItems_SharedBulkVendorMaterial_SingleCeilAcrossBoth()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(100, 110);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 110,
                OutputItemId = 100,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 500, Count = 2 }
                }
            });
            recipeApi.AddSearchResult(200, 210);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 210,
                OutputItemId = 200,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 500, Count = 2 }
                }
            });

            // No TP price for 100, 200 (force-craft, they have a recipe) or
            // for 500 (only the vendor offer prices it).
            var priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(100, "Item A", "a.png");
            itemApi.AddItem(200, "Item B", "b.png");
            itemApi.AddItem(500, "Shared Material", "m.png");

            var tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "GW2CraftingHelper_Tests_" + System.Guid.NewGuid());
            System.IO.Directory.CreateDirectory(tempDir);
            try
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "bulk-material",
                        OutputItemId = 500,
                        OutputCount = 5,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 20 }
                        },
                        MerchantName = "Test NPC",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store);

                // Contrast: solving each item alone costs 20 coin each (40 total).
                var alone100 = await pipeline.GenerateStructuredAsync(
                    100, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
                var alone200 = await pipeline.GenerateStructuredAsync(
                    200, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
                Assert.Equal(20, alone100.Plan.TotalCoinCost);
                Assert.Equal(20, alone200.Plan.TotalCoinCost);

                var items = new List<PlanRequestItem>
                {
                    new PlanRequestItem { ItemId = 100, Quantity = 1 },
                    new PlanRequestItem { ItemId = 200, Quantity = 1 }
                };

                var result = await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var vendorStep = Assert.Single(
                    result.Plan.Steps.Where(s => s.ItemId == 500 && s.Source == AcquisitionSource.BuyFromVendor));
                Assert.Equal(4, vendorStep.Quantity);
                Assert.Equal(20, vendorStep.TotalCost);
                Assert.Equal(20, result.Plan.TotalCoinCost);

                // Both craft steps present, merged into one plan.
                Assert.Contains(result.Plan.Steps, s => s.ItemId == 100 && s.Source == AcquisitionSource.Craft);
                Assert.Contains(result.Plan.Steps, s => s.ItemId == 200 && s.Source == AcquisitionSource.Craft);

                // The synthetic wrapper never surfaces anywhere in the result.
                Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == Gw2Constants.MultiItemWrapperItemId);
                Assert.Null(result.CraftingTree);
                Assert.NotNull(result.MultiItemRoots);
                Assert.Equal(2, result.MultiItemRoots.Count);
                Assert.All(result.MultiItemRoots, r => Assert.NotEqual(Gw2Constants.MultiItemWrapperItemId, r.ItemId));
                Assert.Equal(100, result.MultiItemRoots[0].ItemId);
                Assert.Equal(200, result.MultiItemRoots[1].ItemId);
                Assert.DoesNotContain(result.RequiredRecipes, r => r.OutputItemId == Gw2Constants.MultiItemWrapperItemId);

                Assert.NotNull(result.RequestedItems);
                Assert.Equal(2, result.RequestedItems.Count);
                Assert.Equal(100, result.RequestedItems[0].ItemId);
                Assert.Equal(200, result.RequestedItems[1].ItemId);
            }
            finally
            {
                System.IO.Directory.Delete(tempDir, true);
            }
        }

        private static CraftingPlanPipeline BuildTwoIndependentItemsPipeline(
            out InMemoryPriceApiClient priceApi)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // Item 300 <- recipe 310 <- item 301 (priced ingredient)
            recipeApi.AddSearchResult(300, 310);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 310,
                OutputItemId = 300,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 301, Count = 1 }
                }
            });
            // Item 400 <- recipe 410 <- item 401 (priced ingredient)
            recipeApi.AddSearchResult(400, 410);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 410,
                OutputItemId = 400,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 401, Count = 1 }
                }
            });

            priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(301, buyUnitPrice: 100, sellUnitPrice: 100);
            priceApi.AddPrice(401, buyUnitPrice: 200, sellUnitPrice: 200);
            // Both finished items ALSO have their own (much higher) TP
            // price, so craft (100 / 200) wins by default, but a manual
            // BuyFromTp override on either root is feasible and actually
            // changes its decision - needed to prove an override targeting
            // one root's NodeId is scoped correctly and does not leak into
            // the other root.
            priceApi.AddPrice(300, buyUnitPrice: 5000, sellUnitPrice: 5000);
            priceApi.AddPrice(400, buyUnitPrice: 5000, sellUnitPrice: 5000);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(300, "Item C", "c.png");
            itemApi.AddItem(301, "Ingredient C", "ic.png");
            itemApi.AddItem(400, "Item D", "d.png");
            itemApi.AddItem(401, "Ingredient D", "id.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        [Fact]
        public async Task ResolveWithOverrides_MultiItem_OverrideOnOneRootOnlyAffectsThatRoot()
        {
            var pipeline = BuildTwoIndependentItemsPipeline(out _);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 300, Quantity = 1 },
                new PlanRequestItem { ItemId = 400, Quantity = 1 }
            };

            var initial = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(CraftingDecision.Craft, initial.MultiItemRoots[0].Decision);
            Assert.Equal(CraftingDecision.Craft, initial.MultiItemRoots[1].Decision);

            // Force root B (item 400) to buy instead of craft; root A
            // (item 300) is untouched by this override.
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.MultiItemRoots[1].NodeId, AcquisitionSource.BuyFromTp }
            };

            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Equal(2, resolved.MultiItemRoots.Count);
            Assert.Equal(CraftingDecision.BuyFromTp, resolved.MultiItemRoots[1].Decision);
            Assert.Equal(CraftingDecision.Craft, resolved.MultiItemRoots[0].Decision);
            // Root A's own child ingredient is unaffected too - the override
            // only ever touched root B's NodeId.
            Assert.Equal(CraftingDecision.BuyFromTp, resolved.MultiItemRoots[0].Children[0].Decision);

            Assert.Null(resolved.CraftingTree);
            Assert.NotNull(resolved.RequestedItems);
            Assert.Equal(2, resolved.RequestedItems.Count);
        }

        [Fact]
        public async Task ResolveWithOverrides_MultiItem_IgnoredItemId_ZeroesCostAcrossBothRoots()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // Both items 300 and 400 share the SAME ingredient item 301.
            recipeApi.AddSearchResult(300, 310);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 310,
                OutputItemId = 300,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 301, Count = 2 }
                }
            });
            recipeApi.AddSearchResult(400, 410);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 410,
                OutputItemId = 400,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 301, Count = 3 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(301, buyUnitPrice: 100, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(300, "Item C", "c.png");
            itemApi.AddItem(400, "Item D", "d.png");
            itemApi.AddItem(301, "Shared Ingredient", "s.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 300, Quantity = 1 },
                new PlanRequestItem { ItemId = 400, Quantity = 1 }
            };

            var initial = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // 2x100 + 3x100 = 500 coin before Ignore.
            Assert.Equal(500, initial.Plan.TotalCoinCost);

            var ignored = new HashSet<int> { 301 };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null, ignored);

            // Ignoring item 301 tree-wide zeroes its cost under BOTH roots,
            // matching gw2e's "Ignore marks every occurrence of that item id,
            // tree-wide" semantics extended across multiple selected items.
            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            Assert.Empty(resolved.Plan.Steps.Where(s => s.ItemId == 301));
            Assert.True(resolved.MultiItemRoots[0].Children[0].IsIgnored);
            Assert.True(resolved.MultiItemRoots[1].Children[0].IsIgnored);
        }

        /// <summary>
        /// Item 600's own TP buy price is cheaper than crafting it, so a
        /// standalone single-item solve buys it instead of crafting -
        /// verifies the multi-item wrapper does not silently force-craft
        /// every selected root: each item root gets EXACTLY the same
        /// craft-vs-buy treatment it would get as a standalone tree (M35-B1
        /// item 5 - PlanSolver.Evaluate has no root-only special case at
        /// all, so this holds structurally, not via any new force-craft
        /// code).
        /// </summary>
        [Fact]
        public async Task GenerateStructuredAsync_MultiItem_PerRootDecision_MatchesStandaloneSingleItemSolve()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // Item 600: craft cost (5 x 100 = 500) is more expensive than
            // its own TP buy price (50) - standalone solve buys it.
            recipeApi.AddSearchResult(600, 610);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 610,
                OutputItemId = 600,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 601, Count = 5 }
                }
            });
            // Item 700: no TP price of its own - always force-crafts.
            recipeApi.AddSearchResult(700, 710);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 710,
                OutputItemId = 700,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 701, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(600, buyUnitPrice: 50, sellUnitPrice: 50);
            priceApi.AddPrice(601, buyUnitPrice: 100, sellUnitPrice: 100);
            priceApi.AddPrice(701, buyUnitPrice: 10, sellUnitPrice: 10);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(600, "Item E", "e.png");
            itemApi.AddItem(601, "Ingredient E", "ie.png");
            itemApi.AddItem(700, "Item F", "f.png");
            itemApi.AddItem(701, "Ingredient F", "if.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var standalone600 = await pipeline.GenerateStructuredAsync(
                600, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(CraftingDecision.BuyFromTp, standalone600.CraftingTree.Decision);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 600, Quantity = 1 },
                new PlanRequestItem { ItemId = 700, Quantity = 1 }
            };

            var batch = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(CraftingDecision.BuyFromTp, batch.MultiItemRoots[0].Decision);
            Assert.Equal(CraftingDecision.Craft, batch.MultiItemRoots[1].Decision);
        }
    }
}
