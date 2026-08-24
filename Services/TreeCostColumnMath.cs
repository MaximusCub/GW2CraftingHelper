using System;
using System.Collections.Generic;
using GW2CraftingHelper.Models;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure arithmetic (Blish-free, unit-testable) for the recipe tree's
    /// cost column, which used to right-align one ragged run per row: a
    /// gold/silver/copper row and a currency row both ended at the same x
    /// but shared no interior alignment, so no two coin icons in the whole
    /// tree lined up vertically.
    ///
    /// The column is now four right-aligned sub-columns - gold, silver,
    /// copper, then any non-coin currency - each wide enough for the
    /// widest value ANY row in the tree puts in it. Because every segment
    /// is "number, gap, icon" and each is right-aligned to its own
    /// sub-column's right edge, the fixed-width icons land on the same x
    /// on every row that fills the same bands: straight vertical rules
    /// down the column. Which bands a given row fills is
    /// <see cref="ComputeRowEdges"/>'s business.
    ///
    /// Scanned over the WHOLE tree, not just the currently expanded rows.
    /// Rows are built lazily (TreeSectionController.RenderTreeNode's
    /// toggle handler builds a node's children on first expand), so a
    /// visible-rows-only scan would either miss those rows' widths or have
    /// to re-scan and re-anchor the entire column mid-interaction. Scanning
    /// everything once per render pass costs one walk of an
    /// already-materialised tree and buys a column that never shifts
    /// under the user.
    ///
    /// That same walk now also reports the widest name extent in the tree
    /// (<see cref="ScanColumns"/>), which is what lets the pill+cost block
    /// be pulled in beside the names instead of pinned to the panel edge -
    /// same stability argument, same single pass.
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    public static class TreeCostColumnMath
    {
        /// <summary>
        /// The widest text each denomination's sub-column has to hold this
        /// render, plus the widest whole currency run. A denomination no
        /// row in the tree ever renders has width 0 and its sub-column
        /// collapses away entirely (no reserved band, no gap).
        /// </summary>
        public readonly struct CostColumnWidths
        {
            public readonly int GoldTextWidth;
            public readonly int SilverTextWidth;
            public readonly int CopperTextWidth;
            public readonly int CurrencyRunWidth;

            public CostColumnWidths(int goldTextWidth, int silverTextWidth, int copperTextWidth, int currencyRunWidth)
            {
                GoldTextWidth = goldTextWidth;
                SilverTextWidth = silverTextWidth;
                CopperTextWidth = copperTextWidth;
                CurrencyRunWidth = currencyRunWidth;
            }

            public static readonly CostColumnWidths Empty = new CostColumnWidths(0, 0, 0, 0);
        }

        /// <summary>
        /// Right edge of each sub-column. A collapsed (zero-width)
        /// sub-column reports the same edge as its right-hand neighbour,
        /// which is harmless: no row renders a segment into it.
        /// </summary>
        public readonly struct CostSubColumnEdges
        {
            public readonly int GoldRightEdge;
            public readonly int SilverRightEdge;
            public readonly int CopperRightEdge;
            public readonly int CurrencyRightEdge;

            /// <summary>
            /// Total width the populated sub-columns actually occupy, from
            /// the leftmost reserved x to costRightEdge - what the caller
            /// must reserve for the cost column so a wide row cannot run
            /// back into the decision pills to its left.
            /// </summary>
            public readonly int TotalWidth;

            public CostSubColumnEdges(
                int goldRightEdge, int silverRightEdge, int copperRightEdge, int currencyRightEdge, int totalWidth)
            {
                GoldRightEdge = goldRightEdge;
                SilverRightEdge = silverRightEdge;
                CopperRightEdge = copperRightEdge;
                CurrencyRightEdge = currencyRightEdge;
                TotalWidth = totalWidth;
            }
        }

        /// <summary>
        /// Full width of one "number, gap, icon" segment whose text is
        /// textWidth wide - 0 for an absent denomination, so an absent
        /// sub-column contributes neither width nor a separating gap.
        /// </summary>
        public static int SegmentWidth(int textWidth)
        {
            return textWidth > 0
                ? textWidth + CoinSegmentMath.CoinLabelIconGap + CoinSegmentMath.CoinIconSize
                : 0;
        }

        /// <summary>
        /// Sub-column right edges, derived right-to-left off the cost
        /// column's own right edge so the build pass and every later
        /// resize tick anchor identically (the same shape
        /// ShoppingColumnMath.ComputeEdges uses). Currency sits rightmost
        /// because a mixed row already renders coin-then-currency
        /// left-to-right.
        /// </summary>
        public static CostSubColumnEdges ComputeEdges(int costRightEdge, CostColumnWidths widths)
        {
            const int gap = CoinSegmentMath.CoinSegmentGap;

            int currencyRightEdge = costRightEdge;
            int copperRightEdge = widths.CurrencyRunWidth > 0
                ? costRightEdge - widths.CurrencyRunWidth - gap
                : costRightEdge;

            int copperWidth = SegmentWidth(widths.CopperTextWidth);
            int silverRightEdge = copperWidth > 0 ? copperRightEdge - copperWidth - gap : copperRightEdge;

            int silverWidth = SegmentWidth(widths.SilverTextWidth);
            int goldRightEdge = silverWidth > 0 ? silverRightEdge - silverWidth - gap : silverRightEdge;

            return new CostSubColumnEdges(
                goldRightEdge, silverRightEdge, copperRightEdge, currencyRightEdge, TotalWidth(widths));
        }

        /// <summary>
        /// The sub-column edges ONE row draws into. A row that renders no
        /// currency segments collapses the trailing currency band for
        /// itself, so its coin run - or its unpriceable dash - ends on the
        /// cost column's own right edge, which is the edge the "Cost"
        /// header is right-aligned to.
        /// <para>
        /// Field test, bug 4: with a shared band reserved for the widest
        /// currency run in the tree, every coin-only row stopped short of
        /// that edge by the width of a band it never filled, so gold
        /// figures sat visibly left of the header while currency rows lined
        /// up under it. Rows that DO draw currency keep the shared band, so
        /// their coin runs still line up with each other; the reserved
        /// width (<see cref="TotalWidth"/>) is unchanged either way, so no
        /// row can now reach further right than the column already owned.
        /// </para>
        /// </summary>
        public static CostSubColumnEdges ComputeRowEdges(
            int costRightEdge, CostColumnWidths widths, bool rowDrawsCurrency)
        {
            if (rowDrawsCurrency) return ComputeEdges(costRightEdge, widths);

            var collapsed = ComputeEdges(
                costRightEdge,
                new CostColumnWidths(
                    widths.GoldTextWidth, widths.SilverTextWidth, widths.CopperTextWidth, 0));

            // TotalWidth stays the WHOLE column's reserved width, not this
            // row's: it is what the caller reserves so a wide row cannot run
            // back into the pills, and that budget is a property of the
            // tree, not of whichever row is being placed.
            return new CostSubColumnEdges(
                collapsed.GoldRightEdge, collapsed.SilverRightEdge, collapsed.CopperRightEdge,
                collapsed.CurrencyRightEdge, TotalWidth(widths));
        }

        /// <summary>
        /// Width every populated sub-column plus its separating gaps
        /// occupies - what the tree must reserve for its cost column
        /// (TreeSectionController passes max(this, its own fixed floor) as
        /// ComputeTreeColumnEdges' costColumnWidth) so a tree full of
        /// multi-gold values pushes the decision pills left instead of
        /// silently overprinting them. Independent of costRightEdge, so it
        /// can be asked before any column edge exists.
        /// </summary>
        public static int TotalWidth(CostColumnWidths widths)
        {
            int total = AddBand(0, SegmentWidth(widths.GoldTextWidth));
            total = AddBand(total, SegmentWidth(widths.SilverTextWidth));
            total = AddBand(total, SegmentWidth(widths.CopperTextWidth));
            return AddBand(total, widths.CurrencyRunWidth);
        }

        private static int AddBand(int total, int bandWidth)
        {
            if (bandWidth <= 0) return total;
            return total > 0 ? total + CoinSegmentMath.CoinSegmentGap + bandWidth : bandWidth;
        }

        /// <summary>
        /// Whether a node's cost cell renders its non-coin currency
        /// segments. A node whose children are the synthesised
        /// cost-component leaves shows only its compact coin total (the
        /// breakdown is one expand-click away as real child rows) - see
        /// TreeSectionController.RenderTreeNode, which calls this rather
        /// than re-deriving the rule, so the pre-scan below reserves
        /// currency width for exactly the rows that draw it.
        /// </summary>
        public static bool ShowsCurrencySegments(CraftingTreeNode node)
        {
            if (node?.VendorCurrencyCosts == null || node.VendorCurrencyCosts.Count == 0) return false;
            var children = node.Children;
            return !(children != null && children.Count > 0 && children[0].IsCostComponent);
        }

        /// <summary>
        /// Everything one render pass's single walk of the tree measures:
        /// the cost sub-column widths and the node count (see
        /// <see cref="ScanColumns"/>).
        /// </summary>
        public readonly struct TreeColumnScan
        {
            public readonly CostColumnWidths CostWidths;

            /// <summary>
            /// Every node in the tree, at every depth, expanded or not -
            /// which is exactly the number of rows the section renders once
            /// Expand All is pressed, and so the count the section header
            /// shows in parentheses like every other countable section
            /// (audit batch J, L2). It rides this walk rather than a second
            /// one for the same reason the widths do.
            /// </summary>
            public readonly int NodeCount;

            public TreeColumnScan(CostColumnWidths costWidths, int nodeCount)
            {
                CostWidths = costWidths;
                NodeCount = nodeCount;
            }

            public static readonly TreeColumnScan Empty =
                new TreeColumnScan(CostColumnWidths.Empty, 0);
        }

        /// <summary>
        /// <see cref="ScanColumns"/> without the node count - the
        /// cost-only shape this class started as.
        /// </summary>
        public static CostColumnWidths Scan(
            IReadOnlyList<CraftingTreeNode> roots,
            Func<string, int> measureText,
            Func<CraftingTreeNode, int> measureCurrencyRunWidth)
        {
            return ScanColumns(roots, measureText, measureCurrencyRunWidth).CostWidths;
        }

        /// <summary>
        /// Widest value per denomination across every node in the tree
        /// that renders a cost cell at all (SubtreeCost.HasValue - a
        /// HAVE/UNKNOWN node keeps the column blank and must not widen
        /// it), plus the node count.
        /// <para>
        /// measureText is the caller's own text measurement
        /// (BitmapFont.MeasureString is Blish-bound, so it stays with the
        /// view); measureCurrencyRunWidth is the width of one node's whole
        /// resolved currency run, called only for the nodes that actually
        /// draw one. Both are invoked at most once per node.
        /// </para>
        /// <para>
        /// The walk covers the whole tree, not the currently expanded rows:
        /// rows are built lazily, so a visible-rows-only scan would move
        /// the cost column the first time a node is expanded.
        /// </para>
        /// </summary>
        public static TreeColumnScan ScanColumns(
            IReadOnlyList<CraftingTreeNode> roots,
            Func<string, int> measureText,
            Func<CraftingTreeNode, int> measureCurrencyRunWidth)
        {
            if (roots == null || roots.Count == 0) return TreeColumnScan.Empty;
            if (measureText == null) throw new ArgumentNullException(nameof(measureText));
            if (measureCurrencyRunWidth == null) throw new ArgumentNullException(nameof(measureCurrencyRunWidth));

            int gold = 0, silver = 0, copper = 0, currency = 0, nodeCount = 0;
            foreach (var root in roots)
            {
                ScanNode(
                    root, measureText, measureCurrencyRunWidth,
                    ref gold, ref silver, ref copper, ref currency, ref nodeCount);
            }
            return new TreeColumnScan(
                new CostColumnWidths(gold, silver, copper, currency), nodeCount);
        }

        // Explicit stack rather than recursion: a solver tree's depth is
        // data-driven (a deep reference branch under a deep craft chain),
        // and this walk visits every node including the ones no expand
        // state ever reveals.
        private static void ScanNode(
            CraftingTreeNode root, Func<string, int> measureText, Func<CraftingTreeNode, int> measureCurrencyRunWidth,
            ref int gold, ref int silver, ref int copper, ref int currency,
            ref int nodeCount)
        {
            if (root == null) return;

            var pending = new Stack<CraftingTreeNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node == null) continue;

                nodeCount++;

                // > 0, not merely HasValue: a genuinely zero-and-uncosted
                // decision renders the unpriceable dash instead of coin
                // segments, so it must not reserve a copper column.
                if (node.SubtreeCost.HasValue && node.SubtreeCost.Value > 0)
                {
                    var (goldText, silverText, copperText) = CoinSegmentMath.FormatSegmentTexts(node.SubtreeCost.Value);
                    if (goldText != null) gold = Max(gold, measureText(goldText));
                    if (silverText != null) silver = Max(silver, measureText(silverText));
                    if (copperText != null) copper = Max(copper, measureText(copperText));
                }

                if (node.SubtreeCost.HasValue && ShowsCurrencySegments(node))
                {
                    currency = Max(currency, measureCurrencyRunWidth(node));
                }

                var children = node.Children;
                if (children == null) continue;
                for (int i = 0; i < children.Count; i++)
                {
                    pending.Push(children[i]);
                }
            }
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }
    }
}
