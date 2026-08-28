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

        // Tier 1 of the module's two-tier icon system (owner ruling): the
        // Ranker's rows carry in-game bag-slot-sized item art, like the
        // Snapshot grid and the plan heading.
        public const int IconSize = ItemIconTiers.BagSlotIconSize;
        public const int IconBorder = 1;
        public const int IconTotal = IconSize + 2 * IconBorder;
        public const int IconGap = 8;
        public const int CellGap = 12;
        public const int ButtonGap = 4;

        // 60: the 54px tier-1 icon frame plus 3px of clearance each side.
        public const int RowHeight = 60;

        /// <summary>A text-only sub-line: the gate strip, and a note.</summary>
        public const int SubLineHeight = 20;

        /// <summary>
        /// A currency line carries a wallet-LIST-tier icon (the game's own
        /// 32px), so it cannot sit in a text line's pitch. 4px of clearance,
        /// against the main line's 3.
        /// </summary>
        public const int CurrencyLineHeight = CurrencyIconTiers.WalletListIconSize + 4;

        // RHYTHM. Grouping is carried by the gaps BETWEEN blocks rather than
        // by uniform padding everywhere: the headline, the gate strip and the
        // currency detail are three different kinds of statement, and a
        // reader should see three groups instead of one wall of lines. Inside
        // a block, lines keep their own pitch and take no gap at all.
        public const int GateTopGap = 2;
        public const int CurrencyTopGap = 8;
        public const int NoteTopGap = 8;

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

        /// <summary>
        /// Fits the bold "Ready" header (~50px) and the readiness figure,
        /// which draws one tier above the rest of the row (UiFonts.Status,
        /// 18 bold): "100%" measures wider there than the Body 16 this cell
        /// was first sized for.
        /// </summary>
        public const int ReadyCellWidth = 66;

        /// <summary>Fits bold "Days" (~46px) and body "999d".</summary>
        public const int DaysCellWidth = 54;

        /// <summary>
        /// Floor for the coin cell band, applied inside Compute: fits bold
        /// "Remaining" (~92px). Rows may measure wider; never narrower.
        /// </summary>
        public const int MinRemainingCellWidth = 100;

        /// <summary>The five gate cells of the breakdown sub-line.</summary>
        public const int GateCellCount = 5;

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

            /// <summary>Right edge of the right-aligned days cell.</summary>
            public readonly int DaysRightEdge;

            /// <summary>Right edge handed to CoinCurrencyRenderer's right-aligned value cell.</summary>
            public readonly int RemainingRightEdge;

            /// <summary>Left edge of the move-up button, or -1 when the row has none.</summary>
            public readonly int UpX;

            /// <summary>Left edge of the move-down button, or -1 when the row has none.</summary>
            public readonly int DownX;

            public readonly int RemoveX;

            /// <summary>Left edge of the sub-lines, aligned under the item name.</summary>
            public readonly int SubLineX;

            /// <summary>Width available to a sub-line, out to the row's one right edge.</summary>
            public readonly int SubLineWidth;

            public Bands(
                int rowWidth, int rankX, int iconX, int nameX, int nameWidth,
                int readyRightEdge, int daysRightEdge,
                int remainingRightEdge, int upX, int downX, int removeX,
                int subLineX, int subLineWidth)
            {
                RowWidth = rowWidth;
                RankX = rankX;
                IconX = iconX;
                NameX = nameX;
                NameWidth = nameWidth;
                ReadyRightEdge = readyRightEdge;
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
        /// The affordability chip is NOT a band here: seated between the
        /// Ready and Days rails it broke the header-over-column mapping the
        /// field test flagged, so it now trails the item name inside the
        /// name band (see the view's chip placement).
        /// </summary>
        public static Bands Compute(int rowWidth, int remainingCellWidth, bool showReorder = true)
        {
            rowWidth = Math.Max(0, rowWidth);
            remainingCellWidth = Math.Max(MinRemainingCellWidth, remainingCellWidth);

            int rightEdge = Math.Max(0, rowWidth - Inset);

            // Independent mode has no reorder buttons at all - the order it
            // displays is its own answer, not something to drag. Their rails
            // are not left empty: every band to their left widens into the
            // space, which is what keeps the table justified to the panel
            // rather than stranding 64px of nothing under the header.
            int removeX = rightEdge - ButtonWidth;
            int downX = showReorder ? removeX - ButtonGap - ButtonWidth : -1;
            int upX = showReorder ? downX - ButtonGap - ButtonWidth : -1;

            int remainingRightEdge = (showReorder ? upX : removeX) - CellGap;
            int daysRightEdge = remainingRightEdge - remainingCellWidth - CellGap;
            int readyRightEdge = daysRightEdge - DaysCellWidth - CellGap;

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
                readyRightEdge, daysRightEdge,
                remainingRightEdge, upX, downX, removeX,
                subLineX, subLineWidth);
        }

        /// <summary>
        /// One cell of the gate-breakdown sub-line. The five cells divide the
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

        /// <summary>
        /// Where each block of a row's sub-lines starts, relative to the row
        /// panel's top, and how tall the row ends up. One place, so the row's
        /// HEIGHT and the y its labels are drawn at cannot disagree - they are
        /// the same arithmetic read twice.
        /// <para>
        /// A block with no lines takes no height AND no gap, so a row with
        /// nothing below its headline is exactly RowHeight tall - which is
        /// what compact mode is.
        /// </para>
        /// </summary>
        public readonly struct SubLineBlock
        {
            /// <summary>Y of the gate strip, or -1 when the row has none.</summary>
            public readonly int GateY;

            /// <summary>Y of the first currency line, or -1 when there are none.</summary>
            public readonly int CurrencyY;

            /// <summary>Y of the first note line, or -1 when there are none.</summary>
            public readonly int NoteY;

            /// <summary>Total height of the row, sub-lines included.</summary>
            public readonly int TotalHeight;

            public SubLineBlock(int gateY, int currencyY, int noteY, int totalHeight)
            {
                GateY = gateY;
                CurrencyY = currencyY;
                NoteY = noteY;
                TotalHeight = totalHeight;
            }
        }

        public static SubLineBlock SubLines(bool hasGates, int currencyLines, int noteLines)
        {
            currencyLines = Math.Max(0, currencyLines);
            noteLines = Math.Max(0, noteLines);

            int y = RowHeight;
            int gateY = -1;
            int currencyY = -1;
            int noteY = -1;

            if (hasGates)
            {
                y += GateTopGap;
                gateY = y;
                y += SubLineHeight;
            }

            if (currencyLines > 0)
            {
                y += CurrencyTopGap;
                currencyY = y;
                y += currencyLines * CurrencyLineHeight;
            }

            if (noteLines > 0)
            {
                y += NoteTopGap;
                noteY = y;
                y += noteLines * SubLineHeight;
            }

            // The breath a stack of sub-lines needs so the next row's
            // headline does not sit on this row's last detail line.
            if (y > RowHeight)
            {
                y += GateTopGap;
            }

            return new SubLineBlock(gateY, currencyY, noteY, y);
        }

        /// <summary>Fits the fixed "Refresh" label with clearance; never fed status text.</summary>
        public const int RefreshButtonWidth = 132;

        public readonly struct ToolbarSlots
        {
            /// <summary>Left edge of the right-anchored Refresh button.</summary>
            public readonly int RefreshX;

            /// <summary>Left edge of the compact toggle, seated left of Refresh.</summary>
            public readonly int CompactX;

            /// <summary>Left edge of the status line's band.</summary>
            public readonly int StatusX;

            /// <summary>
            /// Width the status label may fill. Text longer than this is
            /// ellipsized by the view, never allowed to run under the button.
            /// </summary>
            public readonly int StatusWidth;

            public ToolbarSlots(int refreshX, int compactX, int statusX, int statusWidth)
            {
                RefreshX = refreshX;
                CompactX = compactX;
                StatusX = statusX;
                StatusWidth = statusWidth;
            }
        }

        /// <summary>
        /// The toolbar row: one full-width status band on the left, the
        /// Refresh button pinned right, the inline spinner between them.
        /// The refresh-progress text renders in the status band and ONLY
        /// there - the field test showed status-length text stamped onto
        /// the fixed-width button spilling past its edges.
        /// </summary>
        public static ToolbarSlots Toolbar(
            int barWidth, int spinnerSize, int labelGap, int compactWidth = 0)
        {
            int refreshX = Math.Max(0, barWidth - RefreshButtonWidth);
            int compactX = compactWidth <= 0
                ? refreshX
                : Math.Max(Inset, refreshX - CellGap - compactWidth);
            int statusRight = compactX - spinnerSize - 2 * labelGap;
            return new ToolbarSlots(refreshX, compactX, Inset, Math.Max(0, statusRight - Inset));
        }

        public readonly struct ModeStripSlots
        {
            /// <summary>Left edge of the "Compare:" caption, or -1 when it does not fit.</summary>
            public readonly int LabelX;

            /// <summary>Left edge of the first option's indicator.</summary>
            public readonly int FirstX;

            /// <summary>Left edge of the second option's indicator.</summary>
            public readonly int SecondX;

            public ModeStripSlots(int labelX, int firstX, int secondX)
            {
                LabelX = labelX;
                FirstX = firstX;
                SecondX = secondX;
            }
        }

        /// <summary>
        /// The right-anchored comparison-mode strip: caption, then the two
        /// options in reading order, laid out from the right edge so the
        /// last one ends where the table does.
        /// <para>
        /// The caption is the only droppable part. At a width where it
        /// would run under whatever sits to its left (<paramref name="minX"/>
        /// is that control's right edge) it is dropped rather than
        /// overlapped - the two options are self-describing, the word
        /// "Compare:" is not load-bearing, and BOTH options staying legible
        /// is the whole point of the control.
        /// </para>
        /// </summary>
        public static ModeStripSlots ModeStrip(
            int barWidth, int labelWidth, int firstWidth, int secondWidth, int gap, int minX)
        {
            barWidth = Math.Max(0, barWidth);
            minX = Math.Max(0, minX);

            int secondX = Math.Max(minX, barWidth - Inset - secondWidth);
            int firstX = Math.Max(minX, secondX - gap - firstWidth);
            int labelX = firstX - gap - labelWidth;

            return new ModeStripSlots(labelX < minX ? -1 : labelX, firstX, secondX);
        }

        /// <summary>
        /// Currency shortfalls deliberately do NOT share the gate strip's
        /// rails any more. On the shared grid an entry rendered directly
        /// under whichever gate column its index landed on ("Ascalonian
        /// Tear" under Materials, an essence under Disciplines), which the
        /// field test read as children of unrelated gates. Their own
        /// indented, icon-led grid makes them parse as one currency list
        /// owned by the row.
        /// </summary>
        public const int CurrenciesPerLine = 3;
        public const int MaxCurrencyLines = 3;

        /// <summary>Indent of the currency grid under the sub-line band.</summary>
        public const int CurrencyIndent = 16;

        /// <summary>
        /// The breakdown's currency entries draw at the game's wallet LIST
        /// tier (owner ruling, 2026-08-27: "I would like to try getting away
        /// with the larger icons"), which is why a currency line has a pitch
        /// of its own rather than sharing the text sub-line's 20px. The
        /// Remaining cell's inline gold/silver/copper run stays on the BAR
        /// tier: that one really is an inline coin run inside a sentence,
        /// which is what the bar tier is for.
        /// </summary>
        public const int CurrencyIconSize = CurrencyIconTiers.WalletListIconSize;

        /// <summary>Gap between a currency icon's frame and its name.</summary>
        public const int CurrencyIconGap = 6;

        /// <summary>
        /// One cell of the currency grid: CurrenciesPerLine equal cells
        /// across the indented band, remainder to the last cell, same
        /// integer-exact rule as GateCell.
        /// </summary>
        public static void CurrencyCell(in Bands bands, int index, out int x, out int width)
        {
            int bandX = bands.SubLineX + CurrencyIndent;
            int bandWidth = Math.Max(0, bands.SubLineWidth - CurrencyIndent);
            if (index < 0 || index >= CurrenciesPerLine || bandWidth <= 0)
            {
                x = bandX;
                width = 0;
                return;
            }

            int left = bandX + (int)((long)bandWidth * index / CurrenciesPerLine);
            int right = bandX + (int)((long)bandWidth * (index + 1) / CurrenciesPerLine);
            x = left;
            width = Math.Max(0, right - left);
        }

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
