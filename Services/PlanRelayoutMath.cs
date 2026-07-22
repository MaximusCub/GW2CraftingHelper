using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure width-dependent layout arithmetic (Blish-free, unit-testable)
    /// shared by CraftingPlanView's initial section builders and its M33
    /// C2b live in-place resize relayout registry. Every formula here is
    /// called from BOTH the build path (CreateX... methods) and the
    /// relayout/re-ellipsis closures registered alongside them, so the two
    /// paths cannot drift apart - mirrors ShoppingColumnMath's "one source
    /// of truth" shape for the remaining width-dependent geometry m2's
    /// resize-path research identified (tree column anchors, cost-tile
    /// geometry, generic centering/right-alignment/name-column budgeting).
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
        /// build and the M33 C2b relayout/re-ellipsis closures call this
        /// same function, so a tree row's columns and its build-time
        /// counterpart can never drift apart.
        /// </summary>
        public static TreeColumnEdges ComputeTreeColumnEdges(
            int panelWidth, int nameX, int qtyPrefixWidth,
            int pillColumnWidth, int costColumnWidth, int rightMargin)
        {
            int pillColX = panelWidth - (rightMargin + costColumnWidth) - pillColumnWidth;
            int costRightEdge = panelWidth - rightMargin;

            int nameMaxWidth = pillColX - nameX - 8;
            if (nameMaxWidth < 20) nameMaxWidth = 20;

            int nameAvailWidth = nameMaxWidth - qtyPrefixWidth;
            if (nameAvailWidth < 10) nameAvailWidth = 10;

            return new TreeColumnEdges(pillColX, costRightEdge, nameAvailWidth);
        }

        /// <summary>
        /// M34 fix (MustFix review finding): how many of the recipe tree's
        /// decision pills (already-measured widths, in
        /// DecisionPillPlanner.BuildPillSpecs emission order - source pills
        /// first, then the supplementary OwnedInfo/Ignore pills) fit
        /// left-to-right starting at startX before the next one would cross
        /// maxRightEdge (the boundary before the right-aligned cost
        /// column). The tree row has no wrap/second-line support
        /// (TreeRowHeight is a single fixed height shared by every
        /// scroll/layout-height calculation in CraftingPlanView), so pills
        /// that would overlap the cost column are dropped instead of
        /// rendered - CraftingPlanView.RenderDecisionPills is the only
        /// caller.
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
            if (pillWidths == null || pillWidths.Count == 0)
            {
                return 0;
            }

            int x = startX;
            int count = 0;
            for (int i = 0; i < pillWidths.Count; i++)
            {
                int width = pillWidths[i];
                if (i > 0 && x + width > maxRightEdge)
                {
                    break;
                }
                x += width + gap;
                count++;
            }
            return count;
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
        /// centered as a unit within panelWidth. Mirrors
        /// CraftingPlanView.CreateCostTileRow's own arithmetic exactly.
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
