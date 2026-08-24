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
    ///
    /// <para>
    /// The strip is not free to be as wide as its content: the same row
    /// right-anchors five buttons. <see cref="Fit"/> is the whole of the
    /// negotiation between them, and every caller goes through it - the
    /// x's alone are not a placement.
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

        /// <summary>How much of the strip a given width can hold.</summary>
        public enum ChipStripTier
        {
            /// <summary>Both counts, each with its own clear button.</summary>
            Full,

            /// <summary>
            /// The counts without their clear buttons. What a plan's state
            /// IS survives a narrow window; the buttons that change it do
            /// not, because Generate Plan clears both and Best Path clears
            /// the overrides, so no state becomes unreachable - only less
            /// convenient, on a window already below the designed floor.
            /// </summary>
            CountsOnly,

            /// <summary>Neither: not even the counts clear the buttons.</summary>
            Hidden
        }

        /// <summary>
        /// A tier and the slots that go with it. Read
        /// <see cref="ShowCounts"/>/<see cref="ShowButtons"/> rather than
        /// the tier: a caller shows and hides four controls, and those two
        /// questions are what it is actually asking.
        /// </summary>
        public readonly struct Placement
        {
            public readonly ChipStripTier Tier;
            public readonly Slots Slots;

            public Placement(ChipStripTier tier, Slots slots)
            {
                Tier = tier;
                Slots = slots;
            }

            public bool ShowCounts => Tier != ChipStripTier.Hidden;

            public bool ShowButtons => Tier == ChipStripTier.Full;
        }

        /// <summary>
        /// The widest arrangement that ends at or before
        /// <paramref name="limitX"/> - the left edge of whatever the row
        /// anchors on its RIGHT, less whatever separation that caller
        /// wants.
        /// <para>
        /// This exists because the row has two clusters and only one of
        /// them used to have arithmetic. The chips replaced a fixed ~90px
        /// grey caption with up to ~450px of live content, against the
        /// right-anchored button cluster
        /// <see cref="TreeToolbarRowLayout.ChipLimitX"/> measures: on a
        /// narrow enough row they overlap, and two live controls on the
        /// same pixels is a click landing on whichever Blish hit-tests
        /// last. Reachable well inside the module's own supported range -
        /// WindowSizing.EffectiveMinWindowWidth falls back to the client
        /// width on a game window narrower than the 1378 minimum.
        /// </para>
        /// </summary>
        public static Placement Fit(
            int startX, int limitX,
            bool showOverrides, int overridesLabelWidth, int overridesButtonWidth,
            bool showIgnored, int ignoredLabelWidth, int ignoredButtonWidth)
        {
            var full = Layout(
                startX,
                showOverrides, overridesLabelWidth, overridesButtonWidth,
                showIgnored, ignoredLabelWidth, ignoredButtonWidth,
                withButtons: true);
            if (full.EndX <= limitX)
            {
                return new Placement(ChipStripTier.Full, full);
            }

            var counts = Layout(
                startX,
                showOverrides, overridesLabelWidth, overridesButtonWidth,
                showIgnored, ignoredLabelWidth, ignoredButtonWidth,
                withButtons: false);
            if (counts.EndX <= limitX)
            {
                return new Placement(ChipStripTier.CountsOnly, counts);
            }

            return new Placement(
                ChipStripTier.Hidden,
                Layout(startX, false, 0, 0, false, 0, 0, withButtons: true));
        }

        /// <summary>
        /// Where each of the four controls goes. A hidden chip's two x's
        /// are still returned (as the cursor at that point) so a caller
        /// can place a hidden control without branching; they are not read
        /// while it is hidden.
        /// <para>
        /// Without buttons a chip is its count and nothing else - no
        /// button width AND no LabelToButtonGap, so the two counts sit
        /// exactly one ChipGap apart rather than carrying the hole their
        /// buttons left.
        /// </para>
        /// </summary>
        private static Slots Layout(
            int startX,
            bool showOverrides, int overridesLabelWidth, int overridesButtonWidth,
            bool showIgnored, int ignoredLabelWidth, int ignoredButtonWidth,
            bool withButtons)
        {
            int x = startX;

            int overridesLabelX = x;
            int overridesButtonX = x;
            if (showOverrides)
            {
                overridesButtonX = overridesLabelX + overridesLabelWidth + LabelToButtonGap;
                x = withButtons
                    ? overridesButtonX + overridesButtonWidth + ChipGap
                    : overridesLabelX + overridesLabelWidth + ChipGap;
            }

            int ignoredLabelX = x;
            int ignoredButtonX = x;
            if (showIgnored)
            {
                ignoredButtonX = ignoredLabelX + ignoredLabelWidth + LabelToButtonGap;
                x = withButtons
                    ? ignoredButtonX + ignoredButtonWidth + ChipGap
                    : ignoredLabelX + ignoredLabelWidth + ChipGap;
            }

            int endX = x > startX ? x - ChipGap : startX;
            return new Slots(overridesLabelX, overridesButtonX, ignoredLabelX, ignoredButtonX, endX);
        }
    }
}
