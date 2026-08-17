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
    /// section: at W4A time both of those files were DO-NOT-TOUCH (they
    /// are shared infrastructure several other sections' row builders
    /// depend on, and other in-flight work touched them too); they are now
    /// high-evidence zones (formerly DO-NOT-TOUCH; see
    /// docs/KNOWN-ISSUES.md's policy note) - still off-limits for the
    /// broader fold-back this class's own existence sidesteps, but changes
    /// are possible with proof. W4A left both files byte-for-byte
    /// unmodified: Views/CraftingPlanView.cs's one call site
    /// (CreateCollapsibleSection) special-cases PlanSectionType.Summary to
    /// call BodyHeight below instead of
    /// PlanContentHeightMath.SectionBodyHeight, the same way every other
    /// section type still routes through that method unchanged. A later
    /// pass (high-evidence-zones, 2026-08-17) proved
    /// PlanContentHeightMath's own private SummaryBodyHeight method (and
    /// the PlanRowType.CoinTotal enum member it existed to read) were
    /// unreachable for a real Summary section and deleted both outright -
    /// see docs/KNOWN-ISSUES.md's W4A entry for the original rationale.
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
        // Required/Have/Needed columns reserve CurrencyNumberColumnWidth by
        // default, widened per-render when an actual value needs more room
        // (EffectiveCurrencyNumberColumnWidth/ComputeCurrencyColumnEdges'
        // widestNumberWidth parameter below) - mirrors ShoppingColumnMath.
        // ComputeEdges' "clamp to a fixed minimum, widen from an actual
        // per-render widest-value measurement" shape.
        //
        // Review fix: the fixed-60px-only version of
        // this comment claimed Required/Have/Needed have "no realistic risk
        // of a value needing more than a handful of digits" - untrue once
        // the W4A spec UNCLAMPED the Have column to the real wallet
        // holding (PlanViewModelBuilder.BuildCurrencyTableRows): Karma
        // (Gw2Constants id 2) routinely reaches 6-7 digits in a real
        // player's wallet, which can plausibly exceed the 60px floor. Since
        // CreateRightAlignedLabel grows a label LEFTWARD from the column's
        // own right edge, an unreserved overlong value would visually
        // intrude into its left neighbor's column rather than clip.

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
        /// The reserved width actually used for each of the Required/Have/
        /// Needed columns this render: CurrencyNumberColumnWidth, widened
        /// to fit widestNumberWidth (the widest of this render's actual
        /// Required/Have/Needed strings, measured by the caller via
        /// BitmapFont.MeasureString - Blish-bound, so not done here) when
        /// that exceeds the fixed floor.
        /// </summary>
        public static int EffectiveCurrencyNumberColumnWidth(int widestNumberWidth)
        {
            return widestNumberWidth > CurrencyNumberColumnWidth ? widestNumberWidth : CurrencyNumberColumnWidth;
        }

        /// <summary>
        /// Right-to-left column layout for the currency table's Required/
        /// Have/Needed numeric columns plus the trailing full-coverage
        /// marker, derived from panelWidth plus (optionally) this render's
        /// actual widest Required/Have/Needed value width. Mirrors
        /// ShoppingColumnMath.ComputeEdges' "derive right-to-left off a
        /// fixed right edge, using an effective (floor-or-measured) column
        /// width" shape so header and data rows built from the same
        /// panelWidth/widestNumberWidth pair always agree by construction.
        /// widestNumberWidth defaults to 0 (i.e. the fixed
        /// CurrencyNumberColumnWidth floor, unchanged pre-review-fix
        /// behavior) for callers - existing tests among them - that don't
        /// need to pass a data-driven width.
        /// </summary>
        public static CurrencyColumnEdges ComputeCurrencyColumnEdges(int panelWidth, int widestNumberWidth = 0)
        {
            int numberColumnWidth = EffectiveCurrencyNumberColumnWidth(widestNumberWidth);
            int rightEdge = panelWidth - 8;
            int markerX = rightEdge - CurrencyMarkerWidth;
            int neededRightEdge = markerX - CurrencyColumnGap;
            int haveRightEdge = neededRightEdge - numberColumnWidth - CurrencyColumnGap;
            int requiredRightEdge = haveRightEdge - numberColumnWidth - CurrencyColumnGap;
            return new CurrencyColumnEdges(requiredRightEdge, haveRightEdge, neededRightEdge, markerX);
        }
    }
}
