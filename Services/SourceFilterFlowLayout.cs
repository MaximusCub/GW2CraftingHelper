using System.Collections.Generic;

namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// Placement of one cell in a <see cref="SourceFilterFlowLayout"/> run,
    /// relative to the containing row panel's own origin.
    /// </summary>
    public class FlowCellPlacement
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    /// <summary>
    /// The full placement run: one <see cref="FlowCellPlacement"/> per input
    /// width, in input order, plus the height the container must reserve.
    /// </summary>
    public class SourceFilterFlowResult
    {
        public List<FlowCellPlacement> Cells { get; } = new List<FlowCellPlacement>();
        public int RowCount { get; set; }
        public int TotalHeight { get; set; }
    }

    /// <summary>
    /// Wrapping left-to-right placement for the Snapshot tab's source-filter
    /// checkbox row, whose cell count is account-driven (one checkbox per
    /// character, 1 to 15+) and so cannot use the fixed X positions the row
    /// carried while it was four fixed checkboxes. Blish-free by
    /// construction: callers measure their own label widths and apply the
    /// returned offsets (see Views/MainView.cs).
    /// </summary>
    public static class SourceFilterFlowLayout
    {
        /// <summary>
        /// A cell wider than <paramref name="availableWidth"/> still gets
        /// placed at the start of its own row rather than being dropped or
        /// looping forever - overflowing one oversized label is strictly
        /// better than hiding a filter the user cannot then re-enable.
        /// </summary>
        public static SourceFilterFlowResult Layout(
            IReadOnlyList<int> cellWidths,
            int availableWidth,
            int cellHeight,
            int horizontalGap,
            int verticalGap)
        {
            var result = new SourceFilterFlowResult();

            if (cellWidths == null || cellWidths.Count == 0)
            {
                return result;
            }

            int height = cellHeight > 0 ? cellHeight : 0;
            int gapX = horizontalGap > 0 ? horizontalGap : 0;
            int gapY = verticalGap > 0 ? verticalGap : 0;

            int x = 0;
            int rowIndex = 0;

            foreach (int rawWidth in cellWidths)
            {
                int width = rawWidth > 0 ? rawWidth : 0;

                if (x > 0 && x + width > availableWidth)
                {
                    rowIndex++;
                    x = 0;
                }

                result.Cells.Add(new FlowCellPlacement { X = x, Y = rowIndex * (height + gapY) });
                x += width + gapX;
            }

            result.RowCount = rowIndex + 1;
            result.TotalHeight = (result.RowCount * height) + (rowIndex * gapY);
            return result;
        }
    }
}
