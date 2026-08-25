using System.Collections.Generic;
using GW2CraftingHelper.Models;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The Snapshot tab's click-to-sort comparators, over the real
    /// TableSortState cycle its headers drive.
    /// </summary>
    public class SnapshotTableSorterTests
    {
        private static List<SnapshotSearchRow> Items()
        {
            return new List<SnapshotSearchRow>
            {
                new SnapshotSearchRow { Name = "Mystic Clover", TotalCount = 30 },
                new SnapshotSearchRow { Name = "copper ore", TotalCount = 250 },
                new SnapshotSearchRow { Name = "Ectoplasm", TotalCount = 30 }
            };
        }

        private static List<SnapshotWalletEntry> Wallet()
        {
            return new List<SnapshotWalletEntry>
            {
                new SnapshotWalletEntry { CurrencyName = "Karma", Value = 1200 },
                new SnapshotWalletEntry { CurrencyName = "Astral Acclaim", Value = 80 }
            };
        }

        private static TableSortState<SnapshotTableColumn> Clicked(SnapshotTableColumn column, int times)
        {
            var state = new TableSortState<SnapshotTableColumn>();
            for (int i = 0; i < times; i++)
            {
                state.Cycle(column);
            }

            return state;
        }

        [Fact]
        public void NameSort_IsCaseInsensitive_AndNeverMutatesTheInput()
        {
            var rows = Items();
            var order = SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Name, 1));

            Assert.Equal(new[] { "copper ore", "Ectoplasm", "Mystic Clover" }, Names(rows, order));
            Assert.Equal(new[] { "Mystic Clover", "copper ore", "Ectoplasm" }, Names(rows, null));
        }

        [Fact]
        public void SecondClick_Reverses_AndThirdRestoresTheSearchOrder()
        {
            var rows = Items();

            Assert.Equal(
                new[] { "Mystic Clover", "Ectoplasm", "copper ore" },
                Names(rows, SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Name, 2))));

            // Third click resets to None. The view keeps no copy of the
            // search's order - its cells ARE in that order - so this has
            // to come back as "leave them alone", not as a permutation.
            Assert.Null(SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Name, 3)));
            Assert.Null(SnapshotTableSorter.ItemOrder(rows, new TableSortState<SnapshotTableColumn>()));
            Assert.Null(SnapshotTableSorter.ItemOrder(rows, null));
        }

        [Fact]
        public void AmountSort_OrdersByCount_AndTiesKeepTheirOriginalOrder()
        {
            var rows = Items();
            var order = SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Amount, 1));

            // 30 and 30 tie: "Mystic Clover" was emitted first and stays
            // first, so a sort never silently reshuffles equal rows.
            Assert.Equal(new[] { "Mystic Clover", "Ectoplasm", "copper ore" }, Names(rows, order));
        }

        [Fact]
        public void WalletRuns_SortByTheSameTwoColumns()
        {
            var rows = Wallet();
            var byName = SnapshotTableSorter.WalletOrder(rows, Clicked(SnapshotTableColumn.Name, 1));
            var byAmount = SnapshotTableSorter.WalletOrder(rows, Clicked(SnapshotTableColumn.Amount, 2));

            Assert.Equal("Astral Acclaim", rows[byName[0]].CurrencyName);
            Assert.Equal("Karma", rows[byAmount[0]].CurrencyName);
            Assert.Null(SnapshotTableSorter.WalletOrder(rows, null));
        }

        [Fact]
        public void NullNames_DoNotThrow()
        {
            var rows = new List<SnapshotSearchRow>
            {
                new SnapshotSearchRow { Name = null, TotalCount = 1 },
                new SnapshotSearchRow { Name = "Bolt of Silk", TotalCount = 2 }
            };

            var order = SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Name, 1));

            Assert.Null(rows[order[0]].Name);
        }

        [Fact]
        public void ADegenerateRun_HasNoOrderToApply()
        {
            Assert.Null(SnapshotTableSorter.ItemOrder(null, Clicked(SnapshotTableColumn.Name, 1)));
            Assert.Null(SnapshotTableSorter.ItemOrder(
                new List<SnapshotSearchRow>(), Clicked(SnapshotTableColumn.Name, 1)));
            Assert.Null(SnapshotTableSorter.ItemOrder(
                new List<SnapshotSearchRow> { new SnapshotSearchRow { Name = "Only" } },
                Clicked(SnapshotTableColumn.Name, 1)));
        }

        [Fact]
        public void EveryOrder_IsAPermutation_NoRowDroppedOrDoubled()
        {
            var rows = Items();
            var order = SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Amount, 1));

            var seen = new HashSet<int>();
            foreach (int index in order)
            {
                Assert.InRange(index, 0, rows.Count - 1);
                Assert.True(seen.Add(index), "an index may appear once");
            }

            Assert.Equal(rows.Count, seen.Count);
        }

        /// <summary>
        /// The rows as the tab would show them: display position i reads
        /// row order[i], which is the indexing step MainView.PlaceCells
        /// does over the controls it already built. A null order is the
        /// list as the search produced it.
        /// </summary>
        private static string[] Names(IReadOnlyList<SnapshotSearchRow> rows, IReadOnlyList<int> order)
        {
            var names = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                names[i] = rows[order == null ? i : order[i]].Name;
            }

            return names;
        }
    }
}
