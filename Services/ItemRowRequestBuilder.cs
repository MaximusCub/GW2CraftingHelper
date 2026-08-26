using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure row-list state transitions for the multi-item plan input strip
    /// (Blish-free, unit-testable). Mirrors gw2e's own `e.recipes` array
    /// semantics (`addRecipe`/`removeRecipe`, the Remove link's
    /// `recipes.length > 1` visibility gate - docs/gw2e-parity-spec.md)
    /// minus the reorder (`moveRecipe`) affordance - see
    /// docs/KNOWN-ISSUES #21 for that deliberate divergence.
    /// </summary>
    internal static class ItemRowRequestBuilder
    {
        /// <summary>
        /// One input row's already-Blish-free state: the item id selected
        /// via the row's autocomplete (null when nothing has been picked
        /// yet - gw2e's own `{id: null}` row shape) and the row's raw
        /// quantity textbox contents.
        /// </summary>
        public readonly struct RowInput
        {
            public readonly int? ItemId;
            public readonly string QuantityText;

            public RowInput(int? itemId, string quantityText)
            {
                ItemId = itemId;
                QuantityText = quantityText;
            }
        }

        /// <summary>
        /// gw2e never shows the Remove link while only one row remains
        /// (calculator_view.html's `ng-if="recipes.length > 1"`) -
        /// CraftingPlanView calls this rather than hand-duplicating the
        /// `rowCount > 1` check at each row-build call site.
        /// </summary>
        public static bool CanRemoveRow(int rowCount)
        {
            return rowCount > 1;
        }

        /// <summary>
        /// Maps the input strip's live row states into the request list
        /// TriggerGenerate hands the pipeline. A row with no item selected
        /// is skipped rather than treated as an error - gw2e itself
        /// tolerates an empty `{id: null}` row in `e.recipes` sitting
        /// alongside filled ones (its own share-link builder filters them
        /// out the same way: `recipes.filter(r=>r.id)`).
        /// Each row's quantity text is parsed the same way the
        /// single-item quantity box always has (invalid/blank/&lt;1
        /// silently corrected to 1), now applied per row instead of once.
        /// Returns an empty list, never null, when every row is empty.
        /// </summary>
        public static List<PlanRequestItem> Build(IReadOnlyList<RowInput> rows)
        {
            var result = new List<PlanRequestItem>();
            if (rows == null)
            {
                return result;
            }

            foreach (var row in rows)
            {
                if (!row.ItemId.HasValue)
                {
                    continue;
                }

                result.Add(new PlanRequestItem
                {
                    ItemId = row.ItemId.Value,
                    Quantity = NormalizeQuantity(row.QuantityText)
                });
            }

            return result;
        }

        /// <summary>
        /// Blank, unparseable or below 1 all become 1, silently. Shared with
        /// the Crafting Ranker's Add field so the two entry points cannot
        /// drift into two different rules.
        /// </summary>
        public static int NormalizeQuantity(string quantityText)
        {
            return !int.TryParse(quantityText, out int qty) || qty < 1 ? 1 : qty;
        }
    }
}
