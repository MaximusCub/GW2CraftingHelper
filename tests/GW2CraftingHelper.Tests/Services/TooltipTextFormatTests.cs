using System.Collections.Generic;
using System.Linq;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    // The single wrap seam both tooltip composers return through. Blish's
    // own cap is a fixed 500px that knows nothing about this module's window
    // (see docs/KNOWN-ISSUES.md), so these tests are the only guarantee that
    // composed tooltip text stays inside a width the module chose.
    public class TooltipTextFormatTests
    {
        [Fact]
        public void Wrap_LongSingleLine_BreaksAtWordBoundariesUnderBudget()
        {
            string line = "A vendor cost item's buy-order price is unavailable - its instant-buy price is used";

            string[] wrapped = TooltipTextFormat.Wrap(line).Split('\n');

            Assert.True(wrapped.Length > 1);
            Assert.All(wrapped, l => Assert.True(l.Length <= TooltipTextFormat.LineBudgetChars, l));
            // Word boundaries, not mid-word: rejoining with the spaces the
            // wrap consumed reproduces the input exactly.
            Assert.Equal(line, string.Join(" ", wrapped));
        }

        [Fact]
        public void Wrap_ShortLines_ReturnedUntouched()
        {
            string text = "Crafting gold price: 0g 50s 0c\nCurrencies: 2g 50s 0c";

            Assert.Equal(text, TooltipTextFormat.Wrap(text));
        }

        [Fact]
        public void Wrap_BlankSeparatorLines_Preserved()
        {
            string text = "Currencies: 2g 50s 0c\n\nOptimization price: 3g 0s 0c";

            string[] wrapped = TooltipTextFormat.Wrap(text).Split('\n');

            Assert.Equal(3, wrapped.Length);
            Assert.Equal("", wrapped[1]);
        }

        [Fact]
        public void Wrap_ExistingHardBreaks_NotCollapsedIntoOneLine()
        {
            // Each source line keeps its own break even though both would
            // fit together inside one budgeted line.
            string text = "Unit price: 1g 0s 0c\nUnit price: 3 Karma";

            Assert.Equal(text, TooltipTextFormat.Wrap(text));
        }

        [Fact]
        public void Wrap_NullOrEmpty_ReturnedAsIs()
        {
            Assert.Null(TooltipTextFormat.Wrap(null));
            Assert.Equal("", TooltipTextFormat.Wrap(""));
        }

        [Fact]
        public void Wrap_WordLongerThanBudget_SplitRatherThanEllipsized()
        {
            // TextWrapMath hard-splits an unbreakable token instead of
            // dropping its tail, which is what this wrap exists to avoid.
            string word = new string('x', TooltipTextFormat.LineBudgetChars + 20);

            string[] wrapped = TooltipTextFormat.Wrap(word).Split('\n');

            Assert.All(wrapped, l => Assert.True(l.Length <= TooltipTextFormat.LineBudgetChars, l));
            Assert.Equal(word, string.Concat(wrapped));
        }

        [Fact]
        public void Wrap_ManyLongLines_NeverTruncates()
        {
            // Wrapping the composed string in ONE TextWrapMath call would
            // hit its MaxWrappedLines cap (24) and ellipsize the tail; the
            // per-source-line wrap keeps every line.
            var source = Enumerable
                .Range(0, 30)
                .Select(i => $"Line {i}: " + new string('a', 100))
                .ToList();

            string[] wrapped = TooltipTextFormat.Wrap(string.Join("\n", source)).Split('\n');

            Assert.DoesNotContain(wrapped, l => l.EndsWith(TextWrapMath.Ellipsis));
            Assert.True(wrapped.Length >= 60);
        }

        [Fact]
        public void WrapLines_NullInput_ReturnsEmptyList()
        {
            Assert.Empty(TooltipTextFormat.WrapLines(null));
        }

        [Fact]
        public void WrapLines_ExpandsOverBudgetEntryAndKeepsShortOnes()
        {
            var lines = new List<string>
            {
                "Right-click: Open wiki page",
                "A vendor cost item's instant-buy price is unavailable - its buy-order price is used"
            };

            var wrapped = TooltipTextFormat.WrapLines(lines);

            Assert.Equal("Right-click: Open wiki page", wrapped[0]);
            Assert.True(wrapped.Count > 2);
            Assert.All(wrapped, l => Assert.True(l.Length <= TooltipTextFormat.LineBudgetChars, l));
        }

        [Fact]
        public void RealOpportunityCostSentence_FitsBudgetOnEveryLine()
        {
            // The production string, reached through the real builder rather
            // than retyped here: 76 characters unwrapped.
            var node = new CraftingTreeNode
            {
                ItemId = 1,
                NodeId = 1,
                Decision = CraftingDecision.Craft,
                SubtreeCost = 5000,
                DecisionValue = 30000
            };

            Assert.True(ValueDetailTooltipBuilder.TryBuild(node, null, out string text));

            Assert.All(
                text.Split('\n'),
                l => Assert.True(l.Length <= TooltipTextFormat.LineBudgetChars, l));
            Assert.Contains(
                "This is an estimated opportunity cost for the used currencies in the recipe.",
                text.Replace("\n", " "));
        }
    }
}
