using Blish_HUD.Controls;

namespace TaimisToolbench.Views.Rendering
{
    /// <summary>
    /// A sortable column header's TEXT: the sort indicator is part of it,
    /// so nothing is laid out beside the label and every right-aligned
    /// header keeps tracking its column the way it already did (the
    /// relayout closures right-align off the label's own width, which
    /// already includes the indicator - see ColumnHeaderRowRenderer). The
    /// hover and the click belong to the cell around it - see
    /// SortableHeaderCells.
    /// <para>
    /// Indicators are ASCII "^"/"v" from
    /// <see cref="Services.TableSortState{TColumn}"/>, matching the caret
    /// vocabulary the tree and section headers already use.
    /// </para>
    /// </summary>
    internal static class SortableHeaderLabel
    {
        private const string HeaderTooltip =
            "Click to sort by this column. Click again to reverse the order, once more to restore the original order.";

        /// <summary>
        /// Header text carrying its sort indicator, or the bare title when
        /// the column is not the active one.
        /// </summary>
        internal static string Decorate(string title, string indicator)
        {
            return string.IsNullOrEmpty(indicator) ? title : title + " " + indicator;
        }

        /// <summary>
        /// Stamps the shared "click to sort" note on one control of a
        /// sortable header cell. No CLICK is wired here: the hit area is
        /// the whole cell and <see cref="SortableHeaderCells"/> owns it (a
        /// second handler on the label would fire alongside the row's for
        /// one press and cycle the sort twice). The note goes on BOTH the
        /// label and the cell's own surface, because a tooltip resolves on
        /// the deepest control under the cursor and never bubbles.
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
