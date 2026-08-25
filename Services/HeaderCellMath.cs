using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Splits a column-header band into one CELL per header label, so a
    /// sortable header's hover, tooltip and click cover the whole column
    /// they belong to rather than the handful of pixels its text happens to
    /// occupy. Blish-free and tested: the degenerate cases this has to get
    /// right - two labels that touch, a right-aligned label that has slid
    /// left of the one before it at a narrow width - are invisible on a
    /// screenshot and would only show up as a header that answers clicks
    /// meant for its neighbour.
    /// <para>
    /// The split is a partition, not a set of padded boxes: every pixel of
    /// the band belongs to exactly one cell, so there is no dead strip
    /// between two columns where a click lands on nothing. A boundary sits
    /// midway between one label's right edge and the next label's left
    /// edge, which is the point a reader would put it.
    /// </para>
    /// </summary>
    public static class HeaderCellMath
    {
        /// <summary>Where one header label sits inside its band.</summary>
        public readonly struct LabelExtent
        {
            public readonly int X;
            public readonly int Width;

            public LabelExtent(int x, int width)
            {
                X = x;
                Width = width;
            }

            public int Right => X + (Width > 0 ? Width : 0);
        }

        /// <summary>One cell's horizontal span inside the band.</summary>
        public readonly struct CellRange
        {
            public readonly int X;
            public readonly int Width;

            public CellRange(int x, int width)
            {
                X = x;
                Width = width;
            }
        }

        /// <summary>
        /// One range per label, in the order given (which is left to right
        /// at every width a caller actually renders). The first starts at
        /// 0 and the last ends at <paramref name="bandWidth"/>.
        /// <para>
        /// Boundaries are forced non-decreasing and clamped into the band,
        /// so labels that overlap - or arrive out of order because a
        /// right-aligned one has slid past its neighbour in a very narrow
        /// window - yield zero-width cells rather than negative ones that
        /// would swallow the band.
        /// </para>
        /// </summary>
        public static IReadOnlyList<CellRange> Partition(
            int bandWidth, IReadOnlyList<LabelExtent> labels)
        {
            if (labels == null || labels.Count == 0)
            {
                return new CellRange[0];
            }

            int band = bandWidth > 0 ? bandWidth : 0;
            var ranges = new CellRange[labels.Count];

            int start = 0;
            for (int i = 0; i < labels.Count; i++)
            {
                int end;
                if (i == labels.Count - 1)
                {
                    end = band;
                }
                else
                {
                    int gapStart = labels[i].Right;
                    int gapEnd = labels[i + 1].X;
                    end = gapEnd > gapStart ? gapStart + ((gapEnd - gapStart) / 2) : gapEnd;
                }

                if (end < start) end = start;
                if (end > band) end = band;
                if (start > band) start = band;

                ranges[i] = new CellRange(start, end - start);
                start = end;
            }

            return ranges;
        }
    }
}
