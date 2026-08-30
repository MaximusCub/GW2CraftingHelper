using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// How wide the recipe tree's decision-pill column is
    /// (Blish-free, unit-testable) - the twin of TreeCostColumnMath for the
    /// column on the other side of the name.
    /// <para>
    /// The width is data-derived, as the cost column's already is
    /// (TreeSectionController.EffectiveCostColumnWidth): the widest run any
    /// row needs, floored so nothing narrows, and capped by
    /// <see cref="Affordable"/> at the space actually available between
    /// the column's two neighbours' minimums. A flat width chipped "+N"
    /// on rows the window had room for; so did half-the-surplus.
    /// </para>
    /// <para>
    /// INVARIANT: the result is held as a one-way floor for the life of a
    /// plan - see TreeCostColumnFloor for why an edge that narrows under a
    /// click slides every pill out from under the cursor.
    /// docs/ARCHITECTURE.md section V.33.
    /// </para>
    /// </summary>
    internal static class TreePillColumnMath
    {
        /// <summary>
        /// Clearance the renderer keeps between the pill column's own right
        /// edge and the cost column - the gap a run fitted to the very
        /// pixel would otherwise close. Named here because both the fit
        /// (TreeSectionController.RenderDecisionPills) and the width that
        /// has to accommodate it (<see cref="RequiredWidth"/>) need the
        /// same number.
        /// </summary>
        public const int TrailingClearance = 4;

        /// <summary>
        /// Column width one row needs to draw its whole pill run at full
        /// padding: the left-packed leading run and its gaps, then the gap
        /// and the anchored IGNORE slot (Services/TreePillRunLayout), then
        /// <see cref="TrailingClearance"/>.
        /// <paramref name="anchoredWidth"/> 0 is a row with no toggle at
        /// all, which pays for neither the slot nor the gap before it.
        /// </summary>
        public static int RequiredWidth(
            IReadOnlyList<int> leadingPillWidths, int gap, int anchoredWidth)
        {
            int run = 0;
            if (leadingPillWidths != null)
            {
                for (int i = 0; i < leadingPillWidths.Count; i++)
                {
                    if (i > 0)
                    {
                        run += gap;
                    }

                    run += leadingPillWidths[i];
                }
            }

            if (anchoredWidth > 0)
            {
                run += (run > 0 ? gap : 0) + anchoredWidth;
            }

            return run > 0 ? run + TrailingClearance : 0;
        }

        /// <summary>
        /// The most the pill column may claim at this panel width: its
        /// fixed floor, plus the whole surplus the window has beyond the
        /// module's minimum (leftward), plus the cost column's
        /// <paramref name="rightSlack"/> - its reserve above what its rows
        /// actually draw (rightward).
        /// <para>
        /// Each direction stops at that side's own minimum. Leftward, the
        /// name column keeps the budget it holds at the minimum window -
        /// the budgets docs/research/minimum-window-width.md was derived
        /// from - so at or below that minimum the surplus term is zero and
        /// no leftward growth is possible. Rightward, the cost column keeps
        /// TreeCostColumnMath.TotalWidth, and the claim swaps its reserve
        /// for pill width one-for-one (<see cref="RightClaim"/>), so
        /// PillColX - and with it every name budget - holds wherever the
        /// unclaimed layout put it. Widening the window can therefore
        /// never leave the name column narrower than one pixel earlier.
        /// </para>
        /// </summary>
        public static int Affordable(
            int panelWidth, int floorWidth, int minimumPanelWidth, int rightSlack)
        {
            int surplus = panelWidth - minimumPanelWidth;
            if (surplus < 0)
            {
                surplus = 0;
            }

            return floorWidth + surplus + (rightSlack > 0 ? rightSlack : 0);
        }

        /// <summary>
        /// How much of <paramref name="columnWidth"/> came from the cost
        /// column's side: everything beyond the floor and the panel's
        /// whole surplus, clamped to <paramref name="rightSlack"/> - the
        /// reserve the cost column holds above what its rows draw. The
        /// caller shrinks the cost column's reserved width by the answer
        /// (never below TotalWidth), which is what extends the pill
        /// column's right edge toward the cost ink without moving
        /// PillColX, the cost values or any name budget.
        /// </summary>
        public static int RightClaim(
            int columnWidth, int floorWidth, int surplus, int rightSlack)
        {
            int claim = columnWidth - floorWidth - (surplus > 0 ? surplus : 0);
            if (claim < 0)
            {
                claim = 0;
            }

            int slack = rightSlack > 0 ? rightSlack : 0;
            return claim > slack ? slack : claim;
        }

        /// <summary>
        /// The width to reserve: never below <paramref name="floorWidth"/>,
        /// never above <paramref name="affordable"/>, and never more than
        /// the widest row asked for. A row that still does not fit inside
        /// the result degrades exactly as it always did, through
        /// PlanRelayoutMath.ComputePillFit's tightened padding and then its
        /// "+N" chip.
        /// </summary>
        public static int ColumnWidth(int required, int floorWidth, int affordable)
        {
            int width = required > floorWidth ? required : floorWidth;
            if (width > affordable)
            {
                width = affordable > floorWidth ? affordable : floorWidth;
            }

            return width;
        }

        /// <summary>
        /// Widest <see cref="RequiredWidth"/> over every node in the tree.
        /// <para>
        /// The WHOLE tree, not the rows currently expanded, for the reason
        /// TreeCostColumnMath.ScanColumns gives: rows are built lazily, and
        /// a visible-rows-only scan would move the column - and so every
        /// row's pills and name budget - the first time anyone expanded
        /// anything.
        /// </para>
        /// <para>
        /// One measurement per node, which is one
        /// DecisionPillPlanner.BuildPillSpecs per node per render pass on
        /// top of the cost column's own walk. Both are per RENDER, never
        /// per frame and never per resize tick.
        /// </para>
        /// </summary>
        public static int Scan(
            IReadOnlyList<CraftingTreeNode> roots, Func<CraftingTreeNode, int> measureRequiredWidth)
        {
            if (roots == null || roots.Count == 0)
            {
                return 0;
            }

            if (measureRequiredWidth == null)
            {
                throw new ArgumentNullException(nameof(measureRequiredWidth));
            }

            int widest = 0;
            var pending = new Stack<CraftingTreeNode>();
            for (int i = 0; i < roots.Count; i++)
            {
                pending.Push(roots[i]);
            }

            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node == null)
                {
                    continue;
                }

                int width = measureRequiredWidth(node);
                if (width > widest)
                {
                    widest = width;
                }

                var children = node.Children;
                if (children == null)
                {
                    continue;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    pending.Push(children[i]);
                }
            }

            return widest;
        }
    }
}
