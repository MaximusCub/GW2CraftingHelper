using Blish_HUD.Controls;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// The shared "click to sort" note, and the one place its wording lives.
    /// <para>
    /// A sortable header is two controls, not one string: the word, and the
    /// <see cref="SortIndicator"/> beside it. They were one Label until the
    /// indicator became persistent - a rest state that differs from the
    /// active one only in opacity cannot be half of a single Label's text.
    /// </para>
    /// </summary>
    internal static class SortableHeaderLabel
    {
        private const string HeaderTooltip =
            "Click to sort by this column. Click again to reverse the order, once more to restore the original order.";

        /// <summary>
        /// Stamps the note on one control of a sortable header cell. No
        /// CLICK is wired here: the hit area is the whole cell and
        /// <see cref="SortableHeaderCells"/> owns it (a second handler on the
        /// label would fire alongside the row's for one press and cycle the
        /// sort twice). The note goes on the label, the indicator AND the
        /// cell's own surface, because a tooltip resolves on the deepest
        /// control under the cursor and never bubbles.
        /// </summary>
        internal static void MarkSortable(Control control)
        {
            if (control == null)
            {
                return;
            }

            TooltipFacility.ApplyPlain(control, HeaderTooltip);
        }
    }
}
