using System;
using System.Collections.Generic;
using TaimisToolbench.Models;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure arithmetic (Blish-free, unit-testable) for the recipe tree's cost
    /// column: four right-aligned sub-columns - gold, silver, copper, then any
    /// non-coin currency - each wide enough for the widest value ANY row in the
    /// tree puts in it. Every segment is "number, gap, icon" and each is
    /// right-aligned to its own sub-column's right edge, so the fixed-width
    /// icons land on the same x on every row that fills the same bands. Which
    /// bands a given row fills is <see cref="ComputeRowEdges"/>'s business.
    ///
    /// Scanned over the WHOLE tree, not just the currently expanded rows: rows
    /// are built lazily (TreeSectionController.RenderTreeNode's toggle handler
    /// builds a node's children on first expand), so a visible-rows-only scan
    /// would miss those rows' widths or have to re-anchor the whole column
    /// mid-interaction. That same walk reports the tree's node count
    /// (<see cref="ScanColumns"/>), which the section header shows.
    ///
    /// <para>See docs/ARCHITECTURE.md section 4, and "Services Q-Z: relocated
    /// design narrative" for what the column looked like before.</para>
    /// </summary>
    internal static class TreeCostColumnMath
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

            /// <summary>
            /// Widest run of INK the column's rows draw, measured back from
            /// the column's right edge - what the "Cost" header centres
            /// over (<see cref="HeaderX"/>). Strictly narrower than
            /// <see cref="TotalWidth"/> whenever the sub-column maxima come
            /// from different rows, and much narrower when the currency
            /// band exists at all: a coin-only row collapses that band for
            /// itself (<see cref="ComputeRowEdges"/>), so no row's ink ever
            /// reaches the reserve's left edge. Measured in the coin-only
            /// regime wherever the column has a coin row at all, because
            /// the two regimes have different extents and one mixed row
            /// must not place the header for every coin row - see
            /// WidestRowRun. 0 when no row is priced.
            /// <para>
            /// Not derivable from the four widths above, so a caller that
            /// compares two scans field by field to decide whether a
            /// re-solve can refresh rows in place has to compare this one
            /// too, or leave the header on a stale x.
            /// </para>
            /// </summary>
            public readonly int WidestRowRunWidth;

            public CostColumnWidths(
                int goldTextWidth, int silverTextWidth, int copperTextWidth, int currencyRunWidth,
                int widestRowRunWidth = 0)
            {
                GoldTextWidth = goldTextWidth;
                SilverTextWidth = silverTextWidth;
                CopperTextWidth = copperTextWidth;
                CurrencyRunWidth = currencyRunWidth;
                WidestRowRunWidth = widestRowRunWidth;
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
            if (rowDrawsCurrency)
            {
                return ComputeEdges(costRightEdge, widths);
            }

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
            if (bandWidth <= 0)
            {
                return total;
            }

            return total > 0 ? total + CoinSegmentMath.CoinSegmentGap + bandWidth : bandWidth;
        }

        /// <summary>
        /// Left edge of the "Cost" header, centred over the INK -
        /// <see cref="CostColumnWidths.WidestRowRunWidth"/>, ending at
        /// <paramref name="costRightEdge"/> - and not over either reserve
        /// around it: neither the tree's fixed column floor
        /// (TreeSectionController.EffectiveCostColumnWidth) nor
        /// <see cref="TotalWidth"/>, which sums per-denomination maxima
        /// that no one row draws together. On the owner's 2026-08-28
        /// capture the two differed by 106px - the whole width of a
        /// currency band every coin-only row collapses.
        /// <para>
        /// <paramref name="room"/> is the gap to the pill column on one
        /// side and the table's pinned edge on the other
        /// (PlanRelayoutMath.ComputeTreeHeaderRooms), so a header wider
        /// than the ink under it overhangs its own reserve freely and only
        /// right-aligns on the table edge when there is genuinely nowhere
        /// left - which is where a tree with nothing priced at all leaves
        /// it.
        /// </para>
        /// </summary>
        public static int HeaderX(
            int costRightEdge, CostColumnWidths widths, int headerWidth,
            JustifiedColumnTracks.HeaderRoom room)
        {
            return JustifiedColumnTracks.CenteredOverContentRightAligned(
                costRightEdge, widths.WidestRowRunWidth, headerWidth, room);
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
            if (node?.VendorCurrencyCosts == null || node.VendorCurrencyCosts.Count == 0)
            {
                return false;
            }

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
            /// It rides this walk rather than a second
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
            if (roots == null || roots.Count == 0)
            {
                return TreeColumnScan.Empty;
            }

            if (measureText == null)
            {
                throw new ArgumentNullException(nameof(measureText));
            }

            if (measureCurrencyRunWidth == null)
            {
                throw new ArgumentNullException(nameof(measureCurrencyRunWidth));
            }

            var acc = new ScanAccumulator();
            foreach (var root in roots)
            {
                ScanNode(root, measureText, measureCurrencyRunWidth, acc);
            }

            var widths = new CostColumnWidths(acc.Gold, acc.Silver, acc.Copper, acc.Currency);
            return new TreeColumnScan(
                new CostColumnWidths(
                    acc.Gold, acc.Silver, acc.Copper, acc.Currency, WidestRowRun(widths, acc)),
                acc.NodeCount);
        }

        /// <summary>
        /// One walk's running maxima. The four sub-column widths are the
        /// column's RESERVE; the seven Lead* fields are what the ink
        /// extent needs on top of it - the widest FIRST segment any row
        /// draws, per denomination, in each of the two regimes
        /// <see cref="ComputeRowEdges"/> lays a row out in. A row's ink
        /// starts at its leading segment's sub-column edge, so those are
        /// the only per-row measurements the extent depends on, and one
        /// walk can carry them without holding the rows.
        /// </summary>
        private sealed class ScanAccumulator
        {
            public int Gold;
            public int Silver;
            public int Copper;
            public int Currency;
            public int NodeCount;
            public int LeadGoldCoinOnly;
            public int LeadSilverCoinOnly;
            public int LeadCopperCoinOnly;
            public int LeadGoldMixed;
            public int LeadSilverMixed;
            public int LeadCopperMixed;
            public int LeadCurrency;

            /// <summary>Rows drawing ink in each regime - see
            /// WidestRowRun for why the header follows the larger.</summary>
            public int CoinOnlyRows;
            public int MixedRows;
        }

        /// <summary>
        /// Widest ink run the column's rows draw, in pixels back from its
        /// right edge - the extent the "Cost" header centres over
        /// (<see cref="HeaderX"/>). Resolved after the walk because a row's
        /// segments right-align into sub-columns whose edges are not known
        /// until every row has been measured.
        /// <para>
        /// The column lays rows out in two regimes that do not share an
        /// extent: a row with no currency ink collapses the shared currency
        /// band for itself (<see cref="ComputeRowEdges"/>), so coin rows
        /// start a band-plus-gap RIGHT of mixed rows. Taking the max put
        /// the header over an extent no coin row reaches - measured at 43px
        /// off centre with a 96px band. A header sits over ONE extent, so
        /// the regime with more rows wins; a tie goes to coin-only, which
        /// the shared sub-columns are laid out for.
        /// </para>
        /// </summary>
        private static int WidestRowRun(CostColumnWidths widths, ScanAccumulator acc)
        {
            var coinOnly = ComputeRowEdges(0, widths, rowDrawsCurrency: false);
            int coinOnlyRun = Reach(coinOnly.GoldRightEdge, acc.LeadGoldCoinOnly);
            coinOnlyRun = Max(coinOnlyRun, Reach(coinOnly.SilverRightEdge, acc.LeadSilverCoinOnly));
            coinOnlyRun = Max(coinOnlyRun, Reach(coinOnly.CopperRightEdge, acc.LeadCopperCoinOnly));

            var mixed = ComputeEdges(0, widths);
            int mixedRun = Reach(mixed.GoldRightEdge, acc.LeadGoldMixed);
            mixedRun = Max(mixedRun, Reach(mixed.SilverRightEdge, acc.LeadSilverMixed));
            mixedRun = Max(mixedRun, Reach(mixed.CopperRightEdge, acc.LeadCopperMixed));
            mixedRun = Max(mixedRun, Reach(mixed.CurrencyRightEdge, acc.LeadCurrency));

            return acc.MixedRows > acc.CoinOnlyRows ? mixedRun : coinOnlyRun;
        }

        /// <summary>
        /// How far left of the column's right edge a row reaches when its
        /// first segment is leadSegmentWidth wide and right-aligns on
        /// <paramref name="subColumnRightEdge"/>, which is itself <= 0
        /// because <see cref="WidestRowRun"/> computes the edges off a
        /// right edge of 0. No such row means no reach.
        /// </summary>
        private static int Reach(int subColumnRightEdge, int leadSegmentWidth)
        {
            return leadSegmentWidth > 0 ? leadSegmentWidth - subColumnRightEdge : 0;
        }

        // Explicit stack rather than recursion: a solver tree's depth is
        // data-driven (a deep reference branch under a deep craft chain),
        // and this walk visits every node including the ones no expand
        // state ever reveals.
        private static void ScanNode(
            CraftingTreeNode root, Func<string, int> measureText, Func<CraftingTreeNode, int> measureCurrencyRunWidth,
            ScanAccumulator acc)
        {
            if (root == null)
            {
                return;
            }

            var pending = new Stack<CraftingTreeNode>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var node = pending.Pop();
                if (node == null)
                {
                    continue;
                }

                acc.NodeCount++;

                int goldSegment = 0, silverSegment = 0, copperSegment = 0;

                // > 0, not merely HasValue: a genuinely zero-and-uncosted
                // decision renders the unpriceable dash instead of coin
                // segments, so it must not reserve a copper column.
                if (node.SubtreeCost.HasValue && node.SubtreeCost.Value > 0)
                {
                    var (goldText, silverText, copperText) = CoinSegmentMath.FormatSegmentTexts(node.SubtreeCost.Value);
                    if (goldText != null)
                    {
                        int width = measureText(goldText);
                        acc.Gold = Max(acc.Gold, width);
                        goldSegment = SegmentWidth(width);
                    }

                    if (silverText != null)
                    {
                        int width = measureText(silverText);
                        acc.Silver = Max(acc.Silver, width);
                        silverSegment = SegmentWidth(width);
                    }

                    if (copperText != null)
                    {
                        int width = measureText(copperText);
                        acc.Copper = Max(acc.Copper, width);
                        copperSegment = SegmentWidth(width);
                    }
                }

                int currencyRun = 0;
                if (node.SubtreeCost.HasValue && ShowsCurrencySegments(node))
                {
                    currencyRun = measureCurrencyRunWidth(node);
                    acc.Currency = Max(acc.Currency, currencyRun);
                }

                RecordLeadingSegment(acc, goldSegment, silverSegment, copperSegment, currencyRun);

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
        }

        /// <summary>
        /// Files this row's LEFTMOST drawn segment under the denomination
        /// it belongs to. The regime is the same one
        /// <see cref="ComputeRowEdges"/> uses (a row with no currency ink
        /// collapses the shared currency band for itself), and matches the
        /// view's own rowDrawsCurrency, which is likewise "the resolved
        /// currency run is non-empty" rather than the predicate that
        /// reserves the band.
        /// </summary>
        private static void RecordLeadingSegment(
            ScanAccumulator acc, int goldSegment, int silverSegment, int copperSegment, int currencyRun)
        {
            bool mixed = currencyRun > 0;
            if (goldSegment > 0 || silverSegment > 0 || copperSegment > 0 || mixed)
            {
                if (mixed)
                {
                    acc.MixedRows++;
                }
                else
                {
                    acc.CoinOnlyRows++;
                }
            }

            if (goldSegment > 0)
            {
                if (mixed)
                {
                    acc.LeadGoldMixed = Max(acc.LeadGoldMixed, goldSegment);
                }
                else
                {
                    acc.LeadGoldCoinOnly = Max(acc.LeadGoldCoinOnly, goldSegment);
                }
            }
            else if (silverSegment > 0)
            {
                if (mixed)
                {
                    acc.LeadSilverMixed = Max(acc.LeadSilverMixed, silverSegment);
                }
                else
                {
                    acc.LeadSilverCoinOnly = Max(acc.LeadSilverCoinOnly, silverSegment);
                }
            }
            else if (copperSegment > 0)
            {
                if (mixed)
                {
                    acc.LeadCopperMixed = Max(acc.LeadCopperMixed, copperSegment);
                }
                else
                {
                    acc.LeadCopperCoinOnly = Max(acc.LeadCopperCoinOnly, copperSegment);
                }
            }
            else if (mixed)
            {
                acc.LeadCurrency = Max(acc.LeadCurrency, currencyRun);
            }
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }
    }
}
