namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Cross-tab control geometry that was being re-picked per construction
    /// site.
    /// </summary>
    internal static class UiMetrics
    {
        /// <summary>
        /// Height of every StandardButton in the module.
        ///
        /// <para>
        /// Three heights were in use (audit batch J, L3): 30 on the
        /// Snapshot tab's Clear Cache / Refresh Now, 28 on the Log tab's
        /// three buttons, Settings' Save and the plan's Generate Plan, and
        /// 24 on the plan's five Recipe Tree actions and its per-row +/-
        /// pair. 28 wins on count, and on fit: it is exactly the height of
        /// this module's TextBox and Dropdown controls, so a button sharing
        /// a row with either now lines up with it instead of sitting 2px
        /// proud or 4px shallow - which is the visible half of the
        /// complaint, the Recipe Tree strip and the item row each mixing
        /// two heights in one run.
        /// </para>
        ///
        /// <para>
        /// Applied at the construction sites rather than by resizing
        /// afterwards, so every button's y offset is derived from this in
        /// the same expression that positions it.
        /// </para>
        /// </summary>
        internal const int ButtonHeight = 28;
    }
}
