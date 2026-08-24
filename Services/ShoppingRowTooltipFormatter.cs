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
    /// must never diverge - a diverging rebuild silently drops every
    /// currency line on the first resize/settle.
    ///
    /// cc.Amount is this ROW's own currency total (one PlanStep's
    /// VendorCurrencyCosts - see PlanViewModelBuilder.
    /// BuildShoppingListSection), never the whole plan's requirement for
    /// that currency id (that figure is PlanViewModel.CurrencyPlanTotals,
    /// which this renderer is never handed) - so the wording below never
    /// claims "plan requires"; it states only what
    /// this row's own numbers actually mean.
    /// </summary>
    public static class ShoppingRowTooltipFormatter
    {
        /// <summary>
        /// The whole shopping row tooltip as CONTENT rather than as a
        /// string: the item's stat block (which opens with its name in its
        /// rarity colour and closes with a real coin run), then the row's
        /// own acquisition hint and the HAVE/NEED lines below.
        /// <para>
        /// The plain string form this row used to build could only spell a
        /// coin amount out as "1g 23s 45c" and could show no stats at all,
        /// which is why every item-hover surface in the module now goes
        /// through <see cref="ItemRowTooltipComposer"/> instead. A null
        /// <paramref name="stats"/> - a row whose item has not been fetched
        /// this session - degrades to exactly the tooltip this row had
        /// before, never to an empty box.
        /// </para>
        /// </summary>
        public static TooltipContent BuildRowContent(
            ItemStatBlock stats,
            string fullName,
            bool nameTruncated,
            string hintText,
            IReadOnlyList<CurrencyAmountViewModel> currencyCosts)
        {
            var extras = new List<string>();
            if (!string.IsNullOrEmpty(hintText))
            {
                extras.Add(hintText);
            }
            extras.AddRange(BuildCurrencyLines(currencyCosts));

            return ItemRowTooltipComposer.BuildRowContent(stats, fullName, nameTruncated, extras);
        }

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
        /// "HAVE owned/Amount THIS ROW, NEED shortfall". In this branch the
        /// OwnedQuantity clamp is inert (raw &lt; Amount), so OwnedQuantity
        /// already IS the real unclamped holding - nothing more to add.
        ///
        /// Covered rows (wallet holding &gt;= Amount) render
        /// "HAVE Amount/Amount THIS ROW", plus a "(wallet N)" aside only
        /// when the real unclamped holding (RawOwnedQuantity) exceeds
        /// Amount - the clamp on OwnedQuantity hides that surplus, so the
        /// aside is the only place it survives.
        ///
        /// The "THIS ROW" suffix on both halves is deliberate (shoplist-
        /// have-format SCOPE COLLISION): both numbers
        /// are this ROW's own total (cc.Amount, one PlanStep's own
        /// VendorCurrencyCosts - see the class doc comment), never the
        /// whole plan's requirement for that currency id. Without a scope
        /// marker, two shopping rows drawing on the SAME wallet currency
        /// (e.g. Karma split across two vendor rows) can each independently
        /// read as "fully covered" and double-count the one wallet balance
        /// - the same misreading class DecisionPillPlanner's PLAN-scope
        /// "HAVE {have}/{planTotal} TOTAL" pill (AppendCurrencyOwnershipPill)
        /// exists to avoid, via its own explicit "TOTAL" suffix. "THIS ROW"
        /// is this method's row-scope mirror of that same convention - the
        /// vocabulary must never look plan-scope when it isn't. The "(wallet
        /// N)" aside is worded the same way for the same reason: "wallet" is
        /// the one term this codebase now uses for a raw account-wide
        /// holding figure, matching the Summary c-table's "Have" column and
        /// the tree's "HAVE x/y TOTAL" pill - a THIS ROW/wallet line can
        /// never be mistaken for the plan-scope facts those two show.
        /// </summary>
        public static IReadOnlyList<string> BuildCurrencyLines(IReadOnlyList<CurrencyAmountViewModel> currencyCosts)
        {
            if (currencyCosts == null || currencyCosts.Count == 0)
            {
                // Shared empty instance for the common no-cost row (every
                // TP-buy row) - avoids a per-row List<string> allocation
                // that AddReellipsis's closure would otherwise retain for
                // the plan's lifetime for no reason (build-time only, not a
                // hot per-frame path, but free to avoid).
                return System.Array.Empty<string>();
            }

            var lines = new List<string>();
            foreach (var cc in currencyCosts)
            {
                if (!cc.OwnedQuantity.HasValue || cc.Amount <= 0)
                {
                    continue;
                }

                long needed = cc.Amount - cc.OwnedQuantity.Value;
                if (needed > 0)
                {
                    lines.Add($"{cc.Name}: HAVE {cc.OwnedQuantity.Value}/{cc.Amount} THIS ROW, NEED {needed}");
                    continue;
                }

                // RawOwnedQuantity is always set alongside OwnedQuantity by
                // CurrencyDisplayResolver.ResolveAmounts; the ?? fallback
                // only guards against a future caller constructing this
                // view model directly with just OwnedQuantity set.
                long rawHeld = cc.RawOwnedQuantity ?? cc.OwnedQuantity.Value;
                string line = $"{cc.Name}: HAVE {cc.Amount}/{cc.Amount} THIS ROW";
                if (rawHeld > cc.Amount)
                {
                    line += $" (wallet {rawHeld})";
                }
                lines.Add(line);
            }
            return lines;
        }
    }
}
