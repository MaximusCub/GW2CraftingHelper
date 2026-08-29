using System.Collections.Generic;

namespace TaimisToolbench.Services
{
    /// <summary>
    /// Pure column-edge arithmetic (Blish-free, unit-testable) for the
    /// shopping list's Source/Amount/Each/Total table columns. Each column
    /// reserves a band; a right-aligned column's values grow leftward
    /// inside it. Those band widths are derived per-render from the widest
    /// actual string in each column (measured in the view via
    /// BitmapFont.MeasureString, which is Blish-bound and therefore not
    /// tested here), clamped to fixed minimums so short/low-value lists
    /// don't look cramped. See ShoppingListSectionRenderer.Render for
    /// the pre-scan that produces them.
    ///
    /// <para>
    /// The bands are DISTRIBUTED over equal tracks
    /// (<see cref="TrackCount"/>) rather than packed against the panel's
    /// right edge, which is what stops a short item name being stranded far
    /// left with the middle of the row empty. Below the width that supports
    /// distribution they pack right-to-left as they always did.
    /// </para>
    ///
    /// <para>
    /// Cells keep their own rule inside their band - badges left, numbers
    /// and coin runs right - and each HEADER centres over that band
    /// (<see cref="HeaderX"/>) rather than sharing an edge with it. See
    /// <see cref="JustifiedColumnTracks"/> for why a shared edge is not
    /// enough. The Item column is the exception: it flexes, and a flexing
    /// column's header stays on the left rule its names keep.
    /// </para>
    /// </summary>
    internal static class ShoppingColumnMath
    {
        public const int TotalMinWidth = 150;
        public const int EachMinWidth = 110;
        public const int ColumnGap = 20;

        /// <summary>Left x of the row's item icon.</summary>
        public const int IconX = 8;

        /// <summary>
        /// Left x of the item name column, past the row's tier-2 icon
        /// frame plus a gap - and the left end of the track span, which is
        /// why it lives here rather than in the renderer that draws it.
        /// </summary>
        public const int NameX = IconX + PlanContentHeightMath.RowIconFrameSize + 8;

        /// <summary>
        /// Columns the row is divided into between the item name's left
        /// edge and the Total column's pinned right edge: the name takes
        /// <see cref="NameTrackSpan"/> of them, then Source, Amount, Each
        /// and Total take one each.
        /// <para>
        /// RankerRowLayout's shape, for the reason the field report gave
        /// for asking: the four data columns huddled against the panel's
        /// right edge, so a short item name was stranded far left with the
        /// whole middle of the row empty between it and the first datum.
        /// The name spans two tracks because it is the row's subject and
        /// the one column that must not ellipsize at ordinary widths.
        /// </para>
        /// <para>
        /// COUPLED to <see cref="NameTrackSpan"/> and
        /// <see cref="DataColumnCount"/>: the four data columns are read
        /// off tracks NameTrackSpan..TrackCount, so TrackCount has to stay
        /// their sum.
        /// </para>
        /// </summary>
        public const int TrackCount = 6;

        /// <summary>Tracks the item name spans; see <see cref="TrackCount"/>.</summary>
        public const int NameTrackSpan = 2;

        /// <summary>
        /// Source, Amount, Each, Total - one track each. Only the first
        /// three CENTRE on theirs; Total right-aligns on its track's right
        /// edge, which is the panel's own pinned edge.
        /// </summary>
        public const int DataColumnCount = 4;

        public readonly struct ColumnEdges
        {
            public readonly int TotalRightEdge;
            public readonly int EachRightEdge;
            public readonly int QtyRightEdge;

            /// <summary>
            /// LEFT edge of the source-badge column. Left, not right:
            /// badges are words of different lengths, and a column of them
            /// reads as a column because their left edges rule - the same
            /// choice Required Recipes' Discipline column makes.
            /// </summary>
            public readonly int SourceX;

            /// <summary>
            /// Each column's reserved BAND width - what its header has to
            /// centre over (<see cref="HeaderX"/>). Carried on the edges
            /// rather than re-derived by the caller: the header row and the
            /// data rows both read them off one instance, so neither can
            /// centre over a band the other did not reserve.
            /// </summary>
            public readonly int SourceBandWidth;
            public readonly int QtyBandWidth;
            public readonly int EachBandWidth;
            public readonly int TotalBandWidth;

            public ColumnEdges(
                int totalRightEdge, int eachRightEdge, int qtyRightEdge, int sourceX,
                int sourceBandWidth, int qtyBandWidth, int eachBandWidth, int totalBandWidth,
                bool distributed, int trackSpan)
            {
                TotalRightEdge = totalRightEdge;
                EachRightEdge = eachRightEdge;
                QtyRightEdge = qtyRightEdge;
                SourceX = sourceX;
                SourceBandWidth = sourceBandWidth;
                QtyBandWidth = qtyBandWidth;
                EachBandWidth = eachBandWidth;
                TotalBandWidth = totalBandWidth;
                Distributed = distributed;
                TrackSpan = trackSpan;
            }

            /// <summary>
            /// Whether the four data columns are DISTRIBUTED over equal
            /// tracks or packed right-to-left off the pinned edge. False is
            /// the narrow-panel fallback - see <see cref="ComputeEdges"/>.
            /// </summary>
            public readonly bool Distributed;

            /// <summary>The distributed span, from <see cref="NameX"/> to
            /// the Total column's pinned right edge; 0 when packed.</summary>
            public readonly int TrackSpan;

            /// <summary>Left edge of the band each right-aligned column's
            /// cells grow leftward into.</summary>
            public int QtyBandX => QtyRightEdge - QtyBandWidth;

            public int EachBandX => EachRightEdge - EachBandWidth;

            public int TotalBandX => TotalRightEdge - TotalBandWidth;
        }

        /// <summary>
        /// Left edge of a column header centred over the band its own cells
        /// occupy - the module's centred column law, see
        /// <see cref="JustifiedColumnTracks"/>. Every band here is floored
        /// at its own header (the Source and Amount bands explicitly, in
        /// ShoppingListSectionRenderer's pre-scan; Each and Total by their
        /// fixed minimums, both far wider than the words over them), so a
        /// header always fits the band it centres in.
        /// </summary>
        public static int HeaderX(int bandX, int bandWidth, int headerWidth)
        {
            return JustifiedColumnTracks.CenteredInBand(bandX, bandWidth, headerWidth);
        }

        /// <summary>
        /// Every edge of one render of the table, in whichever of the two
        /// regimes the width supports: the four data columns DISTRIBUTED
        /// over equal tracks, or - on a panel too narrow for that - packed
        /// right-to-left off totalRightEdge as they always were. Header and
        /// data rows stay in lockstep by construction either way, because
        /// both are handed the same ColumnEdges instance for a given
        /// render. Total's band width is max(TotalMinWidth, maxTotalWidth);
        /// Each's band width is max(EachMinWidth, maxEachWidth).
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
            int totalBand = EffectiveTotalWidth(maxTotalWidth);
            int eachBand = EffectiveEachWidth(maxEachWidth);

            // A track has to hold the widest band any of the four data
            // columns reserves, plus the gap that keeps it off its
            // neighbour. Below that there is nothing to distribute and the
            // table falls back to the packed right-to-left stack, which
            // fits in less: on a narrow panel a legible cramped table beats
            // an evenly spaced illegible one. Same trade, same test, as
            // RankerRowLayout.Compute and SummarySectionLayoutMath.
            int trackSpan = totalRightEdge - NameX;
            int widestBand = Max(Max(sourceColumnWidth, maxQtyWidth), Max(eachBand, totalBand));
            if (JustifiedColumnTracks.FitsDistributed(trackSpan, TrackCount, widestBand, ColumnGap))
            {
                // Total keeps totalRightEdge, which by the span's own
                // construction IS its track's right edge: it is the band
                // that genuinely pins to the panel (see
                // JustifiedColumnTracks), and a table that stopped half a
                // track short of its own margin would strand the space
                // distribution exists to spend.
                return new ColumnEdges(
                    totalRightEdge,
                    TrackBandX(trackSpan, 2, eachBand) + eachBand,
                    TrackBandX(trackSpan, 1, maxQtyWidth) + maxQtyWidth,
                    TrackBandX(trackSpan, 0, sourceColumnWidth),
                    sourceColumnWidth, maxQtyWidth, eachBand, totalBand,
                    true, trackSpan);
            }

            int eachRightEdge = totalRightEdge - totalBand - ColumnGap;
            int qtyRightEdge = eachRightEdge - eachBand - ColumnGap;
            int packedSourceX = qtyRightEdge - maxQtyWidth - ColumnGap - sourceColumnWidth;

            return new ColumnEdges(
                totalRightEdge, eachRightEdge, qtyRightEdge, packedSourceX,
                sourceColumnWidth, maxQtyWidth, eachBand, totalBand,
                false, 0);
        }

        /// <summary>
        /// Left edge of data column <paramref name="dataIndex"/>'s band,
        /// centred on the track it owns - the module's shared distribution
        /// law, see <see cref="JustifiedColumnTracks"/>. Data column 0 is
        /// Source, which sits on the first track past the name's
        /// <see cref="NameTrackSpan"/>.
        /// </summary>
        private static int TrackBandX(int trackSpan, int dataIndex, int bandWidth)
        {
            return JustifiedColumnTracks.CenteredX(
                NameX, trackSpan, TrackCount, NameTrackSpan + dataIndex, bandWidth);
        }

        /// <summary>
        /// Left edge of the track data column <paramref name="dataIndex"/>
        /// owns - where the column before it stops, and so the boundary
        /// between their two header cells.
        /// </summary>
        private static int TrackX(int trackSpan, int dataIndex)
        {
            return JustifiedColumnTracks.LeftEdge(
                NameX, trackSpan, TrackCount, NameTrackSpan + dataIndex);
        }

        private static int Max(int a, int b)
        {
            return a > b ? a : b;
        }

        /// <summary>
        /// Where each of the five header CELLS ends, in the header row's own
        /// left-to-right order (Total closes the band, so four are written).
        /// These are COLUMN edges: "Item" is a 40px word over a name column
        /// hundreds of pixels wide, so a label-derived boundary would sort a
        /// click above the item NAMES by Source.
        /// <para>
        /// nameGap is the Item column's own gap before the next column, and
        /// is used only by the packed fallback; distributed, the cells are
        /// the tracks themselves.
        /// </para>
        /// </summary>
        public static void HeaderCellBoundaries(ColumnEdges edges, int nameGap, int[] into)
        {
            if (into == null || into.Length < 4)
            {
                return;
            }

            if (edges.Distributed)
            {
                // Each cell is its column's whole TRACK, which is already a
                // partition of the row: no gap to split, and no cell that
                // stops short of the column beside it.
                into[0] = TrackX(edges.TrackSpan, 0);
                into[1] = TrackX(edges.TrackSpan, 1);
                into[2] = TrackX(edges.TrackSpan, 2);
                into[3] = TrackX(edges.TrackSpan, 3);
                return;
            }

            into[0] = edges.SourceX - (nameGap / 2);
            into[1] = edges.SourceX + edges.SourceBandWidth + (ColumnGap / 2);
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
        /// PlanRelayoutMath.PinnedRightEdge, so the table justifies to the
        /// panel at every width. The single entry point the header row,
        /// every data row, and both of their relayout closures call, so no
        /// two of them can anchor the table differently.
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
            if (segmentTextWidths == null || segmentTextWidths.Count == 0)
            {
                return 0;
            }

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
            if (segmentTextWidths == null || segmentTextWidths.Length == 0)
            {
                return 0;
            }

            int width = 0;
            for (int i = 0; i < segmentTextWidths.Length; i++)
            {
                width += segmentTextWidths[i] + labelIconGap + iconSize + segmentGap;
            }

            return width - segmentGap;
        }
    }
}
