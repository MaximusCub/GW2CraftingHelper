using TaimisToolbench.Models;
using TaimisToolbench.Services;
using System.Collections.Generic;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// Ordering behind the Used Materials / Shopping List column headers,
    /// including the Each/Total columns' three-block rule for cells that
    /// are coin, non-coin currency, or unpriceable.
    /// </summary>
    public class PlanTableSorterTests
    {
        private static PlanRowViewModel Row(
            string label, int quantity = 0, long coin = 0, long unitCoin = 0,
            List<CurrencyAmountViewModel> currencies = null,
            List<CurrencyAmountViewModel> unitCurrencies = null)
        {
            return new PlanRowViewModel
            {
                Label = label,
                Quantity = quantity,
                CoinValue = coin,
                UnitCoinValue = unitCoin,
                CurrencyCosts = currencies,
                UnitCurrencyCosts = unitCurrencies,
            };
        }

        private static List<CurrencyAmountViewModel> Currency(string name, long amount)
        {
            return new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Name = name, Amount = amount },
            };
        }

        private static TableSortState<PlanTableColumn> Sorted(PlanTableColumn column, TableSortDirection direction)
        {
            var state = new TableSortState<PlanTableColumn>();
            state.Cycle(column);
            if (direction == TableSortDirection.Descending)
            {
                state.Cycle(column);
            }

            return state;
        }

        private static List<string> Labels(IReadOnlyList<PlanRowViewModel> rows)
        {
            var labels = new List<string>();
            foreach (var row in rows)
            {
                labels.Add(row.Label);
            }

            return labels;
        }

        [Fact]
        public void NoSortState_ReturnsTheSameListInstance()
        {
            var rows = new List<PlanRowViewModel> { Row("B"), Row("A") };

            Assert.Same(rows, PlanTableSorter.Sort(rows, null));
            Assert.Same(rows, PlanTableSorter.Sort(rows, new TableSortState<PlanTableColumn>()));
        }

        [Fact]
        public void NullOrSingleRowList_IsReturnedUntouched()
        {
            var one = new List<PlanRowViewModel> { Row("A") };

            Assert.Null(PlanTableSorter.Sort(null, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending)));
            Assert.Same(one, PlanTableSorter.Sort(one, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending)));
        }

        [Fact]
        public void Sorting_DoesNotMutateTheCallersList()
        {
            var rows = new List<PlanRowViewModel> { Row("B"), Row("A") };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "A", "B" }, Labels(sorted));
            Assert.Equal(new List<string> { "B", "A" }, Labels(rows));
        }

        [Fact]
        public void ItemColumn_SortsByNameIgnoringCase()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("orichalcum ingot"), Row("Ancient Wood Log"), Row("Bolt of Damask"),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Item, TableSortDirection.Descending));

            Assert.Equal(
                new List<string> { "Ancient Wood Log", "Bolt of Damask", "orichalcum ingot" }, Labels(ascending));
            Assert.Equal(
                new List<string> { "orichalcum ingot", "Bolt of Damask", "Ancient Wood Log" }, Labels(descending));
        }

        [Fact]
        public void ItemColumn_NullLabelSortsAsEmptyString()
        {
            var rows = new List<PlanRowViewModel> { Row("Anything"), Row(null) };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending));

            Assert.Null(sorted[0].Label);
            Assert.Equal("Anything", sorted[1].Label);
        }

        [Fact]
        public void AmountColumn_SortsNumerically_NotLexicographically()
        {
            // The in-game fixture's own amounts, plus a single-digit row:
            // a string sort would put "111" before "9".
            var rows = new List<PlanRowViewModel>
            {
                Row("Silk Scrap", 816), Row("Thick Leather Section", 111),
                Row("Mithril Ore", 9), Row("Elder Wood Log", 136),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Amount, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Amount, TableSortDirection.Descending));

            Assert.Equal(new List<int> { 9, 111, 136, 816 }, Quantities(ascending));
            Assert.Equal(new List<int> { 816, 136, 111, 9 }, Quantities(descending));
        }

        [Fact]
        public void EqualKeys_KeepTheirOriginalRelativeOrder()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("first", 5), Row("second", 5), Row("third", 5), Row("fourth", 1),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Amount, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Amount, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "fourth", "first", "second", "third" }, Labels(ascending));
            Assert.Equal(new List<string> { "first", "second", "third", "fourth" }, Labels(descending));
        }

        [Fact]
        public void TotalColumn_SortsCoinRowsByCopperValue()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("expensive", coin: 1234567), Row("cheap", coin: 42), Row("middling", coin: 90000),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "cheap", "middling", "expensive" }, Labels(ascending));
            Assert.Equal(new List<string> { "expensive", "middling", "cheap" }, Labels(descending));
        }

        [Fact]
        public void EachColumn_ReadsTheUnitValues_NotTheTotals()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("bulk", coin: 5000, unitCoin: 10),
                Row("single", coin: 900, unitCoin: 900),
            };

            var byEach = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Each, TableSortDirection.Ascending));
            var byTotal = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "bulk", "single" }, Labels(byEach));
            Assert.Equal(new List<string> { "single", "bulk" }, Labels(byTotal));
        }

        [Fact]
        public void ValueColumns_GroupCoinRowsThenCurrencyRowsThenUnpriceableRows()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("unpriceable"),
                Row("karma", currencies: Currency("Karma", 5000)),
                Row("coin", coin: 100),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "coin", "karma", "unpriceable" }, Labels(ascending));

            // Block order is direction-invariant: only the order WITHIN a
            // block flips, so the dash rows never float to the top.
            Assert.Equal(new List<string> { "coin", "karma", "unpriceable" }, Labels(descending));
        }

        [Fact]
        public void ValueColumns_MixedCoinAndCurrencyRowSortsWithTheCoinRows()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("currency only", currencies: Currency("Spirit Shard", 1)),
                Row("mixed", coin: 500, currencies: Currency("Spirit Shard", 3)),
                Row("coin only", coin: 100),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "coin only", "mixed", "currency only" }, Labels(ascending));
        }

        [Fact]
        public void CurrencyRows_GroupByCurrencyNameThenAmount()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("shards 5", currencies: Currency("Spirit Shard", 5)),
                Row("karma 900", currencies: Currency("Karma", 900)),
                Row("shards 2", currencies: Currency("Spirit Shard", 2)),
                Row("karma 100", currencies: Currency("Karma", 100)),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Descending));

            Assert.Equal(
                new List<string> { "karma 100", "karma 900", "shards 2", "shards 5" }, Labels(ascending));
            Assert.Equal(
                new List<string> { "shards 5", "shards 2", "karma 900", "karma 100" }, Labels(descending));
        }

        private static List<CurrencyAmountViewModel> UnitCurrency(int perBatchCount, int outputCount)
        {
            // Through the real resolver, so the rows carry exactly what the
            // Shopping List renders - including the Amount 0 / "N for M"
            // shape a non-evenly-divisible rate produces.
            var metadata = new Dictionary<int, CurrencyMetadata>
            {
                { 23, new CurrencyMetadata { CurrencyId = 23, Name = "Spirit Shard" } },
            };
            return CurrencyDisplayResolver.ResolveUnitAmounts(
                outputCount, new List<CostLine> { new CostLine { Type = "Currency", Id = 23, Count = perBatchCount } },
                metadata);
        }

        [Fact]
        public void EachColumn_BundlePricedCurrencyRow_SortsOnItsTrueRate_NotItsZeroAmount()
        {
            // The live Philosopher's Stone case: 912 shards for 92 units is
            // ~9.9 each and renders as bundle text with Amount 0, so it must
            // still sort ABOVE a whole-number 5-each row.
            var rows = new List<PlanRowViewModel>
            {
                Row("philosopher's stone", unitCurrencies: UnitCurrency(912, 92)),
                Row("mystic coin", unitCurrencies: UnitCurrency(5, 1)),
            };
            Assert.Equal(0, rows[0].UnitCurrencyCosts[0].Amount);
            Assert.Equal("912 for 92", rows[0].UnitCurrencyCosts[0].BundleLabel);

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Each, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Each, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "mystic coin", "philosopher's stone" }, Labels(ascending));
            Assert.Equal(new List<string> { "philosopher's stone", "mystic coin" }, Labels(descending));
        }

        [Fact]
        public void EachColumn_TwoBundlePricedRowsInOneCurrency_OrderByRate_NotSourcePosition()
        {
            // Both rows key as Amount 0; only the true rates (0.67 vs 333.3)
            // separate them.
            var rows = new List<PlanRowViewModel>
            {
                Row("1000 for 3", unitCurrencies: UnitCurrency(1000, 3)),
                Row("2 for 3", unitCurrencies: UnitCurrency(2, 3)),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Each, TableSortDirection.Ascending));
            var descending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Each, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "2 for 3", "1000 for 3" }, Labels(ascending));
            Assert.Equal(new List<string> { "1000 for 3", "2 for 3" }, Labels(descending));
        }

        [Fact]
        public void MultiCurrencyRow_IsKeyedOnItsOrdinallyFirstCurrency_RegardlessOfListOrder()
        {
            var forward = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Name = "Spirit Shard", Amount = 40 },
                new CurrencyAmountViewModel { Name = "Karma", Amount = 3 },
            };
            var reversed = new List<CurrencyAmountViewModel>
            {
                new CurrencyAmountViewModel { Name = "Karma", Amount = 3 },
                new CurrencyAmountViewModel { Name = "Spirit Shard", Amount = 40 },
            };
            var rows = new List<PlanRowViewModel>
            {
                Row("shards only", currencies: Currency("Spirit Shard", 1)),
                Row("forward", currencies: forward),
                Row("reversed", currencies: reversed),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            // Both mixed-currency rows key on Karma 3, so they precede the
            // Spirit-Shard-only row and keep their own relative order.
            Assert.Equal(new List<string> { "forward", "reversed", "shards only" }, Labels(ascending));
        }

        [Fact]
        public void CurrencyRows_WithNullOrEmptyCostLists_SortAsUnpriceable()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("empty list", currencies: new List<CurrencyAmountViewModel>()),
                Row("null entry", currencies: new List<CurrencyAmountViewModel> { null }),
                Row("real currency", currencies: Currency("Karma", 1)),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "real currency", "empty list", "null entry" }, Labels(ascending));
        }

        [Fact]
        public void CurrencyRows_NullCurrencyNameSortsAsEmptyString()
        {
            var rows = new List<PlanRowViewModel>
            {
                Row("named", currencies: Currency("Karma", 1)),
                Row("unnamed", currencies: Currency(null, 1)),
            };

            var ascending = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "unnamed", "named" }, Labels(ascending));
        }

        [Fact]
        public void NullRowEntries_DoNotThrow()
        {
            var rows = new List<PlanRowViewModel> { Row("real", 5), null, Row("other", 1) };

            var byAmount = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Amount, TableSortDirection.Ascending));
            var byItem = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Item, TableSortDirection.Ascending));
            var byTotal = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Total, TableSortDirection.Ascending));

            Assert.Equal(3, byAmount.Count);
            Assert.Equal(3, byItem.Count);
            Assert.Equal(3, byTotal.Count);
        }

        // --- Source column (the fifth Shopping List column) ---
        private static PlanRowViewModel SourceRow(string label, PlanRowType rowType, string badgeText = null)
        {
            return new PlanRowViewModel { Label = label, RowType = rowType, BadgeText = badgeText };
        }

        [Fact]
        public void SourceColumn_GroupsRowsByTheBadgeTextTheyActuallyShow()
        {
            var rows = new List<PlanRowViewModel>
            {
                SourceRow("v", PlanRowType.ShoppingVendor),
                SourceRow("t1", PlanRowType.ShoppingBuy),
                SourceRow("c", PlanRowType.ShoppingCurrency),
                SourceRow("t2", PlanRowType.ShoppingBuy),
            };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Source, TableSortDirection.Ascending));

            // CURRENCY, TP, TP, VENDOR - and the two TP rows keep their
            // original relative order (the stable tie-break).
            Assert.Equal(new List<string> { "c", "t1", "t2", "v" }, Labels(sorted));
        }

        [Fact]
        public void SourceColumn_SortsBySeededBadge_NotByRowType()
        {
            // A seeded hint replaces "UNKNOWN" with its own badge on the
            // row, so a SALVAGE row must sort with the S's, not with the
            // other ShoppingUnknown rows. Sorting by PlanRowType would put
            // these two adjacent and call it a group.
            var rows = new List<PlanRowViewModel>
            {
                SourceRow("unknown", PlanRowType.ShoppingUnknown),
                SourceRow("tp", PlanRowType.ShoppingBuy),
                SourceRow("salvage", PlanRowType.ShoppingUnknown, badgeText: "SALVAGE"),
            };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Source, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "salvage", "tp", "unknown" }, Labels(sorted));
        }

        [Fact]
        public void SourceColumn_DescendingReversesTheGroups()
        {
            var rows = new List<PlanRowViewModel>
            {
                SourceRow("tp", PlanRowType.ShoppingBuy),
                SourceRow("vendor", PlanRowType.ShoppingVendor),
                SourceRow("currency", PlanRowType.ShoppingCurrency),
            };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Source, TableSortDirection.Descending));

            Assert.Equal(new List<string> { "vendor", "tp", "currency" }, Labels(sorted));
        }

        [Fact]
        public void SourceColumn_UnbadgedRowsDoNotThrowAndSortFirst()
        {
            // A row type the Shopping List does not emit has no badge at
            // all. It must not blow up the comparator.
            var rows = new List<PlanRowViewModel>
            {
                SourceRow("tp", PlanRowType.ShoppingBuy),
                SourceRow("nothing", PlanRowType.UsedMaterial),
            };

            var sorted = PlanTableSorter.Sort(rows, Sorted(PlanTableColumn.Source, TableSortDirection.Ascending));

            Assert.Equal(new List<string> { "nothing", "tp" }, Labels(sorted));
        }

        private static List<int> Quantities(IReadOnlyList<PlanRowViewModel> rows)
        {
            var quantities = new List<int>();
            foreach (var row in rows)
            {
                quantities.Add(row.Quantity);
            }

            return quantities;
        }
    }
}
