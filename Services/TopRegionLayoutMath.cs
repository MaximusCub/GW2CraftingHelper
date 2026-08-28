namespace TaimisToolbench.Services
{
    /// <summary>
    /// Y-offset arithmetic for the plan tab's non-scrolling top strip
    /// (Blish-free, unit-testable). Extracted from
    /// CraftingPlanView.ComputeTopRegionLayout when the strip gained a
    /// conditional row: three call sites - the initial Build, the item-row
    /// add/remove reflow, and the resize handler - all lay the strip out
    /// from this one formula, and a conditional row is exactly the kind of
    /// thing three hand-rolled copies would disagree about.
    /// <para>See docs/ARCHITECTURE.md section 4.</para>
    /// </summary>
    internal static class TopRegionLayoutMath
    {
        public const int TopRegionRowHeight = 35;
        public const int InputRowY = 5;
        public const int TopRegionRowGap = 3;
        /// <summary>
        /// Band the plan tab's status label occupies between the row above
        /// it and the separator rule. 25, not the 23 it was at Body: the
        /// status line moved to TypeRampMetrics.StatusInk, whose lowest ink
        /// is y+23 rather than y+21 measured from the band's own top. The
        /// constant is that ink plus the same 2px it has always kept off
        /// the separator - at 23 the descenders would sit on the rule.
        /// </summary>
        public const int StatusToSeparatorGap = 25;
        public const int SeparatorToContentGap = 5;
        public const int ContentToBottomPad = 5;

        /// <summary>
        /// The Recipe Tree toolbar row. Shorter than TopRegionRowHeight: it holds
        /// 24px buttons and a label, not the input row's text boxes.
        /// </summary>
        public const int TreeToolbarRowHeight = 30;

        /// <summary>
        /// Every fixed element's Y in the strip, plus the total height the
        /// scrollable content area starts below.
        /// <para>
        /// With <paramref name="treeToolbarVisible"/> false every offset is
        /// byte-identical to the layout before that row existed - the row
        /// contributes its height and its gap or nothing at all, never a
        /// partial reservation. That is the guarantee that lets the toolbar
        /// appear and disappear with the plan.
        /// </para>
        /// </summary>
        public static TopRegionLayout Compute(int rowCount, bool treeToolbarVisible)
        {
            int inputPanelHeight = rowCount * TopRegionRowHeight;
            int controlsRowY = InputRowY + inputPanelHeight + TopRegionRowGap;
            int treeToolbarRowY = controlsRowY + TopRegionRowHeight + TopRegionRowGap;
            int statusRowY = treeToolbarVisible
                ? treeToolbarRowY + TreeToolbarRowHeight + TopRegionRowGap
                : treeToolbarRowY;
            int separatorY = statusRowY + StatusToSeparatorGap;
            int contentY = separatorY + SeparatorToContentGap;

            return new TopRegionLayout(
                inputPanelHeight,
                controlsRowY,
                treeToolbarRowY,
                statusRowY,
                separatorY,
                contentY,
                contentY + ContentToBottomPad);
        }
    }

    internal readonly struct TopRegionLayout
    {
        public readonly int InputPanelHeight;
        public readonly int ControlsRowY;

        /// <summary>
        /// Where the tree toolbar row sits when it is shown. Still
        /// populated when it is hidden (it is simply the row the status
        /// label then occupies), so a caller never has to special-case
        /// reading it.
        /// </summary>
        public readonly int TreeToolbarRowY;
        public readonly int StatusRowY;
        public readonly int SeparatorY;
        public readonly int ContentY;
        public readonly int TopRegionHeight;

        public TopRegionLayout(
            int inputPanelHeight, int controlsRowY, int treeToolbarRowY,
            int statusRowY, int separatorY, int contentY, int topRegionHeight)
        {
            InputPanelHeight = inputPanelHeight;
            ControlsRowY = controlsRowY;
            TreeToolbarRowY = treeToolbarRowY;
            StatusRowY = statusRowY;
            SeparatorY = separatorY;
            ContentY = contentY;
            TopRegionHeight = topRegionHeight;
        }
    }
}
