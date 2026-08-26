using System;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Pure column arithmetic for one Plan History row, in the shape of
    /// RankerRowLayout / LogRowLayout.
    ///
    /// Same law as every other table in the module: the name column is
    /// the only flexing element, it consumes every pixel the pinned
    /// right-hand block does not, and at no width is there empty space to
    /// the right of the action buttons.
    /// </summary>
    internal static class PlanHistoryRowLayout
    {
        public const int Inset = 16;
        public const int IconSize = 32;
        public const int IconBorder = 1;
        public const int IconTotal = IconSize + 2 * IconBorder;
        public const int IconGap = 8;
        public const int CellGap = 20;
        public const int ButtonGap = 8;
        public const int RowHeight = 44;
        public const int ActionButtonWidth = 84;
        public const int IconButtonWidth = 28;

        /// <summary>Below this the pinned block cannot fit and the name band collapses to zero.</summary>
        public const int MinNameWidth = 40;

        /// <summary>
        /// Floor for the coin cell band: fits bold "Cost" under
        /// TableHeaderStyle's ColumnHeader font with clearance. Rows may
        /// measure wider; never narrower.
        /// </summary>
        public const int MinCostCellWidth = 60;

        /// <summary>
        /// Floor for the timestamp band: fits bold "Generated" (~92px at
        /// the ColumnHeader font). The live band is the measured widest
        /// StatusText.TimestampFormat stamp, which is wider still.
        /// </summary>
        public const int MinWhenWidth = 100;

        // Detail-panel line heights - see DetailHeight.
        public const int DetailItemLineHeight = 24;
        public const int DetailSettingsLineHeight = 24;
        public const int DetailChipsLineHeight = 26;
        public const int DetailCaptionLineHeight = 22;
        public const int DetailNoteLineHeight = 22;
        public const int DetailPadding = 12;

        public readonly struct Bands
        {
            public readonly int RowWidth;
            public readonly int IconX;
            public readonly int NameX;
            public readonly int NameWidth;

            /// <summary>Right edge handed to CoinCurrencyRenderer's right-aligned value cell.</summary>
            public readonly int CostRightEdge;

            public readonly int WhenX;
            public readonly int WhenWidth;

            public readonly int ViewX;
            public readonly int OpenX;
            public readonly int ResolveX;
            public readonly int PinX;
            public readonly int DeleteX;

            public Bands(
                int rowWidth, int iconX, int nameX, int nameWidth,
                int costRightEdge, int whenX, int whenWidth,
                int viewX, int openX, int resolveX, int pinX, int deleteX)
            {
                RowWidth = rowWidth;
                IconX = iconX;
                NameX = nameX;
                NameWidth = nameWidth;
                CostRightEdge = costRightEdge;
                WhenX = whenX;
                WhenWidth = whenWidth;
                ViewX = viewX;
                OpenX = openX;
                ResolveX = resolveX;
                PinX = pinX;
                DeleteX = deleteX;
            }
        }

        /// <summary>
        /// rowWidth is the SCROLLING panel's width minus
        /// WindowSizing.ScrollbarAllowance - never the container's width.
        /// costWidth comes from CoinCurrencyRenderer.MeasureValueWidth
        /// (table-wide max); whenWidth is the measured widest timestamp
        /// stamp. Both are floored so an empty table cannot collapse a
        /// band narrower than its own header label.
        /// </summary>
        public static Bands Compute(int rowWidth, int costWidth, int whenWidth)
        {
            rowWidth = Math.Max(0, rowWidth);
            costWidth = Math.Max(MinCostCellWidth, costWidth);
            whenWidth = Math.Max(MinWhenWidth, whenWidth);

            int rightEdge = Math.Max(0, rowWidth - Inset);

            int deleteX = rightEdge - IconButtonWidth;
            int pinX = deleteX - ButtonGap - IconButtonWidth;
            int resolveX = pinX - ButtonGap - ActionButtonWidth;
            int openX = resolveX - ButtonGap - ActionButtonWidth;
            int viewX = openX - ButtonGap - ActionButtonWidth;

            int whenX = viewX - CellGap - whenWidth;
            int costRightEdge = whenX - CellGap;

            int iconX = Inset;
            int nameX = iconX + IconTotal + IconGap;
            int nameWidth = costRightEdge - costWidth - CellGap - nameX;

            // A window narrow enough to squeeze the name out clamps
            // rather than emitting a negative width the view would hand
            // to a measure call.
            if (nameWidth < MinNameWidth)
            {
                nameWidth = Math.Max(0, Math.Min(MinNameWidth, rightEdge - nameX));
            }

            return new Bands(
                rowWidth, iconX, nameX, nameWidth,
                costRightEdge, whenX, whenWidth,
                viewX, openX, resolveX, pinX, deleteX);
        }

        /// <summary>
        /// Height of the expanded detail panel: one line per item summary,
        /// the settings line, the chip strip (only when either count is
        /// non-zero), the Generated caption (always), and up to three
        /// optional note/sample lines, plus padding. Computed, not
        /// guessed, so the FlowPanel reflow is exact.
        /// </summary>
        public static int DetailHeight(
            int itemCount, bool hasChips, bool hasSampleLine, bool hasBlobNote, bool hasOverridesNote)
        {
            return Math.Max(0, itemCount) * DetailItemLineHeight
                + DetailSettingsLineHeight
                + (hasChips ? DetailChipsLineHeight : 0)
                + DetailCaptionLineHeight
                + (hasSampleLine ? DetailNoteLineHeight : 0)
                + (hasBlobNote ? DetailNoteLineHeight : 0)
                + (hasOverridesNote ? DetailNoteLineHeight : 0)
                + DetailPadding;
        }
    }
}
