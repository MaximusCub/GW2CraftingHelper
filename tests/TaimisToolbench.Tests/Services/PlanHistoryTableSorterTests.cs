using System;
using System.Collections.Generic;
using System.Linq;
using TaimisToolbench.Models;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The Plan History tab's clickable column headers, driven through the
    /// real <see cref="TableSortState{TColumn}"/> cycle its headers run
    /// rather than by handing the sorter a hand-built state - a click count
    /// is what the user actually performs.
    /// </summary>
    public class PlanHistoryTableSorterTests
    {
        private static PlanHistoryEntry Entry(
            string id, string itemName, long cost, int generatedDaysAgo, bool pinned = false)
        {
            return new PlanHistoryEntry
            {
                EntryId = id,
                Pinned = pinned,
                TotalCoinCostAtGeneration = cost,
                LastGeneratedAtUtc = new DateTime(2026, 8, 20, 12, 0, 0, DateTimeKind.Utc)
                    .AddDays(-generatedDaysAgo),
                ItemSummaries = new List<PlanHistoryItemSummary>
                {
                    new PlanHistoryItemSummary { Name = itemName, Quantity = 1 },
                },
            };
        }

        private static List<PlanHistoryEntry> Corpus()
        {
            return new List<PlanHistoryEntry>
            {
                Entry("a", "Bolt of Damask", 50_000, generatedDaysAgo: 1),
                Entry("b", "aetherized Metal", 10_000, generatedDaysAgo: 3),
                Entry("c", "Chak Egg Sac", 900_000, generatedDaysAgo: 2),
            };
        }

        private static TableSortState<PlanHistoryTableColumn> Clicked(
            PlanHistoryTableColumn column, int times)
        {
            var state = new TableSortState<PlanHistoryTableColumn>();
            for (int i = 0; i < times; i++)
            {
                state.Cycle(column);
            }

            return state;
        }

        private static IReadOnlyList<string> Ids(IReadOnlyList<PlanHistoryEntry> entries)
        {
            return entries.Select(e => e.EntryId).ToList();
        }

        [Fact]
        public void Unsorted_ReturnsTheCallersOwnList()
        {
            var rows = Corpus();

            Assert.Same(
                rows, PlanHistoryTableSorter.Sort(rows, new TableSortState<PlanHistoryTableColumn>()));
        }

        [Fact]
        public void ThirdClick_RestoresTheDefaultOrder()
        {
            var rows = Corpus();

            Assert.Same(
                rows,
                PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 3)));
        }

        [Fact]
        public void Cost_SortsCheapestFirstThenReverses()
        {
            var rows = Corpus();

            Assert.Equal(
                new[] { "b", "a", "c" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 1))));
            Assert.Equal(
                new[] { "c", "a", "b" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 2))));
        }

        [Fact]
        public void Generated_SortsOldestFirst()
        {
            var rows = Corpus();

            Assert.Equal(
                new[] { "b", "c", "a" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Generated, 1))));
        }

        [Fact]
        public void Plan_SortsByTheLabelTheRowActuallyShows_CaseInsensitively()
        {
            var rows = Corpus();

            // "aetherized Metal" is lowercase-initial on purpose: an
            // ordinal sort would file it after every capitalised name.
            Assert.Equal(
                new[] { "b", "a", "c" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Plan, 1))));
        }

        [Fact]
        public void SortingOverridesThePinFirstDefault()
        {
            // Pinned entries lead the default order. Asked for the cheapest
            // plan, a reader means the cheapest plan.
            var rows = new List<PlanHistoryEntry>
            {
                Entry("pinned", "Zojja's Claymore", 900_000, generatedDaysAgo: 1, pinned: true),
                Entry("cheap", "Copper Ingot", 10, generatedDaysAgo: 2),
            };

            Assert.Equal(
                new[] { "cheap", "pinned" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 1))));
        }

        [Fact]
        public void TiesKeepTheirIncomingOrder()
        {
            var rows = new List<PlanHistoryEntry>
            {
                Entry("first", "Same Cost A", 500, generatedDaysAgo: 1),
                Entry("second", "Same Cost B", 500, generatedDaysAgo: 2),
                Entry("third", "Same Cost C", 500, generatedDaysAgo: 3),
            };

            Assert.Equal(
                new[] { "first", "second", "third" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 1))));

            // And descending reverses the KEY, not the tie order.
            Assert.Equal(
                new[] { "first", "second", "third" },
                Ids(PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 2))));
        }

        [Fact]
        public void ShortAndEmptyLists_AreHandedBackUntouched()
        {
            var one = new List<PlanHistoryEntry> { Entry("only", "Item", 1, 1) };
            var none = new List<PlanHistoryEntry>();

            Assert.Same(one, PlanHistoryTableSorter.Sort(one, Clicked(PlanHistoryTableColumn.Cost, 1)));
            Assert.Same(none, PlanHistoryTableSorter.Sort(none, Clicked(PlanHistoryTableColumn.Cost, 1)));
            Assert.Null(PlanHistoryTableSorter.Sort(null, Clicked(PlanHistoryTableColumn.Cost, 1)));
        }

        [Fact]
        public void ANullEntry_SortsRatherThanThrowing()
        {
            // The list is rebuilt from a file on disk; a null has reached
            // row-building code before.
            var rows = new List<PlanHistoryEntry> { null, Entry("real", "Item", 500, 1) };

            var sorted = PlanHistoryTableSorter.Sort(rows, Clicked(PlanHistoryTableColumn.Cost, 1));

            Assert.Equal(2, sorted.Count);
            Assert.Null(sorted[0]);
            Assert.Equal("real", sorted[1].EntryId);
        }
    }
}
