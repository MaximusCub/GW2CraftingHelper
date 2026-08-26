using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using GW2CraftingHelper.Tests.Helpers;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// Every head line a decision pill's tooltip can carry, and which of
    /// them invite the subduing block after it.
    ///
    /// <para>
    /// The expected strings below were read off the pre-extraction
    /// TreeSectionController.RenderDecisionPills, where the same branches
    /// lived inline among the control construction and no test could reach
    /// them. Holding them here is what makes the extraction provably
    /// wording-preserving rather than merely compiling.
    /// </para>
    /// </summary>
    public class PillTooltipTextComposerTests
    {
        [Fact]
        public void AClickableSourcePillOffersToSwitchAndInvitesTheSubduingBlock()
        {
            var plan = Compose(Spec("BUY TP", AcquisitionSource.BuyFromTp, PillKind.Available), Node(), interactive: true);

            Assert.Equal("Switch to BUY TP", plan.Text);
            Assert.True(plan.AppendSubduing);
        }

        [Fact]
        public void AClickableSubduedPillKeepsTheSwitchWordingAndInvitesTheSubduingBlock()
        {
            var plan = Compose(Spec("CRAFT", AcquisitionSource.Craft, PillKind.Subdued), Node(), interactive: true);

            Assert.Equal("Switch to CRAFT", plan.Text);
            Assert.True(plan.AppendSubduing);
        }

        [Fact]
        public void AnUnclickableSubduedPillHasNoHeadLineButStillInvitesTheSubduingBlock()
        {
            var plan = Compose(Spec("CRAFT", AcquisitionSource.Craft, PillKind.Subdued), Node(), interactive: false);

            Assert.Null(plan.Text);
            Assert.True(plan.AppendSubduing);
        }

        [Theory]
        [InlineData(false, "Treat this item as fully in-hand (ignore its owned-stock requirement)")]
        [InlineData(true, "Stop treating this item as fully in-hand")]
        public void TheIgnoreToggleNamesTheDirectionItWouldMove(bool isIgnored, string expected)
        {
            var node = Node();
            node.IsIgnored = isIgnored;

            var plan = Compose(Spec("IGNORE", null, PillKind.Ignore), node, ignoreInteractive: true);

            Assert.Equal(expected, plan.Text);
            Assert.False(plan.AppendSubduing);
        }

        [Fact]
        public void ACostComponentsLockedBadgeExplainsTheBlankCostCell()
        {
            var node = Node();
            node.IsCostComponent = true;

            var plan = Compose(Spec("CURRENCY", null, PillKind.Locked), node);

            Assert.Equal("Paid in a non-coin currency - no gold value to show here", plan.Text);
        }

        [Theory]
        [InlineData(nameof(CraftingDecision.Unknown), null, "No known acquisition source")]
        [InlineData(nameof(CraftingDecision.Unknown), "Drops from Zhaitan", "Drops from Zhaitan")]
        [InlineData(nameof(CraftingDecision.GuildUpgrade), null, "Requires a claimed Guild Hall upgrade")]
        [InlineData(nameof(CraftingDecision.GuildUpgrade), "Unlock in your guild hall", "Unlock in your guild hall")]
        [InlineData(nameof(CraftingDecision.UnrecognizedIngredient), null, "Unrecognized ingredient type - no known acquisition source")]
        [InlineData(nameof(CraftingDecision.Currency), null, "Paid from your wallet as a game currency - no purchase or crafting source applies")]
        [InlineData(nameof(CraftingDecision.BuyFromVendor), null, "Only available source")]
        [InlineData(nameof(CraftingDecision.Craft), null, "Only available source")]
        public void ALockedPillNamesWhyThereIsNoChoice(
            string decisionName, string acquisitionHint, string expected)
        {
            var node = Node();
            node.Decision = EnumArg.Parse<CraftingDecision>(decisionName);
            node.AcquisitionHint = acquisitionHint;

            var plan = Compose(Spec("LOCKED", null, PillKind.Locked), node);

            Assert.Equal(expected, plan.Text);
            Assert.False(plan.AppendSubduing);
        }

        /// <summary>
        /// The UnrecognizedIngredient branch must beat the "Only available
        /// source" default even when a hint is somehow set, because unlike
        /// Unknown/GuildUpgrade it never reads one.
        /// </summary>
        [Fact]
        public void AnUnrecognizedIngredientIgnoresAnyHint()
        {
            var node = Node();
            node.Decision = CraftingDecision.UnrecognizedIngredient;
            node.AcquisitionHint = "should not be shown";

            var plan = Compose(Spec("UNRECOGNIZED", null, PillKind.Locked), node);

            Assert.Equal("Unrecognized ingredient type - no known acquisition source", plan.Text);
        }

        [Fact]
        public void TheCommittedPillNamesTheCurrentSource()
        {
            var plan = Compose(Spec("CRAFT", AcquisitionSource.Craft, PillKind.Selected), Node());

            Assert.Equal("Current source: CRAFT", plan.Text);
        }

        [Fact]
        public void ACurrencyLeafsHavePillReportsPlanScopeCoverageAndThisRowsNeed()
        {
            var node = Node();
            node.ItemId = 2002;
            node.Quantity = 30;
            node.Decision = CraftingDecision.Currency;

            var plan = Compose(
                Spec("HAVE 100/250", null, PillKind.Have), node,
                currencyPlanTotals: new Dictionary<int, long> { { 2002, 250L } },
                ownedCurrencyAmounts: new Dictionary<int, int> { { 2002, 100 } });

            Assert.Equal("Plan needs 250 total, you have 100 - short 150. This row needs 30.", plan.Text);
        }

        [Fact]
        public void AFullyCoveredCurrencyLeafSaysSoRatherThanReportingANegativeShortfall()
        {
            var node = Node();
            node.ItemId = 2002;
            node.Quantity = 30;
            node.Decision = CraftingDecision.Currency;

            var plan = Compose(
                Spec("HAVE 400/250", null, PillKind.Have), node,
                currencyPlanTotals: new Dictionary<int, long> { { 2002, 250L } },
                ownedCurrencyAmounts: new Dictionary<int, int> { { 2002, 400 } });

            Assert.Equal("Plan needs 250 total, you have 400 - fully covered. This row needs 30.", plan.Text);
        }

        /// <summary>
        /// Absent dictionaries are the restored-plan case; the wording must
        /// still be a sentence, not a null reference.
        /// </summary>
        [Fact]
        public void ACurrencyLeafWithNoPlanTotalsReadsAsZeroOfZero()
        {
            var node = Node();
            node.ItemId = 2002;
            node.Quantity = 5;
            node.Decision = CraftingDecision.Currency;

            var plan = Compose(Spec("HAVE", null, PillKind.OwnedInfo), node);

            Assert.Equal("Plan needs 0 total, you have 0 - fully covered. This row needs 5.", plan.Text);
        }

        /// <summary>
        /// A currency-shaped cost component (no SubtreeCost) takes the same
        /// plan-scope branch as a Currency decision.
        /// </summary>
        [Fact]
        public void ACurrencyShapedCostComponentTakesThePlanScopeBranch()
        {
            var node = Node();
            node.ItemId = 2002;
            node.Quantity = 4;
            node.Decision = CraftingDecision.BuyFromVendor;
            node.IsCostComponent = true;
            node.SubtreeCost = null;

            var plan = Compose(Spec("HAVE", null, PillKind.OwnedInfo), node);

            Assert.Equal("Plan needs 0 total, you have 0 - fully covered. This row needs 4.", plan.Text);
        }

        [Fact]
        public void AnOwnedItemsHavePillCountsTheDemandItCovered()
        {
            var node = Node();
            node.Quantity = 0;
            node.OwnedQuantityUsed = 17;
            node.Decision = CraftingDecision.Have;

            var plan = Compose(Spec("HAVE", null, PillKind.Have), node);

            Assert.Equal("Needs 17 - all covered by your materials", plan.Text);
        }

        [Fact]
        public void APartiallyOwnedItemsAnnotationSplitsCoveredFromRemaining()
        {
            var node = Node();
            node.Quantity = 3;
            node.OwnedQuantityUsed = 5;

            var plan = Compose(Spec("HAVE 5/8 NEEDED", null, PillKind.OwnedInfo), node);

            Assert.Equal(
                "Needs 8 total - 5 covered by your materials, 3 left to acquire", plan.Text);
        }

        [Fact]
        public void AnItemCostComponentsOwnBadgeSaysItChangesNothing()
        {
            var node = Node();
            node.IsCostComponent = true;
            node.ComponentOwnedQuantity = 9;

            // A non-null SubtreeCost is what keeps this off the currency
            // branch above - the item-line shape of a cost component.
            node.SubtreeCost = 1234L;

            var plan = Compose(Spec("OWN 9", null, PillKind.OwnedInfo), node);

            Assert.Equal("You own 9 - informational only, does not change the plan cost", plan.Text);
        }

        [Fact]
        public void TheDedupBadgeSaysTheItemIsObtainedOnceNotOwned()
        {
            var plan = Compose(Spec("COUNTED ELSEWHERE", null, PillKind.AchievementBitDeduped), Node());

            Assert.Equal(
                "Already counted elsewhere in the tree - this item is obtained once, not needed again here",
                plan.Text);
        }

        /// <summary>
        /// An inert IGNORE pill on a dimmed row falls through every branch;
        /// the renderer's dead-click line is the only thing left to say.
        /// </summary>
        [Fact]
        public void AnInertIgnorePillHasNoHeadLine()
        {
            var plan = Compose(Spec("IGNORE", null, PillKind.Ignore), Node());

            Assert.Null(plan.Text);
            Assert.False(plan.AppendSubduing);
        }

        private static PillTooltipPlan Compose(
            PillSpec spec,
            CraftingTreeNode node,
            bool interactive = false,
            bool ignoreInteractive = false,
            IReadOnlyDictionary<int, long> currencyPlanTotals = null,
            IReadOnlyDictionary<int, int> ownedCurrencyAmounts = null)
        {
            return PillTooltipTextComposer.Compose(
                spec, node, interactive, ignoreInteractive, currencyPlanTotals, ownedCurrencyAmounts);
        }

        private static PillSpec Spec(string text, AcquisitionSource? source, PillKind kind)
        {
            return new PillSpec(text, source, kind);
        }

        private static CraftingTreeNode Node()
        {
            return new CraftingTreeNode
            {
                ItemId = 1001,
                NodeId = 1,
                Name = "Test Item",
                Quantity = 1,
                Decision = CraftingDecision.BuyFromTp,
            };
        }
    }
}
