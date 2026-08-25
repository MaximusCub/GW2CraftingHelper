namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// The Log tab's toolbar row (Blish-free, unit-testable): a left cluster
    /// from the tab's inset, a right cluster pinned to
    /// <see cref="PlanRelayoutMath.PinnedRightEdge"/>, and the search box as
    /// the one control that flexes into whatever is between them.
    ///
    /// <para>
    /// The gap BETWEEN the two clusters is not stranded space - it is the
    /// plan tab's own controls-row shape, a left cluster and a right cluster
    /// on one bar. Stated here so it is not re-litigated.
    /// </para>
    /// </summary>
    public static class LogToolbarLayout
    {
        public const int Inset = LogGutterLayout.GutterX;
        public const int Gap = 8;
        public const int BarHeight = 40;

        /// <summary>
        /// The narrowest the search box gets at any width the module
        /// supports, down to and including the narrow-screen floor. A
        /// property of the layout, checked in tests - deliberately NOT a
        /// clamp: past that floor the box keeps shrinking instead, because a
        /// toolbar whose clusters overlap is worse than a small box.
        /// </summary>
        public const int SearchMinWidth = 180;

        /// <summary>
        /// Cap on the flexing search box. Past this the box is bigger than
        /// any query typed into it and the growth is decoration.
        /// </summary>
        public const int SearchMaxWidth = 400;

        /// <summary>
        /// One optical centre for controls of different heights on one bar.
        /// Blish's TextBox (26), Dropdown (30), Checkbox (25) and
        /// StandardButton (28) are four fixed heights that this row carries
        /// side by side; UiMetrics.ButtonHeight's own doc comment names that
        /// as a separate, unmade decision. This is that decision.
        /// </summary>
        public static int CenteredY(int controlHeight)
        {
            int y = (BarHeight - (controlHeight > 0 ? controlHeight : 0)) / 2;
            return y > 0 ? y : 0;
        }

        public readonly struct Slots
        {
            public readonly int SearchX;
            public readonly int SearchWidth;
            public readonly int DropdownX;
            public readonly int FollowX;
            public readonly int DeleteX;
            public readonly int CopyX;
            public readonly int ClearViewX;

            public Slots(
                int searchX, int searchWidth, int dropdownX, int followX,
                int deleteX, int copyX, int clearViewX)
            {
                SearchX = searchX;
                SearchWidth = searchWidth;
                DropdownX = dropdownX;
                FollowX = followX;
                DeleteX = deleteX;
                CopyX = copyX;
                ClearViewX = clearViewX;
            }
        }

        /// <summary>
        /// Clear View stays rightmost and Delete Log File leftmost of the
        /// three, unchanged: the two view-only buttons keep their
        /// established spots and the destructive one is not the easiest to
        /// reach. What changed is that they are derived from the pinned
        /// right edge instead of from three literals.
        /// </summary>
        public static Slots Compute(
            int barWidth, int dropdownWidth, int followWidth,
            int deleteWidth, int copyWidth, int clearWidth)
        {
            int rightEdge = PlanRelayoutMath.PinnedRightEdge(barWidth);

            int clearX = PlanRelayoutMath.RightAlignedX(rightEdge, clearWidth);
            int copyX = clearX - Gap - copyWidth;
            int deleteX = copyX - Gap - deleteWidth;

            // Everything else on the row is fixed, so the box takes exactly
            // what the two clusters leave, capped - see SearchMinWidth for
            // why there is no matching floor here.
            int fixedLeft = Gap + dropdownWidth + Gap + followWidth;
            int available = deleteX - Gap - Inset - fixedLeft;
            int searchWidth = available > SearchMaxWidth ? SearchMaxWidth : available;
            if (searchWidth < 0) searchWidth = 0;

            int dropdownX = Inset + searchWidth + Gap;
            int followX = dropdownX + dropdownWidth + Gap;

            return new Slots(Inset, searchWidth, dropdownX, followX, deleteX, copyX, clearX);
        }
    }
}
