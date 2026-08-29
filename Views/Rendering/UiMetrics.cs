namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// Cross-tab control geometry that was being re-picked per construction
    /// site.
    /// </summary>
    internal static class UiMetrics
    {
        /// <summary>
        /// Height of every StandardButton on a TAB. The two dialogs
        /// (ModalDialog, ApiAccessDialog) keep their own 25px footer buttons and
        /// are not covered: their geometry is hand-placed against a fixed window
        /// size, which is a separate decision and an unmade one.
        /// <para>
        /// It is NOT the module's input height. TextBoxes are 26 at nine of
        /// their eleven sites and the two Dropdowns outside the plan tab are 30,
        /// so a button on the Log toolbar still does not share a baseline with
        /// the search box and level dropdown beside it. That run is a separate
        /// decision - do not read this constant as having settled it.
        /// </para>
        /// <para>
        /// Applied at the construction sites rather than by resizing afterwards,
        /// so every button's y offset is derived from this in the same
        /// expression that positions it.
        /// </para>
        /// How 28 was picked: docs/ARCHITECTURE.md, "Views: relocated design
        /// narrative".
        /// </summary>
        internal const int ButtonHeight = 28;
    }
}
