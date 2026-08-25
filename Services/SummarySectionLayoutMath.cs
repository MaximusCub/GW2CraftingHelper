using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The redesigned Summary (Total Cost) section's own pure layout
    /// arithmetic (Blish-free, unit-testable) - total content height and
    /// the currency table's column edges.
    ///
    /// Deliberately kept OUT of Services/PlanContentHeightMath.cs and
    /// Services/PlanRelayoutMath.cs, even though this class's role is
    /// otherwise the same kind of thing both already do for every other
    /// section: both are shared infrastructure several other sections'
    /// row builders depend on and are high-evidence zones (see
    /// docs/KNOWN-ISSUES.md#policy-high-evidence-zones) - off-limits for the broader
    /// fold-back this class's own existence sidesteps.
    /// Views/CraftingPlanView.cs's one call site
    /// (CreateCollapsibleSection) special-cases PlanSectionType.Summary to
    /// call BodyHeight below instead of
    /// PlanContentHeightMath.SectionBodyHeight, the same way every other
    /// section type still routes through that method unchanged - see
    /// KNOWN-ISSUES #46 for the original rationale.
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
        /// SummarySectionRenderer.Render populates it (see
        /// PlanContentHeightMath's own class doc comment for why this
        /// matters), so it must stay in exact agreement with what that
        /// renderer actually builds:
        ///   - at most one CostBandHeight-tall row for the cost formula
        ///     band (always present - 1 or 3 CostFormulaTile rows both
        ///     render as ONE tile row, per the collapse rule), taller
        ///     again when the plan carries currency costs the coin figure
        ///     cannot speak for (see CostBandHeight);
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
            if (hasCostBand) height += CostBandHeight(currencyRowCount > 0);
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

        // --- Cost formula band (the plan's headline figure) ---
        //
        // The cost band's result tile is the one number a user comes to
        // this section for. It used to say so with a promoted DefaultFont32
        // amount; the maintainer's field test replaced that with a tinted,
        // semi-transparent highlight box around the result tile, so all
        // three tiles now share ONE amount font and the band reads as one
        // formula again. The band's height is therefore no longer a
        // promoted font's leading - it is the box's own padding around a
        // caption line, an optional disclosure line and one ordinary
        // amount run, which is what the constants below spell out.
        //
        // In a currency-bearing plan that gold figure is not the whole
        // cost: PlanViewModelBuilder.BuildCostFormulaBand sources it from
        // Plan.TotalCoinCost, which by construction excludes every
        // CurrencyCost row the table below lists. The band therefore
        // carries an extra disclosure line under the result caption, and
        // the band grows by exactly one line's worth to hold it. Both the
        // renderer's row height and BodyHeight's count come from
        // CostBandHeight so they cannot disagree about that growth.

        /// <summary>Gap between the band's own edge and the highlight box.</summary>
        public const int CostBandBoxMarginY = 6;

        /// <summary>Highlight box padding above the caption / below the amount.</summary>
        public const int CostBandBoxPadY = 6;

        /// <summary>Highlight box padding left and right of its widest line.</summary>
        public const int CostBandBoxPadX = 14;

        /// <summary>
        /// Caption y inside the cost band - the box's top edge plus its own
        /// padding, so the box never starts above the band.
        /// </summary>
        public const int CostBandCaptionY = CostBandBoxMarginY + CostBandBoxPadY;

        /// <summary>
        /// Bottom pad under the cost band's amount run, symmetric with
        /// <see cref="CostBandCaptionY"/>: the box's padding plus its margin.
        /// </summary>
        public const int CostBandAmountBottomPad = CostBandBoxPadY + CostBandBoxMarginY;

        /// <summary>
        /// Height reserved for one caption line, deliberately above what
        /// the font actually measures: the renderer places the caption from
        /// real font metrics and clamps the amount below it, so this
        /// reserve has to cover the tallest plausible metric or the band
        /// clips its own amount (the renderer's DEBUG assert is what
        /// catches that). 32, not 25: the tile captions moved to
        /// TypeRampMetrics.ColumnHeaderInk and its measured line height
        /// with them, 18 -> 25, so the reserve carries the same 7px of
        /// slack over the real metric as before.
        /// </summary>
        public const int CostBandCaptionLineHeight = 32;

        /// <summary>Gap between the caption block and the amount run.</summary>
        public const int CostBandCaptionToAmountGap = 4;

        /// <summary>
        /// Extra band height reserved for the disclosure line. 23, not 18,
        /// for the same measured 13 -> 18 caption line-height move as
        /// <see cref="CostBandCaptionLineHeight"/>.
        /// </summary>
        public const int CostBandCurrencyNoteHeight = 23;

        /// <summary>
        /// Height of the cost formula band's single tile row: the highlight
        /// box's margin+padding, a caption line, the disclosure line when
        /// there is one, the gap, and one amount run (a coin run is never
        /// shorter than CoinSegmentMath.CoinIconSize, which is what makes
        /// that the amount block's reserved height).
        /// hasCurrencyNote must be "this Summary section has at least one
        /// CurrencyCost row" - the same condition
        /// Views/Rendering/SummarySectionRenderer.Render uses to decide
        /// whether to draw the disclosure line at all.
        /// </summary>
        public static int CostBandHeight(bool hasCurrencyNote)
        {
            return CostBandCaptionY
                + CostBandCaptionLineHeight
                + (hasCurrencyNote ? CostBandCurrencyNoteHeight : 0)
                + CostBandCaptionToAmountGap
                + CoinSegmentMath.CoinIconSize
                + CostBandAmountBottomPad;
        }

        /// <summary>
        /// Top y of an amountHeight-tall amount run inside a band of
        /// rowHeight: bottom-anchored above bottomPad, never allowed above
        /// the caption block. rowHeight is a fixed constant while
        /// captionBlockBottom comes from whatever font metrics Blish
        /// loaded, so a font taller than the band was sized for overflows
        /// downward (loud - the renderer's DEBUG assert catches it) rather
        /// than silently overprinting the caption.
        /// </summary>
        public static int BandAmountY(int rowHeight, int amountHeight, int captionBlockBottom, int bottomPad)
        {
            int y = rowHeight - bottomPad - amountHeight;
            return y > captionBlockBottom ? y : captionBlockBottom;
        }

        /// <summary>
        /// Band-space top edge of the highlight box: one pad above the
        /// caption, which by <see cref="CostBandCaptionY"/>'s construction
        /// is exactly one margin below the band's own top edge.
        /// </summary>
        public const int CostBandBoxTop = CostBandCaptionY - CostBandBoxPadY;

        /// <summary>
        /// Height of the highlight box around an amountHeight-tall amount
        /// run whose top sits at band-space amountY: from
        /// <see cref="CostBandBoxTop"/> down to one pad below the amount.
        /// The box is the band's lowest ink, so this - not the amount run -
        /// is what has to fit inside <see cref="CostBandHeight"/>.
        /// </summary>
        public static int CostBandBoxHeight(int amountY, int amountHeight)
        {
            return amountY + amountHeight + CostBandBoxPadY - CostBandBoxTop;
        }

        /// <summary>
        /// Width of the highlight box around its widest measured line
        /// (caption, disclosure line or coin run). Never clamped to the
        /// tile slice: Blish clips a container's children, so a box
        /// narrower than its content would cut the amount off where an
        /// unboxed tile merely overlaps its neighbour.
        /// </summary>
        public static int CostBandBoxWidth(int widestContentWidth)
        {
            return widestContentWidth + 2 * CostBandBoxPadX;
        }

        /// <summary>
        /// The disclosure line's text, or null when the plan has no
        /// currency costs (in which case the coin figure genuinely IS the
        /// whole cost and no line is drawn). Pure copy rather than
        /// geometry, kept beside CostBandHeight because the two are one
        /// decision - the same precedent
        /// RequiredRecipesVisibility.BuildHeaderTitle already set for
        /// honest, count-derived header copy living in Services.
        /// </summary>
        public static string CurrencyRequirementNote(int currencyRowCount)
        {
            if (currencyRowCount <= 0) return null;
            // Deliberately short: it sits under a caption inside one tile
            // slice of a three-tile band, and the reason WHY it matters
            // lives in the hover text rather than widening this line past
            // its tile.
            return currencyRowCount == 1
                ? "+ 1 currency required"
                : $"+ {currencyRowCount} currencies required";
        }

        /// <summary>
        /// Hover text for the disclosure line: the currency names
        /// themselves, in the order the table below lists them. Null when
        /// there is nothing to disclose. Never shows currency IDs (repo
        /// invariant: IDs are internal-only).
        /// </summary>
        public static string CurrencyRequirementNoteTooltip(IReadOnlyList<PlanRowViewModel> currencyRows)
        {
            if (currencyRows == null || currencyRows.Count == 0) return null;

            var names = new List<string>(currencyRows.Count);
            foreach (var row in currencyRows)
            {
                if (!string.IsNullOrEmpty(row.Label)) names.Add(row.Label);
            }
            if (names.Count == 0) return null;

            return string.Join(", ", names)
                + "\nThese are spent on top of the coin cost shown - see the Currency table below.";
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
        // The widening matters: the Have column is unclamped to the real
        // wallet holding (PlanViewModelBuilder.BuildCurrencyTableRows), and
        // Karma (Gw2Constants id 2) routinely reaches 6-7 digits in a real
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
        /// CurrencyNumberColumnWidth floor, unchanged prior
        /// behavior) for callers - existing tests among them - that don't
        /// need to pass a data-driven width.
        /// </summary>
        public static CurrencyColumnEdges ComputeCurrencyColumnEdges(int panelWidth, int widestNumberWidth = 0)
        {
            return EdgesFromRightEdge(
                PlanRelayoutMath.PinnedRightEdge(panelWidth),
                EffectiveCurrencyNumberColumnWidth(widestNumberWidth));
        }

        private static CurrencyColumnEdges EdgesFromRightEdge(int rightEdge, int numberColumnWidth)
        {
            int markerX = rightEdge - CurrencyMarkerWidth;
            int neededRightEdge = markerX - CurrencyColumnGap;
            int haveRightEdge = neededRightEdge - numberColumnWidth - CurrencyColumnGap;
            int requiredRightEdge = haveRightEdge - numberColumnWidth - CurrencyColumnGap;
            return new CurrencyColumnEdges(requiredRightEdge, haveRightEdge, neededRightEdge, markerX);
        }

    }
}
