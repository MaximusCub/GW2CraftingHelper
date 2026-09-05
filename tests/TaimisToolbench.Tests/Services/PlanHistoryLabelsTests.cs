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

        // Reported in game: a Plan History hover showed only an icon
        // and a name. A one-item entry has no extra lines - its one
        // item is the header - so the stat body is the whole rest of the
        // tooltip, and this is what decides the row is owed one.
        [Fact]
        public void SingleItemId_OneItemEntry_ReturnsThatItemsId()
        {
            Assert.Equal(1, PlanHistoryLabels.SingleItemId(EntryWith(("Twilight", 1))));
        }

        [Fact]
        public void SingleItemId_MultiItemEntry_ReturnsZero()
        {
            Assert.Equal(0, PlanHistoryLabels.SingleItemId(
                EntryWith(("Twilight", 1), ("Mithril Ingot", 250))));
        }

        [Fact]
        public void SingleItemId_NullEntrySummariesOrEmptyList_ReturnZero()
        {
            Assert.Equal(0, PlanHistoryLabels.SingleItemId(null));
            Assert.Equal(0, PlanHistoryLabels.SingleItemId(new PlanHistoryEntry()));
            Assert.Equal(0, PlanHistoryLabels.SingleItemId(EntryWith()));
        }

        // A capture that predates the id being recorded names its item but
        // cannot key a stat lookup - 0, the same answer as a multi-item
        // entry, rather than a lookup of id 0.
        [Fact]
        public void SingleItemId_SummaryWithNoUsableId_ReturnsZero()
        {
            var entry = new PlanHistoryEntry
            {
                ItemSummaries = new List<PlanHistoryItemSummary>
                {
                    new PlanHistoryItemSummary { ItemId = 0, Name = "Twilight", Quantity = 1 },
                },
            };

            Assert.Equal(0, PlanHistoryLabels.SingleItemId(entry));
        }

        // Nulls are skipped by ItemLineTexts too, so the entry that renders
        // one line is the entry that counts as one item.
        [Fact]
        public void SingleItemId_NullSummariesSkippedLikeItemLineTexts()
        {
            var entry = new PlanHistoryEntry
            {
                ItemSummaries = new List<PlanHistoryItemSummary>
                {
                    null,
                    new PlanHistoryItemSummary { ItemId = 42, Name = "Twilight", Quantity = 1 },
                    null,
                },
            };

            Assert.Single(PlanHistoryLabels.ItemLineTexts(entry));
            Assert.Equal(42, PlanHistoryLabels.SingleItemId(entry));
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
