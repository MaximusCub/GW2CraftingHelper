using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Splits a column-header band into one CELL per header label, so a
    /// sortable header's hover, tooltip and click cover the whole column
    /// rather than the pixels its text happens to occupy.
    /// <para>
    /// A partition, not a set of padded boxes: every pixel of the band
    /// belongs to exactly one cell, so no click lands in a dead strip
    /// between two columns. A boundary sits midway between one label's
    /// right edge and the next label's left edge.
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
        /// One range per label, in the order given (left to right at every
        /// width a caller renders). The first starts at 0, the last ends at
        /// <paramref name="bandWidth"/>, and boundaries are forced
        /// non-decreasing and clamped into the band - so labels that
        /// overlap, or arrive out of order because a right-aligned one has
        /// slid past its neighbour in a narrow window, shrink a cell rather
        /// than inverting it.
        /// </summary>
        public static IReadOnlyList<CellRange> Partition(
            int bandWidth, IReadOnlyList<LabelExtent> labels)
        {
            if (labels == null || labels.Count == 0)
            {
                return new CellRange[0];
            }

            var ranges = new CellRange[labels.Count];
            Partition(bandWidth, labels, ranges);
            return ranges;
        }

        /// <summary>
        /// The same split, written into a buffer the caller owns. The
        /// header renderers re-split on every frame of a resize drag - a
        /// right-pinned column's x is a function of the panel width - and
        /// that is not a path to allocate an array per header per frame on.
        /// </summary>
        public static void Partition(
            int bandWidth, IReadOnlyList<LabelExtent> labels, CellRange[] into)
        {
            if (labels == null || into == null)
            {
                return;
            }

            int band = bandWidth > 0 ? bandWidth : 0;
            int count = labels.Count < into.Length ? labels.Count : into.Length;

            int start = 0;
            for (int i = 0; i < count; i++)
            {
                int end;
                if (i == count - 1)
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

                into[i] = new CellRange(start, end - start);
                start = end;
            }
        }
    }
}
