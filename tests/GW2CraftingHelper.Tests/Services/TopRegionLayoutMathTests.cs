using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The plan tab's top strip is laid out from this one formula by three
    /// separate call sites (initial Build, item-row add/remove reflow,
    /// resize handler). The Recipe Tree toolbar row is conditional, so the
    /// property that matters most is that a hidden row costs exactly
    /// nothing - not "almost nothing".
    /// </summary>
    public class TopRegionLayoutMathTests
    {
        [Fact]
        public void SingleRow_NoToolbar_MatchesTheFixedLayoutItReplaced()
        {
            // Absolute pixel literals, not the constants recomputed: these
            // are the offsets the strip had before it was parameterised at
            // all, so a constant changing must fail here rather than
            // silently move the strip.
            //
            // Re-baselined twice, by two pixels each time, and only ever
            // below the status label: 21 -> 23 for the +2pt body bump, then
            // 23 -> 25 when the status line moved to its own tier
            // (TypeRampMetrics.StatusInk, lowest ink 23 against Body's 21).
            // Every row ABOVE the status label is unmoved both times, which
            // is the point of listing them all here.
            var layout = TopRegionLayoutMath.Compute(rowCount: 1, treeToolbarVisible: false);

            Assert.Equal(35, layout.InputPanelHeight);
            Assert.Equal(43, layout.ControlsRowY);
            Assert.Equal(81, layout.StatusRowY);
            Assert.Equal(106, layout.SeparatorY);
            Assert.Equal(111, layout.ContentY);
            Assert.Equal(116, layout.TopRegionHeight);
        }

        [Fact]
        public void StatusBand_KeepsTheStatusLinesDescendersOffTheSeparator()
        {
            // StatusToSeparatorGap is measured from the status label's own
            // top, so the separator sits exactly that far under the text -
            // and the status tier's descenders reach 23px down, not Body's
            // 21. The 2px is the scissor-safe clearance every rule in the
            // module keeps (LabelHelpers.CreateRowDivider's M36b note).
            Assert.True(
                TypeRampMetrics.StatusInk.LowestInk + 2 <= TopRegionLayoutMath.StatusToSeparatorGap,
                $"status ink bottom {TypeRampMetrics.StatusInk.LowestInk} crowds the separator at "
                    + $"{TopRegionLayoutMath.StatusToSeparatorGap}");
        }

        [Fact]
        public void StatusBand_HoldsTheSpinnerToo()
        {
            // The spinner is centred on the label's line box, so it starts
            // inside the band; it must also END inside it, or it overlaps
            // the separator the label was measured to clear.
            int spinnerTop = (TypeRampMetrics.StatusInk.LineHeight - InlineSpinnerLayout.PlanStripSize) / 2;
            if (spinnerTop < 0)
            {
                spinnerTop = 0;
            }

            Assert.True(
                spinnerTop + InlineSpinnerLayout.PlanStripSize <= TopRegionLayoutMath.StatusToSeparatorGap,
                $"spinner bottom {spinnerTop + InlineSpinnerLayout.PlanStripSize} overruns the "
                    + $"{TopRegionLayoutMath.StatusToSeparatorGap}px status band");
        }

        [Fact]
        public void HiddenToolbar_CostsNothing_AndTheRowSitsWhereStatusDoes()
        {
            var hidden = TopRegionLayoutMath.Compute(rowCount: 3, treeToolbarVisible: false);

            // The toolbar's Y is still meaningful when hidden (callers
            // never special-case reading it) - it is simply the row the
            // status label occupies instead.
            Assert.Equal(hidden.StatusRowY, hidden.TreeToolbarRowY);
        }

        [Fact]
        public void VisibleToolbar_AddsExactlyItsRowAndOneGap()
        {
            var hidden = TopRegionLayoutMath.Compute(rowCount: 2, treeToolbarVisible: false);
            var shown = TopRegionLayoutMath.Compute(rowCount: 2, treeToolbarVisible: true);

            int delta = TopRegionLayoutMath.TreeToolbarRowHeight + TopRegionLayoutMath.TopRegionRowGap;

            // Everything at or above the toolbar row is untouched...
            Assert.Equal(hidden.InputPanelHeight, shown.InputPanelHeight);
            Assert.Equal(hidden.ControlsRowY, shown.ControlsRowY);
            Assert.Equal(hidden.TreeToolbarRowY, shown.TreeToolbarRowY);

            // ...and everything below it shifts by the same single amount.
            Assert.Equal(hidden.StatusRowY + delta, shown.StatusRowY);
            Assert.Equal(hidden.SeparatorY + delta, shown.SeparatorY);
            Assert.Equal(hidden.ContentY + delta, shown.ContentY);
            Assert.Equal(hidden.TopRegionHeight + delta, shown.TopRegionHeight);
        }

        [Fact]
        public void ToolbarRow_SitsBelowTheControlsRow_WithoutOverlappingIt()
        {
            var shown = TopRegionLayoutMath.Compute(rowCount: 1, treeToolbarVisible: true);

            Assert.True(shown.TreeToolbarRowY >= shown.ControlsRowY + TopRegionLayoutMath.TopRegionRowHeight);
            Assert.True(
                shown.StatusRowY >= shown.TreeToolbarRowY + TopRegionLayoutMath.TreeToolbarRowHeight);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(5)]
        [InlineData(20)]
        public void RowCount_OnlyMovesThingsDown_NeverReordersThem(int rowCount)
        {
            foreach (bool toolbar in new[] { false, true })
            {
                var layout = TopRegionLayoutMath.Compute(rowCount, toolbar);

                Assert.Equal(rowCount * TopRegionLayoutMath.TopRegionRowHeight, layout.InputPanelHeight);
                Assert.True(layout.ControlsRowY > TopRegionLayoutMath.InputRowY);
                Assert.True(layout.TreeToolbarRowY >= layout.ControlsRowY);
                Assert.True(layout.StatusRowY >= layout.TreeToolbarRowY);
                Assert.True(layout.SeparatorY > layout.StatusRowY);
                Assert.True(layout.ContentY > layout.SeparatorY);
                Assert.True(layout.TopRegionHeight > layout.ContentY);
            }
        }

        [Fact]
        public void ZeroRows_StillProducesAnOrderedStrip()
        {
            // Defensive: the view always seeds one row, but the arithmetic
            // must not invert if it ever does not.
            var layout = TopRegionLayoutMath.Compute(rowCount: 0, treeToolbarVisible: true);

            Assert.Equal(0, layout.InputPanelHeight);
            Assert.True(layout.ContentY > layout.SeparatorY);
        }
    }
}
