namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Horizontal geometry of ONE settings row inside one column of the
    /// Settings tab's section board (Blish-free, unit-testable).
    ///
    /// <para>
    /// The tab's real structure is a TABLE of settings, not a form: the
    /// setting's NAME flexes and ellipsizes, its control cluster pins to
    /// the column's right edge, so every input in a column lands on one
    /// vertical line at every width. That is
    /// <see cref="PlanRelayoutMath.PinnedRightEdge"/>'s rule applied to a
    /// form - see that method for the invariant itself; it is not restated
    /// here.
    /// </para>
    /// </summary>
    public static class SettingsFormLayout
    {
        /// <summary>Left inset inside every board column - the same 16 the
        /// section titles sit at.</summary>
        public const int CellLeftPad = 16;

        /// <summary>Gap between a flexing name and the column pinned to its
        /// right: the module's one name-to-column gap.</summary>
        public const int NameToControlGap = SnapshotItemGridLayout.CellAmountGap;

        public const int InputWidth = 80;
        public const int InputToTagGap = 8;
        public const int RowHeight = 30;
        public const int DescriptionLineHeight = 22;

        /// <summary>Gap between one control group and the next inside a
        /// section.</summary>
        public const int RowGap = 10;

        /// <summary>Gap between one section block and the next in a board
        /// column.</summary>
        public const int SectionGap = 20;

        /// <summary>Gap between a section's title band and its first content
        /// row.</summary>
        public const int TitleToContentGap = 6;

        /// <summary>
        /// The run the narrowest column holds without ellipsizing a setting
        /// name: 22 characters at
        /// <see cref="SnapshotItemGridLayout.MaxCharWidthPx"/>, the
        /// upper-bound-per-character rule that class already uses. 22 covers
        /// the widest label the tab ships ("Metal (Metal Forge)", 19).
        /// </summary>
        public const int NameRunChars = 22;

        public const int NameFloor = NameRunChars * SnapshotItemGridLayout.MaxCharWidthPx;

        // The click-volume row's cluster - the widest on the tab, and so
        // what MinColumnWidth is sized to hold. The slider stays FIXED at
        // 200: only the name flexes, exactly as in a plan table, because a
        // 700px volume slider on a wide column is a worse artefact than the
        // space it would fill.
        public const int SliderWidth = 200;
        public const int SliderToReadoutGap = NameToControlGap;
        public const int ReadoutWidth = 44;
        public const int ReadoutToTestGap = InputToTagGap;
        public const int TestButtonWidth = 72;

        public const int WidestClusterWidth =
            SliderWidth + SliderToReadoutGap + ReadoutWidth + ReadoutToTestGap + TestButtonWidth;

        /// <summary>
        /// Narrowest board column a settings section fits in, term by term:
        /// the left pad, a 22-character name floor, the name-to-control gap,
        /// the widest cluster the tab ships, and the table right margin.
        /// </summary>
        public const int MinColumnWidth =
            CellLeftPad + NameFloor + NameToControlGap + WidestClusterWidth
            + PlanRelayoutMath.TableRightMargin;

        /// <summary>
        /// Widest a line of prose on this tab is allowed to run: one board
        /// column's own content width. A settings section is the widest
        /// thing a sentence here has to sit under, so nothing on the tab -
        /// including the full-width Currency Valuations section's notes -
        /// runs a line wider than one, whatever the panel does.
        /// </summary>
        public const int ProseMeasure =
            MinColumnWidth - CellLeftPad - PlanRelayoutMath.TableRightMargin;

        /// <summary>Right edge every row's control cluster pins to.</summary>
        public static int ClusterRightEdge(int columnWidth)
        {
            return PlanRelayoutMath.PinnedRightEdge(columnWidth);
        }

        /// <summary>Left edge of a cluster of the given width.</summary>
        public static int ClusterX(int columnWidth, int clusterWidth)
        {
            return PlanRelayoutMath.RightAlignedX(ClusterRightEdge(columnWidth), clusterWidth);
        }

        /// <summary>
        /// Left edge of the row's one tag slot - the unit hint OR the
        /// validation error, never both. The slot is banded at
        /// max(widest unit, widest error) across the section, so the column
        /// does not MOVE when a row fails validation.
        /// </summary>
        public static int TagX(int columnWidth, int tagBandWidth)
        {
            return ClusterRightEdge(columnWidth) - tagBandWidth;
        }

        public static int InputX(int columnWidth, int tagBandWidth)
        {
            return TagX(columnWidth, tagBandWidth) - InputToTagGap - InputWidth;
        }

        /// <summary>Cluster width of an input row: the box, the gap, the tag
        /// band.</summary>
        public static int InputClusterWidth(int tagBandWidth)
        {
            return InputWidth + InputToTagGap + (tagBandWidth > 0 ? tagBandWidth : 0);
        }

        public static int TestButtonX(int columnWidth)
        {
            return ClusterRightEdge(columnWidth) - TestButtonWidth;
        }

        public static int VolumeReadoutX(int columnWidth)
        {
            return TestButtonX(columnWidth) - ReadoutToTestGap - ReadoutWidth;
        }

        public static int VolumeSliderX(int columnWidth)
        {
            return VolumeReadoutX(columnWidth) - SliderToReadoutGap - SliderWidth;
        }

        /// <summary>Width a setting's name may occupy before its cluster.</summary>
        public static int NameMaxWidth(int columnWidth, int clusterWidth)
        {
            return PlanRelayoutMath.NameMaxWidthBeforeColumn(
                ClusterRightEdge(columnWidth), clusterWidth, NameToControlGap, CellLeftPad);
        }

        /// <summary>
        /// Budget for a row's own description sub-line: the NAME column, not
        /// the whole row - prose under a control must not run beneath the
        /// control - and capped at <see cref="ProseMeasure"/>, because a
        /// wide column widens the name budget past what a line of prose
        /// should be.
        /// </summary>
        public static int DescriptionMaxWidth(int columnWidth, int clusterWidth)
        {
            int budget = NameMaxWidth(columnWidth, clusterWidth);
            return budget < ProseMeasure ? budget : ProseMeasure;
        }

        /// <summary>
        /// Budget for prose that belongs to a whole section (or to the
        /// full-width currency section) rather than to one row: the
        /// column's content width, capped at <see cref="ProseMeasure"/>.
        /// </summary>
        public static int SectionProseMaxWidth(int columnWidth)
        {
            int available = ClusterRightEdge(columnWidth) - CellLeftPad;
            if (available < 20)
            {
                available = 20;
            }

            return available < ProseMeasure ? available : ProseMeasure;
        }
    }
}
