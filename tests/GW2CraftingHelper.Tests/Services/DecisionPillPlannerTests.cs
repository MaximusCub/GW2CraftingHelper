using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Contracts;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Full CanCraft/CanBuyTp/CanBuyVendor combination matrix (m3-display-
    /// decision-map.md's decision -> pill table) plus the HAVE/CURRENCY
    /// short-circuits, exercising the real DecisionPillPlanner.BuildPillSpecs
    /// production code - KNOWN-ISSUES #18.
    /// </summary>
    public class DecisionPillPlannerTests
    {
        private static CraftingTreeNode Node(
            CraftingDecision decision,
            bool canCraft = false, bool canBuyTp = false, bool canBuyVendor = false,
            string acquisitionBadge = null)
        {
            return new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Name = "Test Item",
                Quantity = 1,
                Decision = decision,
                CanCraft = canCraft,
                CanBuyTp = canBuyTp,
                CanBuyVendor = canBuyVendor,
                AcquisitionBadge = acquisitionBadge
            };
        }

        // --- HAVE / CURRENCY short-circuits ---

        [Fact]
        public void Have_SingleHavePill_NotInteractive()
        {
            var node = Node(CraftingDecision.Have);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("HAVE", specs[0].Text);
            Assert.Equal(PillKind.Have, specs[0].Kind);
            Assert.Null(specs[0].Source);
        }

        [Fact]
        public void Currency_SingleLockedPill_NotInteractive()
        {
            var node = Node(CraftingDecision.Currency);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("CURRENCY", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
            Assert.Null(specs[0].Source);
        }

        // --- (F,F,F): no feasible source at all ---

        [Fact]
        public void NoSource_NoBadge_LockedUnknownPill()
        {
            var node = Node(CraftingDecision.Unknown);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("UNKNOWN", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
            Assert.Null(specs[0].Source);
        }

        [Fact]
        public void NoSource_WithBadge_LockedBadgePill_NotUnknown()
        {
            var node = Node(CraftingDecision.Unknown, acquisitionBadge: "SALVAGE");
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("SALVAGE", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
        }

        // --- Exactly one feasible source: single Locked pill ---

        [Fact]
        public void OnlyTp_SingleLockedTpPill()
        {
            var node = Node(CraftingDecision.BuyFromTp, canBuyTp: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("TP", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
            Assert.Null(specs[0].Source);
        }

        [Fact]
        public void OnlyVendor_SingleLockedVendorPill()
        {
            var node = Node(CraftingDecision.BuyFromVendor, canBuyVendor: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("VENDOR", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
        }

        [Fact]
        public void OnlyCraft_SingleLockedCraftPill()
        {
            var node = Node(CraftingDecision.Craft, canCraft: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Single(specs);
            Assert.Equal("CRAFT", specs[0].Text);
            Assert.Equal(PillKind.Locked, specs[0].Kind);
        }

        // --- Two feasible sources: multi-pill, selected == node.Decision ---

        [Theory]
        [InlineData(CraftingDecision.BuyFromTp, "TP", "VENDOR")]
        [InlineData(CraftingDecision.BuyFromVendor, "VENDOR", "TP")]
        public void TpAndVendor_TwoPills_SelectedMatchesDecision(
            CraftingDecision decision, string selectedText, string availableText)
        {
            var node = Node(decision, canBuyTp: true, canBuyVendor: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Equal(2, specs.Count);
            var selected = specs.Single(s => s.Kind == PillKind.Selected);
            var available = specs.Single(s => s.Kind == PillKind.Available);

            Assert.Equal(selectedText, selected.Text);
            Assert.Null(selected.Source); // selected pill is a no-op, never clickable
            Assert.Equal(availableText, available.Text);
            Assert.NotNull(available.Source); // available pill applies an override
        }

        [Theory]
        [InlineData(CraftingDecision.Craft, "CRAFT", "TP")]
        [InlineData(CraftingDecision.BuyFromTp, "TP", "CRAFT")]
        public void CraftAndTp_TwoPills_SelectedMatchesDecision(
            CraftingDecision decision, string selectedText, string availableText)
        {
            var node = Node(decision, canCraft: true, canBuyTp: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Equal(2, specs.Count);
            Assert.Equal(selectedText, specs.Single(s => s.Kind == PillKind.Selected).Text);
            Assert.Equal(availableText, specs.Single(s => s.Kind == PillKind.Available).Text);
        }

        [Theory]
        [InlineData(CraftingDecision.Craft, "CRAFT", "VENDOR")]
        [InlineData(CraftingDecision.BuyFromVendor, "VENDOR", "CRAFT")]
        public void CraftAndVendor_TwoPills_SelectedMatchesDecision(
            CraftingDecision decision, string selectedText, string availableText)
        {
            var node = Node(decision, canCraft: true, canBuyVendor: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Equal(2, specs.Count);
            Assert.Equal(selectedText, specs.Single(s => s.Kind == PillKind.Selected).Text);
            Assert.Equal(availableText, specs.Single(s => s.Kind == PillKind.Available).Text);
        }

        // --- All three feasible: the highlighted pill MUST match the
        // solver's actual committed Source, whichever of the three it is
        // (KNOWN-ISSUES #18b) ---

        [Theory]
        [InlineData(CraftingDecision.Craft, "CRAFT")]
        [InlineData(CraftingDecision.BuyFromTp, "TP")]
        [InlineData(CraftingDecision.BuyFromVendor, "VENDOR")]
        public void AllThreeFeasible_SelectedPillAlwaysMatchesCommittedSource(
            CraftingDecision decision, string expectedSelectedText)
        {
            var node = Node(decision, canCraft: true, canBuyTp: true, canBuyVendor: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            Assert.Equal(3, specs.Count);
            Assert.Equal(new[] { "CRAFT", "TP", "VENDOR" }, specs.Select(s => s.Text));

            var selected = specs.Single(s => s.Kind == PillKind.Selected);
            Assert.Equal(expectedSelectedText, selected.Text);
            Assert.Null(selected.Source);

            // Every other pill is Available and independently clickable -
            // the M21 per-pill override model, not a single cycle button.
            foreach (var other in specs.Where(s => s.Kind != PillKind.Selected))
            {
                Assert.Equal(PillKind.Available, other.Kind);
                Assert.NotNull(other.Source);
            }
        }

        [Fact]
        public void AvailablePill_SourceMatchesItsOwnAcquisitionSource()
        {
            var node = Node(CraftingDecision.BuyFromTp, canCraft: true, canBuyTp: true, canBuyVendor: true);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            var craftPill = specs.Single(s => s.Text == "CRAFT");
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            Assert.Equal(AcquisitionSource.Craft, craftPill.Source);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorPill.Source);
        }

        // --- End-to-end via the real solver + tree builder: proves the
        // pill mapping never desyncs from an actual PlanSolver decision,
        // not just a hand-built CraftingTreeNode. ---

        private static RecipeNode Leaf(int id, int quantity)
        {
            return new RecipeNode { Id = id, IngredientType = "Item", Quantity = quantity, Recipes = new List<RecipeOption>() };
        }

        [Fact]
        public void RealSolver_TpCheaperThanVendor_TpPillSelected()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice> { { 1, new ItemPrice { ItemId = 1, BuyInstant = 50 } } };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        new VendorOffer
                        {
                            OfferId = "v1", OutputItemId = 1, OutputCount = 1,
                            CostLines = new List<CostLine> { new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 200 } },
                            MerchantName = "Test Vendor", Locations = new List<string>()
                        }
                    }
                }
            };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers);
            var builder = new CraftingTreeBuilder();
            var node = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);

            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            Assert.Equal(2, specs.Count); // TP, VENDOR (no recipe -> no CRAFT)
            var selected = specs.Single(s => s.Kind == PillKind.Selected);
            Assert.Equal("TP", selected.Text);
        }

        [Fact]
        public void RealSolver_VendorCheaperThanTp_VendorPillSelected()
        {
            var tree = Leaf(1, 1);
            var prices = new Dictionary<int, ItemPrice> { { 1, new ItemPrice { ItemId = 1, BuyInstant = 500 } } };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        new VendorOffer
                        {
                            OfferId = "v1", OutputItemId = 1, OutputCount = 1,
                            CostLines = new List<CostLine> { new CostLine { Type = "Currency", Id = Gw2Constants.CoinCurrencyId, Count = 50 } },
                            MerchantName = "Test Vendor", Locations = new List<string>()
                        }
                    }
                }
            };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers);
            var builder = new CraftingTreeBuilder();
            var node = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            Assert.Equal(CraftingDecision.BuyFromVendor, node.Decision);

            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            var selected = specs.Single(s => s.Kind == PillKind.Selected);
            Assert.Equal("VENDOR", selected.Text);
        }

        [Fact]
        public void RealSolver_FallbackOnlyVendor_StillShowsAvailableVendorPill()
        {
            // A vendor offer priced entirely in an unvalued non-coin
            // currency is fallback-tier only (never actually compared
            // against TP in PickCheapest - PlanSolver.cs EvaluateVendorOffers),
            // yet CanBuyVendor is still true (B1's deliberate one-flag
            // design - "would overriding to Vendor succeed" - see
            // SolverDecision.CanBuyVendor's doc comment). Per KNOWN-ISSUES
            // #18a, the VENDOR pill still renders as a real, clickable
            // Available alternative - this is intentional, not a bug to
            // suppress.
            var tree = Leaf(1, 2);
            var prices = new Dictionary<int, ItemPrice> { { 1, new ItemPrice { ItemId = 1, BuyInstant = 50 } } };
            var vendorOffers = new Dictionary<int, IReadOnlyList<VendorOffer>>
            {
                {
                    1, new List<VendorOffer>
                    {
                        new VendorOffer
                        {
                            OfferId = "v1", OutputItemId = 1, OutputCount = 1,
                            CostLines = new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = 500 } },
                            MerchantName = "Test Vendor", Locations = new List<string>()
                        }
                    }
                }
            };

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, vendorOffers);
            var builder = new CraftingTreeBuilder();
            var node = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            // TP wins (comparable vendor value is null - only the fallback
            // tier exists), but the vendor offer is still overridable.
            Assert.Equal(CraftingDecision.BuyFromTp, node.Decision);
            Assert.True(node.CanBuyVendor);

            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            Assert.Equal(PillKind.Available, vendorPill.Kind);
            Assert.Equal(AcquisitionSource.BuyFromVendor, vendorPill.Source);
        }

        [Fact]
        public void RealSolver_UnknownSource_NeverHasChildren_NoLiveCraftSubtreeUnderUnknownPill()
        {
            // KNOWN-ISSUES #18c: the UNKNOWN pill must never coexist with a
            // live craft subtree. Post-M33-B1, this is structurally
            // guaranteed (CanCraft is now always true whenever a recipe
            // exists, so Decision == Unknown implies no recipe at all,
            // hence no children could ever be built) - this test locks
            // that invariant in against a future regression.
            var tree = Leaf(1, 1); // no recipes, no price, no vendor offer
            var prices = new Dictionary<int, ItemPrice>();

            var solver = new PlanSolver();
            var solveResult = solver.Solve(tree, prices, null);
            var builder = new CraftingTreeBuilder();
            var node = builder.BuildTree(tree, solveResult.Decisions, new Dictionary<int, ItemMetadata>());

            Assert.Equal(CraftingDecision.Unknown, node.Decision);
            Assert.Empty(node.Children);
            Assert.False(node.CanCraft);

            var specs = DecisionPillPlanner.BuildPillSpecs(node);
            Assert.Single(specs);
            Assert.Equal("UNKNOWN", specs[0].Text);
        }
    }
}
