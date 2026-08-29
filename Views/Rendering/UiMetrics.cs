using Microsoft.Xna.Framework;

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
        /// (ModalDialog, ApiAccessDialog) keep their own 25px footer
        /// buttons and are not covered: their geometry is hand-placed
        /// against a fixed window size, so a height change there moves a
        /// button relative to a window edge rather than to a row of
        /// neighbours, which is a separate decision and an unmade one.
        ///
        /// <para>
        /// Three heights were in use across the tabs:
        /// 30 on the Snapshot tab's Clear Cache / Refresh Now, 28 on the Log
        /// tab's three buttons, Settings' Save and the plan's Generate Plan, and
        /// 24 on the plan's five Recipe Tree actions and its per-row +/-
        /// pair. 28 wins on button count, and it is the height of the one
        /// input row a button already shares - the plan's item row, whose
        /// AutocompleteTextBox and quantity TextBox are both 28, beside its
        /// +/- pair.
        /// </para>
        ///
        /// <para>
        /// It is NOT the module's input height: TextBoxes are 26 at nine of
        /// their eleven sites (Settings' six, the Snapshot and Log search
        /// boxes, About's), and the two Dropdowns outside the plan tab are
        /// 30. So a button on the Log toolbar still does not share a
        /// baseline with the search box and level dropdown beside it. That
        /// run is three input heights wide before any button is placed and
        /// is a separate decision from this one - do not read this constant
        /// as having settled it.
        /// </para>
        ///
        /// <para>
        /// Applied at the construction sites rather than by resizing
        /// afterwards, so every button's y offset is derived from this in
        /// the same expression that positions it.
        /// </para>
        /// </summary>
        internal const int ButtonHeight = 28;

        /// <summary>The matched 16px X of Blish's own remove pair, by .dat asset id.</summary>
        internal const int RowRemoveMarkAssetId = 733269;

        /// <summary>
        /// Dark ink for a row button's icon. 733269 is authored white for a
        /// dark window and the button plate under it is parchment, so an
        /// untinted blit is invisible on it - the measured case
        /// FeedbackButton.IconTint exists for.
        /// </summary>
        internal static readonly Color RowButtonIconTint = new Color(45, 42, 38);
    }
}
