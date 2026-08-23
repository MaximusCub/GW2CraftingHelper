using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    // Factors the "row divider +
    // width-only relayout closure" tail that closes every row builder in
    // CraftStepsSectionRenderer, DisciplinesSectionRenderer,
    // RecipesSectionRenderer, ShoppingListSectionRenderer, and
    // UsedMaterialsSectionRenderer - confirmed byte-identical in shape
    // across all five:
    //   Panel divider = isLast ? null : LabelHelpers.CreateRowDivider(rowPanel, panelWidth, rowHeight, bottomClearance);
    //   sink.AddRelayout(w =>
    //   {
    //       rowPanel.Size = new Point(w, rowHeight);
    //       <the row's own per-control repositioning, whatever it is>
    //       if (divider != null) divider.Size = new Point(w, 2);
    //   });
    // Only the bracketed line varies per row (a different set of controls
    // to reposition) - that stays the caller's own responsibility, passed
    // in as extraRelayout, invoked at the exact same point in the closure
    // every pre-extraction caller already ran it (after the rowPanel resize,
    // before the divider resize). LabelHelpers.CreateRowDivider itself - its
    // divider math and the M36b bottom-clearance calls - is called exactly
    // as before, unedited (DO-NOT-TOUCH #6); this only wraps the
    // surrounding boilerplate, not the divider's own arithmetic.
    //
    // Not adopted by SummarySectionRenderer.CreateCostTileRow/
    // CreateCurrencyRow: neither builds a LabelHelpers.CreateRowDivider at
    // all (the Summary section has no list-style rows, just the cost-tile
    // band and standalone currency/note lines - see that class's doc
    // comment), so there is no divider half of this shape to share; forcing
    // an isLast-always-true call through this helper just to reuse the
    // relayout-wrapping half would be exactly the kind of contortion this
    // package's brief warns against, for zero real duplication removed.
    internal static class RowRelayoutHelpers
    {
        /// <summary>
        /// Creates the row's trailing divider (skipped when isLast) and
        /// registers the width-only AddRelayout closure that resizes
        /// rowPanel, runs the row's own extraRelayout repositioning, then
        /// resizes the divider - in that order, matching every
        /// pre-extraction row builder's own closure exactly. extraRelayout
        /// may be null for a row with nothing else to reposition (none of
        /// today's five callers needs this, but a future flush-fit row with
        /// only font-fixed content might).
        /// <para>
        /// dividerWidthForWidth bounds the rule to the table it belongs to
        /// (audit batch H fix round): once a table's right-hand block is
        /// pulled in beside its names, a full-panel-width rule runs hundreds
        /// of px past the last column into empty space and advertises the
        /// gutter it was supposed to close. Callers pass their own
        /// "block right edge + PlanRelayoutMath.TableRightMargin", which is
        /// exactly the panel width whenever the block is still pinned - so a
        /// pinned table's rule is byte-identical to the one it drew before.
        /// Null keeps the full width for rows with no right-hand block at all.
        /// The result is clamped into [0, w] so a caller's arithmetic can
        /// never produce a rule wider than its own row panel.
        /// </para>
        /// </summary>
        internal static void FinishRow(
            Panel rowPanel, int panelWidth, int rowHeight, bool isLast, int bottomClearance,
            ISectionRelayoutSink sink, Action<int> extraRelayout, Func<int, int> dividerWidthForWidth = null)
        {
            Panel divider = isLast
                ? null
                : LabelHelpers.CreateRowDivider(
                    rowPanel, DividerWidth(dividerWidthForWidth, panelWidth), rowHeight, bottomClearance);
            sink.AddRelayout(w =>
            {
                rowPanel.Size = new Point(w, rowHeight);
                extraRelayout?.Invoke(w);
                if (divider != null) divider.Size = new Point(DividerWidth(dividerWidthForWidth, w), 2);
            });
        }

        private static int DividerWidth(Func<int, int> dividerWidthForWidth, int panelWidth)
        {
            if (dividerWidthForWidth == null) return panelWidth;

            int width = dividerWidthForWidth(panelWidth);
            if (width > panelWidth) width = panelWidth;
            return width > 0 ? width : 0;
        }
    }
}
