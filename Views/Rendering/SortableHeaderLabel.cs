using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using System;

namespace GW2CraftingHelper.Views.Rendering
{
    /// <summary>
    /// Makes a plan table's column-header label its own sort control:
    /// the label IS the click target and the sort indicator is part of its
    /// text, so nothing new is laid out beside it and every right-aligned
    /// header keeps tracking its column the way it already did (the
    /// relayout closures right-align off the label's own Width, which
    /// already includes the indicator - see CTableHeaderRenderer).
    /// <para>
    /// Indicators are ASCII "^"/"v" from
    /// <see cref="Services.TableSortState{TColumn}"/>, matching the caret
    /// vocabulary the tree and section headers already use.
    /// </para>
    /// </summary>
    internal static class SortableHeaderLabel
    {
        /// <summary>
        /// Hover tint for a sortable header - the only affordance that
        /// says a header is clickable before it has been clicked, since
        /// an unsorted column deliberately shows no indicator.
        /// </summary>
        private static readonly Color HoverColor = new Color(255, 224, 150);

        private const string HeaderTooltip =
            "Click to sort by this column. Click again to reverse the order, once more to restore the plan's own order.";

        /// <summary>
        /// Header text carrying its sort indicator, or the bare title when
        /// the column is not the active one.
        /// </summary>
        internal static string Decorate(string title, string indicator)
        {
            return string.IsNullOrEmpty(indicator) ? title : title + " " + indicator;
        }

        internal static void MakeClickable(Label label, Action onClick)
        {
            if (label == null || onClick == null) return;

            TooltipFacility.ApplyPlain(label, HeaderTooltip);
            label.MouseEntered += (_, __) => label.TextColor = HoverColor;
            label.MouseLeft += (_, __) => label.TextColor = TableHeaderStyle.LabelColor;
            label.Click += (_, __) => onClick();
        }
    }
}
