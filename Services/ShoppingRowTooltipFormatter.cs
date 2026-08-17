using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Builds the shopping-row tooltip's per-currency HAVE/NEED line(s)
    /// (shoplist-have-format) from an already-resolved
    /// CurrencyAmountViewModel list - the same list
    /// ShoppingListSectionRenderer.CreateShoppingRow renders in the Total
    /// cell (row.CurrencyCosts). Pure, Blish-free string shaping (mirrors
    /// RequestLabelFormatter's own doc comment) so the exact wording is
    /// directly unit-testable without a live BasicTooltipText - and,
    /// critically, so both CreateShoppingRow's initial tooltip build and
    /// its AddReellipsis rebuild call the identical code path: the two
    /// used to diverge, with the rebuild silently dropping every currency
    /// line on the first resize/settle (shoplist-have-format review
    /// finding #1).
    ///
    /// cc.Amount is this ROW's own currency total (one PlanStep's
    /// VendorCurrencyCosts - see PlanViewModelBuilder.
    /// BuildShoppingListSection), never the whole plan's requirement for
    /// that currency id (that figure is PlanViewModel.CurrencyPlanTotals,
    /// which this renderer is never handed) - so the wording below never
    /// claims "plan requires" (review finding #2); it states only what
    /// this row's own numbers actually mean.
    /// </summary>
    public static class ShoppingRowTooltipFormatter
    {
        /// <summary>
        /// One line per currency cost with a resolved wallet holding
        /// (cc.OwnedQuantity.HasValue); a currency with no wallet data at
        /// all (OwnedQuantity null) or a non-positive Amount (nothing
        /// meaningful to report - guards a future zero/negative-Amount
        /// caller from rendering a content-free "HAVE 0/0" line) is
        /// silently skipped. Never returns null - an empty list for
        /// "nothing to say" lets callers AddRange without a null check.
        ///
        /// Shortfall rows (wallet holding &lt; Amount) render
        /// "HAVE owned/Amount, NEED shortfall". OwnedQuantity is never
        /// clamped below Amount, so it already equals the real unclamped
        /// holding here - nothing more to add.
        ///
        /// Covered rows (wallet holding &gt;= Amount) render
        /// "HAVE Amount/Amount", plus a "(you hold N)" aside only when the
        /// real unclamped holding (RawOwnedQuantity) exceeds Amount - the
        /// clamp on OwnedQuantity hides that surplus, so the aside is the
        /// only place it survives.
        /// </summary>
        public static List<string> BuildCurrencyLines(IReadOnlyList<CurrencyAmountViewModel> currencyCosts)
        {
            var lines = new List<string>();
            if (currencyCosts == null)
            {
                return lines;
            }

            foreach (var cc in currencyCosts)
            {
                if (!cc.OwnedQuantity.HasValue || cc.Amount <= 0)
                {
                    continue;
                }

                long needed = cc.Amount - cc.OwnedQuantity.Value;
                if (needed > 0)
                {
                    lines.Add($"{cc.Name}: HAVE {cc.OwnedQuantity.Value}/{cc.Amount}, NEED {needed}");
                    continue;
                }

                // RawOwnedQuantity is always set alongside OwnedQuantity by
                // CurrencyDisplayResolver.ResolveAmounts; the ?? fallback
                // only guards against a future caller constructing this
                // view model directly with just OwnedQuantity set.
                long rawHeld = cc.RawOwnedQuantity ?? cc.OwnedQuantity.Value;
                string line = $"{cc.Name}: HAVE {cc.Amount}/{cc.Amount}";
                if (rawHeld > cc.Amount)
                {
                    line += $" (you hold {rawHeld})";
                }
                lines.Add(line);
            }
            return lines;
        }
    }
}
