using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class PlanHistoryRowLayoutTests
    {
        // The five desktop-gate widths, as PANEL widths (the window minus
        // its chrome), matching RankerRowLayoutTests' own convention.
        public static TheoryData<int> GateWidths => new TheoryData<int>
        {
            WindowSizing.TabPanelWidthFor(1378),
            WindowSizing.TabPanelWidthFor(1638),
            WindowSizing.TabPanelWidthFor(1836),
            WindowSizing.TabPanelWidthFor(2406),
            WindowSizing.TabPanelWidthFor(2560),
        };

        private const int CostWidth = 120;
        private const int WhenWidth = 150;

        [Theory]
        [MemberData(nameof(GateWidths))]
        public void AtEveryGateWidth_NameFlexes_NothingOverlaps_ClusterEndsAtTheRightEdge(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            // The name band is real and never runs under the cost cell.
            Assert.True(bands.NameWidth > 0);
            int costLeftEdge = bands.CostRightEdge - CostWidth;
            Assert.True(bands.NameX + bands.NameWidth <= costLeftEdge);

            // Cost sits left of the timestamp, timestamp left of the cluster.
            Assert.True(bands.CostRightEdge < bands.WhenX);
            Assert.True(bands.WhenX + bands.WhenWidth < bands.ViewX);

            // The cluster runs View, Open, Re-solve, the Pin toggle and
            // Delete with no overlap...
            Assert.True(bands.ViewX + PlanHistoryRowLayout.ActionButtonWidth <= bands.OpenX);
            Assert.True(bands.OpenX + PlanHistoryRowLayout.ActionButtonWidth <= bands.ResolveX);
            Assert.True(bands.ResolveX + PlanHistoryRowLayout.ActionButtonWidth <= bands.PinX);
            Assert.True(bands.PinX + PlanHistoryRowLayout.PinToggleWidth <= bands.DeleteX);

            // ...and the rightmost button's right edge lands exactly at
            // rowWidth - Inset: no band of empty space to the right.
            Assert.Equal(rowWidth - PlanHistoryRowLayout.Inset,
                bands.DeleteX + PlanHistoryRowLayout.IconButtonWidth);
        }

        [Theory]
        [MemberData(nameof(GateWidths))]
        public void NameConsumesEveryPixelThePinnedBlockDoesNot(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            // The flexing law: NameWidth is exactly the space between the
            // name's left edge and the cost cell's left edge minus one
            // cell gap - nothing is left stranded.
            Assert.Equal(
                bands.CostRightEdge - CostWidth - PlanHistoryRowLayout.CellGap - bands.NameX,
                bands.NameWidth);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-50)]
        [InlineData(200)]
        public void DegenerateWidths_ClampRatherThanGoingNegative(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            Assert.True(bands.NameWidth >= 0);
            Assert.True(bands.WhenWidth >= 0);
            Assert.True(bands.RowWidth >= 0);
        }

        [Fact]
        public void CostAndWhenBands_AreFlooredSoAnEmptyTableCannotCollapseThem()
        {
            // The header-label collision RankerRowLayout documents: a
            // measured width of 0 must behave exactly like the floor.
            var floored = PlanHistoryRowLayout.Compute(1200, 0, 0);
            var atFloor = PlanHistoryRowLayout.Compute(
                1200, PlanHistoryRowLayout.MinCostCellWidth, PlanHistoryRowLayout.MinWhenWidth);

            Assert.Equal(atFloor.NameWidth, floored.NameWidth);
            Assert.Equal(atFloor.WhenX, floored.WhenX);
            Assert.Equal(PlanHistoryRowLayout.MinWhenWidth, floored.WhenWidth);

            // Above the floor, every extra cost pixel comes out of the
            // flexing name band, nothing else moves.
            var wide = PlanHistoryRowLayout.Compute(1200, PlanHistoryRowLayout.MinCostCellWidth + 40, 0);
            Assert.Equal(40, floored.NameWidth - wide.NameWidth);
            Assert.Equal(floored.WhenX, wide.WhenX);
        }

        [Fact]
        public void TheRowIsTierOne_AndItsHeightIsDerivedFromThatFrame()
        {
            // The defect this pins: the tab shipped before the two-tier
            // ruling and carried a local 32px legacy icon in a 44px row,
            // while every other "one row, one object" surface in the module
            // had moved to tier 1.
            Assert.Equal(ItemIconTiers.BagSlotIconSize, PlanHistoryRowLayout.IconSize);
            Assert.Equal(
                PlanHistoryRowLayout.IconSize + 2 * PlanHistoryRowLayout.IconBorder,
                PlanHistoryRowLayout.IconTotal);

            // Frame plus breathing room, and the frame is centred in it -
            // no divider term, because these rows draw none.
            Assert.Equal(
                PlanHistoryRowLayout.IconTotal + 2 * PlanHistoryRowLayout.IconPad,
                PlanHistoryRowLayout.RowHeight);
            Assert.Equal(
                PlanHistoryRowLayout.RowHeight,
                PlanHistoryRowLayout.IconY + PlanHistoryRowLayout.IconTotal
                    + PlanHistoryRowLayout.IconPad);

            // The sibling tier-1 row list in the module: a history row and
            // a watchlist row must not be different heights.
            Assert.Equal(RankerRowLayout.RowHeight, PlanHistoryRowLayout.RowHeight);
        }

        [Fact]
        public void TheExpandedItemListIsTierTwo_AndItsLineHeightIsDerivedFromThatFrame()
        {
            // The expanded row's per-item list is a DENSE item list, so it
            // takes the plan tab's own row-icon tier - not the tier-1 art
            // the row above it headlines with, and not the local 20px it
            // used to hard-code.
            Assert.Equal(ItemIconTiers.BagSidebarIconSize, PlanHistoryRowLayout.DetailIconSize);
            Assert.Equal(PlanContentHeightMath.RowIconBorder, PlanHistoryRowLayout.DetailIconBorder);
            Assert.Equal(PlanContentHeightMath.RowIconFrameSize, PlanHistoryRowLayout.DetailIconTotal);
            Assert.True(PlanHistoryRowLayout.DetailIconSize < PlanHistoryRowLayout.IconSize);

            Assert.Equal(
                PlanHistoryRowLayout.DetailIconTotal + 2 * PlanHistoryRowLayout.IconPad,
                PlanHistoryRowLayout.DetailItemLineHeight);
        }

        [Theory]
        [MemberData(nameof(GateWidths))]
        public void TheNameColumnStartsClearOfTheTierOneFrame(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            Assert.Equal(PlanHistoryRowLayout.Inset, bands.IconX);
            Assert.Equal(
                bands.IconX + PlanHistoryRowLayout.IconTotal + PlanHistoryRowLayout.IconGap,
                bands.NameX);
        }

        [Fact]
        public void EveryTextSeatCentresItsLineBoxOnTheIconBesideIt()
        {
            AssertCentredOnFrame(
                PlanHistoryRowLayout.MainLineTextY,
                PlanHistoryRowLayout.RowHeight,
                PlanHistoryRowLayout.IconY,
                PlanHistoryRowLayout.IconTotal);

            AssertCentredOnFrame(
                PlanHistoryRowLayout.DetailItemTextY,
                PlanHistoryRowLayout.DetailItemLineHeight,
                PlanHistoryRowLayout.IconPad,
                PlanHistoryRowLayout.DetailIconTotal);
        }

        /// <summary>
        /// A text seat is right when the Body line box is centred in its
        /// band (to the pixel integer division leaves), its lowest ink
        /// stays inside the band, and it reads on the same line as the icon
        /// frame beside it rather than floating above or below it.
        /// </summary>
        private static void AssertCentredOnFrame(int textY, int bandHeight, int frameY, int frameSize)
        {
            var ink = TypeRampMetrics.Regular16;

            int above = textY;
            int below = bandHeight - (textY + ink.LineHeight);
            Assert.True(above >= 0 && below >= 0);
            Assert.True(System.Math.Abs(above - below) <= 1);
            Assert.True(textY + ink.LowestInk <= bandHeight);

            Assert.True(textY >= frameY);
            Assert.True(textY + ink.LineHeight <= frameY + frameSize);
        }

        [Fact]
        public void DetailHeight_IsMonotonicInItemCount()
        {
            int previous = -1;
            for (int items = 0; items <= 10; items++)
            {
                int height = PlanHistoryRowLayout.DetailHeight(
                    items, hasChips: false, hasSampleLine: false, hasBlobNote: false, hasOverridesNote: false);
                Assert.True(height > previous);
                previous = height;
            }
        }

        /// <summary>
        /// The owner's report: Plan / Cost / Generated packed together and
        /// left a stranded band before the action controls. The columns are
        /// distributed now, on the same law the Crafting Plan tab's
        /// currency table uses, and the property that says so is
        /// geometric - Cost's right edge sits two thirds of the way across
        /// the span, Generated's on its end - not a restatement of the
        /// arithmetic that produced it.
        /// </summary>
        [Theory]
        [MemberData(nameof(GateWidths))]
        public void AtEveryGateWidth_TheColumnsAreJustifiedAcrossTheSpanRatherThanPacked(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            int spanEnd = bands.WhenRightEdge;
            int beforeCost = bands.CostRightEdge - bands.NameX;
            int afterCost = spanEnd - bands.CostRightEdge;

            // Two tracks before Cost, one after: the label is the flexing
            // column and takes the slack of the first data column's track.
            Assert.InRange(beforeCost - (2 * afterCost), -2, 2);

            // ...and every track is a real, usable width, not a sliver.
            Assert.True(afterCost > WhenWidth);
        }

        /// <summary>
        /// The defect this distribution was asked to remove: a wide dead
        /// band between the last data column and the action cluster. The
        /// cluster is right-anchored, so the columns' own right edge is one
        /// cell gap clear of it at EVERY width.
        /// </summary>
        [Theory]
        [MemberData(nameof(GateWidths))]
        public void GeneratedEndsExactlyOneCellGapBeforeTheActionCluster(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            Assert.Equal(bands.ViewX - PlanHistoryRowLayout.CellGap, bands.WhenRightEdge);
        }

        /// <summary>
        /// A header that computes its own right edge is how the Ranker's
        /// drifted 37px off the column it named. Both seats read one
        /// value per column, so they cannot disagree - this pins that the
        /// value exists and is the cell's own edge.
        /// </summary>
        [Theory]
        [MemberData(nameof(GateWidths))]
        public void EveryColumnHasOneRightEdgeForItsHeaderAndItsCell(int rowWidth)
        {
            var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);

            Assert.Equal(bands.WhenX + bands.WhenWidth, bands.WhenRightEdge);

            // Ordered and non-overlapping, in the order the headers read:
            // Plan, then Cost, then Generated.
            Assert.True(bands.NameX < bands.CostRightEdge - CostWidth);
            Assert.True(bands.CostRightEdge < bands.WhenX);
            Assert.True(bands.WhenRightEdge < bands.ViewX);
        }

        /// <summary>
        /// Widening the window may not walk a column backwards - a column
        /// that moves left as the row grows is the artifact distribution
        /// exists to remove.
        /// </summary>
        [Fact]
        public void EveryColumnEdgeMovesRightAsTheRowWidens()
        {
            int previousCost = int.MinValue;
            int previousWhen = int.MinValue;

            for (int rowWidth = 600; rowWidth <= 3000; rowWidth += 17)
            {
                var bands = PlanHistoryRowLayout.Compute(rowWidth, CostWidth, WhenWidth);
                Assert.True(bands.CostRightEdge >= previousCost, $"Cost went backwards at {rowWidth}");
                Assert.True(bands.WhenRightEdge >= previousWhen, $"Generated went backwards at {rowWidth}");
                previousCost = bands.CostRightEdge;
                previousWhen = bands.WhenRightEdge;
            }
        }

        /// <summary>
        /// Below the width a track can hold its own band in, the row falls
        /// back to the packed right-to-left stack rather than distributing
        /// into slivers - the currency table's own rule. Cost then sits one
        /// cell gap left of the Generated band, and nothing overlaps.
        /// </summary>
        [Fact]
        public void ANarrowRowPacksInsteadOfDistributingIntoSlivers()
        {
            const int narrow = 620;
            var bands = PlanHistoryRowLayout.Compute(narrow, CostWidth, WhenWidth);

            Assert.Equal(bands.WhenX - PlanHistoryRowLayout.CellGap, bands.CostRightEdge);
            Assert.True(bands.CostRightEdge < bands.WhenX);
            Assert.True(bands.WhenRightEdge <= bands.ViewX);
            Assert.True(bands.NameWidth >= 0);
        }

        /// <summary>
        /// The distribution reads off the CONTENT width, so a wider row
        /// gives the plan name more room rather than parking the extra
        /// pixels somewhere the eye has to cross.
        /// </summary>
        [Fact]
        public void TheNameBandGrowsWithTheRowButNeverSwallowsTheWholeSlack()
        {
            var narrow = PlanHistoryRowLayout.Compute(900, CostWidth, WhenWidth);
            var wide = PlanHistoryRowLayout.Compute(1800, CostWidth, WhenWidth);

            Assert.True(wide.NameWidth > narrow.NameWidth);

            // Under the old packed law the name band took EVERY added
            // pixel - 900 of them. Under distribution it takes the two
            // tracks it owns and the Generated column keeps the third.
            int addedToName = wide.NameWidth - narrow.NameWidth;
            Assert.InRange(addedToName, 560, 640);
        }

        [Fact]
        public void DetailHeight_AddsExactlyOneLinePerOptionalBlock()
        {
            int baseline = PlanHistoryRowLayout.DetailHeight(2, false, false, false, false);

            Assert.Equal(baseline + PlanHistoryRowLayout.DetailChipsLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, true, false, false, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, true, false, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, false, true, false));
            Assert.Equal(baseline + PlanHistoryRowLayout.DetailNoteLineHeight,
                PlanHistoryRowLayout.DetailHeight(2, false, false, false, true));
        }
    }
}
