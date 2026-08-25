namespace GW2CraftingHelper.Services
{
    /// <summary>
    /// How the Snapshot tab's result area stacks: a titled, column-headed
    /// section for the item run and another for the wallet run, each
    /// present only when it has rows. Blish-free and tested, because it is
    /// the arithmetic that decides where every cell lands - the view
    /// (MainView.LayoutResultGrid) only copies the results onto controls,
    /// and it writes every one of these y's itself rather than betting on a
    /// FlowPanel re-flowing a later sibling when an earlier one changes
    /// height (the reason the two runs already shared one panel).
    /// <para>
    /// Each section's grid placements are computed at its own offset, so
    /// <see cref="SnapshotItemGridLayout.Grid.Cells"/> are absolute in the
    /// result panel and the view needs no second offset of its own.
    /// </para>
    /// </summary>
    public static class SnapshotResultLayout
    {
        /// <summary>
        /// Gap above a section that follows another one. The first section
        /// starts flush: it sits directly under the coin row's own gap.
        /// </summary>
        public const int SectionGapY = 8;

        public readonly struct Section
        {
            /// <summary>False when the run has no rows: the whole section -
            /// title, header band and grid - is absent, not empty.</summary>
            public readonly bool Present;

            public readonly int TitleY;
            public readonly int HeaderY;
            public readonly SnapshotItemGridLayout.Grid Grid;

            public Section(bool present, int titleY, int headerY, SnapshotItemGridLayout.Grid grid)
            {
                Present = present;
                TitleY = titleY;
                HeaderY = headerY;
                Grid = grid;
            }
        }

        public sealed class Result
        {
            public Section Items { get; }

            public Section Wallet { get; }

            /// <summary>
            /// Height the result panel has to be given. Nothing here
            /// auto-sizes, and a short panel clips its own last row.
            /// </summary>
            public int TotalHeight { get; }

            internal Result(Section items, Section wallet, int totalHeight)
            {
                Items = items;
                Wallet = wallet;
                TotalHeight = totalHeight;
            }
        }

        public static Result Compute(
            int itemCount, int walletCount, int gridWidth,
            int itemRowHeight, int walletRowHeight, int titleBandHeight, int headerBandHeight)
        {
            int y = 0;

            var items = Stack(
                itemCount, gridWidth, itemRowHeight, titleBandHeight, headerBandHeight, ref y);
            if (items.Present && walletCount > 0)
            {
                y += SectionGapY;
            }

            var wallet = Stack(
                walletCount, gridWidth, walletRowHeight, titleBandHeight, headerBandHeight, ref y);

            return new Result(items, wallet, y);
        }

        private static Section Stack(
            int count, int gridWidth, int rowHeight, int titleBandHeight, int headerBandHeight, ref int y)
        {
            if (count <= 0)
            {
                return new Section(false, 0, 0, SnapshotItemGridLayout.Compute(0, gridWidth, rowHeight));
            }

            int titleY = y;
            int headerY = titleY + titleBandHeight;
            int gridY = headerY + headerBandHeight;
            var grid = SnapshotItemGridLayout.Compute(count, gridWidth, rowHeight, gridY);

            y = gridY + grid.Height;
            return new Section(true, titleY, headerY, grid);
        }
    }
}
