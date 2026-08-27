using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// (redesign,
    /// docs/gw2e-considerations.md): DecisionPillPlanner-level coverage of
    /// the Subdued pill Kind - exercises the real BuildPillSpecs production
    /// code with CraftingTreeNode fixtures carrying hand-set
    /// PillSourceCostBreakdown fields (same construction style
    /// DecisionPillPlannerTests already uses for the rest of the pill
    /// matrix). See PillSubduingEvaluatorTests for the underlying
    /// detection rules and PlanSolverPillSubduingTests for real Solve()-
    /// path coverage of how those breakdowns get computed in the first
    /// place.
    /// </summary>
    public class DecisionPillPlannerSubduingTests
    {
        private static PillSourceCostBreakdown Breakdown(long rawCoin, long? decisionValue = null)
        {
            return new PillSourceCostBreakdown { IsAvailable = true, RawCoin = rawCoin, DecisionValue = decisionValue };
        }

        private static CraftingTreeNode ThreeOptionNode(
            CraftingDecision decision,
            PillSourceCostBreakdown craft, PillSourceCostBreakdown tp, PillSourceCostBreakdown vendor)
        {
            return new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Name = "Test Item",
                Quantity = 1,
                Decision = decision,
                CanCraft = true,
                CanBuyTp = true,
                CanBuyVendor = true,
                CraftCostBreakdown = craft,
                BuyFromTpCostBreakdown = tp,
                BuyFromVendorCostBreakdown = vendor,
            };
        }

        [Fact]
        public void DecisivelyLosingPill_GetsSubduedKindAndResult()
        {
            // TP selected (200 coin); CRAFT decisively dominates worse
            // (needs the same coin plus more); VENDOR ties (not subdued).
            var tp = Breakdown(200, 200);
            var craft = Breakdown(500, 500);
            var vendor = Breakdown(200, 200);

            var node = ThreeOptionNode(CraftingDecision.BuyFromTp, craft, tp, vendor);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            var craftPill = specs.Single(s => s.Text == "CRAFT");
            var vendorPill = specs.Single(s => s.Text == "VENDOR");
            var tpPill = specs.Single(s => s.Text == "TP");

            Assert.Equal(PillKind.Subdued, craftPill.Kind);
            Assert.NotNull(craftPill.SubduingResult);
            Assert.Equal(PillSubduingRule.StrictDomination, craftPill.SubduingResult.Rule);
            // Still a real, clickable override - only the styling/tooltip
            // changed.
            Assert.Equal(AcquisitionSource.Craft, craftPill.Source);

            Assert.Equal(PillKind.Selected, tpPill.Kind);
            Assert.Null(tpPill.SubduingResult);

            Assert.Equal(PillKind.Available, vendorPill.Kind);
            Assert.Null(vendorPill.SubduingResult);
        }

        [Fact]
        public void SelectedPill_NeverSubdued()
        {
            // Even a pathological breakdown shape can never mark the
            // SELECTED pill itself Subdued - BuildPillSpecs only ever
            // compares non-selected options against it.
            var craft = Breakdown(200, 200);
            var tp = Breakdown(999, 999); // selected, "loses" to craft on paper
            var vendor = Breakdown(999, 999);

            var node = ThreeOptionNode(CraftingDecision.BuyFromTp, craft, tp, vendor);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            var selected = specs.Single(s => s.Kind == PillKind.Selected);
            Assert.Equal("TP", selected.Text);
            Assert.Null(selected.SubduingResult);
        }

        [Fact]
        public void NonDecisiveLosingPill_ExactTie_StaysAvailable()
        {
            var tp = Breakdown(200, 200);
            var craft = Breakdown(200, 200); // exact tie - not decisive
            var vendor = Breakdown(200, 200); // exact tie - not decisive

            var node = ThreeOptionNode(CraftingDecision.BuyFromTp, craft, tp, vendor);
            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            foreach (var pill in specs.Where(s => s.Text == "CRAFT" || s.Text == "VENDOR"))
            {
                Assert.Equal(PillKind.Available, pill.Kind);
                Assert.Null(pill.SubduingResult);
            }
        }

        [Fact]
        public void NullBreakdowns_NeverSubdued_MatchesPreExistingNoDataBehavior()
        {
            // A node built without any breakdown data (every pre-existing
            // caller/test) must reproduce pre-existing behavior exactly -
            // GetCostBreakdown returns null, Evaluate returns None.
            var node = new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Name = "Test Item",
                Quantity = 1,
                Decision = CraftingDecision.BuyFromTp,
                CanCraft = true,
                CanBuyTp = true,
                CanBuyVendor = true,
            };

            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            foreach (var spec in specs.Where(s => s.Kind != PillKind.Selected && s.Kind != PillKind.Ignore))
            {
                Assert.Equal(PillKind.Available, spec.Kind);
                Assert.Null(spec.SubduingResult);
            }
        }

        [Fact]
        public void SingleOptionLockedNode_NeverSubdued()
        {
            var node = new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Name = "Test Item",
                Quantity = 1,
                Decision = CraftingDecision.Craft,
                CanCraft = true,
                CraftCostBreakdown = Breakdown(999, 999),
            };

            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            var craftPill = specs.Single(s => s.Text == "CRAFT");
            Assert.Equal(PillKind.Locked, craftPill.Kind);
            Assert.Null(craftPill.SubduingResult);
        }

        [Fact]
        public void VendorComponentCostsUnreliable_SuppressesSubduingEntirely()
        {
            // A merged multi-occurrence vendor step's BuyFromVendorCostBreakdown
            // could disagree with the corrected TotalCost (see
            // CraftingTreeNode.VendorComponentCostsUnreliable's own doc
            // comment) - the SAME conservative posture
            // ValueDetailTooltipBuilder/BuildVendorCostComponentLeaves
            // already take, so subduing must be suppressed entirely rather
            // than risk a wrong verdict off stale numbers.
            var craft = Breakdown(500, 500);
            var tp = Breakdown(500, 500);
            var vendor = Breakdown(200, 200); // selected, "always cheaper" on paper

            var node = ThreeOptionNode(CraftingDecision.BuyFromVendor, craft, tp, vendor);
            node.VendorComponentCostsUnreliable = true;

            var specs = DecisionPillPlanner.BuildPillSpecs(node);

            foreach (var pill in specs.Where(s => s.Text == "CRAFT" || s.Text == "TP"))
            {
                Assert.Equal(PillKind.Available, pill.Kind);
                Assert.Null(pill.SubduingResult);
            }
        }
    }
}
