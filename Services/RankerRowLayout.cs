using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure column arithmetic for one Crafting Ranker row, in the shape of
    /// LogRowLayout / ShoppingColumnMath.
    ///
    /// Same law as every other table in the module: the name column is the
    /// only flexing element, it consumes every pixel the pinned right-hand
    /// block does not, and at no width is there empty space to the right of
    /// the action buttons.
    /// </summary>
    public static class RankerRowLayout
    {
        public const int Inset = 16;
        public const int IconSize = 32;
        public const int IconBorder = 1;
        public const int IconTotal = IconSize + 2 * IconBorder;
        public const int IconGap = 8;
        public const int CellGap = 12;
        public const int ButtonGap = 4;
        public const int RowHeight = 44;
        public const int SubLineHeight = 20;
        public const int ButtonWidth = 28;

        /// <summary>Room for "25." at UiFonts.Caption plus clearance.</summary>
        public const int RankWidth = 26;

        // The three right-hand cells each reserve enough width for BOTH
        // their widest cell text ("100%", "999d", a coin amount) and their
        // own column-header label at TableHeaderStyle's bold ColumnHeader
        // font. The header labels right-align at the same edges the cells
        // do, so a band narrower than its header collides the headers into
        // each other - the live desktop gate caught exactly that
        // ("ReadhyDaining") when an empty table let the coin band collapse
        // to the width of a dash.

        /// <summary>Fits bold "Ready" (~50px) and body "100%".</summary>
        public const int ReadyCellWidth = 58;

        /// <summary>Fits bold "Days" (~46px) and body "999d".</summary>
        public const int DaysCellWidth = 54;

        /// <summary>
        /// Floor for the coin cell band, applied inside Compute: fits bold
        /// "Remaining" (~92px). Rows may measure wider; never narrower.
        /// </summary>
        public const int MinRemainingCellWidth = 100;

        /// <summary>The four gate cells of the breakdown sub-line.</summary>
        public const int GateCellCount = 4;

        /// <summary>Below this the pinned block cannot fit and the name band collapses to zero.</summary>
        public const int MinNameWidth = 40;

        public readonly struct Bands
        {
            public readonly int RowWidth;
            public readonly int RankX;
            public readonly int IconX;
            public readonly int NameX;
            public readonly int NameWidth;

            /// <summary>Right edge of the right-aligned readiness percentage.</summary>
            public readonly int ReadyRightEdge;

            public readonly int ChipX;
            public readonly int ChipWidth;

            /// <summary>Right edge of the right-aligned days cell.</summary>
            public readonly int DaysRightEdge;

            /// <summary>Right edge handed to CoinCurrencyRenderer's right-aligned value cell.</summary>
            public readonly int RemainingRightEdge;

            public readonly int UpX;
            public readonly int DownX;
            public readonly int RemoveX;

            /// <summary>Left edge of the sub-lines, aligned under the item name.</summary>
            public readonly int SubLineX;

            /// <summary>Width available to a sub-line, out to the row's one right edge.</summary>
            public readonly int SubLineWidth;

            public Bands(
                int rowWidth, int rankX, int iconX, int nameX, int nameWidth,
                int readyRightEdge, int chipX, int chipWidth, int daysRightEdge,
                int remainingRightEdge, int upX, int downX, int removeX,
                int subLineX, int subLineWidth)
            {
                RowWidth = rowWidth;
                RankX = rankX;
                IconX = iconX;
                NameX = nameX;
                NameWidth = nameWidth;
                ReadyRightEdge = readyRightEdge;
                ChipX = chipX;
                ChipWidth = chipWidth;
                DaysRightEdge = daysRightEdge;
                RemainingRightEdge = remainingRightEdge;
                UpX = upX;
                DownX = downX;
                RemoveX = removeX;
                SubLineX = subLineX;
                SubLineWidth = subLineWidth;
            }
        }

        /// <summary>
        /// rowWidth is the SCROLLING panel's width minus
        /// WindowSizing.ScrollbarAllowance, never the container's width.
        /// chipWidth of 0 removes the chip and its gap entirely.
        /// </summary>
        public static Bands Compute(int rowWidth, int remainingCellWidth, int chipWidth)
        {
            rowWidth = Math.Max(0, rowWidth);
            remainingCellWidth = Math.Max(MinRemainingCellWidth, remainingCellWidth);
            chipWidth = Math.Max(0, chipWidth);

            int rightEdge = Math.Max(0, rowWidth - Inset);

            int removeX = rightEdge - ButtonWidth;
            int downX = removeX - ButtonGap - ButtonWidth;
            int upX = downX - ButtonGap - ButtonWidth;

            int remainingRightEdge = upX - CellGap;
            int daysRightEdge = remainingRightEdge - remainingCellWidth - CellGap;

            int chipRightEdge = daysRightEdge - DaysCellWidth - CellGap;
            int chipX = chipWidth > 0 ? chipRightEdge - chipWidth : chipRightEdge;
            int readyRightEdge = chipWidth > 0 ? chipX - CellGap : chipRightEdge;

            int rankX = Inset;
            int iconX = rankX + RankWidth;
            int nameX = iconX + IconTotal + IconGap;

            // The name band ends at the Ready CELL's left edge, not at the
            // Ready text's right edge - the right-aligned "100%" extends
            // ReadyCellWidth's worth of pixels left of readyRightEdge, and a
            // name allowed to run under it would collide.
            int nameWidth = readyRightEdge - ReadyCellWidth - CellGap - nameX;

            // A window narrow enough to squeeze the name out clamps rather
            // than emitting a negative width the view would hand to a
            // measure call.
            if (nameWidth < MinNameWidth)
            {
                nameWidth = Math.Max(0, Math.Min(MinNameWidth, rightEdge - nameX));
            }

            int subLineX = nameX;
            int subLineWidth = Math.Max(0, rightEdge - subLineX);

            return new Bands(
                rowWidth, rankX, iconX, nameX, nameWidth,
                readyRightEdge, chipX, chipWidth, daysRightEdge,
                remainingRightEdge, upX, downX, removeX,
                subLineX, subLineWidth);
        }

        /// <summary>
        /// One cell of the gate-breakdown sub-line. The four cells divide the
        /// sub-line's full width evenly so the strip is justified to the panel
        /// rather than left-packed with dead space on the right.
        /// </summary>
        public static void GateCell(in Bands bands, int index, out int x, out int width)
        {
            if (index < 0 || index >= GateCellCount || bands.SubLineWidth <= 0)
            {
                x = bands.SubLineX;
                width = 0;
                return;
            }

            // Integer-exact edges: the last cell absorbs the remainder rather
            // than leaving a rounding gap at the right edge.
            int left = bands.SubLineX + (int)((long)bands.SubLineWidth * index / GateCellCount);
            int right = bands.SubLineX + (int)((long)bands.SubLineWidth * (index + 1) / GateCellCount);
            x = left;
            width = Math.Max(0, right - left);
        }

        /// <summary>Total height of a row carrying <paramref name="subLineCount"/> sub-lines.</summary>
        public static int TotalRowHeight(int subLineCount)
        {
            return RowHeight + Math.Max(0, subLineCount) * SubLineHeight;
        }

        /// <summary>
        /// Currency shortfalls sit on the SAME four-column grid as the gate
        /// breakdown strip (GateCell), one currency per cell, so every value
        /// in a row's sub-lines shares one set of vertical rails - the live
        /// desktop gate showed that a second, different grid under the gate
        /// strip reads as each value finding its own x.
        /// </summary>
        public const int CurrenciesPerLine = GateCellCount;
        public const int MaxCurrencyLines = 2;

        /// <summary>
        /// How many currency shortfall sub-lines a row needs. Deliberately
        /// independent of width: a width-dependent count would change a
        /// row's HEIGHT mid-drag.
        /// </summary>
        public static int CurrencyLineCount(int currencyCount)
        {
            if (currencyCount <= 0)
            {
                return 0;
            }

            int shown = Math.Min(currencyCount, CurrenciesPerLine * MaxCurrencyLines);
            return (shown + CurrenciesPerLine - 1) / CurrenciesPerLine;
        }
    }
}
