namespace GW2CraftingHelper.Services
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
    public static class TopRegionLayoutMath
    {
        public const int RowHeight = 35;
        public const int InputRowY = 5;
        public const int RowGap = 3;
        public const int StatusToSeparatorGap = 21;
        public const int SeparatorToContentGap = 5;
        public const int ContentToBottomPad = 5;

        /// <summary>
        /// The Recipe Tree toolbar row. Shorter than RowHeight: it holds
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
            int inputPanelHeight = rowCount * RowHeight;
            int controlsRowY = InputRowY + inputPanelHeight + RowGap;
            int treeToolbarRowY = controlsRowY + RowHeight + RowGap;
            int statusRowY = treeToolbarVisible
                ? treeToolbarRowY + TreeToolbarRowHeight + RowGap
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

    public readonly struct TopRegionLayout
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
