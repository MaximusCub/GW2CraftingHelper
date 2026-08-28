using System.Collections.Generic;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using TaimisToolbench.Tests.Helpers;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanHistoryLabelsTests
    {
        private static PlanHistoryEntry EntryWith(params (string Name, int Qty)[] items)
        {
            var summaries = new List<PlanHistoryItemSummary>();
            int id = 1;
            foreach (var (name, qty) in items)
            {
                summaries.Add(new PlanHistoryItemSummary { ItemId = id++, Name = name, Quantity = qty });
            }

            return new PlanHistoryEntry { ItemSummaries = summaries };
        }

        [Fact]
        public void ItemLineTexts_QuantitySuffixOnlyAboveOne()
        {
            var lines = PlanHistoryLabels.ItemLineTexts(EntryWith(("Twilight", 1), ("Mithril Ingot", 250)));

            Assert.Equal(new[] { "Twilight", "Mithril Ingot x250" }, lines);
        }

        [Fact]
        public void ItemLineTexts_UnnamedItemNeverShowsAnId()
        {
            var lines = PlanHistoryLabels.ItemLineTexts(EntryWith((null, 3)));

            Assert.Equal(new[] { PlanHistoryLabels.UnnamedItem + " x3" }, lines);
            Assert.DoesNotContain("1", lines[0]);
        }

        [Fact]
        public void ItemLineTexts_NullEntryOrSummaries_ReturnEmpty()
        {
            Assert.Empty(PlanHistoryLabels.ItemLineTexts(null));
            Assert.Empty(PlanHistoryLabels.ItemLineTexts(new PlanHistoryEntry()));
        }

        [Fact]
        public void RowLabel_CapsAtThreeEntriesWithPlusNMore()
        {
            string label = PlanHistoryLabels.RowLabel(
                EntryWith(("A", 1), ("B", 2), ("C", 1), ("D", 1), ("E", 1)));

            Assert.Equal("A, B x2, C, +2 more", label);
        }

        [Fact]
        public void FullItemList_IsUncapped()
        {
            string full = PlanHistoryLabels.FullItemList(
                EntryWith(("A", 1), ("B", 1), ("C", 1), ("D", 1)));

            Assert.Equal("A\nB\nC\nD", full);
        }

        [Theory]
        [InlineData(true, nameof(PriceBasis.BuyOrder), true,
            "Own materials: on   Prices: buy orders   Value own materials: on")]
        [InlineData(false, nameof(PriceBasis.InstantBuy), false,
            "Own materials: off   Prices: instant buy   Value own materials: off")]
        public void SettingsLine_SpellsAllThreeFlags(
            bool useOwn, string basisName, bool valueOwn, string expected)
        {
            var basis = EnumArg.Parse<PriceBasis>(basisName);
            Assert.Equal(expected, PlanHistoryLabels.SettingsLine(useOwn, basis, valueOwn));
        }
    }
}
