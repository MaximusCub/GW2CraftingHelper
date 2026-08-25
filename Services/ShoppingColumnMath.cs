using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure column-edge arithmetic (Blish-free, unit-testable) for the
    /// shopping list's Amount/Each/Total table columns. The Each and Total
    /// price columns are right-aligned and grow leftward from a fixed right
    /// edge; their reserved band widths are derived per-render from the
    /// widest actual coin-value string in each column (measured in the view
    /// via BitmapFont.MeasureString, which is Blish-bound and therefore not
    /// tested here), clamped to fixed minimums so short/low-value lists
    /// don't look cramped. See ShoppingListSectionRenderer.Render for
    /// the pre-scan that produces maxEachWidth/maxTotalWidth.
    /// </summary>
    public static class ShoppingColumnMath
    {
        public const int TotalMinWidth = 150;
        public const int EachMinWidth = 110;
        public const int ColumnGap = 20;

        public readonly struct ColumnEdges
        {
            public readonly int TotalRightEdge;
            public readonly int EachRightEdge;
            public readonly int QtyRightEdge;

            /// <summary>
            /// LEFT edge of the source-badge column, and where its header
            /// label sits. Left, not right: badges are words of different
            /// lengths, and a column of them reads as a column because
            /// their left edges rule - the same choice Required Recipes'
            /// Discipline column makes.
            /// </summary>
            public readonly int SourceX;

            public ColumnEdges(int totalRightEdge, int eachRightEdge, int qtyRightEdge, int sourceX)
            {
                TotalRightEdge = totalRightEdge;
                EachRightEdge = eachRightEdge;
                QtyRightEdge = qtyRightEdge;
                SourceX = sourceX;
            }
        }

        /// <summary>
        /// Right edges for the Source/Amount/Each/Total columns, derived
        /// right-to-left off totalRightEdge so header and data rows stay in
        /// lockstep by construction (both are handed the same ColumnEdges
        /// instance for a given render). Total's band width is
        /// max(TotalMinWidth, maxTotalWidth); Each's band width is
        /// max(EachMinWidth, maxEachWidth) - each band plus a ColumnGap is
        /// reserved to its right neighbor's left.
        /// <para>
        /// The source badge used to be glued to the name's right edge, so
        /// its x moved with every row's own name length and no two rows'
        /// badges lined up. It is a column in the right-hand block now:
        /// maxQtyWidth and sourceColumnWidth are BAND widths (the widest
        /// value each column draws this render), never one row's own, so a
        /// short "1x" row cannot let its name run under the widest "429750x"
        /// beside it.
        /// </para>
        /// </summary>
        public static ColumnEdges ComputeEdges(
            int totalRightEdge, int maxEachWidth, int maxTotalWidth,
            int maxQtyWidth = 0, int sourceColumnWidth = 0)
        {
            int eachRightEdge = totalRightEdge - EffectiveTotalWidth(maxTotalWidth) - ColumnGap;
            int qtyRightEdge = eachRightEdge - EffectiveEachWidth(maxEachWidth) - ColumnGap;
            int sourceX = qtyRightEdge - maxQtyWidth - ColumnGap - sourceColumnWidth;

            return new ColumnEdges(totalRightEdge, eachRightEdge, qtyRightEdge, sourceX);
        }

        /// <summary>
        /// Where each of the five header CELLS ends, in the header row's own
        /// left-to-right order (Total closes the band, so four are written).
        /// These are COLUMN edges: "Item" is a 40px word over a name column
        /// hundreds of pixels wide, so a label-derived boundary would sort a
        /// click above the item NAMES by Source.
        /// </summary>
        public static void HeaderCellBoundaries(
            ColumnEdges edges, int sourceColumnWidth, int nameGap, int[] into)
        {
            if (into == null || into.Length < 4)
            {
                return;
            }

            into[0] = edges.SourceX - (nameGap / 2);
            into[1] = edges.SourceX + sourceColumnWidth + (ColumnGap / 2);
            into[2] = edges.QtyRightEdge + (ColumnGap / 2);
            into[3] = edges.EachRightEdge + (ColumnGap / 2);
        }

        private static int EffectiveTotalWidth(int maxTotalWidth)
        {
            return maxTotalWidth > TotalMinWidth ? maxTotalWidth : TotalMinWidth;
        }

        private static int EffectiveEachWidth(int maxEachWidth)
        {
            return maxEachWidth > EachMinWidth ? maxEachWidth : EachMinWidth;
        }

        /// <summary>
        /// <see cref="ComputeEdges"/> from the panel width instead of a
        /// right edge: the Total column's right edge is
        /// PlanRelayoutMath.PinnedRightEdge, so the whole block justifies
        /// to the panel and the Item column absorbs whatever is left. The
        /// single entry point the header row, every data row, and both of
        /// their relayout closures call, so no two of them can anchor the
        /// table differently.
        /// </summary>
        public static ColumnEdges ComputeEdgesForPanel(
            int panelWidth, int maxEachWidth, int maxTotalWidth,
            int maxQtyWidth = 0, int sourceColumnWidth = 0)
        {
            return ComputeEdges(
                PlanRelayoutMath.PinnedRightEdge(panelWidth),
                maxEachWidth, maxTotalWidth, maxQtyWidth, sourceColumnWidth);
        }

        /// <summary>
        /// Total width of a horizontal run of "label, gap, icon, gap"
        /// segments - the same layout convention CraftingPlanView's coin
        /// AND currency segments both use. Callers pass
        /// their own already-measured (Blish-bound BitmapFont.MeasureString)
        /// per-segment text widths plus their own iconSize/labelIconGap/
        /// segmentGap constants, so this arithmetic can never drift from
        /// what the view actually lays out - it does not bake in any of
        /// CraftingPlanView's specific pixel values itself. Empty/null
        /// input is 0 width (no trailing gap to subtract).
        /// </summary>
        public static int SegmentRunWidth(
            IReadOnlyList<int> segmentTextWidths, int iconSize, int labelIconGap, int segmentGap)
        {
            if (segmentTextWidths == null || segmentTextWidths.Count == 0) return 0;

            int width = 0;
            foreach (var textWidth in segmentTextWidths)
            {
                width += textWidth + labelIconGap + iconSize + segmentGap;
            }
            return width - segmentGap;
        }

        /// <summary>
        /// int[] twin of the IReadOnlyList&lt;int&gt; overload above, same
        /// formula. Every per-frame relayout closure (replayed on
        /// every OnPanelResized drag tick) calls this with
        /// SegmentLayoutHandle.TextWidths, which is always a concrete
        /// int[]; without this overload the compiler binds those hot-path
        /// calls to the IReadOnlyList&lt;int&gt; overload instead, and on
        /// .NET Framework foreaching an array through IEnumerable&lt;T&gt;/
        /// IReadOnlyList&lt;T&gt; allocates a heap enumerator per call. This
        /// overload lets the compiler lower the foreach to a plain indexed
        /// loop, so the resize hot path stays allocation-free as documented
        /// on its callers (RepositionValueCellRightAligned, the cost-tile
        /// relayout closure).
        /// </summary>
        public static int SegmentRunWidth(
            int[] segmentTextWidths, int iconSize, int labelIconGap, int segmentGap)
        {
            if (segmentTextWidths == null || segmentTextWidths.Length == 0) return 0;

            int width = 0;
            for (int i = 0; i < segmentTextWidths.Length; i++)
            {
                width += segmentTextWidths[i] + labelIconGap + iconSize + segmentGap;
            }
            return width - segmentGap;
        }
    }
}
