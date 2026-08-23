using GW2CraftingHelper.Models;
using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Columns the Crafting Plan's two sortable tables expose. Used
    /// Materials has Item/Amount only; the Shopping List has all four.
    /// </summary>
    public enum PlanTableColumn
    {
        Item,
        Amount,
        Each,
        Total
    }

    /// <summary>
    /// Comparators behind the clickable column headers of the Used
    /// Materials and Shopping List tables. Blish-free: it reorders the
    /// already-built row view models, so the renderers keep every column
    /// measurement and height calculation they had (row COUNT and row
    /// contents are untouched - only the order changes).
    /// <para>
    /// Sorting never mutates the caller's list. An unsorted table gets the
    /// very same instance back, so the default path allocates nothing.
    /// </para>
    /// </summary>
    public static class PlanTableSorter
    {
        /// <summary>
        /// Rows in the order the given sort state asks for, or
        /// <paramref name="rows"/> itself when no sort is active.
        /// Ties keep their original relative order (stable).
        /// </summary>
        public static IReadOnlyList<PlanRowViewModel> Sort(
            IReadOnlyList<PlanRowViewModel> rows, TableSortState<PlanTableColumn> state)
        {
            if (rows == null || rows.Count < 2) return rows;
            if (state == null || state.Direction == TableSortDirection.None || !state.Column.HasValue) return rows;

            PlanTableColumn column = state.Column.Value;
            TableSortDirection direction = state.Direction;

            var order = new int[rows.Count];
            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int compared = Compare(rows[a], rows[b], column, direction);
                return compared != 0 ? compared : a.CompareTo(b);
            });

            var sorted = new List<PlanRowViewModel>(rows.Count);
            for (int i = 0; i < order.Length; i++)
            {
                sorted.Add(rows[order[i]]);
            }
            return sorted;
        }

        private static int Compare(
            PlanRowViewModel a, PlanRowViewModel b, PlanTableColumn column, TableSortDirection direction)
        {
            switch (column)
            {
                case PlanTableColumn.Item:
                    return Flip(
                        string.Compare(a?.Label ?? string.Empty, b?.Label ?? string.Empty, StringComparison.OrdinalIgnoreCase),
                        direction);
                case PlanTableColumn.Amount:
                    return Flip((a?.Quantity ?? 0).CompareTo(b?.Quantity ?? 0), direction);
                case PlanTableColumn.Each:
                    return CompareValue(
                        a?.UnitCoinValue ?? 0, a?.UnitCurrencyCosts,
                        b?.UnitCoinValue ?? 0, b?.UnitCurrencyCosts, direction);
                case PlanTableColumn.Total:
                    return CompareValue(
                        a?.CoinValue ?? 0, a?.CurrencyCosts,
                        b?.CoinValue ?? 0, b?.CurrencyCosts, direction);
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Ordering for the Shopping List's Each/Total cells, which are not
        /// one scale but three kinds of cell: a coin price, a price paid in
        /// some non-coin currency (spirit shards, karma), and a genuinely
        /// unpriceable row that renders a dash. A copper amount and a
        /// spirit-shard amount are not comparable - the module's pricing
        /// rules forbid inventing an exchange rate between them - so the
        /// column sorts in three blocks: coin rows (including mixed
        /// coin+currency rows, keyed on their copper part, which is the one
        /// magnitude every coin row shares), then currency-only rows, then
        /// unpriceable rows.
        /// <para>
        /// The block order is deliberately direction-INVARIANT while the
        /// order WITHIN a block flips. Reversing the blocks would express
        /// nothing - "5 spirit shards" is neither more nor less than "3
        /// gold" in either direction - and it would float the dash rows to
        /// the top, where they are pure noise. Descending therefore means
        /// "most expensive coin row first, still followed by the currency
        /// rows, still followed by the dashes".
        /// </para>
        /// <para>
        /// Currency-only rows sort by currency name first (ordinal,
        /// case-insensitive) so every karma row lands beside every other
        /// karma row, then by amount within that currency - the only
        /// numeric comparison in that block that is actually meaningful.
        /// A row carrying more than one currency is keyed on its
        /// ordinally-first currency name (and that entry's amount), which
        /// is stable regardless of the order the resolver emitted them in;
        /// no attempt is made to add amounts across currencies.
        /// </para>
        /// <para>
        /// The numeric key inside a currency is
        /// <see cref="CurrencyAmountViewModel.UnitRate"/> when the resolver
        /// set one, NOT Amount: a per-unit amount whose rate does not
        /// divide evenly carries Amount 0 and shows its rate as bundle
        /// text ("912 for 92"), so keying on Amount would sort every such
        /// row as free and tie them all with each other.
        /// </para>
        /// </summary>
        private static int CompareValue(
            long aCoin, IReadOnlyList<CurrencyAmountViewModel> aCurrencies,
            long bCoin, IReadOnlyList<CurrencyAmountViewModel> bCurrencies,
            TableSortDirection direction)
        {
            int aBlock = ValueBlock(aCoin, aCurrencies);
            int bBlock = ValueBlock(bCoin, bCurrencies);
            if (aBlock != bBlock) return aBlock.CompareTo(bBlock);

            if (aBlock == CoinBlock)
            {
                return Flip(aCoin.CompareTo(bCoin), direction);
            }

            if (aBlock == CurrencyBlock)
            {
                CurrencyAmountViewModel aKey = KeyCurrency(aCurrencies);
                CurrencyAmountViewModel bKey = KeyCurrency(bCurrencies);
                int byName = string.Compare(
                    aKey?.Name ?? string.Empty, bKey?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                if (byName != 0) return Flip(byName, direction);
                return Flip(NumericKey(aKey).CompareTo(NumericKey(bKey)), direction);
            }

            return 0;
        }

        private const int CoinBlock = 0;
        private const int CurrencyBlock = 1;
        private const int UnpricedBlock = 2;

        /// <summary>
        /// Which of the three blocks a value cell belongs to. Mirrors what
        /// the renderer actually draws: CoinCurrencyRenderer treats a
        /// copper amount of 0 as "no coin part", and a cell with neither
        /// coin nor currency is the dash cell.
        /// </summary>
        private static int ValueBlock(long coin, IReadOnlyList<CurrencyAmountViewModel> currencies)
        {
            if (coin > 0) return CoinBlock;
            return KeyCurrency(currencies) != null ? CurrencyBlock : UnpricedBlock;
        }

        private static CurrencyAmountViewModel KeyCurrency(IReadOnlyList<CurrencyAmountViewModel> currencies)
        {
            if (currencies == null) return null;

            CurrencyAmountViewModel key = null;
            for (int i = 0; i < currencies.Count; i++)
            {
                var candidate = currencies[i];
                if (candidate == null) continue;
                if (key == null)
                {
                    key = candidate;
                    continue;
                }

                int byName = string.Compare(
                    candidate.Name ?? string.Empty, key.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                if (byName < 0 || (byName == 0 && NumericKey(candidate) > NumericKey(key)))
                {
                    key = candidate;
                }
            }
            return key;
        }

        /// <summary>
        /// The amount a currency cell really represents: its exact
        /// per-unit rate where the resolver computed one, otherwise its
        /// whole Amount.
        /// </summary>
        private static double NumericKey(CurrencyAmountViewModel amount)
        {
            if (amount == null) return 0;
            return amount.UnitRate ?? amount.Amount;
        }

        private static int Flip(int comparison, TableSortDirection direction)
        {
            return direction == TableSortDirection.Descending ? -comparison : comparison;
        }
    }
}
