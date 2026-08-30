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
    /// The column was a flat <see cref="PlanRelayoutMath.TreePillColumnWidth"/>
    /// at every panel width, so a row whose pills did not fit in it showed a
    /// "+N" chip however much room the window had. On a wide window with
    /// short names that is a lie: the name column flexes and absorbs every
    /// pixel the pills were denied. Reported on an Obsidian Heavy
    /// Breastplate plan, where rows carrying a HAVE annotation alongside
    /// their source pills chipped at panel widths with hundreds of pixels
    /// stranded in the name column.
    /// </para>
    /// <para>
    /// So the column is data-derived, exactly as the cost column already is
    /// (TreeSectionController.EffectiveCostColumnWidth): the widest run any
    /// row needs, floored at the fixed width so nothing narrows, and capped
    /// by <see cref="Affordable"/> so the name column cannot be starved.
    /// Like the cost column's, the result must be held as a one-way floor
    /// for the life of a plan - see TreeCostColumnFloor for why a column
    /// edge that narrows under a click slides every pill out from under the
    /// cursor.
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
        /// fixed floor, plus half of whatever the window has beyond the
        /// module's minimum.
        /// <para>
        /// At the minimum width the column cannot grow at all, so every
        /// pinned deep-row budget the minimum was derived from
        /// (docs/research/minimum-window-width.md) is untouched. Above it
        /// the two flexible things - the pills' unmet need and the name
        /// column - split the surplus, so widening the window can never
        /// leave the name column narrower than it was one pixel earlier.
        /// Half rather than all of it because the name column is what the
        /// extra width is normally FOR; the pills only take from that share
        /// what they can actually use, since
        /// <see cref="ColumnWidth"/> never returns more than the widest row
        /// asked for.
        /// </para>
        /// </summary>
        public static int Affordable(int panelWidth, int floorWidth, int minimumPanelWidth)
        {
            int surplus = panelWidth - minimumPanelWidth;
            return surplus > 0 ? floorWidth + (surplus / 2) : floorWidth;
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
