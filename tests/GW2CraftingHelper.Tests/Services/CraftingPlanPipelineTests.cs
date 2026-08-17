using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineTests
    {
        [Fact]
        public async Task SimpleCraftableItem_ProducesPlanWithStepsAndMetadata()
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
            Assert.NotNull(result.ItemMetadata);
            Assert.True(result.ItemMetadata.ContainsKey(1));
            Assert.Equal("Target Item", result.ItemMetadata[1].Name);
        }

        [Fact]
        public async Task LeafOnlyItem_ProducesSingleBuyStep()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 5, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(5, result.Plan.Steps[0].Quantity);
            Assert.True(result.ItemMetadata.ContainsKey(1));
        }

        [Fact]
        public async Task AllStepItemIds_HaveMetadataPopulated()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(3, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final Item", "final.png");
            itemApi.AddItem(2, "Part A", "a.png");
            itemApi.AddItem(3, "Part B", "b.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            foreach (var step in result.Plan.Steps)
            {
                Assert.True(result.ItemMetadata.ContainsKey(step.ItemId),
                    $"Missing metadata for item {step.ItemId}");
            }
        }

        // KNOWN-ISSUES api-degradation F4: a failing learned-recipes fetch
        // must degrade to null (the same supported "unknown known-recipe
        // status" state PlanResultBuilder already handles) rather than
        // aborting an otherwise fully-successful, fully-priced plan.
        [Fact]
        public async Task GenerateStructuredAsync_LearnedRecipeFetchFails_DegradesToNull_DoesNotAbortPlan()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var accountClient = new InMemoryAccountRecipeClient();
            accountClient.ThrowOnGet = true; // has permission by default, but the fetch itself fails

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                accountRecipeClient: accountClient);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The plan itself must be fully built despite the failure.
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);

            // Recipe 10 is not inherently-available (no MysticForge/
            // Achievement/Merchant discipline) and learnedRecipeIds is
            // null, so IsMissing must be null ("unknown"), not crash and
            // not silently claim the recipe is known.
            var recipe = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 10);
            Assert.NotNull(recipe);
            Assert.Null(recipe.IsMissing);
        }

        // KNOWN-ISSUES api-degradation F4 (audit follow-up): the same
        // catch-and-degrade-to-null fix above is duplicated verbatim in
        // GenerateStructuredMultiAsync (the 2+ item path reached via the
        // IReadOnlyList<PlanRequestItem> overload below); only the
        // single-item path above had a regression test. Mirrors that test
        // exactly, just with two roots, so a future refactor that
        // reverts/diverges the multi-item copy of this catch block fails a
        // test instead of silently aborting an otherwise fully-priced
        // multi-item plan.
        [Fact]
        public async Task GenerateStructuredMultiAsync_LearnedRecipeFetchFails_DegradesToNull_DoesNotAbortPlan()
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
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            var accountClient = new InMemoryAccountRecipeClient();
            accountClient.ThrowOnGet = true; // has permission by default, but the fetch itself fails

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                accountRecipeClient: accountClient);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var result = await pipeline.GenerateStructuredAsync(items, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // The multi-item plan itself must be fully built despite the
            // learned-recipe fetch failure.
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
            Assert.Equal(2, result.MultiItemRoots.Count);

            // Neither recipe is inherently-available and learnedRecipeIds
            // is null, so IsMissing must be null ("unknown") for both
            // roots' recipes, not crash and not silently claim either
            // recipe is known.
            var recipeA = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 10);
            var recipeB = result.RequiredRecipes.FirstOrDefault(r => r.RecipeId == 20);
            Assert.NotNull(recipeA);
            Assert.NotNull(recipeB);
            Assert.Null(recipeA.IsMissing);
            Assert.Null(recipeB.IsMissing);
        }

        [Fact]
        public async Task MissingItemMetadata_StillProducesValidPlan()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            // No metadata for item 1

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.False(result.ItemMetadata.ContainsKey(1));
        }

        [Fact]
        public async Task VendorOfferAvailable_SolverUsesIt()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            // TP price is 500
            priceApi.AddPrice(1, buyUnitPrice: 500, sellUnitPrice: 5000);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Vendor Item", "vendor.png");

            // Vendor offers 1x item for 100 coin - cheaper than TP
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-vendor",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 }
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

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                Assert.Equal(100, result.Plan.TotalCoinCost);
            }
        }

        // opportunity-notes (SEASONAL VENDOR TIP, review-fix finding 5):
        // real-path proof that SeasonalOfferFilter.ExcludeSeasonal is
        // actually wired into the SOLVE call site, not just unit-tested in
        // isolation (SeasonalOfferFilterTests). An item whose ONLY vendor
        // offer is seasonal must fall back to the TP, never BuyFromVendor
        // - and, with the festival active, the excluded offer should still
        // surface as a SeasonalVendorTips entry (the informational Notes
        // row this whole exclusion exists to make room for).
        [Fact]
        public async Task SeasonalOnlyVendorOffer_ExcludedFromSolve_FallsBackToTp_SurfacesAsTip()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            // TP price is 500
            priceApi.AddPrice(1, buyUnitPrice: 500, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Festival Item", "festival.png");

            // The ONLY vendor offer for item 1 is a seasonal (Halloween)
            // offer at 100 coin - cheaper than the 500 TP price, so if the
            // exclusion were NOT wired at this solve call site the solver
            // would wrongly pick BuyFromVendor here.
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-seasonal-vendor",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 100 }
                        },
                        MerchantName = "Candy Corn Vendor (Weekly)",
                        Locations = new List<string>(),
                        SeasonalFestival = Gw2Constants.HalloweenFestivalName
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    activeFestivalNames: () => new[] { Gw2Constants.HalloweenFestivalName });

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                // Never chosen as BuyFromVendor - the seasonal-only offer
                // was excluded from the solver's own candidate set.
                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
                Assert.Equal(500, result.Plan.TotalCoinCost);

                // Surfaces as an informational tip instead - the excluded
                // offer is still cheaper than the plan's own TP price.
                var tip = Assert.Single(result.SeasonalVendorTips);
                Assert.Equal(1, tip.ItemId);
                Assert.Equal(Gw2Constants.HalloweenFestivalName, tip.Festival);
                Assert.Equal(100, tip.OfferUnitCost);
                Assert.Equal(500, tip.PlanUnitPrice);
            }
        }

        [Fact]
        public async Task NullVendorStore_PipelineStillWorks()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                null);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.Plan);
            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
        }

        // --- W4B: vendor cost-component leaves, end-to-end through the real pipeline ---

        /// <summary>
        /// Real field case shape: a vendor-only item (no recipe, no TP
        /// price) whose winning offer mixes a TP-valued Item cost line
        /// (Globs of Ectoplasm, id 42) with a non-coin currency cost line
        /// (id 23) - 2 kinds, so CraftingTreeBuilder synthesizes component
        /// leaves. Proves the metadata-fetch widening (item 42's real name/
        /// icon resolve, not "Unknown Item"), the leaf synthesis itself
        /// through the full pipeline (not just the unit-level builder
        /// tests), and the parent/leaf consistency end to end.
        /// </summary>
        private static async Task<(CraftingPlanPipeline Pipeline, CraftingPlanResult Result)> GenerateMixedVendorPlanAsync(
            AccountSnapshot snapshot = null)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - vendor-only, matching the real
            // Amalgamated Rift Essence field case.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(42, buyUnitPrice: 10, sellUnitPrice: 20);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Amalgamated Rift Essence", "essence.png");
            itemApi.AddItem(42, "Glob of Ectoplasm", "ecto.png");

            CraftingPlanPipeline pipeline;
            CraftingPlanResult result;
            // Scoped like every other tmp-dir call site in this file -
            // VendorOfferStore loads everything it needs into memory
            // inside this block; nothing after GenerateStructuredAsync
            // returns re-reads the directory (ResolveWithOverrides reuses
            // the in-memory PlanSolveContext.VendorOffers captured here,
            // never the store itself again).
            using (var tmp = new TempDirectory())
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tmp.Path, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-mixed-w4b",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Item", Id = 42, Count = 5 },
                            new CostLine { Type = "Currency", Id = 23, Count = 3 }
                        },
                        MerchantName = "Test NPC",
                        Locations = new List<string>()
                    }
                });

                pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                result = await pipeline.GenerateStructuredAsync(1, 2, snapshot, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);
            }
            return (pipeline, result);
        }

        [Fact]
        public async Task MixedVendorOffer_SynthesizesLeavesWithRealMetadata()
        {
            var (_, result) = await GenerateMixedVendorPlanAsync();

            Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
            Assert.NotNull(result.CraftingTree);
            Assert.Equal(2, result.CraftingTree.Children.Count);

            var itemLeaf = result.CraftingTree.Children.Single(c => c.ItemId == 42);
            Assert.True(itemLeaf.IsCostComponent);
            // Metadata-fetch widening: item 42 is never a recipe-tree
            // ingredient (only a vendor CostLines entry), so its real
            // name/icon only resolves if AddVendorItemComponentIds worked.
            Assert.Equal("Glob of Ectoplasm", itemLeaf.Name);
            Assert.Equal("ecto.png", itemLeaf.IconUrl);
            Assert.Equal(10, itemLeaf.Quantity); // 5 * requested qty 2
            Assert.Equal(200, itemLeaf.SubtreeCost); // 10 * unit price 10

            var currencyLeaf = result.CraftingTree.Children.Single(c => c.ItemId == 23);
            Assert.True(currencyLeaf.IsCostComponent);
            Assert.Equal(6, currencyLeaf.Quantity);
            Assert.Null(currencyLeaf.SubtreeCost);

            // Parent total == the item leaf's exact gold value (no raw
            // coin, no other component in this offer).
            Assert.Equal(itemLeaf.SubtreeCost, result.CraftingTree.SubtreeCost);
        }

        [Fact]
        public async Task MixedVendorOffer_NoSnapshot_HavePillDataAbsent()
        {
            var (_, result) = await GenerateMixedVendorPlanAsync(snapshot: null);

            Assert.Null(result.SolveContext.OwnedVendorItemAmounts);
            var itemLeaf = result.CraftingTree.Children.Single(c => c.ItemId == 42);
            Assert.Equal(0, itemLeaf.ComponentOwnedQuantity);
        }

        [Fact]
        public async Task MixedVendorOffer_WithSnapshot_HavePillDataFlowsToLeaves()
        {
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 42, Count = 4, Source = AccountItemIndex.SourceMaterialStorage }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 23, Value = 999 }
                }
            };

            var (_, result) = await GenerateMixedVendorPlanAsync(snapshot);

            Assert.NotNull(result.SolveContext.OwnedVendorItemAmounts);
            Assert.Equal(4, result.SolveContext.OwnedVendorItemAmounts[42]);

            var itemLeaf = result.CraftingTree.Children.Single(c => c.ItemId == 42);
            Assert.Equal(4, itemLeaf.ComponentOwnedQuantity); // partial: need 10, own 4
            Assert.Equal(10, itemLeaf.Quantity); // unchanged by ownership

            var currencyLeaf = result.CraftingTree.Children.Single(c => c.ItemId == 23);
            Assert.Equal(999, currencyLeaf.ComponentOwnedQuantity); // raw holding (own 999, need 6) - never clamped
        }

        [Fact]
        public async Task MixedVendorOffer_ResolveWithOverrides_LeavesSurviveRoundTrip_StableIds()
        {
            var (pipeline, result) = await GenerateMixedVendorPlanAsync();
            var itemLeafBefore = result.CraftingTree.Children.Single(c => c.ItemId == 42);
            var currencyLeafBefore = result.CraftingTree.Children.Single(c => c.ItemId == 23);

            // A plain no-op re-solve (null overrides), exactly what a
            // decision-pill click on some OTHER node in a real plan would
            // trigger - proves the component leaves survive
            // ResolveWithOverrides' rebuild with the SAME NodeIds (so
            // TreeSectionController's expansion-state dictionary is not
            // silently orphaned).
            var resolved = pipeline.ResolveWithOverrides(result.SolveContext, null);

            Assert.NotNull(resolved.CraftingTree);
            Assert.Equal(2, resolved.CraftingTree.Children.Count);
            var itemLeafAfter = resolved.CraftingTree.Children.Single(c => c.ItemId == 42);
            var currencyLeafAfter = resolved.CraftingTree.Children.Single(c => c.ItemId == 23);

            Assert.Equal(itemLeafBefore.NodeId, itemLeafAfter.NodeId);
            Assert.Equal(currencyLeafBefore.NodeId, currencyLeafAfter.NodeId);
            Assert.Equal(itemLeafBefore.SubtreeCost, itemLeafAfter.SubtreeCost);
        }

        [Fact]
        public async Task MixedVendorOffer_BuildPresetOverrides_WalksSolverTreeOnly_UnaffectedByComponentLeaves()
        {
            // BuildPresetOverrides/CollectPresetOverrides walk
            // PlanSolveContext.Tree (RecipeNode/RecipeOption) - a
            // completely separate object graph from CraftingTreeNode/the
            // synthetic component leaves, which never correspond to any
            // RecipeNode at all. This proves it end to end: building a
            // preset override map and resolving with it neither throws nor
            // somehow keys an override off a negative synthetic NodeId (a
            // RecipeNode's own NodeId is always >= 0 - see RecipeNodeIds).
            var (pipeline, result) = await GenerateMixedVendorPlanAsync();

            var buyAll = CraftingPlanPipeline.BuildPresetOverrides(result.SolveContext, AcquisitionSource.BuyFromTp);
            Assert.All(buyAll.Keys, nodeId => Assert.True(nodeId >= 0));

            var resolved = pipeline.ResolveWithOverrides(result.SolveContext, buyAll);

            // Item 1 has no TP price at all, so the "buy all" preset cannot
            // apply to it (infeasible override is ignored) - it keeps its
            // vendor decision and its component leaves, proving the preset
            // build/resolve pass never disturbed them.
            Assert.Equal(AcquisitionSource.BuyFromVendor, resolved.Plan.Steps[0].Source);
            Assert.Equal(2, resolved.CraftingTree.Children.Count);
        }

        /// <summary>
        /// W4B review-fix (Must Fix): item 1's BASELINE decision is Craft
        /// (a cheap 1x item-2 recipe undercuts the vendor offer below), so
        /// the winning offer's item cost component (id 42, "Glob of
        /// Ectoplasm") is never scanned by the decisions-only
        /// AddVendorItemComponentIds overload - only
        /// AddAllVendorOfferItemComponentIds (scanning every vendorOffers
        /// entry, not just the winning decision) can widen metadata AND
        /// (the parallel Must Fix this test now also covers)
        /// BuildOwnedVendorItemComponentAmounts' ownership scan for it. A
        /// manual per-node override forcing item 1 to BuyFromVendor via
        /// ResolveWithOverrides - an ordinary, commonly-used interaction -
        /// then surfaces that item's component leaf; without the metadata
        /// fix it would render "Unknown Item"/null icon forever, and
        /// without the ownership fix it would show correct name/icon but
        /// NO have pill forever, even with the item sitting in the account
        /// (ResolveWithOverrides never re-fetches EITHER - see its own doc
        /// comment).
        ///
        /// W4B review-fix round 2 (Must Fix): the offer's non-coin Currency
        /// cost line (id 23) is the exact currency-side sibling of the
        /// item-side gap above - BuildOwnedCurrencyAmounts used to scope
        /// its ownership scan strictly to the baseline plan's aggregated
        /// CurrencyCosts, so a currency component surfaced only by this
        /// same override would get correct name/icon but NO have pill
        /// either, permanently, even with a full wallet. The wallet entry
        /// for currency 23 below proves BuildOwnedCurrencyAmounts's own
        /// AddAllVendorOfferCurrencyComponentIds widening now covers it the
        /// same way the item side already did.
        /// </summary>
        [Fact]
        public async Task MixedVendorOffer_NotBaselineWinner_ResolveWithOverrides_StillResolvesRealItemMetadataAndOwnership()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(2, buyUnitPrice: 1, sellUnitPrice: 1); // craft is cheap - the baseline winner
            priceApi.AddPrice(42, buyUnitPrice: 10, sellUnitPrice: 20);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Amalgamated Rift Essence", "essence.png");
            itemApi.AddItem(2, "Cheap Ingredient", "cheap.png");
            itemApi.AddItem(42, "Glob of Ectoplasm", "ecto.png");

            // Snapshot has 4 of item 42 in the account - partial coverage
            // of the 10 (5 * requested qty 2) the override's winning offer
            // will need. Attached at generation time (while the baseline
            // decision is still Craft, so item 42 never touches
            // decisions-scoped ownership either) to prove
            // BuildOwnedVendorItemComponentAmounts' widened vendorOffers
            // scan - not just AddVendorItemComponentIds' decisions scan -
            // is what puts 42 into PlanSolveContext.OwnedVendorItemAmounts.
            //
            // Wallet has 5 of currency 23 - full coverage of the 6 (3 *
            // requested qty 2) the override's winning offer will need for
            // that non-coin currency component, same "baseline decision is
            // Craft, so decisions-scoped ownership never sees it" setup as
            // item 42 above, proving BuildOwnedCurrencyAmounts' widened
            // vendorOffers scan populates PlanSolveContext.
            // OwnedCurrencyAmounts for it too.
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 42, Count = 4, Source = AccountItemIndex.SourceMaterialStorage }
                },
                Wallet = new List<SnapshotWalletEntry>
                {
                    new SnapshotWalletEntry { CurrencyId = 23, Value = 5 }
                }
            };

            CraftingPlanPipeline pipeline;
            CraftingPlanResult result;
            using (var tmp = new TempDirectory())
            {
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tmp.Path, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-not-baseline-w4b",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Item", Id = 42, Count = 5 },
                            new CostLine { Type = "Currency", Id = 23, Count = 3 }
                        },
                        MerchantName = "Test NPC",
                        Locations = new List<string>()
                    }
                });

                pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                result = await pipeline.GenerateStructuredAsync(1, 2, snapshot, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);
            }

            // Baseline: craft wins, so no component leaves exist yet, and
            // the winning decision never touched VendorItemCosts/
            // VendorCurrencyCosts at all - but OwnedVendorItemAmounts/
            // OwnedCurrencyAmounts must already carry item 42's and
            // currency 23's owned counts, both widened from vendorOffers
            // rather than decisions.
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            Assert.Empty(result.CraftingTree.Children.Where(c => c.IsCostComponent));
            Assert.NotNull(result.SolveContext.OwnedVendorItemAmounts);
            Assert.Equal(4, result.SolveContext.OwnedVendorItemAmounts[42]);
            Assert.NotNull(result.SolveContext.OwnedCurrencyAmounts);
            Assert.Equal(5, result.SolveContext.OwnedCurrencyAmounts[23]);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { result.CraftingTree.NodeId, AcquisitionSource.BuyFromVendor }
            };
            var resolved = pipeline.ResolveWithOverrides(result.SolveContext, overrides);

            Assert.Equal(CraftingDecision.BuyFromVendor, resolved.CraftingTree.Decision);
            var itemLeaf = resolved.CraftingTree.Children.Single(c => c.ItemId == 42);
            Assert.True(itemLeaf.IsCostComponent);
            Assert.Equal("Glob of Ectoplasm", itemLeaf.Name);
            Assert.Equal("ecto.png", itemLeaf.IconUrl);
            // The ownership fix under test: 4 owned out of 10 needed
            // (5 * requested qty 2) must survive the local re-solve, not
            // silently reset to 0 because this offer was never the
            // baseline winner.
            Assert.Equal(10, itemLeaf.Quantity);
            Assert.Equal(4, itemLeaf.ComponentOwnedQuantity);

            // W4B review-fix round 2: the currency-side twin of the item
            // assertion above - 5 owned out of 6 needed (3 * requested
            // qty 2) must equally survive the local re-solve, proving
            // BuildOwnedCurrencyAmounts' widened vendorOffers scan (not
            // just plan.CurrencyCosts) is what put currency 23 into
            // PlanSolveContext.OwnedCurrencyAmounts in the first place.
            var currencyLeaf = resolved.CraftingTree.Children.Single(c => c.ItemId == 23);
            Assert.True(currencyLeaf.IsCostComponent);
            Assert.Equal(6, currencyLeaf.Quantity);
            Assert.Equal(5, currencyLeaf.ComponentOwnedQuantity);
            // Cost cell deliberately blank for currency components (repo
            // invariant restated in BuildVendorCostComponentLeaves' doc
            // comment) - unaffected by this round's ownership fix.
            Assert.Null(currencyLeaf.SubtreeCost);
        }

        // M38 WP-14: this test used to prove the (now-deleted, test-only)
        // GenerateAsync produced the same base plan as GenerateStructuredAsync
        // with a null snapshot, plus the latter's extra structured fields.
        // With only one entry point left, the assertion intent becomes:
        // GenerateStructuredAsync itself, with no snapshot, still produces
        // the expected craft-vs-buy plan (same economics as before - craft
        // via 3x item 2 at 300 total beats buying item 1 outright at 10000)
        // and still populates its structured-only fields on a snapshot-free
        // run.
        [Fact]
        public async Task GenerateStructuredAsync_NullSnapshot_ProducesCraftPlanAndPopulatesStructuredFields()
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
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
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

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Craft (3 x 100 = 300, InstantBuy basis) beats buying item 1
            // outright (10000): one craft step for item 1, one buy step for
            // its 3x item 2 ingredient.
            Assert.Equal(2, result.Plan.Steps.Count);
            var craftStep = result.Plan.Steps.FirstOrDefault(s => s.ItemId == 1);
            Assert.NotNull(craftStep);
            Assert.Equal(AcquisitionSource.Craft, craftStep.Source);
            var buyStep = result.Plan.Steps.FirstOrDefault(s => s.ItemId == 2);
            Assert.NotNull(buyStep);
            Assert.Equal(AcquisitionSource.BuyFromTp, buyStep.Source);
            Assert.Equal(3, buyStep.Quantity);

            // Structured result has extra fields populated
            Assert.NotNull(result.RequiredDisciplines);
            Assert.NotNull(result.RequiredRecipes);
            Assert.NotNull(result.DebugLog);
            Assert.Empty(result.UsedMaterials);
        }

        [Fact]
        public async Task GenerateStructuredAsync_WithSnapshot_ReducesTree()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 }
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

            // Snapshot owns 3 of ingredient (item 2)
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 3, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var withoutSnapshot = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            var withSnapshot = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // With snapshot should buy fewer of item 2
            var buyStepWithout = withoutSnapshot.Plan.Steps
                .FirstOrDefault(s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp);
            var buyStepWith = withSnapshot.Plan.Steps
                .FirstOrDefault(s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp);

            Assert.NotNull(buyStepWithout);
            Assert.Equal(5, buyStepWithout.Quantity);
            Assert.NotNull(buyStepWith);
            Assert.Equal(2, buyStepWith.Quantity); // 5 - 3 = 2

            // UsedMaterials should report the 3 consumed
            Assert.Single(withSnapshot.UsedMaterials);
            Assert.Equal(2, withSnapshot.UsedMaterials[0].ItemId);
            Assert.Equal(3, withSnapshot.UsedMaterials[0].QuantityUsed);
        }

        // --- W3C review-fix (mustFix): zero prior coverage on the pipeline
        // wiring that carries AccountSnapshot.CharacterDisciplines through
        // to CraftingPlanResult/PlanSolveContext and back out again through
        // a local ResolveWithOverrides re-solve - only the leaf builder
        // (PlanResultBuilderTests) and the store (SnapshotStoreTests) had
        // coverage; the snapshot -> result -> re-solve wiring that makes
        // the feature appear at all was unverified. ---

        [Fact]
        public async Task GenerateStructuredAsync_WithCharacterDisciplines_CarriesIntoResultAndContext()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
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
                new ItemMetadataService(itemApi));

            var snapshot = new AccountSnapshot
            {
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Weaponsmith", Rating = 500, Active = true }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.CharacterDisciplines);
            Assert.Single(result.CharacterDisciplines);
            Assert.Equal("Anna", result.CharacterDisciplines[0].CharacterName);
            Assert.NotNull(result.SolveContext);
            Assert.Same(result.CharacterDisciplines, result.SolveContext.CharacterDisciplines);
        }

        // Adversarial-review fix (#7, source-selection-simplification
        // design-law gap): real pipeline round-trip (recipe API -> solve
        // -> CraftingTreeBuilder -> CompetencyOpportunityCalculator),
        // proving the whole CraftExcludedByCompetency threading actually
        // reaches CraftingPlanResult.CompetencyOpportunities end-to-end,
        // not just the isolated calculator unit coverage in
        // CompetencyOpportunityCalculatorTests.
        [Fact]
        public async Task GenerateStructuredAsync_CraftExcludedByCompetency_PopulatesCompetencyOpportunities()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
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
                new ItemMetadataService(itemApi));

            var snapshot = new AccountSnapshot
            {
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    // Untrained relative to the recipe's MinRating 400 -
                    // craft (10c) is excluded from the automatic pick even
                    // though far cheaper than the TP buy (5000c).
                    new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Weaponsmith", Rating = 100, Active = true }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            var targetStep = Assert.Single(result.Plan.Steps, s => s.ItemId == 1);
            Assert.Equal(AcquisitionSource.BuyFromTp, targetStep.Source);
            Assert.NotNull(result.CompetencyOpportunities);
            var opportunity = Assert.Single(result.CompetencyOpportunities);
            Assert.Equal(1, opportunity.ItemId);
            Assert.Equal(targetStep.TotalCost - opportunity.CraftCost, opportunity.DeltaCost);
            Assert.True(opportunity.DeltaCost > 0);
            Assert.Equal("Weaponsmith", Assert.Single(opportunity.Disciplines));
            Assert.Equal(400, opportunity.MinRating);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NullSnapshot_CharacterDisciplinesIsNull()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest possible leaf-only plan.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Null(result.CharacterDisciplines);
            Assert.Null(result.SolveContext.CharacterDisciplines);
        }

        [Fact]
        public async Task GenerateStructuredMultiAsync_WithCharacterDisciplines_CarriesIntoResultAndContext()
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
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var snapshot = new AccountSnapshot
            {
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Bob", Discipline = "Chef", Rating = 300, Active = false }
                }
            };

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var result = await pipeline.GenerateStructuredAsync(items, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.CharacterDisciplines);
            Assert.Single(result.CharacterDisciplines);
            Assert.Equal("Bob", result.CharacterDisciplines[0].CharacterName);
            Assert.NotNull(result.SolveContext);
            Assert.Same(result.CharacterDisciplines, result.SolveContext.CharacterDisciplines);
        }

        // --- W3C review-fix round 2 (mustFix): the explicit
        // characterDisciplines argument (see GenerateStructuredAsync's own
        // doc comment on that parameter) must feed PlanResultBuilder.Build's
        // discipline tiebreak on the list overload's SINGLE-item
        // short-circuit exactly like a non-null snapshot would - this is
        // the precise call shape Module.cs's useOwn:false branch uses
        // (snapshot: null, characterDisciplines: the real account list) to
        // keep the Required Disciplines row from silently reporting a
        // discipline the account doesn't have (and then rewriting itself on
        // the very next local override re-solve, once SolveContext started
        // carrying the real list forward). ---
        [Fact]
        public async Task GenerateStructuredAsync_ListOverload_NullSnapshotWithExplicitCharacterDisciplines_TiebreakPrefersAccountDiscipline()
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                // No single craft step elsewhere in the plan to seed a Pass
                // 1 preference - matches PlanResultBuilderTests.
                // RequiredDisciplines_MultiDisciplineRecipe_PrefersAccountDiscipline's
                // own setup, so a bare alphabetical fallback would report
                // "Armorsmith" here if the tiebreak never saw account data.
                Disciplines = new List<string> { "Armorsmith", "Leatherworker", "Tailor" },
                MinRating = 450
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
                new ItemMetadataService(itemApi));

            var accountDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Tailor", Rating = 500, Active = true }
            };

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 }
            };

            // snapshot: null (as Module.cs passes when "Use Own Materials"
            // is off) but characterDisciplines explicitly supplied - the
            // exact shape of the bug this test guards against.
            var result = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                characterDisciplines: accountDisciplines);

            Assert.Single(result.RequiredDisciplines);
            Assert.Equal("Tailor", result.RequiredDisciplines[0].Discipline);
            Assert.Same(accountDisciplines, result.CharacterDisciplines);
            Assert.Same(accountDisciplines, result.SolveContext.CharacterDisciplines);

            // The bug this guards against: a local override re-solve used
            // to see a DIFFERENT (newly non-null) CharacterDisciplines than
            // the initial Build() call did, silently changing the reported
            // discipline. Since SolveContext already carries the correct
            // list from generation time, a no-op re-solve must report the
            // identical discipline, not "discover" Tailor for the first
            // time here.
            var resolved = pipeline.ResolveWithOverrides(result.SolveContext, null);
            Assert.Single(resolved.RequiredDisciplines);
            Assert.Equal("Tailor", resolved.RequiredDisciplines[0].Discipline);
        }

        [Fact]
        public async Task GenerateStructuredMultiAsync_NullSnapshotWithExplicitCharacterDisciplines_TiebreakPrefersAccountDiscipline()
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
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                },
                Disciplines = new List<string> { "Armorsmith", "Leatherworker", "Tailor" },
                MinRating = 450
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var accountDisciplines = new List<SnapshotCharacterDiscipline>
            {
                new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Tailor", Rating = 500, Active = true }
            };

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var result = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                characterDisciplines: accountDisciplines);

            Assert.Contains(result.RequiredDisciplines, d => d.Discipline == "Tailor");
            Assert.DoesNotContain(result.RequiredDisciplines, d => d.Discipline == "Armorsmith" || d.Discipline == "Leatherworker");
            Assert.Same(accountDisciplines, result.CharacterDisciplines);
            Assert.Same(accountDisciplines, result.SolveContext.CharacterDisciplines);
        }

        [Fact]
        public async Task GenerateStructuredAsync_OwnedIntermediate_RemovesCraftStep_And_Discipline()
        {
            var recipeApi = new InMemoryRecipeApiClient();

            // Item 1 -> recipe 10 (Weaponsmith 500) -> item 2
            // Item 2 -> recipe 20 (Armorsmith 400) -> item 3
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500,
                Flags = new List<string> { "AutoLearned" }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Armorsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50000, sellUnitPrice: 100000);
            priceApi.AddPrice(2, buyUnitPrice: 10000, sellUnitPrice: 50000);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final", "f.png");
            itemApi.AddItem(2, "Intermediate", "m.png");
            itemApi.AddItem(3, "Raw Mat", "r.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            // Own item 2 - the intermediate craftable
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Item 2's Craft step (recipe 20) should be gone
            Assert.DoesNotContain(result.Plan.Steps,
                s => s.RecipeId == 20 && s.Source == AcquisitionSource.Craft);

            // Item 3's buy step should also be gone (no longer needed)
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 3);

            // Armorsmith discipline should NOT be required (recipe 20 pruned)
            Assert.DoesNotContain(result.RequiredDisciplines,
                d => d.Discipline == "Armorsmith");

            // Recipe 20 should NOT be in required recipes
            Assert.DoesNotContain(result.RequiredRecipes, r => r.RecipeId == 20);

            // Weaponsmith discipline SHOULD still be required (recipe 10 still needed)
            Assert.Contains(result.RequiredDisciplines,
                d => d.Discipline == "Weaponsmith");

            // Recipe 10 SHOULD still be in required recipes
            Assert.Contains(result.RequiredRecipes, r => r.RecipeId == 10);

            // UsedMaterials should report item 2 consumed
            Assert.Contains(result.UsedMaterials,
                u => u.ItemId == 2 && u.QuantityUsed == 1);
        }

        [Fact]
        public async Task GenerateStructuredAsync_UsedMaterialIds_HaveMetadata()
        {
            var recipeApi = new InMemoryRecipeApiClient();

            // Item 1 -> recipe 10 -> item 2 (intermediate) -> recipe 20 -> item 3
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 500
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50000, sellUnitPrice: 100000);
            priceApi.AddPrice(2, buyUnitPrice: 10000, sellUnitPrice: 50000);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Final", "f.png");
            itemApi.AddItem(2, "Intermediate", "m.png");
            itemApi.AddItem(3, "Raw Mat", "r.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            // Own the intermediate item 2 - it gets pruned from steps but
            // should still have metadata for display in UsedMaterials section
            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 1, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // UsedMaterials includes item 2
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 2);

            // Item 2 should have metadata even though it's not in plan steps
            Assert.True(result.ItemMetadata.ContainsKey(2),
                "UsedMaterial item ID should have metadata populated");
            Assert.Equal("Intermediate", result.ItemMetadata[2].Name);
        }

        [Fact]
        public async Task GenerateStructuredAsync_DebugLogContainsTimingEntries()
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.DebugLog);

            // These 6 phase prefixes are shared with (were originally pinned
            // against) the now-deleted GenerateAsync and must still appear
            // with timing (M38 WP-10: the dead "Resolve vendor offers" step
            // was removed along with the always-null VendorOfferResolver
            // seam); GenerateStructuredAsync's own additional phases
            // (Inventory reduction, Fetch currency metadata, Fetch learned
            // recipes, Build result) are a superset and not asserted here.
            var expectedPrefixes = new[]
            {
                "Build recipe tree",
                "Collect item IDs",
                "Fetch TP prices",
                "Query vendor offers",
                "Solve",
                "Fetch item metadata"
            };

            var timingPattern = new Regex(@"\d+ms");

            foreach (var prefix in expectedPrefixes)
            {
                var match = result.DebugLog.FirstOrDefault(
                    line => line.StartsWith(prefix) && timingPattern.IsMatch(line));
                Assert.True(match != null,
                    $"DebugLog missing timing entry for phase '{prefix}'. "
                    + $"Entries: [{string.Join(", ", result.DebugLog)}]");
            }

            // Timing summary block must be present
            Assert.Contains(result.DebugLog,
                line => line == "--- Timing Summary ---");
        }

        [Fact]
        public async Task GenerateStructuredAsync_ReportsProgressForEachPhase()
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
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
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

            var progress = new CapturingProgress<PlanStatus>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, progress,
                priceBasis: PriceBasis.InstantBuy);

            // All 9 expected phase messages in pipeline order
            // (M38 WP-10: the dead "Resolving vendor offers..." message was
            // removed along with the always-null VendorOfferResolver seam)
            var expectedSubstrings = new[]
            {
                "recipe tree",
                "Collecting item IDs",
                "Fetching prices",
                "Looking up vendor offers",
                "Reducing inventory",
                "Solving crafting plan",
                "Fetching item details",
                "Checking learned recipes",
                "Building final result"
            };

            Assert.True(progress.Reports.Count >= expectedSubstrings.Length,
                $"Expected >= {expectedSubstrings.Length} progress reports, "
                + $"got {progress.Reports.Count}: "
                + $"[{string.Join(", ", progress.Reports.Select(r => r.Message))}]");

            // Verify each expected substring appears in order
            int searchFrom = 0;
            foreach (var expected in expectedSubstrings)
            {
                int found = -1;
                for (int i = searchFrom; i < progress.Reports.Count; i++)
                {
                    if (progress.Reports[i].Message != null
                        && progress.Reports[i].Message.Contains(expected))
                    {
                        found = i;
                        break;
                    }
                }
                Assert.True(found >= 0,
                    $"Progress message containing '{expected}' not found at or after index {searchFrom}. "
                    + $"Reports: [{string.Join(", ", progress.Reports.Select(r => r.Message))}]");
                searchFrom = found + 1;
            }
        }

        // --- W3B: generation progress + rich logging ---

        [Fact]
        public async Task GenerateStructuredAsync_ReportsPhaseEventsInOrderWithSanePayloads()
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
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
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

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy, phaseProgress: phaseProgress);

            var expectedOrder = new[]
            {
                PlanPhase.BuildingTree,
                PlanPhase.FetchingPrices,
                PlanPhase.SolvingDecisions,
                PlanPhase.FetchingItemDetails,
                PlanPhase.BuildingDisplay
            };

            Assert.Equal(expectedOrder.Length, phaseProgress.Reports.Count);
            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.Equal(expectedOrder[i], phaseProgress.Reports[i].Phase);
                Assert.False(string.IsNullOrEmpty(phaseProgress.Reports[i].DisplayName));
                // Phase-level granularity only in v1 - no per-item Done
                // count on any event (see PlanPhaseEvent.Done's own doc
                // comment).
                Assert.Null(phaseProgress.Reports[i].Done);
            }

            // FetchingPrices/FetchingItemDetails know an up-front item
            // count; the other three phases do not.
            Assert.True(phaseProgress.Reports[1].Total > 0);
            Assert.True(phaseProgress.Reports[3].Total > 0);
            Assert.Null(phaseProgress.Reports[0].Total);
            Assert.Null(phaseProgress.Reports[2].Total);
            Assert.Null(phaseProgress.Reports[4].Total);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NullPhaseProgress_BehavesIdenticallyToOmitted()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var withOmittedParam = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            var withExplicitNull = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                phaseProgress: null);

            Assert.Equal(withOmittedParam.Plan.TotalCoinCost, withExplicitNull.Plan.TotalCoinCost);
            Assert.Equal(withOmittedParam.Plan.Steps.Count, withExplicitNull.Plan.Steps.Count);
            Assert.Equal(withOmittedParam.CraftingProfit, withExplicitNull.CraftingProfit);
            Assert.Equal(withOmittedParam.NetSaleValue, withExplicitNull.NetSaleValue);
        }

        [Fact]
        public async Task GenerateStructuredMultiAsync_ReportsPhaseEventsInOrder()
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
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                phaseProgress: phaseProgress);

            var expectedOrder = new[]
            {
                PlanPhase.BuildingTree,
                PlanPhase.FetchingPrices,
                PlanPhase.SolvingDecisions,
                PlanPhase.FetchingItemDetails,
                PlanPhase.BuildingDisplay
            };
            Assert.Equal(expectedOrder.Length, phaseProgress.Reports.Count);
            for (int i = 0; i < expectedOrder.Length; i++)
            {
                Assert.Equal(expectedOrder[i], phaseProgress.Reports[i].Phase);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_WritesRichModuleLogEntries_IntoRealTempDirStore()
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                // Isolated instance (not ModuleLog.Shared) - see ModuleLog's
                // own class doc comment on why Shared is unsuitable for
                // exact-count/content assertions.
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);
                // Debug entries only reach the file sink when this is true
                // (see ModuleLog.ShouldWriteToFile) - the per-phase Debug
                // lines this test asserts on need it.
                log.DiagnosticsEnabled = true;

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Orrax Manifested x1");

                // The file-sink append happens on a background flush queue
                // (never on the calling thread) - only guaranteed to have
                // landed once this returns true.
                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                // Info on start: real item name + quantity, never an
                // internal item id.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for Orrax Manifested x1");

                // Debug: one bounded entry per phase as it completes
                // (timing + counts where known) - exactly 5, matching
                // PlanPhase's 5 values, no per-item spam.
                var phaseDebugEntries = entries
                    .Where(e => e.Level == ModuleLogLevel.Debug && e.Tag == "plan")
                    .ToList();
                Assert.Equal(5, phaseDebugEntries.Count);
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building recipe tree:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching prices:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Solving decisions:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching item details:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building display:") && e.Message.Contains("ms"));

                // Info on finish: one compact per-phase summary line,
                // naming the plan by the same label the start line used.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Orrax Manifested x1: tree ")
                    && e.Message.Contains("prices ") && e.Message.Contains("solve ")
                    && e.Message.Contains("item details ") && e.Message.Contains("display ")
                    && e.Message.Contains(" - total "));

                // Every entry this run wrote used the "plan" category, per
                // the milestone's own rich-logging contract.
                Assert.All(entries, e => Assert.Equal("plan", e.Tag));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_MultiItem_WritesRichModuleLogEntries_IntoRealTempDirStore()
        {
            // W3B review-fix: the 1-item rich-ModuleLog test above only
            // exercises the list overload's single-entry short-circuit (see
            // GenerateStructuredAsync's own doc comment), which delegates
            // straight to the untouched single-item overload - this covers
            // the GENUINE 2+ item multi-item path
            // (GenerateStructuredMultiAsync) end to end against a real
            // ModuleLog + ModuleLogStore in a temp dir, mirroring
            // GenerateStructuredMultiAsync_ReportsPhaseEventsInOrder's own
            // fakes above.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 1 }
                }
            });
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 4, Count = 1 }
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 60, sellUnitPrice: 1200);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(4, buyUnitPrice: 20, sellUnitPrice: 200);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item A", "targeta.png");
            itemApi.AddItem(2, "Target Item B", "targetb.png");
            itemApi.AddItem(3, "Ingredient A", "ingredienta.png");
            itemApi.AddItem(4, "Ingredient B", "ingredientb.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);
                log.DiagnosticsEnabled = true;

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem>
                {
                    new PlanRequestItem { ItemId = 1, Quantity = 1 },
                    new PlanRequestItem { ItemId = 2, Quantity = 1 }
                };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Target Item A x1, Target Item B x1");

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                // Info on start: the real multi-item label, never an
                // internal item id or the "(N items)" fallback wording.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for Target Item A x1, Target Item B x1");

                // Debug: one bounded entry per phase as it completes -
                // exactly 5, same as the single-item path, confirming the
                // multi-item branch drives the SAME PhaseTracker.
                var phaseDebugEntries = entries
                    .Where(e => e.Level == ModuleLogLevel.Debug && e.Tag == "plan")
                    .ToList();
                Assert.Equal(5, phaseDebugEntries.Count);
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building recipe tree:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching prices:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Solving decisions:") && e.Message.Contains("ms"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Fetching item details:") && e.Message.Contains("items"));
                Assert.Contains(phaseDebugEntries, e => e.Message.StartsWith("Building display:") && e.Message.Contains("ms"));

                // Info on finish: the compact per-phase summary line, named
                // by the same multi-item label the start line used.
                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Target Item A x1, Target Item B x1: tree ")
                    && e.Message.Contains("prices ") && e.Message.Contains("solve ")
                    && e.Message.Contains("item details ") && e.Message.Contains("display ")
                    && e.Message.Contains(" - total "));

                Assert.All(entries, e => Assert.Equal("plan", e.Tag));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_NoRequestLabel_FallsBackToItemCountWording()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                // No requestLabel supplied - matches every pre-W3B caller
                // (and any future non-UI caller) that bypasses
                // CraftingPlanView's item-name resolution.
                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message == "Generating plan for 1 item");
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_List_FinishSummary_IncludesWallClockTotalDistinctFromPhaseSum()
        {
            // W3B review-fix: the finish summary's "total" used to be the
            // SUM of the raw per-step timing lines, which necessarily
            // excludes every un-instrumented gap between them and so
            // silently under-reports the wall-clock duration a field
            // tester actually experiences. It must now show the wrapper's
            // own Stopwatch elapsed time as "total", with the phase sum
            // appended alongside as "(phases Nms)" - see
            // PlanPhaseTimingSummary.FormatCompactSummary's own doc
            // comment.
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

                await pipeline.GenerateStructuredAsync(
                    items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    requestLabel: "Target x1");

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                var finishEntry = entries.Single(e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.StartsWith("Plan for Target x1:"));

                Assert.Contains(" - total ", finishEntry.Message);
                // The phase sum is now a parenthetical alongside the real
                // wall-clock total, never the total itself.
                Assert.Contains("ms (phases ", finishEntry.Message);
                Assert.EndsWith("ms)", finishEntry.Message);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_RecipeDiscoveryDiagnostic_ReachesModuleLog_EvenWithNullPlanStatusProgress()
        {
            // W3B review-fix: CraftingPlanView now passes progress: null
            // (IProgress<PlanStatus>) on every real Generate click - the
            // coarse phase events replace PlanStatus for the live status
            // strip. RecipeService.OnStatusUpdate's "first run" diagnostic
            // must still reach ModuleLog in that case instead of being
            // silently lost. A fresh RecipeService's default
            // InMemoryRecipeCacheStore starts empty, so the very first
            // search deterministically misses (SearchMisses > SearchHits),
            // which is exactly the condition RecipeService.PreWarmCacheAsync
            // uses to report this message.
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            using (var tmp = new TempDirectory())
            {
                var store = new ModuleLogStore(tmp.Path);
                var log = new ModuleLog();
                log.Configure(store, maxFileSizeBytes: 0, onStoreError: null);

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    moduleLog: log);

                await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, progress: null,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.True(log.WaitForPendingFileWrites(TimeSpan.FromSeconds(5)));
                var entries = store.ReadAll();

                Assert.Contains(entries, e =>
                    e.Level == ModuleLogLevel.Info && e.Tag == "plan"
                    && e.Message.Contains("Discovering recipes from API"));
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_BuildingTreePhaseEvent_CarriesFirstRunHintAsDetail()
        {
            // W3B review-fix: the pre-W3B "(may take several seconds on
            // first run)" PlanStatus hint is now unreachable once the view
            // passes progress: null - it must still surface somewhere live,
            // via PlanPhaseEvent.Detail on the BuildingTree event (see
            // CraftingPlanView.FormatPhaseText).
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var phaseProgress = new CapturingProgress<PlanPhaseEvent>();

            await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy, phaseProgress: phaseProgress);

            var treeEvent = phaseProgress.Reports.Single(r => r.Phase == PlanPhase.BuildingTree);
            Assert.False(string.IsNullOrEmpty(treeEvent.Detail));
            Assert.Contains("first run", treeEvent.Detail);

            // Every OTHER phase carries no Detail - reserved for the
            // BuildingTree first-run hint only (v1 scope).
            foreach (var report in phaseProgress.Reports)
            {
                if (report.Phase != PlanPhase.BuildingTree)
                {
                    Assert.Null(report.Detail);
                }
            }
        }

        // --- Sell-side economics ---

        private static CraftingPlanPipeline BuildEconomicsPipeline(
            out InMemoryPriceApiClient priceApi)
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
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));
        }

        [Fact]
        public async Task VendorOfferItemCost_OutsideTree_PriceFetchedAndOfferUsed()
        {
            // Regression (Gift of Glory): a vendor offer charging an ITEM
            // that appears nowhere in the recipe tree was skipped as
            // unpriceable because the cost item's TP price was never
            // fetched, leaving the target as UnknownSource.
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe and no TP price for target item 1
            var priceApi = new InMemoryPriceApiClient();
            // Cost item 999 (not in any tree) has a TP price of 2c
            priceApi.AddPrice(999, buyUnitPrice: 1, sellUnitPrice: 2);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Gifted Item", "g.png");
            itemApi.AddItem(999, "Cost Token", "t.png");

            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-item-cost-outside-tree",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Item", Id = 999, Count = 250 }
                        },
                        MerchantName = "Token Vendor",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store);

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                // 250 x 2c (instant-buy basis) = 500
                Assert.Equal(500, result.Plan.TotalCoinCost);
            }
        }

        [Fact]
        public async Task Structured_TargetHasBuyOrders_ProfitFieldsComputed()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            // Target: buy orders at 400 (sell revenue), sell listings at 1000.
            // Ingredient: instant buy 100 -> craft cost 3x100=300 < buy 1000.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(300, result.Plan.TotalCoinCost);
            Assert.Equal(400, result.TargetUnitSellPrice);
            // 400 - 20 (5%) - 40 (10%) = 340 net
            Assert.Equal(340, result.NetSaleValue);
            Assert.Equal(40, result.CraftingProfit);
            Assert.Equal(PriceBasis.InstantBuy, result.PriceBasis);
        }

        [Fact]
        public async Task Structured_NoBuyOrders_ProfitFieldsNull()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 0, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Null(result.TargetUnitSellPrice);
            Assert.Null(result.NetSaleValue);
            Assert.Null(result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_RootRecipeOverproduces_RevenueCoversWholeBatch()
        {
            // Recipe outputs 5 per craft; requesting 1 still costs a full
            // craft, so all 5 produced units count as sellable revenue.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 5,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 3 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 10000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");
            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            // Craft cost: 3x100 = 300 for a batch of 5
            Assert.Equal(300, result.Plan.TotalCoinCost);
            Assert.Equal(5, result.SellableQuantity);
            // Revenue: 5 x 400 = 2000 total; -100 listing -200 exchange = 1700
            Assert.Equal(1700, result.NetSaleValue);
            Assert.Equal(1400, result.CraftingProfit);
        }

        [Fact]
        public async Task ResolveWithOverrides_LocalResolveFlipsDecisionAndEconomics()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            // Craft (300) beats buy (1000); target sells to orders at 400.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.NotNull(initial.SolveContext);
            Assert.Equal(300, initial.Plan.TotalCoinCost);

            // Force the root to be bought instead
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Single(resolved.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, resolved.Plan.Steps[0].Source);
            Assert.Equal(1000, resolved.Plan.TotalCoinCost);
            // Economics recomputed: 340 net - 1000 cost = -660
            Assert.Equal(-660, resolved.CraftingProfit);
            // Context is carried forward for subsequent re-solves
            Assert.Same(initial.SolveContext, resolved.SolveContext);
            // Tree reflects the forced decision and availability
            Assert.Equal(CraftingDecision.BuyFromTp, resolved.CraftingTree.Decision);
            Assert.True(resolved.CraftingTree.CanCraft);
            Assert.True(resolved.CraftingTree.CanBuyTp);
        }

        // W3C review-fix (mustFix): a local override re-solve must keep
        // carrying CharacterDisciplines forward from the generation-time
        // context (see PlanSolveContext.CharacterDisciplines' own doc
        // comment) - deleting the one-line passthrough in
        // ResolveWithOverrides still leaves the whole suite green without
        // this test, since only the leaf builder and the store were
        // previously covered.
        [Fact]
        public async Task ResolveWithOverrides_CarriesCharacterDisciplinesForward()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var snapshot = new AccountSnapshot
            {
                CharacterDisciplines = new List<SnapshotCharacterDiscipline>
                {
                    new SnapshotCharacterDiscipline { CharacterName = "Anna", Discipline = "Weaponsmith", Rating = 500, Active = true }
                }
            };

            var initial = await pipeline.GenerateStructuredAsync(1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.NotNull(initial.CharacterDisciplines);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Same(initial.CharacterDisciplines, resolved.CharacterDisciplines);
        }

        // Companion null-snapshot case: a generation with no account data
        // must keep re-solving to a null CharacterDisciplines, never
        // fabricate one on a later override.
        [Fact]
        public async Task ResolveWithOverrides_NullSnapshot_CharacterDisciplinesStaysNull()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.Null(initial.CharacterDisciplines);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Null(resolved.CharacterDisciplines);
        }

        // Review nice-to-have (audit row 56 follow-up): CraftingPlanPipeline
        // assigns DailyCooldownItems at five hand-copied sites (mirroring
        // AcquisitionHints), none of which had a test pinning the seed
        // survives a GenerateStructuredAsync -> ResolveWithOverrides round
        // trip - a future refactor could silently drop one site. This
        // mirrors ResolveWithOverrides_CarriesCharacterDisciplinesForward's
        // shape immediately above, applied to the daily-cooldown seed
        // instead.
        [Fact]
        public async Task DailyCooldownItems_SurvivesGenerateStructuredAsync_AndResolveWithOverridesRoundTrip()
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
                }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target Item", "target.png");
            itemApi.AddItem(2, "Ingredient", "ingredient.png");

            var seed = new Dictionary<int, DailyCooldownItem>
            {
                [2] = new DailyCooldownItem { ItemId = 2, PerDayCap = 1 }
            };

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                vendorOfferStore: null,
                reducer: null,
                accountRecipeClient: null,
                currencyMetadataService: null,
                acquisitionHints: null,
                moduleLog: null,
                dailyCooldownItems: seed);

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            Assert.Same(seed, initial.DailyCooldownItems);

            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Same(seed, resolved.DailyCooldownItems);
        }

        [Fact]
        public async Task BuildPresetOverrides_CraftAll_ReachesNodesUnderBoughtIntermediates()
        {
            // Item 1 <- recipe(2) <- recipe(3). Intermediate 2 is cheap to
            // buy, so the best path hides node 3 below a bought node; the
            // Craft All preset must still force-craft both levels.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddSearchResult(2, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 1 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 2,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            var priceApi = new InMemoryPriceApiClient();
            // Buying 2 (50) beats crafting it (2x100=200); buying 1 (500)
            // loses to crafting-from-bought-2 (50)
            priceApi.AddPrice(1, buyUnitPrice: 10, sellUnitPrice: 500);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 50);
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 100);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Mid", "m.png");
            itemApi.AddItem(3, "Base", "b.png");
            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);
            // Baseline: craft 1 from bought 2 (50)
            Assert.Equal(50, initial.Plan.TotalCoinCost);

            var craftAll = CraftingPlanPipeline.BuildPresetOverrides(
                initial.SolveContext, AcquisitionSource.Craft);
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, craftAll);

            // Now both levels craft: cost = 2x100 for item 3
            Assert.Equal(200, resolved.Plan.TotalCoinCost);
            Assert.Contains(resolved.Plan.Steps,
                s => s.Source == AcquisitionSource.Craft && s.ItemId == 2);
            // Metadata must cover items surfaced only by the override
            // (regression: items under bought nodes were never fetched and
            // rendered as "Unknown Item")
            Assert.All(resolved.Plan.Steps,
                s => Assert.True(resolved.ItemMetadata.ContainsKey(s.ItemId)));

            // Buy All flips everything buyable to TP purchases
            var buyAll = CraftingPlanPipeline.BuildPresetOverrides(
                initial.SolveContext, AcquisitionSource.BuyFromTp);
            var bought = pipeline.ResolveWithOverrides(initial.SolveContext, buyAll);
            Assert.Single(bought.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, bought.Plan.Steps[0].Source);
        }

        [Fact]
        public async Task Structured_BuyOrderBasis_MaterialsCostedAtBuyOrders()
        {
            var pipeline = BuildEconomicsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            // Ingredient: instant 100, buy order 10 -> craft cost 30 at basis.
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.BuyOrder);

            Assert.Equal(30, result.Plan.TotalCoinCost);
            Assert.Equal(PriceBasis.BuyOrder, result.PriceBasis);
            Assert.Equal(310, result.CraftingProfit);
        }

        // --- Currency valuation threading ---

        [Fact]
        public async Task GenerateStructuredAsync_CurrencyValuation_ThreadsIntoSolverAndContext()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 2000);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Karma Item", "karma.png");

            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-karma-offer",
                        OutputItemId = 1,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = 2, Count = 50 }
                        },
                        MerchantName = "Karma Vendor",
                        Locations = new List<string>()
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                var valuation = new CurrencyValuation(new Dictionary<int, long> { { 2, 5 } });

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None,
                    currencyValuation: valuation,
                    priceBasis: PriceBasis.InstantBuy);

                // Vendor wins: 50 karma x 5 copper = 250 < 1000 TP
                Assert.Single(result.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, result.Plan.Steps[0].Source);
                Assert.Equal(0, result.Plan.Steps[0].TotalCost);
                Assert.Single(result.Plan.CurrencyCosts);
                Assert.Equal(2, result.Plan.CurrencyCosts[0].CurrencyId);
                Assert.Equal(50, result.Plan.CurrencyCosts[0].Amount);

                // The valuation is captured on the context for later local re-solves
                Assert.NotNull(result.SolveContext.CurrencyValuation);
                Assert.True(result.SolveContext.CurrencyValuation.TryGetCopperValue(2, out long copperPerUnit));
                Assert.Equal(5, copperPerUnit);

                // A subsequent local re-solve (no network calls, no overrides)
                // must keep using the valuation carried on the context.
                var resolved = pipeline.ResolveWithOverrides(result.SolveContext, null);
                Assert.Equal(AcquisitionSource.BuyFromVendor, resolved.Plan.Steps[0].Source);
                Assert.Single(resolved.Plan.CurrencyCosts);
                Assert.Equal(50, resolved.Plan.CurrencyCosts[0].Amount);
            }
        }

        [Fact]
        public async Task GenerateStructuredAsync_NoCurrencyValuationArgument_ContextDefaultsToNone()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.SolveContext.CurrencyValuation);
            Assert.False(result.SolveContext.CurrencyValuation.TryGetCopperValue(2, out _));
        }

        // --- M37 (KNOWN-ISSUES #24): Homestead Refinement efficiency tiers
        // are snapshotted on PlanSolveContext at generation time and reused
        // as-is by a local override re-solve, matching every other
        // settings-snapshot field on that class (CurrencyValuation,
        // OwnMaterialsMode, ...). ---

        [Fact]
        public async Task GenerateStructuredAsync_NoHomesteadTiersArgument_ContextDefaultsToTierZero()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Item", "icon.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.NotNull(result.SolveContext.HomesteadTiers);
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadFiberItemId));
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));
            Assert.Equal(0, result.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadWoodItemId));
        }

        [Fact]
        public async Task GenerateStructuredAsync_HomesteadTiersArgument_GatesVendorOfferAndSnapshotsOnContext()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var recipeApi = new InMemoryRecipeApiClient();
                var priceApi = new InMemoryPriceApiClient();
                priceApi.AddPrice(102205, buyUnitPrice: 1000, sellUnitPrice: 1000);
                var itemApi = new InMemoryItemApiClient();
                itemApi.AddItem(102205, "Refined Homestead Metal", "icon.png");

                var loader = new VendorOfferLoader();
                var store = new VendorOfferStore(tempDir, loader);
                store.LoadBaseline(null);
                store.AddOffersToOverlay(new[]
                {
                    new VendorOffer
                    {
                        OfferId = "test-homestead-tier0",
                        OutputItemId = 102205,
                        OutputCount = 2,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 400 }
                        },
                        MerchantName = "Homestead Refinement\u2014Metal Forge",
                        Locations = new List<string>(),
                        HomesteadTier = 0
                    },
                    new VendorOffer
                    {
                        OfferId = "test-homestead-tier2",
                        OutputItemId = 102205,
                        OutputCount = 1,
                        CostLines = new List<CostLine>
                        {
                            new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 1 }
                        },
                        MerchantName = "Homestead Refinement\u2014Metal Forge",
                        Locations = new List<string>(),
                        HomesteadTier = 2
                    }
                });

                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    store,
                    reducer: new InventoryReducer());

                // Default (no homesteadTiers argument): tier-2 offer excluded,
                // the far more expensive tier-0 offer is the only candidate.
                var defaultResult = await pipeline.GenerateStructuredAsync(
                    102205, 2, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
                Assert.Single(defaultResult.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, defaultResult.Plan.Steps[0].Source);
                Assert.Equal(400, defaultResult.Plan.Steps[0].TotalCost);

                // Tier 2 configured: the far cheaper tier-2 offer is admitted.
                var tier2 = new HomesteadEfficiencyTiers(new Dictionary<int, int>
                {
                    { Gw2Constants.RefinedHomesteadMetalItemId, 2 }
                });
                var tieredResult = await pipeline.GenerateStructuredAsync(
                    102205, 2, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy,
                    homesteadTiers: tier2);
                Assert.Single(tieredResult.Plan.Steps);
                Assert.Equal(AcquisitionSource.BuyFromVendor, tieredResult.Plan.Steps[0].Source);
                Assert.Equal(2, tieredResult.Plan.Steps[0].TotalCost);

                // Snapshotted on the context...
                Assert.NotNull(tieredResult.SolveContext.HomesteadTiers);
                Assert.Equal(2, tieredResult.SolveContext.HomesteadTiers.GetTier(Gw2Constants.RefinedHomesteadMetalItemId));

                // ...and reused as-is by a local override re-solve (no
                // network calls, no overrides) - matching CurrencyValuation's
                // own reuse contract.
                var resolved = pipeline.ResolveWithOverrides(tieredResult.SolveContext, null);
                Assert.Equal(AcquisitionSource.BuyFromVendor, resolved.Plan.Steps[0].Source);
                Assert.Equal(2, resolved.Plan.Steps[0].TotalCost);
            }
        }

        // --- Own-materials valuation (M28) ---

        private static CraftingPlanPipeline BuildOwnMaterialsPipeline(
            out InMemoryPriceApiClient priceApi, int ingredientCount = 5)
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
                    new RawIngredient { Type = "Item", Id = 2, Count = ingredientCount }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        private static AccountSnapshot OwnIngredient(int count)
        {
            return new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 2,
                        Count = count,
                        Source = AccountItemIndex.SourceMaterialStorage
                    }
                }
            };
        }

        [Fact]
        public async Task Structured_ValuedMode_DeductsMaterialOpportunityCostFromProfit()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            // Ingredient: SellInstant=10 (opportunity-cost basis), BuyInstant=100 (craft-cost basis).
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own 3 of the 5 needed; the other 2 are bought.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            // Craft cost: (5 - 3) x 100 = 200
            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Single(result.UsedMaterials);
            Assert.Equal(3, result.UsedMaterials[0].QuantityUsed);

            // Opportunity cost: selling 3 x 10c = 30 total; fees -2 (5%) -3 (10%) = 25 net.
            Assert.Equal(25, result.MaterialOpportunityCost);

            // Sell value (unchanged): 400 - 20 (5%) - 40 (10%) = 340
            Assert.Equal(340, result.NetSaleValue);
            // Profit: 340 - 200 (coin cost) - 25 (opportunity cost) = 115
            Assert.Equal(115, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_FreeMode_MaterialOpportunityCostNullAndProfitUnchanged()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Default mode (no ownMaterialsMode argument) - Free.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(200, result.Plan.TotalCoinCost);
            Assert.Null(result.MaterialOpportunityCost);
            // Profit unaffected by ownership: 340 - 200 = 140
            Assert.Equal(140, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_NoSnapshot_MaterialOpportunityCostNull()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Valued mode but nothing was reduced (no snapshot) - no owned
            // materials, so there is nothing to have forgone selling.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Empty(result.UsedMaterials);
            Assert.Null(result.MaterialOpportunityCost);
            // All 5 ingredients bought at 100 each = 500; profit = 340 - 500 = -160
            Assert.Equal(500, result.Plan.TotalCoinCost);
            Assert.Equal(-160, result.CraftingProfit);
        }

        [Fact]
        public async Task Structured_ValuedMode_UnsellableUsedMaterial_ContributesZero()
        {
            // Two ingredients are owned and consumed: item 2 is sellable,
            // item 3 has no buy orders (SellInstant 0) and must contribute
            // 0 to the opportunity cost rather than being skipped/erroring
            // or zeroing the whole sum.
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                    new RawIngredient { Type = "Item", Id = 3, Count = 4 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // sellable, SellInstant=10
            priceApi.AddPrice(3, buyUnitPrice: 0, sellUnitPrice: 50);   // unsellable, SellInstant=0

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Sellable Ingredient", "i.png");
            itemApi.AddItem(3, "Unsellable Ingredient", "j.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());

            var snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 5, Source = AccountItemIndex.SourceMaterialStorage },
                    new SnapshotItemEntry { ItemId = 3, Count = 4, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(2, result.UsedMaterials.Count);

            // Only item 2's 5 units count: 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            // Item 3 contributes 0 despite 4 units being used.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }

        [Fact]
        public async Task ResolveWithOverrides_PreservesOwnMaterialsMode()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Valued, initial.SolveContext.OwnMaterialsMode);
            Assert.Equal(25, initial.MaterialOpportunityCost);
            Assert.Equal(115, initial.CraftingProfit);

            // A no-op local re-solve must keep valuing owned materials the
            // same way the original Generate did (context-carried, like
            // CurrencyValuation).
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(25, resolved.MaterialOpportunityCost);
            Assert.Equal(115, resolved.CraftingProfit);
        }

        [Fact]
        public async Task GenerateStructuredAsync_NoOwnMaterialsModeArgument_ContextDefaultsToFree()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(OwnMaterialsMode.Free, result.SolveContext.OwnMaterialsMode);
            Assert.Null(result.MaterialOpportunityCost);
        }

        // --- M34-B2b: "Ignore" pill threaded through ResolveWithOverrides ---

        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000); // buying the target outright is far pricier - craft wins
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            // No snapshot: nothing owned via real reduction, so the baseline
            // craft cost is the full 5x100=500.
            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(500, initial.Plan.TotalCoinCost);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 2 });

            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            // Item 2 (the ignored ingredient) generates no step at all;
            // item 1 (the root) still crafts, now at zero cost.
            Assert.DoesNotContain(resolved.Plan.Steps, s => s.ItemId == 2);
            Assert.Contains(resolved.Plan.Steps, s => s.ItemId == 1 && s.Source == AcquisitionSource.Craft && s.TotalCost == 0);
            Assert.Equal(CraftingDecision.Have, resolved.CraftingTree.Children[0].Decision);
            Assert.True(resolved.CraftingTree.Children[0].IsIgnored);
        }

        [Fact]
        public async Task ResolveWithOverrides_NullIgnoredItemIds_BehavesExactlyAsBefore()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(500, resolved.Plan.TotalCoinCost);
            Assert.False(resolved.CraftingTree.Children[0].IsIgnored);
        }

        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_ManualOverrideOnSameNodeStillApplies()
        {
            // Ignore and the craft/buy override pill are documented as
            // orthogonal (r2 report Section 3.2) - overriding the ROOT to
            // BuyFromTp while its ingredient is separately ignored must
            // still switch the root to BuyFromTp; the two mechanisms key
            // off different things (NodeId vs ItemId) and must not collide.
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            int rootNodeId = initial.SolveContext.Tree.NodeId;

            var overrides = new Dictionary<int, AcquisitionSource> { { rootNodeId, AcquisitionSource.BuyFromTp } };
            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, overrides, new HashSet<int> { 2 });

            Assert.Equal(CraftingDecision.BuyFromTp, resolved.CraftingTree.Decision);
            Assert.Equal(20000, resolved.Plan.TotalCoinCost); // manual override wins on the root regardless of the sibling Ignore
        }

        [Fact]
        public async Task Structured_ValuedMode_UsedMaterialPrices_AlreadyCoveredByTreeFetch()
        {
            // Design assertion (see M28 spec): prices are fetched for
            // allItemIds, which is collected from the PRE-reduction tree
            // (Step 2 runs before Step 6's reduction), so every used
            // material - being a tree item that reduction happened to
            // remove - already has a price entry by the time
            // ApplySellSideEconomics runs. No separate fetch is needed for
            // MaterialOpportunityCost, and this test pins that: the used
            // material's price came from the ordinary tree price fetch.
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            // Own ALL of the required ingredient, so nothing is left to buy
            // for item 2 (any remaining step is a zero-quantity/zero-cost
            // placeholder) - its only real trace is UsedMaterials. If its
            // price had to be fetched specially for the opportunity-cost
            // calc rather than coming from the tree-wide fetch, this would
            // be null/0 instead of the expected net value.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(5), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 2 && s.Quantity > 0);
            Assert.Equal(5, result.UsedMaterials[0].QuantityUsed);

            // 5x10=50 total; fees -3 (5%) -5 (10%) = 42 net.
            Assert.Equal(42, result.MaterialOpportunityCost);
        }

        // --- M30 review: currency metadata wired through the pipeline ---

        private class StubCurrencyHandler : HttpMessageHandler
        {
            private readonly string _body;

            public StubCurrencyHandler(string body)
            {
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body)
                };
                return Task.FromResult(response);
            }
        }

        private const string CurrencySampleJson = @"[
            { ""id"": 2, ""name"": ""Karma"", ""icon"": ""https://render.guildwars2.com/file/karma.png"" }
        ]";

        [Fact]
        public async Task GenerateStructuredAsync_WithCurrencyMetadataService_PopulatesCurrencyMetadata()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest leaf-buy plan.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            using (var handler = new StubCurrencyHandler(CurrencySampleJson))
            using (var http = new HttpClient(handler))
            {
                var currencyService = new CurrencyMetadataService(http);
                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    currencyMetadataService: currencyService);

                var result = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);

                Assert.NotNull(result.CurrencyMetadata);
                Assert.True(result.CurrencyMetadata.ContainsKey(2));
                Assert.Equal("Karma", result.CurrencyMetadata[2].Name);
            }
        }

        [Fact]
        public async Task ResolveWithOverrides_PreservesCurrencyMetadataViaSolveContext()
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
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            // Craft (300) beats buy (1000), matching the existing override
            // test's economics so the override below actually flips a
            // real decision.
            priceApi.AddPrice(1, buyUnitPrice: 400, sellUnitPrice: 1000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            using (var handler = new StubCurrencyHandler(CurrencySampleJson))
            using (var http = new HttpClient(handler))
            {
                var currencyService = new CurrencyMetadataService(http);
                var pipeline = new CraftingPlanPipeline(
                    new RecipeService(recipeApi),
                    new TradingPostService(priceApi),
                    new PlanSolver(),
                    new ItemMetadataService(itemApi),
                    currencyMetadataService: currencyService);

                var initial = await pipeline.GenerateStructuredAsync(1, 1, null, CancellationToken.None,
                    priceBasis: PriceBasis.InstantBuy);
                Assert.NotNull(initial.CurrencyMetadata);
                Assert.True(initial.CurrencyMetadata.ContainsKey(2));
                Assert.NotNull(initial.SolveContext.CurrencyMetadata);

                var overrides = new Dictionary<int, AcquisitionSource>
                {
                    { initial.CraftingTree.NodeId, AcquisitionSource.BuyFromTp }
                };
                var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

                // The local re-solve is purely from the cached SolveContext
                // (no network calls) - CurrencyMetadata must still be there.
                Assert.NotNull(resolved.CurrencyMetadata);
                Assert.True(resolved.CurrencyMetadata.ContainsKey(2));
                Assert.Equal("Karma", resolved.CurrencyMetadata[2].Name);
            }
        }

        // --- M34-B2a #3: force-buy pre-pass (zero-owned baseline) ---

        /// <summary>
        /// Reuses BuildOwnMaterialsPipeline's identical tree shape (item 1
        /// &lt;- recipe 10 &lt;- 5x item 2), then sets prices for the
        /// force-buy scenario: NOTE InMemoryPriceApiClient's
        /// (buyUnitPrice, sellUnitPrice) map to raw GW2-API
        /// buys/sells.unit_price - TradingPostService then maps BuyInstant
        /// (cost to instant-BUY) from the RAW sellUnitPrice param, and
        /// SellInstant from the raw buyUnitPrice param (see
        /// TradingPostService.cs) - so the SECOND argument here is the one
        /// that drives GetUnitPrice at PriceBasis.InstantBuy.
        ///
        /// Fresh (zero-owned) check: buy(100) &lt; craft(5x30=150)*0.85=127.5
        /// -&gt; item 1 is force-buy-flagged on a truly zero-owned baseline.
        /// </summary>
        private static CraftingPlanPipeline BuildForceBuyPipeline(out InMemoryPriceApiClient priceApi)
        {
            var pipeline = BuildOwnMaterialsPipeline(out priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 1000, sellUnitPrice: 100);
            priceApi.AddPrice(2, buyUnitPrice: 300, sellUnitPrice: 30);
            return pipeline;
        }

        private static AccountSnapshot OwnFourOfIngredient()
        {
            return new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry
                    {
                        ItemId = 2,
                        Count = 4,
                        Source = AccountItemIndex.SourceMaterialStorage
                    }
                }
            };
        }

        [Fact]
        public async Task Structured_ValuedMode_ForceBuyPrePass_UsesZeroOwnedBaseline()
        {
            // Own 4 of the 5 needed of item 2: post-reduction, item 1's
            // craft cost collapses to 1x30=30 - misleadingly cheaper than
            // buy(100) if evaluated AFTER reduction. The force-buy flag,
            // computed on the zero-owned (pre-reduction) baseline, must
            // still keep item 1 bought rather than "crafted" from an
            // artificially cheap remainder.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(1, result.Plan.Steps[0].ItemId);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
            Assert.Equal(100, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task Structured_ValuedMode_ForceBuyPrePass_NoPhantomUsedMaterialsOrOpportunityCost()
        {
            // VOM design (Candidate A) - direct proof of the audited row-31
            // "phantom UsedMaterials" bug fix, using the exact same fixture
            // as Structured_ValuedMode_ForceBuyPrePass_UsesZeroOwnedBaseline
            // above (item 1 is force-buy-flagged; owns 4 of 5 needed of
            // item 2). Before this milestone, InventoryReducer.Reduce ran
            // BEFORE the force-buy decision existed and walked item 1's
            // primary recipe regardless, phantom-consuming all 4 owned
            // units of item 2 even though item 1 is never crafted - so item
            // 2 would show QuantityUsed=4 in UsedMaterials and
            // MaterialOpportunityCost would deduct that phantom value from
            // CraftingProfit. Now: InventoryReducer.Reduce is guided by the
            // zero-owned decision pass, sees item 1's decision is
            // BuyFromTp, and never touches item 2's pool at all.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
            Assert.Empty(result.UsedMaterials);
            Assert.Null(result.MaterialOpportunityCost);
        }

        /// <summary>
        /// Shared fixture for the decision-invariance pair below: two
        /// recipe options for item 1, option A (recipe 10) needs 5x item 2
        /// (30 each = 150 zero-owned), option B (recipe 20) needs 5x item 3
        /// (20 each = 100 zero-owned) - option B is objectively cheaper at
        /// zero-owned market prices. Item 1 itself is far pricier to buy
        /// outright than either craft option, so the solver always crafts -
        /// only WHICH option is in question. Snapshot owns ALL 5 units of
        /// option A's ingredient (item 2).
        /// </summary>
        private static CraftingPlanPipeline BuildCompetingRecipeOptionsPipeline(out AccountSnapshot snapshot)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // Both recipe ids must be in the SAME search result so
            // RecipeService discovers them as competing options on one
            // node - AddSearchResult(1, 10) alone would give item 1 only
            // ONE recipe option, defeating the whole point of this fixture.
            recipeApi.AddSearchResult(1, 10, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 3, Count = 5 }
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" }
            });

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 30); // option A: 5x30=150
            priceApi.AddPrice(3, buyUnitPrice: 10, sellUnitPrice: 20); // option B: 5x20=100 (cheaper)

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient A", "a.png");
            itemApi.AddItem(3, "Ingredient B", "b.png");

            snapshot = new AccountSnapshot
            {
                Items = new List<SnapshotItemEntry>
                {
                    new SnapshotItemEntry { ItemId = 2, Count = 5, Source = AccountItemIndex.SourceMaterialStorage }
                }
            };

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        [Fact]
        public async Task Structured_ValuedMode_CompetingRecipeOptions_DecisionInvariant_OwnedStockNeverFlipsChoice()
        {
            // Decision invariance (the core VOM design guarantee): owning
            // ALL 5 units of option A's ingredient (item 2) must NOT flip
            // the decision toward option A (which the pre-VOM primary-
            // option heuristic - node.Recipes[0] always gets discounted,
            // regardless of price - would have done, since option A is
            // listed first): the guided reduction only lets the option the
            // zero-owned pass actually chose (option B) consume owned
            // stock, so an un-chosen option can never look artificially
            // cheaper than a genuinely cheaper alternative. Contrast with
            // Structured_FreeMode_CompetingRecipeOptions_PrimaryOptionOwnedStockFlipsChoice
            // below, which pins that Free mode still has this exact bias.
            var pipeline = BuildCompetingRecipeOptionsPipeline(out var snapshot);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            // Option B (RecipeId 20, the zero-owned-cheaper option) wins,
            // NOT option A - even though item 2 (option A's ingredient) is
            // fully owned and option A is listed first.
            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            Assert.Contains(result.Plan.Steps, s => s.ItemId == 1 && s.RecipeId == 20);
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 1 && s.RecipeId == 10);
            // Item 2's owned stock is never consumed (option A was never
            // chosen), so it does not appear in UsedMaterials at all.
            Assert.DoesNotContain(result.UsedMaterials, u => u.ItemId == 2);
            // Item 3 (option B's ingredient) is bought fresh at full price.
            Assert.Equal(100, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task Structured_FreeMode_CompetingRecipeOptions_PrimaryOptionOwnedStockFlipsChoice()
        {
            // Free-mode sibling of the Valued-mode decision-invariant test
            // above (post-review coverage gap fix - closes the design's
            // byte-equivalence gate for the competing-recipe-options case,
            // which the pre-existing Structured_FreeMode_
            // SameOwnershipScenario_CraftsFromReducedRemainder fixture
            // cannot: it only has ONE recipe option). Free mode never
            // builds a guide, so InventoryReducer falls back to the legacy
            // i==0-primary-option heuristic: option A (RecipeId 10, listed
            // first) always gets discounted regardless of price. Owning
            // all 5 units of its ingredient (item 2) collapses option A's
            // POST-reduction cost to 0, flipping the solver's choice away
            // from option B (the genuinely cheaper option at market
            // prices) - the exact recipe-option bias the Valued-mode
            // decision-invariant guarantee exists to prevent, still present
            // (by design - unchanged pre-VOM behavior) when Valued mode is
            // off.
            var pipeline = BuildCompetingRecipeOptionsPipeline(out var snapshot);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, snapshot, CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy); // default Free

            Assert.Equal(CraftingDecision.Craft, result.CraftingTree.Decision);
            // Option A (RecipeId 10, listed first) wins here, NOT option B -
            // the opposite outcome from Valued mode with the identical
            // fixture/ownership.
            Assert.Contains(result.Plan.Steps, s => s.ItemId == 1 && s.RecipeId == 10);
            Assert.DoesNotContain(result.Plan.Steps, s => s.ItemId == 1 && s.RecipeId == 20);
            // All 5 owned units of item 2 were consumed by option A.
            Assert.Contains(result.UsedMaterials, u => u.ItemId == 2 && u.QuantityUsed == 5);
            // Nothing needed to be bought at all - item 2 was fully owned
            // and item 3 (option B's ingredient) was never touched.
            Assert.Equal(0, result.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task Structured_FreeMode_SameOwnershipScenario_CraftsFromReducedRemainder()
        {
            // Control for the test above: Free mode never runs the
            // force-buy pre-pass, so the (misleadingly cheap) post-
            // reduction craft path wins normally, same as before M34.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                priceBasis: PriceBasis.InstantBuy); // default Free

            Assert.Contains(result.Plan.Steps,
                s => s.ItemId == 1 && s.Source == AcquisitionSource.Craft);
            // Only the 1 remaining unit of item 2 is bought.
            Assert.Contains(result.Plan.Steps,
                s => s.ItemId == 2 && s.Source == AcquisitionSource.BuyFromTp && s.Quantity == 1);
        }

        [Fact]
        public async Task Structured_ValuedMode_NoSnapshot_ForceBuyPrePassDoesNotRun()
        {
            // Valued mode alone (no snapshot) must not activate the
            // force-buy pre-pass at all - see CraftingPlanPipeline's own
            // gate comment. The full (unreduced) craft cost (5x30=150)
            // genuinely beats buy(100)? No - buy(100) beats craft(150)
            // outright already, so normal (non-forced) PickCheapest already
            // buys here regardless; this test pins that no snapshot means
            // no special force-buy machinery runs, not just that the
            // outcome happens to match.
            var pipeline = BuildForceBuyPipeline(out _);

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Single(result.Plan.Steps);
            Assert.Equal(AcquisitionSource.BuyFromTp, result.Plan.Steps[0].Source);
        }

        [Fact]
        public async Task ResolveWithOverrides_ForceBuyPrePass_ManualOverrideStillWins()
        {
            var pipeline = BuildForceBuyPipeline(out _);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(CraftingDecision.BuyFromTp, initial.CraftingTree.Decision);
            Assert.True(initial.CraftingTree.CanCraft); // flag reflects true feasibility

            // Manually force craft on the root - must win over the
            // automatic force-buy pre-pass (gw2e parity: manual pill always
            // beats the automatic pre-pass).
            var overrides = new Dictionary<int, AcquisitionSource>
            {
                { initial.CraftingTree.NodeId, AcquisitionSource.Craft }
            };
            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, overrides);

            Assert.Equal(CraftingDecision.Craft, resolved.CraftingTree.Decision);
            // Item 1's zero-owned decision was BuyFromTp (the force-buy
            // flag), so the guided InventoryReducer.Reduce that fed
            // initial.SolveContext.Tree correctly never consumed the owned
            // 4 units of item 2 down item 1's never-chosen craft branch at
            // GENERATION time (the audited row-31 phantom-UsedMaterials bug
            // fix). ResolveWithOverrides re-runs the SAME zero-owned-
            // decision-pass-then-Reduce dance, this time with `overrides`
            // folded into the decision pass (see PlanSolveContext.
            // UnreducedTree's doc comment), so overriding item 1 to Craft
            // here correctly re-discounts item 2's subtree against the
            // user's real owned stock: 1 unit bought at 30 (the other 4
            // come from inventory), matching what master already returned
            // and what the user will actually spend.
            Assert.Equal(30, resolved.Plan.TotalCoinCost);
        }

        [Fact]
        public async Task ResolveWithOverrides_NoOpResolve_ForceBuyDecisionUnchanged()
        {
            // A no-op local re-solve (no overrides at all) must keep
            // applying the force-buy pre-pass exactly as the original
            // generation did - not "forget" it on the first re-solve.
            var pipeline = BuildForceBuyPipeline(out _);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnFourOfIngredient(), CancellationToken.None,
                ownMaterialsMode: OwnMaterialsMode.Valued,
                priceBasis: PriceBasis.InstantBuy);

            var resolved = pipeline.ResolveWithOverrides(initial.SolveContext, null);

            Assert.Equal(AcquisitionSource.BuyFromTp, resolved.Plan.Steps[0].Source);
            Assert.Equal(100, resolved.Plan.TotalCoinCost);
        }

        // --- M34-B2a #4: owned currency (cosmetic only, never affects decisions) ---

        private static CraftingPlanPipeline BuildVendorCurrencyPipeline(
            out VendorOfferStore store, string tempDir)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1, and (deliberately) no TP price either -
            // a vendor-only purchase. The offer's karma cost line is never
            // valued (no CurrencyValuation passed below), so it can only
            // win via the "fallback" tier (PlanSolver's last-resort branch
            // when nothing coin-priceable/craftable exists at all) - giving
            // it a TP price here would make TP win outright instead.
            var priceApi = new InMemoryPriceApiClient();

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Karma Item", "karma.png");

            var loader = new VendorOfferLoader();
            store = new VendorOfferStore(tempDir, loader);
            store.LoadBaseline(null);
            store.AddOffersToOverlay(new[]
            {
                new VendorOffer
                {
                    OfferId = "test-karma-offer",
                    OutputItemId = 1,
                    OutputCount = 1,
                    CostLines = new List<CostLine>
                    {
                        new CostLine { Type = "Currency", Id = 2, Count = 500 }
                    },
                    MerchantName = "Karma Vendor",
                    Locations = new List<string>()
                }
            });

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                store,
                reducer: new InventoryReducer());
        }

        [Fact]
        public async Task OwnedCurrency_DoesNotAffectDecisionsOrTotals()
        {
            // Regression guard (M34-B2a #4): wallet currency data is
            // cosmetic-only annotation. A plan generated WITH wallet karma
            // must produce the IDENTICAL decisions/costs as one generated
            // with none - only OwnedCurrencyAmounts may differ.
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var withoutWallet = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var snapshotWithWallet = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 100000 }
                    }
                };
                var withWallet = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshotWithWallet, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                // Decisions/costs identical regardless of wallet content.
                Assert.Equal(withoutWallet.Plan.Steps.Count, withWallet.Plan.Steps.Count);
                Assert.Equal(withoutWallet.Plan.Steps[0].Source, withWallet.Plan.Steps[0].Source);
                Assert.Equal(withoutWallet.Plan.TotalCoinCost, withWallet.Plan.TotalCoinCost);
                Assert.Equal(withoutWallet.Plan.CurrencyCosts.Count, withWallet.Plan.CurrencyCosts.Count);
                Assert.Equal(withoutWallet.Plan.CurrencyCosts[0].Amount, withWallet.Plan.CurrencyCosts[0].Amount);
                Assert.Equal(withoutWallet.CraftingTree.Decision, withWallet.CraftingTree.Decision);

                // Only the annotation differs. CraftingPlanResult.
                // OwnedCurrencyAmounts stores the RAW wallet amount
                // (capping-at-needed is a view-model presentation concern -
                // see PlanViewModelBuilder / the CurrencyCostRow test below).
                Assert.Null(withoutWallet.OwnedCurrencyAmounts);
                Assert.NotNull(withWallet.OwnedCurrencyAmounts);
                Assert.Equal(100000, withWallet.OwnedCurrencyAmounts[2]);
            }
        }

        [Fact]
        public async Task OwnedCurrency_PartialWalletAmount_CappedAtNeeded()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var snapshot = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 200 }
                    }
                };
                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshot, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                // Needs 500, owns only 200 - reported as-is (not capped to
                // itself, since 200 < 500).
                Assert.Equal(200, result.OwnedCurrencyAmounts[2]);
                // The plan itself still needs the full 500 (owned currency
                // never nets against the plan's own currency total).
                Assert.Equal(500, result.Plan.CurrencyCosts[0].Amount);
            }
        }

        [Fact]
        public async Task OwnedCurrency_NoWalletAtAll_AmountsNull()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                Assert.Null(result.OwnedCurrencyAmounts);
            }
        }

        [Fact]
        public async Task OwnedCurrency_ViewModel_CurrencyCostRowGetsOwnedQuantity()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var snapshot = new AccountSnapshot
                {
                    Wallet = new List<SnapshotWalletEntry>
                    {
                        new SnapshotWalletEntry { CurrencyId = 2, Value = 200 }
                    }
                };
                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, snapshot, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var vm = new PlanViewModelBuilder().Build(result);
                var summarySection = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
                var currencyRow = summarySection.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);

                Assert.Equal(200, currencyRow.CurrencyOwnedQuantity);
                Assert.Equal(500, currencyRow.Quantity);
            }
        }

        [Fact]
        public async Task OwnedCurrency_ViewModel_NoWallet_OwnedQuantityNull()
        {
            using (var tmp = new TempDirectory())
            {
                var tempDir = tmp.Path;
                var pipeline = BuildVendorCurrencyPipeline(out _, tempDir);

                var result = await pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

                var vm = new PlanViewModelBuilder().Build(result);
                var summarySection = vm.Sections.First(s => s.SectionType == PlanSectionType.Summary);
                var currencyRow = summarySection.Rows.First(r => r.RowType == PlanRowType.CurrencyCost);

                Assert.Null(currencyRow.CurrencyOwnedQuantity);
            }
        }

        // --- M38 WP-18 (tests T6/T8/T9): pipeline-level cancellation,
        // dependency-throws (degrade vs abort), and Ignore x owned-materials
        // interaction coverage. Every existing test above calls
        // GenerateStructuredAsync with CancellationToken.None and a fully-
        // healthy set of in-memory fixtures - nothing here exercised
        // cancellation or a thrown dependency until now. ---

        // KNOWN-ISSUES 31c-audit (M37 audit-fix): TradingPostService's
        // AwaitRespectingOwnCancellationAsync races the caller's own ct
        // against the shared upstream fetch it started, throwing promptly
        // without waiting for the fetch to finish. Gating the fake price
        // API's response lets this test cancel deterministically "between
        // phases" (after the recipe tree is built, while the price fetch is
        // still in flight) with no sleep/timing race - the same idiom
        // TradingPostServiceTests' own ConcurrentCalls_*Cancelled* tests
        // already use one layer down.
        [Fact]
        public async Task GenerateStructuredAsync_List_SingleItem_CancelledWhilePriceFetchInFlight_PropagatesCancellation()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest leaf-buy tree, so Step 1
            // (build recipe tree) completes synchronously and the pipeline
            // reaches the price fetch immediately.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            var gate = new TaskCompletionSource<bool>();
            priceApi.Gate = gate.Task;

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var cts = new CancellationTokenSource();
            var items = new List<PlanRequestItem> { new PlanRequestItem { ItemId = 1, Quantity = 1 } };

            // This is the ONE production entry point Module.cs actually
            // calls (see GenerateStructuredAsync's own doc comment) - a
            // single-entry list short-circuits straight to the single-item
            // core, so this also exercises that method's own
            // catch (OperationCanceledException) { ...; throw; } vs
            // catch (Exception) { ...; throw; } distinction.
            var planTask = pipeline.GenerateStructuredAsync(
                items, null, cts.Token, priceBasis: PriceBasis.InstantBuy);

            cts.Cancel();
            gate.SetResult(true); // release the now-abandoned fetch so nothing is left hanging

            await Assert.ThrowsAsync<OperationCanceledException>(() => planTask);
            Assert.True(planTask.IsCanceled);
        }

        // Same race, through the genuine 2+ item path (GenerateStructuredMultiAsync)
        // instead of the single-item short-circuit - a separate method with
        // its own step sequence, worth proving independently.
        [Fact]
        public async Task GenerateStructuredAsync_List_MultiItem_CancelledWhilePriceFetchInFlight_PropagatesCancellation()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipes for items 1/2 - both are simplest leaf-buy trees.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            priceApi.AddPrice(2, buyUnitPrice: 20, sellUnitPrice: 200);
            var gate = new TaskCompletionSource<bool>();
            priceApi.Gate = gate.Task;

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");
            itemApi.AddItem(2, "Iron Ore", "iron.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            var cts = new CancellationTokenSource();
            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 2, Quantity = 1 }
            };

            var planTask = pipeline.GenerateStructuredAsync(
                items, null, cts.Token, priceBasis: PriceBasis.InstantBuy);

            cts.Cancel();
            gate.SetResult(true);

            await Assert.ThrowsAsync<OperationCanceledException>(() => planTask);
            Assert.True(planTask.IsCanceled);
        }

        // Builds a pipeline whose target item (id 1) crafts from
        // `ingredientCount` distinct, individually-priced/metadata'd leaf
        // ingredient items - large enough to exceed TradingPostService's and
        // ItemMetadataService's shared BatchSize (200), so a single bad
        // batch's documented degrade-vs-abort boundary (KNOWN-ISSUES
        // api-degradation F2/F3) is observable end to end through the real
        // pipeline, not just at TradingPostServiceTests'/
        // ItemMetadataServiceTests' own service-level unit tests.
        private static CraftingPlanPipeline BuildManyLeafIngredientsPipeline(
            int ingredientCount,
            out InMemoryPriceApiClient priceApi,
            out InMemoryItemApiClient itemApi)
        {
            var recipeApi = new InMemoryRecipeApiClient();
            recipeApi.AddSearchResult(1, 10);

            priceApi = new InMemoryPriceApiClient();
            itemApi = new InMemoryItemApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 1, sellUnitPrice: 2);
            itemApi.AddItem(1, "Target", "t.png");

            var ingredients = new List<RawIngredient>(ingredientCount);
            for (int i = 0; i < ingredientCount; i++)
            {
                int id = 1000 + i;
                ingredients.Add(new RawIngredient { Type = "Item", Id = id, Count = 1 });
                priceApi.AddPrice(id, buyUnitPrice: 1, sellUnitPrice: 2);
                itemApi.AddItem(id, "Ingredient " + id, "i.png");
            }

            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 10,
                OutputItemId = 1,
                OutputItemCount = 1,
                Ingredients = ingredients,
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400
            });

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));
        }

        // KNOWN-ISSUES api-degradation F2: TradingPostService degrades a
        // single failing batch to missing prices instead of aborting the
        // whole GetPricesAsync call. This proves that degrade behavior
        // survives being called THROUGH the pipeline, not just at
        // TradingPostServiceTests.OneBatchFails_DegradesToHolesInsteadOfAbortingWholeCall's
        // own service-level test.
        [Fact]
        public async Task GenerateStructuredAsync_OneOfManyPriceBatchesFails_DegradesInsteadOfAborting()
        {
            var pipeline = BuildManyLeafIngredientsPipeline(210, out var priceApi, out _);
            priceApi.ThrowOnCallNumber = 2; // second of two sequential batches fails

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // Proves the multi-batch scenario was genuinely exercised (not a
            // vacuous pass that would also hold if BatchSize ever changed to
            // no longer split 211 ids into two calls) - the second batch's
            // failure must not have short-circuited the fetch into only
            // attempting one batch.
            Assert.Equal(2, priceApi.Calls.Count);
            // The actual "degrades, does not abort" claim: the pipeline
            // still completed and produced a usable plan despite the second
            // batch's failure, rather than propagating it as a thrown
            // exception (see the AllPriceBatchesFail sibling test below for
            // the total-outage case, which DOES throw).
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
        }

        // KNOWN-ISSUES api-degradation F2's other half: a genuine total
        // price-API outage (every batch fails) must still surface as a
        // thrown exception through the pipeline, not silently degrade to an
        // all-unpriceable "success".
        [Fact]
        public async Task GenerateStructuredAsync_AllPriceBatchesFail_AbortsInsteadOfSilentlyDegrading()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest leaf-buy tree.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);
            priceApi.ThrowAlways = true;

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy));
        }

        // KNOWN-ISSUES api-degradation F3: ItemMetadataService degrades a
        // single failing first-wave batch (retry wave/seed fallback/
        // omission) instead of aborting GetMetadataAsync entirely. Same
        // large-fixture shape as the price-side degrade test above, proven
        // through the real pipeline.
        [Fact]
        public async Task GenerateStructuredAsync_OneOfManyMetadataBatchesFails_DegradesInsteadOfAborting()
        {
            var pipeline = BuildManyLeafIngredientsPipeline(210, out _, out var itemApi);
            itemApi.ThrowOnCallNumber = 2; // second of two sequential first-wave batches fails

            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            // Proves the multi-batch scenario was genuinely exercised (at
            // least the 2 first-wave batches; a 3rd retry-wave call is
            // possible per ItemMetadataService's own degrade behavior).
            Assert.True(itemApi.Calls.Count >= 2);
            Assert.NotNull(result.Plan);
            Assert.True(result.Plan.Steps.Count > 0);
        }

        // KNOWN-ISSUES api-degradation F3's other half: a genuine total item
        // API outage (the only first-wave batch fails) must still surface
        // as a thrown exception through the pipeline.
        [Fact]
        public async Task GenerateStructuredAsync_AllMetadataBatchesFail_AbortsInsteadOfSilentlyDegrading()
        {
            var recipeApi = new InMemoryRecipeApiClient();
            // No recipe for item 1 - simplest leaf-buy tree, single item
            // metadata batch.

            var priceApi = new InMemoryPriceApiClient();
            priceApi.AddPrice(1, buyUnitPrice: 50, sellUnitPrice: 500);

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Copper Ore", "copper.png");
            itemApi.ThrowOnCallNumber = 1; // the sole first-wave batch fails

            var pipeline = new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                pipeline.GenerateStructuredAsync(
                    1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy));
        }

        // KNOWN-ISSUES 20.4's "Conservative reading": Ignore (per-solve,
        // keyed by ItemId, zeroes cost via PlanSolver's ignoredItemIds) and
        // ownership (InventoryReducer, runs BEFORE Solve, zeroes cost by
        // reducing node.Quantity) are two independently-evolved mechanisms.
        // Unlike ResolveWithOverrides_IgnoredItemIds_ZeroesIngredientCost
        // above (which deliberately tests Ignore alone, "no snapshot"), this
        // combines both: 3 of 5 needed units are genuinely owned via a real
        // reduction, and the same ingredient id is then also Ignored on a
        // later local re-solve.
        [Fact]
        public async Task ResolveWithOverrides_IgnoredItemIds_PartiallyOwnedIngredient_ShowsBothOwnedAndIgnored()
        {
            var pipeline = BuildOwnMaterialsPipeline(out var priceApi, ingredientCount: 5);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100); // BuyInstant (craft-cost basis) = 100

            // Own 3 of the 5 needed via a real reduction.
            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, OwnIngredient(3), CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(200, initial.Plan.TotalCoinCost); // (5-3) x 100 = 200, unaffected by Ignore
            Assert.Equal(3, initial.CraftingTree.Children[0].OwnedQuantityUsed);
            Assert.False(initial.CraftingTree.Children[0].IsIgnored);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 2 });

            // Ignore zeroes cost outright, same as with no ownership at all -
            // it does not matter that 3 of the 5 were already owned.
            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            Assert.DoesNotContain(resolved.Plan.Steps, s => s.ItemId == 2);

            // Both mechanisms leave their own mark on the same node:
            // CraftingTreeBuilder.BuildNode sets OwnedQuantityUsed
            // unconditionally BEFORE its IsIgnored early return, so both
            // survive on the same CraftingTreeNode simultaneously.
            var ingredientNode = resolved.CraftingTree.Children[0];
            Assert.Equal(CraftingDecision.Have, ingredientNode.Decision);
            Assert.True(ingredientNode.IsIgnored);
            Assert.Equal(3, ingredientNode.OwnedQuantityUsed);

            // The top-level UsedMaterials list (set once at generation/
            // reduction time) is untouched by the later Ignore re-solve.
            Assert.Single(resolved.UsedMaterials);
            Assert.Equal(3, resolved.UsedMaterials[0].QuantityUsed);
        }
    }
}
