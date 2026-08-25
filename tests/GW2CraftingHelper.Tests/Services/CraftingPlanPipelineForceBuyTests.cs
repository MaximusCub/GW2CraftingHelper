using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    public class CraftingPlanPipelineForceBuyTests
    {
        // --- Force-buy pre-pass (zero-owned baseline) ---

        /// <summary>
        /// Reuses PipelineBuilder.BuildOwnMaterialsPipeline's identical
        /// tree shape (item 1
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
            var pipeline = PipelineBuilder.BuildOwnMaterialsPipeline(out priceApi);
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
            // above (closes the design's
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
            // reduction craft path wins normally.
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
    }
}
