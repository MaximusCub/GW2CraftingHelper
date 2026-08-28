using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    // The three tooltip composers now build structure once and expose a
    // plain-text view of it. Two properties are worth pinning: the gold
    // figures survive as coin spans (without that the rich surface cannot
    // draw coin ICONS, which is the whole point of the migration), and the
    // plain view is still exactly what the composer used to return - the
    // existing per-composer test files assert the strings themselves, so
    // these tests only have to prove the two forms agree.
    public class TooltipComposerContentTests
    {
        private static IEnumerable<TooltipSpan> AllSpans(TooltipContent content)
        {
            return content.Lines.SelectMany(l => l.Spans);
        }

        private static CraftingTreeNode ValueNode(long subtreeCost, long decisionValue)
        {
            return new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Decision = CraftingDecision.Craft,
                SubtreeCost = subtreeCost,
                DecisionValue = decisionValue,
            };
        }

        // --- ValueDetailTooltipBuilder ---
        [Fact]
        public void ValueDetail_EveryGoldFigureIsACoinSpan()
        {
            Assert.True(ValueDetailTooltipBuilder.TryBuildContent(
                ValueNode(10000, 35000), null, out var content));

            long[] coins = AllSpans(content).Where(s => s.IsCoin).Select(s => s.CoinCopper).ToArray();

            // Real gold, the currency delta, and the optimization total.
            Assert.Equal(new long[] { 10000, 25000, 35000 }, coins);
        }

        [Fact]
        public void ValueDetail_RendersItsThreeFigureLines()
        {
            Assert.True(ValueDetailTooltipBuilder.TryBuildContent(
                ValueNode(10000, 35000), null, out var content));
            // Coin spelling changed with the CoinSegmentMath.GameStyleText
            // consolidation: every composer now spells a coin amount the
            // way the icons beside it do (leading all-zero units omitted,
            // trailing units zero-padded).
            Assert.Contains("Crafting gold price: 1g 0s 0c", content.ToPlainText());
            Assert.Contains("Optimization price: 3g 50s 0c", content.ToPlainText());
        }

        [Fact]
        public void ValueDetail_SeparatorLineBeforeOptimizationPriceSurvivesAsABlankLine()
        {
            Assert.True(ValueDetailTooltipBuilder.TryBuildContent(
                ValueNode(10000, 35000), null, out var content));

            var lines = content.ToPlainLines();
            int blank = lines.IndexOf("");

            Assert.True(blank > 0);
            Assert.StartsWith("Optimization price:", lines[blank + 1]);
            Assert.Empty(content.Lines[blank].Spans);
        }

        [Fact]
        public void ValueDetail_SuppressedCases_YieldNoContent()
        {
            Assert.False(ValueDetailTooltipBuilder.TryBuildContent(null, null, out var content));
            Assert.Null(content);
            Assert.False(ValueDetailTooltipBuilder.TryBuildContent(ValueNode(100, 100), null, out _));
        }

        // --- PillSubduingTooltipBuilder ---
        [Fact]
        public void Subduing_WeightedMargin_IsACoinSpanInsideItsSentence()
        {
            var result = new PillSubduingResult(PillSubduingRule.Weighted, 12345, null, hasNonCoinCost: true);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.Contains(AllSpans(content), s => s.IsCoin && s.CoinCopper == 12345);
            // The suffix after the coin run is still there - the layout has
            // to be able to place text on both sides of a coin span.
            Assert.Equal(
                "More expensive at your current currency values (1g 23s 45c more)",
                content.ToPlainText());
        }

        [Fact]
        public void Subduing_WeightedWithoutAMargin_HasNoCoinSpan()
        {
            var result = new PillSubduingResult(PillSubduingRule.Weighted, null, null);

            var content = PillSubduingTooltipBuilder.BuildContent(result, null, null);

            Assert.DoesNotContain(AllSpans(content), s => s.IsCoin);
            Assert.Equal("More expensive", content.ToPlainText());
        }

        [Fact]
        public void Subduing_StrictDominationCoinDelta_IsACoinSpanInACommaList()
        {
            var deltas = new List<PillCostDelta>
            {
                new PillCostDelta("Coin", 0, 5000),
                new PillCostDelta("Item", 100, 10),
            };
            var itemMetadata = new Dictionary<int, ItemMetadata>
            {
                { 100, new ItemMetadata { ItemId = 100, Name = "Glob of Ectoplasm" } },
            };
            var result = new PillSubduingResult(PillSubduingRule.StrictDomination, null, deltas);

            var content = PillSubduingTooltipBuilder.BuildContent(result, itemMetadata, null);

            Assert.Contains(AllSpans(content), s => s.IsCoin && s.CoinCopper == 5000);
            Assert.Equal(
                "Always more expensive - needs everything the selected option needs, " +
                "plus 50s 0c more, 10 more Glob of Ectoplasm",
                content.ToPlainText());
        }

        [Fact]
        public void Subduing_NoRule_YieldsNoContent()
        {
            Assert.Null(PillSubduingTooltipBuilder.BuildContent(PillSubduingResult.None, null, null));
            Assert.Null(PillSubduingTooltipBuilder.BuildContent(null, null, null));
        }

        // --- TreeRowTooltipComposer ---
        [Fact]
        public void TreeRow_UnitPrice_IsACoinSpan()
        {
            var node = new CraftingTreeNode
            {
                ItemId = 5,
                NodeId = 5,
                Name = "Mithril Ingot",
                Decision = CraftingDecision.BuyFromTp,
                Quantity = 4,
                UnitCost = 2345,
            };

            var content = TreeRowTooltipComposer.BuildExtraTooltipContent(node, null, null);

            Assert.Contains(AllSpans(content), s => s.IsCoin && s.CoinCopper == 2345);
            // Coin spelling changed with the CoinSegmentMath.GameStyleText
            // consolidation: every composer now spells a coin amount the
            // way the icons beside it do (leading all-zero units omitted,
            // trailing units zero-padded).
            Assert.Contains("Unit price: 23s 45c", content.ToPlainText());
        }

        [Fact]
        public void TreeRow_LongCaveat_StaysOneUnwrappedLine()
        {
            // The 83-character price-side caveat: still wrapped by the
            // character seam on the plain path, still one unwrapped line in
            // the structured form the rich surface wraps by pixels.
            var node = new CraftingTreeNode
            {
                ItemId = 5,
                NodeId = 5,
                Name = "Mithril Ingot",
                Decision = CraftingDecision.BuyFromVendor,
                PriceSideFellBack = true,
            };

            var plan = new PlanViewModel { PriceBasis = PriceBasis.BuyOrder };
            var content = TreeRowTooltipComposer.BuildExtraTooltipContent(node, null, plan);

            // Left long on purpose: the rich surface measures and wraps it
            // against a real font, so the composer must not pre-break it.
            Assert.Contains(content.ToPlainLines(), l => l.Length > TooltipTextFormat.LineBudgetChars);
        }

        [Fact]
        public void TreeRow_CaptionStillLandsAtTheFront()
        {
            var node = new CraftingTreeNode
            {
                ItemId = 5,
                NodeId = 5,
                Name = "Mithril Ingot",
                Decision = CraftingDecision.BuyFromTp,
                Quantity = 4,
                UnitCost = 100,
            };

            var content = TreeRowTooltipComposer.BuildExtraTooltipContent(node, "What-if: 4 extra", null);

            Assert.Equal("What-if: 4 extra", content.ToPlainLines()[0]);
        }

        [Fact]
        public void TreeRow_NullNode_YieldsEmptyContent()
        {
            Assert.True(TreeRowTooltipComposer.BuildExtraTooltipContent(null, null, null).IsEmpty);
        }
    }
}
