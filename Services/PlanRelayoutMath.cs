using System;
using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure width-dependent layout arithmetic (Blish-free, unit-testable)
    /// shared by CraftingPlanView's initial section builders and its
    /// live in-place resize relayout registry. Every formula here is
    /// called from BOTH the build path (CreateX... methods) and the
    /// relayout/re-ellipsis closures registered alongside them, so the two
    /// paths cannot drift apart - mirrors ShoppingColumnMath's "one source
    /// of truth" shape for the remaining width-dependent geometry m2's
    /// resize-path research identified (tree column anchors, cost-tile
    /// geometry, generic centering/right-alignment/name-column budgeting).
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    public static class PlanRelayoutMath
    {
        /// <summary>
        /// Left edge that centers a contentWidth-wide block inside a
        /// containerWidth-wide space, clamped to never go negative (a block
        /// wider than its container starts flush left rather than at a
        /// negative x). Used by the plan header's title centering and the
        /// cost-tile row's own row centering.
        /// </summary>
        public static int CenterX(int containerWidth, int contentWidth)
        {
            int x = (containerWidth - contentWidth) / 2;
            return x > 0 ? x : 0;
        }

        /// <summary>
        /// Left edge that right-aligns a width-wide control to rightEdge.
        /// </summary>
        public static int RightAlignedX(int rightEdge, int width)
        {
            return rightEdge - width;
        }

        /// <summary>
        /// Max width available for a name label sitting before a fixed-
        /// width trailing column (e.g. a right-aligned quantity), reserving
        /// gapBeforeColumn px between the name and that column and starting
        /// at nameX. Clamped to a 20px floor so a very narrow panel never
        /// yields a negative/zero ellipsis width. Shared by the Used
        /// Materials and Shopping List name columns - both use exactly this
        /// shape, just with a different columnRightXBeforeGap
        /// (panelWidth-8 vs. the shopping edges' QtyRightEdge).
        /// </summary>
        public static int NameMaxWidthBeforeColumn(
            int columnRightXBeforeGap, int trailingColumnWidth, int gapBeforeColumn, int nameX)
        {
            int width = columnRightXBeforeGap - trailingColumnWidth - gapBeforeColumn - nameX;
            return width > 20 ? width : 20;
        }

        /// <summary>
        /// Gap left between the widest name a table renders and the
        /// right-hand block of columns pulled in beside it. Chosen to be
        /// wider than every per-table name-to-column gap that feeds
        /// <see cref="NameMaxWidthBeforeColumn"/> (8 in the tree, 12 in
        /// Used Materials/Shopping List, 14 in the currency table), which
        /// is what makes <see cref="RightBlockX"/> unable to ellipsize a
        /// name it just measured - see that method.
        /// </summary>
        public const int TableGutterBreathingRoom = 24;

        /// <summary>
        /// Left x a right-hand block is never pulled in past, however short
        /// a table's names are: a two-column table whose numbers start at
        /// x=90 reads as a fragment floating at the panel's left edge
        /// rather than a table.
        /// </summary>
        public const int TableRightBlockMinX = 240;

        /// <summary>
        /// Left x of a data table's right-hand block of columns (the
        /// right-aligned numeric/pill columns every plan table pins to the
        /// panel edge) once the dead gutter between it and the left-pinned
        /// name column is closed. The block is pulled LEFT to sit
        /// <paramref name="breathingRoom"/> past the widest name the table
        /// renders, and is never pushed right of <paramref name="pinnedX"/>
        /// (the panel-edge-anchored position it had before this existed),
        /// so a narrow window degrades to exactly the old layout rather
        /// than overrunning the panel.
        /// <para>
        /// <paramref name="widestNameEnd"/> is the largest
        /// "name x + rendered name width" over the rows this table
        /// measures, from the UNTRUNCATED names - the ellipsis budget is
        /// derived from the block position, so feeding it truncated widths
        /// would be circular. Because the same
        /// <paramref name="breathingRoom"/> exceeds every gap the callers
        /// pass to <see cref="NameMaxWidthBeforeColumn"/>, a pulled-in
        /// block always leaves the widest name at least its own full width,
        /// i.e. closing the gutter can never introduce an ellipsis.
        /// </para>
        /// <para>
        /// 0 (or less) means "nothing measured" and keeps the block pinned,
        /// so a caller with no rows to scan is byte-identical to the old
        /// behaviour.
        /// </para>
        /// </summary>
        public static int RightBlockX(
            int pinnedX,
            int widestNameEnd,
            int breathingRoom = TableGutterBreathingRoom,
            int minBlockX = TableRightBlockMinX)
        {
            if (widestNameEnd <= 0) return pinnedX;

            int desired = widestNameEnd + breathingRoom;
            if (desired < minBlockX) desired = minBlockX;
            return desired < pinnedX ? desired : pinnedX;
        }

        /// <summary>
        /// Gap every plan table keeps between its right-hand block and the
        /// panel's right edge - the margin the block was pinned to before
        /// <see cref="RightBlockX"/> existed, and the one a bounded row
        /// divider or header band keeps past the block's right edge so a
        /// pinned table's chrome is byte-identical to what it drew before.
        /// </summary>
        public const int TableRightMargin = 8;

        /// <summary>
        /// Width of the recipe tree's decision-pill column, the
        /// pillColumnWidth every tree caller passes
        /// <see cref="ComputeTreeColumnEdges"/>. Lives here, next to the
        /// function that consumes it, so the Blish-free width tests assert
        /// against the shipped column rather than a copy of it.
        /// <para>
        /// 240 until the minimum-width research measured the standard
        /// CRAFT/TP/VENDOR/IGNORE run at 222px against the 236px budget a
        /// 240px column leaves: any slightly wider label ran the row through
        /// the tightened-padding pass. 256 gives that run its comfort margin
        /// (budget 252). Cost: 16px off every row's name column, which the
        /// window minimum was raised to absorb - see
        /// docs/research/minimum-window-width.md.
        /// </para>
        /// </summary>
        public const int TreePillColumnWidth = 256;

        /// <summary>
        /// Right edge of a blockWidth-wide right-hand block once
        /// <see cref="RightBlockX"/> has closed the dead gutter before it -
        /// i.e. the anchor every column inside the block derives from. The
        /// one expression each flat plan table's build pass and relayout
        /// closures share, so no two of them can anchor a table differently.
        /// widestNameEnd 0 gives exactly the old
        /// <c>panelWidth - TableRightMargin</c> edge.
        /// </summary>
        public static int RightBlockRightEdge(int panelWidth, int blockWidth, int widestNameEnd)
        {
            return RightBlockX(panelWidth - TableRightMargin - blockWidth, widestNameEnd) + blockWidth;
        }

        public readonly struct TreeColumnEdges
        {
            public readonly int PillColX;
            public readonly int CostRightEdge;
            public readonly int NameMaxWidth;

            public TreeColumnEdges(int pillColX, int costRightEdge, int nameMaxWidth)
            {
                PillColX = pillColX;
                CostRightEdge = costRightEdge;
                NameMaxWidth = nameMaxWidth;
            }
        }

        /// <summary>
        /// Recipe tree's fixed right-anchored column grid (pills, cost) plus
        /// the resulting name-column budget, entirely as a function of
        /// panelWidth plus the row's own indent-derived nameX and the
        /// qty-prefix text width (font-only, invariant to panelWidth).
        /// Mirrors CraftingPlanView.RenderTreeNode's own pillColX/
        /// costRightEdge/nameMaxWidth arithmetic exactly - both the initial
        /// build and the relayout/re-ellipsis closures call this
        /// same function, so a tree row's columns and its build-time
        /// counterpart can never drift apart.
        /// <para>
        /// widestNameEnd closes the tree's dead gutter: the pill+cost block
        /// moves as ONE unit (see <see cref="RightBlockX"/>), so the
        /// pill column's own budget - maxRightEdge minus pillColX, which
        /// <see cref="ComputePillFit"/> resolves against - is exactly as
        /// wide as it was when the block was pinned to the panel edge. A
        /// pill that fitted before still fits. Left at its 0 default the
        /// whole grid is byte-identical to the pinned layout.
        /// </para>
        /// </summary>
        public static TreeColumnEdges ComputeTreeColumnEdges(
            int panelWidth, int nameX, int qtyPrefixWidth,
            int pillColumnWidth, int costColumnWidth, int rightMargin, int widestNameEnd = 0)
        {
            int pillColX = RightBlockX(
                panelWidth - (rightMargin + costColumnWidth) - pillColumnWidth, widestNameEnd);
            int costRightEdge = pillColX + pillColumnWidth + costColumnWidth;

            int nameMaxWidth = pillColX - nameX - 8;
            if (nameMaxWidth < 20) nameMaxWidth = 20;

            int nameAvailWidth = nameMaxWidth - qtyPrefixWidth;
            if (nameAvailWidth < 10) nameAvailWidth = 10;

            return new TreeColumnEdges(pillColX, costRightEdge, nameAvailWidth);
        }

        /// <summary>
        /// how many of the recipe tree's
        /// decision pills (already-measured widths, in
        /// DecisionPillPlanner.BuildPillSpecs emission order - source pills
        /// first, then the supplementary OwnedInfo/Ignore pills) fit
        /// left-to-right starting at startX before the next one would cross
        /// maxRightEdge (the boundary before the right-aligned cost
        /// column). The tree row has no wrap/second-line support
        /// (TreeRowHeight is a single fixed height shared by every
        /// scroll/layout-height calculation in CraftingPlanView), so pills
        /// that would overlap the cost column cannot be rendered.
        /// <para>
        /// This is the primitive, not the policy: <see cref="ComputePillFit"/>
        /// calls it up to three times (normal padding, tightened padding,
        /// tightened padding with room reserved for a "+N" pill) and is
        /// what the renderer actually uses. Dropping the remainder without
        /// announcing it is what ComputePillFit exists to stop.
        /// </para>
        ///
        /// Always returns at least 1 when pillWidths is non-empty, even if
        /// that first pill alone would exceed the budget: a completely
        /// empty pill column reads worse than one slightly-overflowing
        /// pill, and every pill after the first is dropped strictly once it
        /// would not entirely fit (a node's pills only ever grow wider
        /// left-to-right, so once one is cut every later one would be too).
        /// </summary>
        public static int ComputeVisiblePillCount(
            IReadOnlyList<int> pillWidths, int gap, int startX, int maxRightEdge)
        {
            return ComputeVisiblePillCount(pillWidths, 0, gap, startX, maxRightEdge);
        }

        /// <summary>
        /// <see cref="ComputeVisiblePillCount"/> with every pill narrowed by
        /// widthReduction - the tightened-padding pass, without a second
        /// measured-width list existing for the tree to allocate per row.
        /// A pill can never narrow below 1px however large the reduction.
        /// </summary>
        private static int ComputeVisiblePillCount(
            IReadOnlyList<int> pillWidths, int widthReduction, int gap, int startX, int maxRightEdge)
        {
            if (pillWidths == null || pillWidths.Count == 0)
            {
                return 0;
            }

            int x = startX;
            int count = 0;
            for (int i = 0; i < pillWidths.Count; i++)
            {
                int width = ReducedWidth(pillWidths[i], widthReduction);
                if (i > 0 && x + width > maxRightEdge)
                {
                    break;
                }
                x += width + gap;
                count++;
            }
            return count;
        }

        /// <summary>
        /// One pill's width at a given padding reduction, floored at 1px.
        /// The renderer calls this too, so build and fit cannot disagree
        /// about how wide a tightened pill is.
        /// </summary>
        public static int ReducedWidth(int fullWidth, int widthReduction)
        {
            int width = fullWidth - widthReduction;
            return width > 1 ? width : 1;
        }

        /// <summary>
        /// How <see cref="ComputePillFit"/> resolved one tree row's pill
        /// column: how many pills to draw, how much narrower to draw them
        /// (0 = their measured width), how many were left out, and how wide
        /// the trailing "+N" pill announcing them must be (0 when nothing
        /// was left out).
        /// </summary>
        public readonly struct PillFitPlan
        {
            public readonly int VisibleCount;
            public readonly int HiddenCount;
            public readonly int WidthReduction;
            public readonly int OverflowPillWidth;

            public PillFitPlan(int visibleCount, int hiddenCount, int widthReduction, int overflowPillWidth)
            {
                VisibleCount = visibleCount;
                HiddenCount = hiddenCount;
                WidthReduction = widthReduction;
                OverflowPillWidth = overflowPillWidth;
            }
        }

        /// <summary>
        /// Full pill-column plan for one tree row, in the order the fix
        /// escalates:
        /// <list type="number">
        /// <item><description>Every pill fits at its normal padding - draw
        /// them all, nothing else changes.</description></item>
        /// <item><description>They fit once every pill is narrowed by
        /// <paramref name="widthReduction"/> - draw them all that narrow.
        /// Squeezing is cheaper than hiding a real option.
        /// </description></item>
        /// <item><description>They still do not fit - reserve room for a
        /// trailing "+N" pill and draw as many tightened pills as fit
        /// before it, so the row states that options exist rather than
        /// dropping them silently.</description></item>
        /// </list>
        /// The "+N" pill's own width depends on N, which depends on how
        /// many pills its width displaced, so the last step iterates to a
        /// fixed point. N is non-decreasing across iterations and bounded
        /// by the pill count, so it settles immediately in practice (only a
        /// digit-count change moves it at all); the loop is capped anyway,
        /// and HiddenCount is derived from the final VisibleCount either
        /// way, so an uncoverged width is a few pixels wrong and never a
        /// wrong count.
        /// <para>
        /// <paramref name="overflowPillWidthForHidden"/> measures "+N" for
        /// a given N. Null degrades to the old drop-silently behaviour
        /// rather than throwing, as does a non-positive
        /// <paramref name="widthReduction"/> for the tightening step.
        /// </para>
        /// <para>
        /// Like <see cref="ComputeVisiblePillCount"/>, at least one pill is
        /// always drawn even if it alone overruns - so a row whose budget
        /// cannot even hold one pill plus the "+N" draws both slightly
        /// over, which still beats an empty column.
        /// </para>
        /// </summary>
        public static PillFitPlan ComputePillFit(
            IReadOnlyList<int> pillWidths,
            int widthReduction,
            int gap,
            int startX,
            int maxRightEdge,
            Func<int, int> overflowPillWidthForHidden)
        {
            if (pillWidths == null || pillWidths.Count == 0)
            {
                return new PillFitPlan(0, 0, 0, 0);
            }

            int count = pillWidths.Count;
            int fullFit = ComputeVisiblePillCount(pillWidths, 0, gap, startX, maxRightEdge);
            if (fullFit >= count)
            {
                return new PillFitPlan(fullFit, 0, 0, 0);
            }

            int reduction = widthReduction > 0 ? widthReduction : 0;
            int reducedFit = reduction > 0
                ? ComputeVisiblePillCount(pillWidths, reduction, gap, startX, maxRightEdge)
                : fullFit;
            if (reducedFit >= count)
            {
                return new PillFitPlan(reducedFit, 0, reduction, 0);
            }

            if (overflowPillWidthForHidden == null)
            {
                return new PillFitPlan(reducedFit, 0, reduction, 0);
            }

            int hidden = count - reducedFit;
            int visible = reducedFit;
            int overflowWidth = 0;
            for (int i = 0; i < 4; i++)
            {
                int candidateWidth = overflowPillWidthForHidden(hidden);
                if (candidateWidth < 0) candidateWidth = 0;

                int fit = ComputeVisiblePillCount(
                    pillWidths, reduction, gap, startX, maxRightEdge - candidateWidth - gap);
                int nextHidden = count - fit;

                overflowWidth = candidateWidth;
                visible = fit;
                if (nextHidden == hidden) break;
                hidden = nextHidden;
            }

            return new PillFitPlan(visible, count - visible, reduction, overflowWidth);
        }

        public readonly struct CostTileGeometry
        {
            public readonly int TileWidth;
            public readonly int StartX;

            public CostTileGeometry(int tileWidth, int startX)
            {
                TileWidth = tileWidth;
                StartX = startX;
            }
        }

        /// <summary>
        /// Equal-width stat-tile row geometry (Summary section's cost
        /// tiles): tileWidth clamped to a minimum, then the row of tiles is
        /// centered as a unit within panelWidth. Used by
        /// SummarySectionRenderer.CreateFormulaBand.
        /// tileCount &lt;= 0 returns a zero-width, zero-offset geometry
        /// (caller already skips rendering the row entirely in that case).
        /// </summary>
        public static CostTileGeometry ComputeCostTileGeometry(
            int panelWidth, int tileCount, int totalMargin, int minTileWidth)
        {
            if (tileCount <= 0)
            {
                return new CostTileGeometry(0, 0);
            }

            int tileWidth = (panelWidth - totalMargin) / tileCount;
            if (tileWidth < minTileWidth) tileWidth = minTileWidth;
            int rowContentWidth = tileWidth * tileCount;

            return new CostTileGeometry(tileWidth, CenterX(panelWidth, rowContentWidth));
        }
    }
}
