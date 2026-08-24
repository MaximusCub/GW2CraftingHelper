namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Left-to-right x positions for the Recipe Tree toolbar row's two
    /// per-plan STATE chips (Blish-free, unit-testable). Each chip is a
    /// count label with its own clear button beside it: "Overrides: 3
    /// [Clear Overrides]", "Ignored: 2 [Clear Ignored]".
    ///
    /// <para>
    /// The slot they occupy used to hold a grey "Recipe Tree:" caption -
    /// small AND grey, labelling five buttons whose own verbs and tooltips
    /// already said what they act on. Real information replaces a caption
    /// that named nothing.
    /// </para>
    ///
    /// <para>
    /// A chip is HIDDEN entirely at zero rather than shown reading zero: a
    /// standing "Overrides: 0" spends attention on the absence of a thing,
    /// and a permanently-disabled clear button beside it invites "why is
    /// this disabled?". A hidden chip contributes no width and no gap, so
    /// the surviving chip sits exactly where a lone chip should.
    /// </para>
    /// </summary>
    public static class TreeChipStripLayout
    {
        /// <summary>Gap between a count label and its own clear button.</summary>
        public const int LabelToButtonGap = 8;

        /// <summary>
        /// Gap between the two chips. Wider than the within-chip gap, so
        /// the row reads as two clusters rather than four controls - the
        /// same grouping rule the five buttons on the right already use.
        /// </summary>
        public const int ChipGap = 20;

        public readonly struct Slots
        {
            public readonly int OverridesLabelX;
            public readonly int OverridesButtonX;
            public readonly int IgnoredLabelX;
            public readonly int IgnoredButtonX;

            /// <summary>
            /// x just past the rightmost visible control, or
            /// <c>startX</c> when neither chip is shown - what a caller
            /// checks the right-hand button cluster against.
            /// </summary>
            public readonly int EndX;

            public Slots(
                int overridesLabelX, int overridesButtonX,
                int ignoredLabelX, int ignoredButtonX, int endX)
            {
                OverridesLabelX = overridesLabelX;
                OverridesButtonX = overridesButtonX;
                IgnoredLabelX = ignoredLabelX;
                IgnoredButtonX = ignoredButtonX;
                EndX = endX;
            }
        }

        /// <summary>
        /// Where each of the four controls goes. A hidden chip's two x's
        /// are still returned (as the cursor at that point) so a caller can
        /// place a hidden control without branching; they are not read
        /// while it is hidden.
        /// </summary>
        public static Slots Compute(
            int startX,
            bool showOverrides, int overridesLabelWidth, int overridesButtonWidth,
            bool showIgnored, int ignoredLabelWidth, int ignoredButtonWidth)
        {
            int x = startX;

            int overridesLabelX = x;
            int overridesButtonX = x;
            if (showOverrides)
            {
                overridesButtonX = overridesLabelX + overridesLabelWidth + LabelToButtonGap;
                x = overridesButtonX + overridesButtonWidth + ChipGap;
            }

            int ignoredLabelX = x;
            int ignoredButtonX = x;
            if (showIgnored)
            {
                ignoredButtonX = ignoredLabelX + ignoredLabelWidth + LabelToButtonGap;
                x = ignoredButtonX + ignoredButtonWidth + ChipGap;
            }

            int endX = x > startX ? x - ChipGap : startX;
            return new Slots(overridesLabelX, overridesButtonX, ignoredLabelX, ignoredButtonX, endX);
        }
    }
}
