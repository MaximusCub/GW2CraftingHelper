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
        /// (ModalDialog, ApiAccessDialog) are not covered: their 25px footer
        /// buttons come from Services/DialogLayoutMath, which sizes them
        /// beside the offsets they have to agree with.
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
