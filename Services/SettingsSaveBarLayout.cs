namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Slots on the Settings tab's non-scrolling save bar (Blish-free,
    /// unit-testable): the dirty chip on the left, the status line in the
    /// middle, and [Discard] [Save] pinned to the bar's right edge.
    ///
    /// <para>
    /// Chip and Discard are HIDDEN entirely at zero unsaved changes - a
    /// standing "0 unsaved changes" spends attention on the absence of a
    /// thing, and a permanently-disabled Discard beside it invites "why is
    /// this disabled?" (TreeChipStripLayout's rule, reused). A hidden
    /// control contributes no width and no gap, so what remains sits
    /// exactly where a lone control should.
    /// </para>
    /// </summary>
    public static class SettingsSaveBarLayout
    {
        public const int Inset = SettingsFormLayout.CellLeftPad;

        /// <summary>Gap between the chip and the status line, and between
        /// the status line and the button cluster: the strip reads as
        /// clusters rather than as a run of controls.</summary>
        public const int ChipToStatusGap = TreeChipStripLayout.ChipGap;

        public const int ButtonGap = 8;

        /// <summary>Floor for the status line's own budget - below this it
        /// would ellipsize to nothing on a bar whose buttons already fit.</summary>
        public const int MinStatusWidth = 120;

        public readonly struct Slots
        {
            public readonly int ChipX;
            public readonly int StatusX;
            public readonly int StatusMaxWidth;
            public readonly int DiscardX;
            public readonly int SaveX;

            public Slots(int chipX, int statusX, int statusMaxWidth, int discardX, int saveX)
            {
                ChipX = chipX;
                StatusX = statusX;
                StatusMaxWidth = statusMaxWidth;
                DiscardX = discardX;
                SaveX = saveX;
            }
        }

        /// <summary>
        /// Pass 0 for <paramref name="chipWidth"/> or
        /// <paramref name="discardWidth"/> to hide that control. Save is
        /// rightmost as the primary action, matching the plan tab's
        /// right-anchored Generate Plan.
        /// </summary>
        public static Slots Compute(int barWidth, int chipWidth, int discardWidth, int saveWidth)
        {
            int safeChip = chipWidth > 0 ? chipWidth : 0;
            int safeDiscard = discardWidth > 0 ? discardWidth : 0;
            int safeSave = saveWidth > 0 ? saveWidth : 0;

            int rightEdge = PlanRelayoutMath.PinnedRightEdge(barWidth);
            int saveX = PlanRelayoutMath.RightAlignedX(rightEdge, safeSave);
            int discardX = safeDiscard > 0 ? saveX - ButtonGap - safeDiscard : saveX;

            int chipX = Inset;
            int statusX = safeChip > 0 ? Inset + safeChip + ChipToStatusGap : Inset;

            int statusMaxWidth = discardX - ChipToStatusGap - statusX;
            if (statusMaxWidth < MinStatusWidth)
            {
                statusMaxWidth = MinStatusWidth;
            }

            return new Slots(chipX, statusX, statusMaxWidth, discardX, saveX);
        }
    }
}
