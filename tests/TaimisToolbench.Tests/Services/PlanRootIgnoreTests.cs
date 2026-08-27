using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;
using static TaimisToolbench.Tests.Helpers.RecipeNodeBuilders;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The plan's own target is what the user asked to craft, not an
    /// acquisition decision to opt out of, so no IGNORE pill is offered on
    /// a root row (maintainer field feedback). Covers the pill layer
    /// (DecisionPillPlanner), the flag's single production write site
    /// (CraftingTreeBuilder.BuildTree) and the end-to-end pipeline path,
    /// including the all-children-ignored plan the suppression leaves
    /// reachable and the unpriced-plan counterexample that must not be
    /// mistaken for it.
    /// </summary>
    public class PlanRootIgnoreTests
    {
        private static CraftingTreeNode Node(
            CraftingDecision decision, bool isPlanRoot,
            bool canCraft = false, bool canBuyTp = false,
            int ownedQuantityUsed = 0, bool isIgnored = false, int quantity = 1)
        {
            return new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Name = "Test Item",
                Quantity = quantity,
                Decision = decision,
                CanCraft = canCraft,
                CanBuyTp = canBuyTp,
                OwnedQuantityUsed = ownedQuantityUsed,
                IsIgnored = isIgnored,
                IsPlanRoot = isPlanRoot,
            };
        }

        // --- Pill suppression on the root ---
        [Fact]
        public void Root_MultiSourceNode_KeepsSourcePills_DropsIgnorePill()
        {
            var root = Node(CraftingDecision.Craft, isPlanRoot: true, canCraft: true, canBuyTp: true);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);

            Assert.Contains(specs, s => s.Text == "CRAFT");
            Assert.Contains(specs, s => s.Text == "TP");
            Assert.DoesNotContain(specs, s => s.Kind == PillKind.Ignore);
        }

        [Fact]
        public void NonRoot_SameNodeShape_StillOffersIgnorePill()
        {
            // The suppression is keyed on root-ness alone: an otherwise
            // identical child keeps gw2e's always-offered Ignore toggle.
            var child = Node(CraftingDecision.Craft, isPlanRoot: false, canCraft: true, canBuyTp: true);

            var specs = DecisionPillPlanner.BuildPillSpecs(child);

            Assert.Equal("IGNORE", specs.Single(s => s.Kind == PillKind.Ignore).Text);
        }

        [Fact]
        public void Root_NoFeasibleSource_DropsIgnorePillToo()
        {
            // The zero-option ("UNKNOWN") and single-option paths are
            // separate returns in BuildPillSpecs and both append the
            // ownership pills - neither may leak the toggle back onto a
            // root.
            var unknownRoot = Node(CraftingDecision.Unknown, isPlanRoot: true);
            var soleSourceRoot = Node(CraftingDecision.Craft, isPlanRoot: true, canCraft: true);

            var unknownSpecs = DecisionPillPlanner.BuildPillSpecs(unknownRoot);
            var soleSourceSpecs = DecisionPillPlanner.BuildPillSpecs(soleSourceRoot);

            Assert.Equal("UNKNOWN", Assert.Single(unknownSpecs).Text);
            Assert.Equal("CRAFT", Assert.Single(soleSourceSpecs).Text);
        }

        [Fact]
        public void Root_PartiallyOwned_KeepsOwnedAnnotation_DropsIgnorePill()
        {
            var root = Node(
                CraftingDecision.Craft, isPlanRoot: true, canCraft: true, canBuyTp: true,
                ownedQuantityUsed: 2, quantity: 3);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);

            Assert.Equal("HAVE 2/5 NEEDED", specs.Single(s => s.Kind == PillKind.OwnedInfo).Text);
            Assert.DoesNotContain(specs, s => s.Kind == PillKind.Ignore);
        }

        [Fact]
        public void Root_AlreadyIgnored_KeepsIgnoredToggleSoTheStateIsRecoverable()
        {
            // Ignores are keyed by item id and apply tree-wide within one
            // solve, so a multi-item batch can reach a root that is itself
            // ignored (see MultiItemBatch_IgnoringAnIngredient_...).
            // Suppressing the un-ignore half here would leave that root
            // permanently zeroed with no way back.
            var root = Node(CraftingDecision.Have, isPlanRoot: true, isIgnored: true);

            var specs = DecisionPillPlanner.BuildPillSpecs(root);

            var toggle = specs.Single(s => s.Kind == PillKind.Ignore);
            Assert.Equal("IGNORED", toggle.Text);
            Assert.True(DecisionPillPlanner.IsInteractive(toggle));
        }

        // --- The flag's production write site ---
        [Fact]
        public void BuildTree_MarksTheReturnedRootOnly()
        {
            var ingredient = Leaf(2, 3);
            var root = Craftable(1, 1, Option(10, 1, 1, ingredient));

            var prices = new Dictionary<int, ItemPrice>
            {
                { 1, new ItemPrice { ItemId = 1, BuyInstant = 1000 } },
                { 2, new ItemPrice { ItemId = 2, BuyInstant = 10 } },
            };
            var solveResult = new PlanSolver().Solve(root, prices, null);
            var metadata = new Dictionary<int, ItemMetadata>
            {
                { 1, new ItemMetadata { ItemId = 1, Name = "Root", IconUrl = "r.png" } },
                { 2, new ItemMetadata { ItemId = 2, Name = "Child", IconUrl = "c.png" } },
            };

            var treeNode = new CraftingTreeBuilder().BuildTree(root, solveResult.Decisions, metadata);

            Assert.True(treeNode.IsPlanRoot);
            Assert.Single(treeNode.Children);
            Assert.False(treeNode.Children[0].IsPlanRoot);

            Assert.DoesNotContain(
                DecisionPillPlanner.BuildPillSpecs(treeNode), s => s.Kind == PillKind.Ignore);
            Assert.Contains(
                DecisionPillPlanner.BuildPillSpecs(treeNode.Children[0]), s => s.Kind == PillKind.Ignore);
        }

        [Fact]
        public async Task MultiItemBatch_EveryRequestedRootIsAPlanRoot()
        {
            // A batch has N roots, not one: the synthetic wrapper never
            // becomes a CraftingTreeNode, so each requested item's own
            // tree has to carry the flag.
            var pipeline = BuildPipeline(out var priceApi, secondTarget: true);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(3, buyUnitPrice: 10000, sellUnitPrice: 20000);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 3, Quantity = 1 },
            };

            var result = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(2, result.MultiItemRoots.Count);
            foreach (var root in result.MultiItemRoots)
            {
                Assert.True(root.IsPlanRoot);
                Assert.DoesNotContain(
                    DecisionPillPlanner.BuildPillSpecs(root), s => s.Kind == PillKind.Ignore);
            }
        }

        [Fact]
        public async Task MultiItemBatch_IgnoringAnIngredientAlsoIgnoresTheSiblingRootOfThatItem()
        {
            // The reachable route to an ignored ROOT, and the whole reason
            // the "IGNORED" un-ignore pill is exempt from the suppression:
            // ignores are keyed by item id and apply tree-wide, so ignoring
            // item 3 where it appears as an ingredient of root 1 also
            // flips requested root 3. Root 3 offers no "IGNORE" pill, so
            // the un-ignore toggle is the ONLY way back.
            var pipeline = BuildNestedBatchPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);
            priceApi.AddPrice(3, buyUnitPrice: 100, sellUnitPrice: 200);

            var items = new List<PlanRequestItem>
            {
                new PlanRequestItem { ItemId = 1, Quantity = 1 },
                new PlanRequestItem { ItemId = 3, Quantity = 1 },
            };

            var initial = await pipeline.GenerateStructuredAsync(
                items, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            var siblingRootBefore = initial.MultiItemRoots.Single(r => r.ItemId == 3);
            Assert.False(siblingRootBefore.IsIgnored);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 3 });

            var siblingRoot = resolved.MultiItemRoots.Single(r => r.ItemId == 3);
            Assert.True(siblingRoot.IsPlanRoot);
            Assert.True(siblingRoot.IsIgnored);

            var specs = DecisionPillPlanner.BuildPillSpecs(siblingRoot);
            var toggle = specs.Single(s => s.Kind == PillKind.Ignore);
            Assert.Equal("IGNORED", toggle.Text);
            Assert.True(DecisionPillPlanner.IsInteractive(toggle));
        }

        // --- The state root suppression leaves reachable ---
        [Fact]
        public async Task AllIngredientsIgnored_RootKeepsNoIgnorePill_AndSummaryShowsFullZeroBand()
        {
            // Root ignore is gone as a UI path, but ignoring every child
            // still zeroes the plan - the Total Cost band must render the
            // whole "Total Materials Value - Your Materials Used = Actual
            // Cost to Craft" formula at 0 rather than collapsing to a lone
            // "0c" tile.
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000); // buying the target outright loses to crafting
            priceApi.AddPrice(2, buyUnitPrice: 10, sellUnitPrice: 100);

            var initial = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);
            Assert.Equal(500, initial.Plan.TotalCoinCost);

            var resolved = pipeline.ResolveWithOverrides(
                initial.SolveContext, null, new HashSet<int> { 2 });

            Assert.Equal(0, resolved.Plan.TotalCoinCost);
            Assert.False(resolved.CraftingTree.IsIgnored);
            Assert.DoesNotContain(
                DecisionPillPlanner.BuildPillSpecs(resolved.CraftingTree), s => s.Kind == PillKind.Ignore);
            Assert.Equal("IGNORED",
                DecisionPillPlanner.BuildPillSpecs(resolved.CraftingTree.Children[0])
                    .Single(s => s.Kind == PillKind.Ignore).Text);

            var vm = new PlanViewModelBuilder().Build(resolved);
            var summary = vm.Sections.Single(s => s.SectionType == PlanSectionType.Summary);
            var costTiles = summary.Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Equal(3, costTiles.Count);
            Assert.Equal("Total Materials Value", costTiles[0].Label);
            Assert.Equal("Your Materials Used", costTiles[1].Label);
            Assert.Equal("Actual Cost to Craft", costTiles[2].Label);
            Assert.All(costTiles, t => Assert.Equal(0L, t.CoinValue));
            Assert.All(costTiles, t => Assert.False(string.IsNullOrEmpty(t.TooltipText)));
            Assert.Contains(summary.Rows, r => r.RowType == PlanRowType.SummaryFootnote);
        }

        [Fact]
        public async Task UnpricedIngredient_ZeroesThePlanAndMarksTheZeroBand()
        {
            // An ingredient with no recipe and no price also totals 0, but
            // that 0 is unmeasured, not free. The band keeps all three
            // cells (dropping them read as a broken section) and says so
            // instead: every tile carries the marker and the section
            // carries the matching footnote.
            var pipeline = BuildPipeline(out var priceApi);
            priceApi.AddPrice(1, buyUnitPrice: 10000, sellUnitPrice: 20000);
            // Item 2, the sole ingredient, is deliberately left unpriced.
            var result = await pipeline.GenerateStructuredAsync(
                1, 1, null, CancellationToken.None, priceBasis: PriceBasis.InstantBuy);

            Assert.Equal(0, result.Plan.TotalCoinCost);
            Assert.Equal(CraftingDecision.Unknown, result.CraftingTree.Children[0].Decision);
            Assert.False(result.CraftingTree.Children[0].IsIgnored);

            var vm = new PlanViewModelBuilder().Build(result);
            var summary = vm.Sections.Single(s => s.SectionType == PlanSectionType.Summary);
            var costTiles = summary.Rows.Where(r => r.RowType == PlanRowType.CostFormulaTile).ToList();

            Assert.Equal(3, costTiles.Count);
            Assert.All(costTiles, t => Assert.Equal(0L, t.CoinValue));
            Assert.All(costTiles, t => Assert.EndsWith(
                PlanViewModelBuilder.UnpricedTileMarker, t.Label));
            Assert.All(costTiles, t => Assert.Contains(
                PlanViewModelBuilder.UnpricedTooltipSuffix, t.TooltipText));
            Assert.Contains(
                summary.Rows,
                r => r.RowType == PlanRowType.SummaryFootnote
                    && r.Label == PlanViewModelBuilder.UnpricedFootnoteText);

            // The profit band still suppresses on an unpriced zero: the
            // target HAS a sell price here, so its tiles would not be
            // zeros at all - "Sell Value - Total Materials Value 0 =
            // Profit if Sold" claims the craft consumes nothing and
            // profits its entire sale price. Its absence is accounted for
            // in text rather than left as two missing cells.
            Assert.DoesNotContain(
                summary.Rows, r => r.RowType == PlanRowType.ProfitFormulaTile);
            Assert.Contains(
                summary.Rows,
                r => r.RowType == PlanRowType.SummaryFootnote
                    && r.Label == PlanViewModelBuilder.ProfitSuppressedFootnoteText);
        }

        /// <summary>
        /// Batch shape where one requested root (item 3) is also an
        /// ingredient of the other requested root (item 1): 1 &lt;- 2x 3,
        /// 3 &lt;- 2x 2.
        /// </summary>
        private static CraftingPlanPipeline BuildNestedBatchPipeline(
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
                    new RawIngredient { Type = "Item", Id = 3, Count = 2 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
            });
            recipeApi.AddSearchResult(3, 20);
            recipeApi.AddRecipe(new RawRecipe
            {
                Id = 20,
                OutputItemId = 3,
                OutputItemCount = 1,
                Ingredients = new List<RawIngredient>
                {
                    new RawIngredient { Type = "Item", Id = 2, Count = 2 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
            });

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Base", "b.png");
            itemApi.AddItem(3, "Shared Intermediate", "s.png");

            priceApi = new InMemoryPriceApiClient();

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }

        private static CraftingPlanPipeline BuildPipeline(
            out InMemoryPriceApiClient priceApi, bool secondTarget = false)
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
                    new RawIngredient { Type = "Item", Id = 2, Count = 5 },
                },
                Disciplines = new List<string> { "Weaponsmith" },
                MinRating = 400,
                Flags = new List<string> { "AutoLearned" },
            });

            var itemApi = new InMemoryItemApiClient();
            itemApi.AddItem(1, "Target", "t.png");
            itemApi.AddItem(2, "Ingredient", "i.png");

            if (secondTarget)
            {
                recipeApi.AddSearchResult(3, 20);
                recipeApi.AddRecipe(new RawRecipe
                {
                    Id = 20,
                    OutputItemId = 3,
                    OutputItemCount = 1,
                    Ingredients = new List<RawIngredient>
                    {
                        new RawIngredient { Type = "Item", Id = 2, Count = 2 },
                    },
                    Disciplines = new List<string> { "Weaponsmith" },
                    MinRating = 400,
                    Flags = new List<string> { "AutoLearned" },
                });
                itemApi.AddItem(3, "Second Target", "s.png");
            }

            priceApi = new InMemoryPriceApiClient();

            return new CraftingPlanPipeline(
                new RecipeService(recipeApi),
                new TradingPostService(priceApi),
                new PlanSolver(),
                new ItemMetadataService(itemApi),
                reducer: new InventoryReducer());
        }
    }
}
