using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Splits a column-header band into one CELL per header label, so a
    /// sortable header's hover, tooltip and click cover the whole column
    /// rather than the pixels its text occupies. A partition, not padded
    /// boxes: every pixel belongs to exactly one cell, so no click lands
    /// in a dead strip between two columns.
    /// </summary>
    public static class HeaderCellMath
    {
        /// <summary>
        /// Where one header label sits inside its band, and - when the
        /// caller knows it - where its COLUMN ends. The label-gap midpoint
        /// is a fallback, not the truth: a header's text is far narrower
        /// than the column it names, so a midpoint between two words puts
        /// the boundary well inside the left column ("Item" over a name
        /// column hundreds of pixels wide).
        /// </summary>
        public readonly struct LabelExtent
        {
            /// <summary>No explicit column edge - derive one from the gap.</summary>
            public const int NoBoundary = int.MinValue;

            public readonly int X;
            public readonly int Width;
            public readonly int CellEnd;

            public LabelExtent(int x, int width)
                : this(x, width, NoBoundary)
            {
            }

            public LabelExtent(int x, int width, int cellEnd)
            {
                X = x;
                Width = width;
                CellEnd = cellEnd;
            }

            public int Right => X + (Width > 0 ? Width : 0);

            public bool HasBoundary => CellEnd != NoBoundary;
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
        /// One range per label, left to right. The first starts at 0, the
        /// last ends at <paramref name="bandWidth"/>, and boundaries are
        /// forced non-decreasing and clamped into the band - so overlapping
        /// or out-of-order labels shrink a cell rather than inverting it.
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
        /// The same split, written into a buffer the caller owns: the
        /// plan's header renderers re-split on every frame of a resize
        /// drag, which is not a path to allocate an array per header on.
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
                else if (labels[i].HasBoundary)
                {
                    end = labels[i].CellEnd;
                }
                else
                {
                    int gapStart = labels[i].Right;
                    int gapEnd = labels[i + 1].X;
                    end = gapEnd > gapStart ? gapStart + ((gapEnd - gapStart) / 2) : gapEnd;
                }

                if (end < start)
                {
                    end = start;
                }

                if (end > band)
                {
                    end = band;
                }

                if (start > band)
                {
                    start = band;
                }

                into[i] = new CellRange(start, end - start);
                start = end;
            }
        }
    }
}
