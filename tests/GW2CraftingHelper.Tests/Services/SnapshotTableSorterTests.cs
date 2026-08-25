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
        public void NoSort_ReturnsTheVerySameInstance()
        {
            var rows = Items();

            Assert.Same(rows, SnapshotTableSorter.SortItems(rows, new TableSortState<SnapshotTableColumn>()));
            Assert.Same(rows, SnapshotTableSorter.SortItems(rows, null));
        }

        [Fact]
        public void NameSort_IsCaseInsensitive_AndNeverMutatesTheInput()
        {
            var rows = Items();
            var sorted = SnapshotTableSorter.SortItems(rows, Clicked(SnapshotTableColumn.Name, 1));

            Assert.Equal(new[] { "copper ore", "Ectoplasm", "Mystic Clover" }, Names(sorted));
            Assert.Equal(new[] { "Mystic Clover", "copper ore", "Ectoplasm" }, Names(rows));
        }

        [Fact]
        public void SecondClick_Reverses_AndThirdRestoresTheSearchOrder()
        {
            var rows = Items();

            Assert.Equal(
                new[] { "Mystic Clover", "Ectoplasm", "copper ore" },
                Names(SnapshotTableSorter.SortItems(rows, Clicked(SnapshotTableColumn.Name, 2))));

            // Third click resets to None, which is the data source's order.
            Assert.Same(rows, SnapshotTableSorter.SortItems(rows, Clicked(SnapshotTableColumn.Name, 3)));
        }

        [Fact]
        public void AmountSort_OrdersByCount_AndTiesKeepTheirOriginalOrder()
        {
            var sorted = SnapshotTableSorter.SortItems(Items(), Clicked(SnapshotTableColumn.Amount, 1));

            // 30 and 30 tie: "Mystic Clover" was emitted first and stays
            // first, so a sort never silently reshuffles equal rows.
            Assert.Equal(new[] { "Mystic Clover", "Ectoplasm", "copper ore" }, Names(sorted));
        }

        [Fact]
        public void WalletRuns_SortByTheSameTwoColumns()
        {
            var byName = SnapshotTableSorter.SortWallet(Wallet(), Clicked(SnapshotTableColumn.Name, 1));
            var byAmount = SnapshotTableSorter.SortWallet(Wallet(), Clicked(SnapshotTableColumn.Amount, 2));

            Assert.Equal("Astral Acclaim", byName[0].CurrencyName);
            Assert.Equal("Karma", byAmount[0].CurrencyName);
        }

        [Fact]
        public void NullNames_DoNotThrow()
        {
            var rows = new List<SnapshotSearchRow>
            {
                new SnapshotSearchRow { Name = null, TotalCount = 1 },
                new SnapshotSearchRow { Name = "Bolt of Silk", TotalCount = 2 }
            };

            var sorted = SnapshotTableSorter.SortItems(rows, Clicked(SnapshotTableColumn.Name, 1));

            Assert.Null(sorted[0].Name);
        }

        // The order the Snapshot tab actually renders with: its rows are
        // built once in the search's order and a click re-PLACES them, so
        // the permutation is what the view consumes and SortItems is the
        // same permutation applied to a list. One has to follow the other.
        [Fact]
        public void ItemOrder_IsTheSamePermutationSortItemsApplies()
        {
            var rows = Items();
            var order = SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Amount, 2));
            var sorted = SnapshotTableSorter.SortItems(rows, Clicked(SnapshotTableColumn.Amount, 2));

            Assert.Equal(rows.Count, order.Count);
            for (int i = 0; i < order.Count; i++)
            {
                Assert.Same(sorted[i], rows[order[i]]);
            }
        }

        [Fact]
        public void WalletOrder_IsTheSamePermutationSortWalletApplies()
        {
            var rows = Wallet();
            var order = SnapshotTableSorter.WalletOrder(rows, Clicked(SnapshotTableColumn.Name, 1));
            var sorted = SnapshotTableSorter.SortWallet(rows, Clicked(SnapshotTableColumn.Name, 1));

            for (int i = 0; i < order.Count; i++)
            {
                Assert.Same(sorted[i], rows[order[i]]);
            }
        }

        // The third click. The view keeps no copy of the search's order -
        // its cells ARE in that order - so "no sort" has to come back as
        // "leave them alone" rather than as a permutation.
        [Fact]
        public void CyclingBackToNone_LeavesTheSearchsOwnOrder()
        {
            var rows = Items();

            Assert.Null(SnapshotTableSorter.ItemOrder(rows, Clicked(SnapshotTableColumn.Name, 3)));
            Assert.Null(SnapshotTableSorter.ItemOrder(rows, new TableSortState<SnapshotTableColumn>()));
            Assert.Null(SnapshotTableSorter.ItemOrder(rows, null));
            Assert.Null(SnapshotTableSorter.WalletOrder(Wallet(), null));
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

        private static string[] Names(IReadOnlyList<SnapshotSearchRow> rows)
        {
            var names = new string[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                names[i] = rows[i].Name;
            }

            return names;
        }
    }
}
