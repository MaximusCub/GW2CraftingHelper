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
    /// docs/KNOWN-ISSUES.md's policy note) - off-limits for the broader
    /// fold-back this class's own existence sidesteps.
    /// Views/CraftingPlanView.cs's one call site
    /// (CreateCollapsibleSection) special-cases PlanSectionType.Summary to
    /// call BodyHeight below instead of
    /// PlanContentHeightMath.SectionBodyHeight, the same way every other
    /// section type still routes through that method unchanged - see
    /// docs/KNOWN-ISSUES.md's W4A entry for the original rationale.
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
        // this section for, so it renders at a promoted amount font
        // (PlanContentHeightMath.PromotedCostTileRowHeight) rather than
        // sharing DefaultFont16 with the two derived tiles beside it.
        //
        // In a currency-bearing plan that gold figure is not the whole
        // cost: PlanViewModelBuilder.BuildCostFormulaBand sources it from
        // Plan.TotalCoinCost, which by construction excludes every
        // CurrencyCost row the table below lists. The band therefore
        // carries an extra disclosure line under the result caption, and
        // the band grows by exactly one line's worth to hold it. Both the
        // renderer's row height and BodyHeight's count come from
        // CostBandHeight so they cannot disagree about that growth.

        /// <summary>Extra band height reserved for the disclosure line.</summary>
        public const int CostBandCurrencyNoteHeight = 18;

        /// <summary>
        /// Height of the cost formula band's single tile row.
        /// hasCurrencyNote must be "this Summary section has at least one
        /// CurrencyCost row" - the same condition
        /// Views/Rendering/SummarySectionRenderer.Render uses to decide
        /// whether to draw the disclosure line at all.
        /// </summary>
        public static int CostBandHeight(bool hasCurrencyNote)
        {
            return PlanContentHeightMath.PromotedCostTileRowHeight
                + (hasCurrencyNote ? CostBandCurrencyNoteHeight : 0);
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
            return EdgesFromRightEdge(panelWidth - 8, EffectiveCurrencyNumberColumnWidth(widestNumberWidth));
        }

        private static CurrencyColumnEdges EdgesFromRightEdge(int rightEdge, int numberColumnWidth)
        {
            int markerX = rightEdge - CurrencyMarkerWidth;
            int neededRightEdge = markerX - CurrencyColumnGap;
            int haveRightEdge = neededRightEdge - numberColumnWidth - CurrencyColumnGap;
            int requiredRightEdge = haveRightEdge - numberColumnWidth - CurrencyColumnGap;
            return new CurrencyColumnEdges(requiredRightEdge, haveRightEdge, neededRightEdge, markerX);
        }

        /// <summary>
        /// Width the Required/Have/Needed/marker block occupies: the three
        /// number bands, the marker band, and the three gaps between them -
        /// i.e. from the Required column's left edge to the marker's right
        /// edge.
        /// </summary>
        public static int CurrencyBlockWidth(int widestNumberWidth)
        {
            return (3 * EffectiveCurrencyNumberColumnWidth(widestNumberWidth))
                + (3 * CurrencyColumnGap)
                + CurrencyMarkerWidth;
        }

        /// <summary>
        /// <see cref="ComputeCurrencyColumnEdges"/> with the dead gutter
        /// between the currency NAME column and the numbers closed: the
        /// Required column starts relative to the widest name the table
        /// renders instead of wherever the panel edge leaves it (audit
        /// batch H). The whole block moves together, so the numbers keep
        /// their existing relative geometry and the full-coverage marker
        /// stays at the block's right end rather than at the panel's.
        /// widestNameEnd must come from untruncated names - see
        /// PlanRelayoutMath.RightBlockX.
        /// </summary>
        public static CurrencyColumnEdges ComputeCurrencyColumnEdgesForPanel(
            int panelWidth, int widestNumberWidth, int widestNameEnd)
        {
            int numberColumnWidth = EffectiveCurrencyNumberColumnWidth(widestNumberWidth);
            int blockWidth = CurrencyBlockWidth(widestNumberWidth);
            int blockX = PlanRelayoutMath.RightBlockX(panelWidth - 8 - blockWidth, widestNameEnd);
            return EdgesFromRightEdge(blockX + blockWidth, numberColumnWidth);
        }
    }
}
