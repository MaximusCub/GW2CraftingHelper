using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// W4A (Total Cost section redesign): the redesigned Summary section's
    /// own pure layout arithmetic (Blish-free, unit-testable) - total
    /// content height and the currency table's column edges.
    ///
    /// Deliberately kept OUT of Services/PlanContentHeightMath.cs and
    /// Services/PlanRelayoutMath.cs, even though this class's role is
    /// otherwise the same kind of thing both already do for every other
    /// section: the W4A task brief lists both of those files DO-NOT-TOUCH
    /// (they are shared infrastructure several other sections' row builders
    /// depend on, and other in-flight work touches them too). Both files
    /// are left byte-for-byte unmodified by this package - in particular
    /// PlanContentHeightMath's own private SummaryBodyHeight method (and
    /// its existing PlanContentHeightMathTests.cs coverage) still compiles
    /// and still passes exactly as before, it is simply no longer wired to
    /// any REAL Summary section: Views/CraftingPlanView.cs's one call site
    /// (CreateCollapsibleSection) now special-cases
    /// PlanSectionType.Summary to call BodyHeight below instead of
    /// PlanContentHeightMath.SectionBodyHeight, the same way every other
    /// section type still routes through that method unchanged. See
    /// docs/KNOWN-ISSUES.md's W4A entry for the full rationale and the
    /// PlanRowType.CoinTotal enum member's own doc comment for the
    /// matching note on the model side.
    ///
    /// The row-height CONSTANTS themselves are not redefined here - every
    /// formula below reads PlanContentHeightMath's existing public
    /// CostTileRowHeight/CTableHeaderRowHeight/CurrencyRowHeight/
    /// FallbackTextRowHeight constants directly, so this class can never
    /// drift from the fixed-row-height convention every other section
    /// already follows; only the Summary-specific COUNTING logic (how many
    /// of each fixed-height row this section's new formula-band/currency-
    /// table/footnote shape actually has) lives here.
    /// </summary>
    public static class SummarySectionLayoutMath
    {
        /// <summary>
        /// Total height of the redesigned Total Cost section's content
        /// FlowPanel - CraftingPlanView.CreateCollapsibleSection assigns
        /// this to contentFlow.Height synchronously right after
        /// SummarySectionRenderer.Render populates it (M33 C2a directive A -
        /// see PlanContentHeightMath's own class doc comment for why this
        /// matters), so it must stay in exact agreement with what that
        /// renderer actually builds:
        ///   - at most one CostTileRowHeight-tall row for the cost formula
        ///     band (always present - 1 or 3 CostFormulaTile rows both
        ///     render as ONE tile row, per the collapse rule);
        ///   - at most one CostTileRowHeight-tall row for the profit
        ///     formula band (present only when ProfitFormulaTile rows
        ///     exist - always exactly 3 when present);
        ///   - one CTableHeaderRowHeight header plus one CurrencyRowHeight
        ///     row per CurrencyCost row, only when at least one exists;
        ///   - one FallbackTextRowHeight row per MultiItemNote row;
        ///   - one FallbackTextRowHeight row for the SummaryFootnote row
        ///     (always exactly one in practice, but summed rather than
        ///     assumed so a null/absent footnote degrades gracefully
        ///     instead of desyncing height from what actually rendered).
        /// </summary>
        public static int BodyHeight(IReadOnlyList<PlanRowViewModel> rows)
        {
            rows = rows ?? Array.Empty<PlanRowViewModel>();

            bool hasCostBand = false;
            bool hasProfitBand = false;
            int currencyRowCount = 0;
            int noteRowCount = 0;
            int footnoteRowCount = 0;

            foreach (var row in rows)
            {
                switch (row.RowType)
                {
                    case PlanRowType.CostFormulaTile:
                        hasCostBand = true;
                        break;
                    case PlanRowType.ProfitFormulaTile:
                        hasProfitBand = true;
                        break;
                    case PlanRowType.CurrencyCost:
                        currencyRowCount++;
                        break;
                    case PlanRowType.MultiItemNote:
                        noteRowCount++;
                        break;
                    case PlanRowType.SummaryFootnote:
                        footnoteRowCount++;
                        break;
                }
            }

            int height = 0;
            if (hasCostBand) height += PlanContentHeightMath.CostTileRowHeight;
            if (hasProfitBand) height += PlanContentHeightMath.CostTileRowHeight;
            if (currencyRowCount > 0)
            {
                height += PlanContentHeightMath.CTableHeaderRowHeight
                    + currencyRowCount * PlanContentHeightMath.CurrencyRowHeight;
            }
            height += noteRowCount * PlanContentHeightMath.FallbackTextRowHeight;
            height += footnoteRowCount * PlanContentHeightMath.FallbackTextRowHeight;
            return height;
        }

        // --- Currency table column geometry ---
        //
        // Required/Have/Needed are always short plain integers (or "-" for
        // no-wallet-data) - unlike the Shopping List's Each/Total columns
        // (ShoppingColumnMath), there are no coin icons and no realistic
        // risk of a value needing more than a handful of digits, so fixed
        // column widths (rather than a per-render widest-value pre-scan)
        // are the simple, sufficient choice here.

        /// <summary>Left x of the currency icon.</summary>
        public const int CurrencyIconX = 8;

        /// <summary>Icon size for the currency table's leading icon.</summary>
        public const int CurrencyIconSize = 18;

        /// <summary>
        /// Left x of the currency name label - past the icon plus a gap.
        /// </summary>
        public const int CurrencyNameX = CurrencyIconX + CurrencyIconSize + 8;

        /// <summary>Gap reserved before/between the right-side columns.</summary>
        public const int CurrencyColumnGap = 14;

        /// <summary>Reserved band width for each of Required/Have/Needed.</summary>
        public const int CurrencyNumberColumnWidth = 60;

        /// <summary>Reserved band width for the full-coverage marker.</summary>
        public const int CurrencyMarkerWidth = 34;

        public readonly struct CurrencyColumnEdges
        {
            public readonly int RequiredRightEdge;
            public readonly int HaveRightEdge;
            public readonly int NeededRightEdge;
            public readonly int MarkerX;

            public CurrencyColumnEdges(int requiredRightEdge, int haveRightEdge, int neededRightEdge, int markerX)
            {
                RequiredRightEdge = requiredRightEdge;
                HaveRightEdge = haveRightEdge;
                NeededRightEdge = neededRightEdge;
                MarkerX = markerX;
            }
        }

        /// <summary>
        /// Right-to-left column layout for the currency table's Required/
        /// Have/Needed numeric columns plus the trailing full-coverage
        /// marker, all derived from panelWidth alone (no data-dependent
        /// pre-scan - see the class doc comment above). Mirrors
        /// ShoppingColumnMath.ComputeEdges' "derive right-to-left off a
        /// fixed right edge" shape so header and data rows built from the
        /// same panelWidth always agree by construction.
        /// </summary>
        public static CurrencyColumnEdges ComputeCurrencyColumnEdges(int panelWidth)
        {
            int rightEdge = panelWidth - 8;
            int markerX = rightEdge - CurrencyMarkerWidth;
            int neededRightEdge = markerX - CurrencyColumnGap;
            int haveRightEdge = neededRightEdge - CurrencyNumberColumnWidth - CurrencyColumnGap;
            int requiredRightEdge = haveRightEdge - CurrencyNumberColumnWidth - CurrencyColumnGap;
            return new CurrencyColumnEdges(requiredRightEdge, haveRightEdge, neededRightEdge, markerX);
        }
    }
}
