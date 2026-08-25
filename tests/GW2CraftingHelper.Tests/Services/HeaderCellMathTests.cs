using System.Collections.Generic;
using GW2CraftingHelper.Services;
using Xunit;

namespace GW2CraftingHelper.Tests.Services
{
    /// <summary>
    /// The split behind a sortable header's whole-cell hit area. What is
    /// worth pinning is what a screenshot cannot show: that the band is
    /// partitioned rather than padded (no dead strip between two columns,
    /// no overlap where one header answers the other's click), and that the
    /// degenerate widths produce empty cells rather than cells that swallow
    /// the band.
    /// </summary>
    public class HeaderCellMathTests
    {
        private static IReadOnlyList<HeaderCellMath.CellRange> Partition(
            int bandWidth, params (int X, int Width)[] labels)
        {
            var extents = new HeaderCellMath.LabelExtent[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                extents[i] = new HeaderCellMath.LabelExtent(labels[i].X, labels[i].Width);
            }

            return HeaderCellMath.Partition(bandWidth, extents);
        }

        private static void AssertPartitions(IReadOnlyList<HeaderCellMath.CellRange> ranges, int bandWidth)
        {
            Assert.Equal(0, ranges[0].X);
            for (int i = 0; i < ranges.Count; i++)
            {
                Assert.True(ranges[i].Width >= 0, "no cell may have negative width");
                if (i > 0)
                {
                    Assert.Equal(ranges[i - 1].X + ranges[i - 1].Width, ranges[i].X);
                }
            }

            var last = ranges[ranges.Count - 1];
            Assert.Equal(bandWidth, last.X + last.Width);
        }

        [Fact]
        public void TwoColumns_BoundaryLandsMidwayBetweenTheLabels()
        {
            // Used Materials' shape: "Item" at x=50, "Amount" right-aligned
            // near the panel edge. The whole left half of the band belongs
            // to Item - which is the point of the change, since the Item
            // column IS everything left of Amount.
            var ranges = Partition(600, (50, 40), (500, 79));

            AssertPartitions(ranges, 600);
            Assert.Equal(0, ranges[0].X);
            Assert.Equal(295, ranges[0].Width);
            Assert.Equal(295, ranges[1].X);
        }

        [Fact]
        public void AnUnsortableMiddleColumn_StillSeparatesTheTwoBesideIt()
        {
            // The tree's "Source" header is inert, but a cell that ignored
            // it would hand its pixels to Item or Cost.
            var ranges = Partition(900, (50, 40), (400, 60), (800, 40));

            AssertPartitions(ranges, 900);
            Assert.True(ranges[0].X + ranges[0].Width <= 400);
            Assert.True(ranges[1].X >= 90);
        }

        [Fact]
        public void EveryPixelOfTheBandBelongsToExactlyOneCell()
        {
            // The Shopping List's five columns, at a width where three of
            // them are crowded together on the right.
            var ranges = Partition(700, (50, 40), (300, 60), (450, 79), (540, 50), (640, 55));

            AssertPartitions(ranges, 700);
            Assert.Equal(5, ranges.Count);
        }

        [Fact]
        public void OverlappingLabels_PutTheBoundaryAtTheLaterLabel()
        {
            // A very narrow window can leave one header's text running
            // under the next one's x. There is no midpoint to take, so the
            // boundary is the later label's own left edge: the cells stay
            // in order and neither inverts into the other's pixels.
            var ranges = Partition(200, (50, 120), (100, 60));

            AssertPartitions(ranges, 200);
            Assert.Equal(100, ranges[0].Width);
            Assert.Equal(100, ranges[1].X);
        }

        [Fact]
        public void LabelsOutOfOrder_ShrinkTheEarlierCellRatherThanInvertingIt()
        {
            // The pathological case the clamp exists for: a right-aligned
            // header that has slid LEFT of the one before it.
            var ranges = Partition(200, (120, 40), (30, 40));

            // The boundary is still the later label's x, so the first cell
            // shrinks to what is left of it instead of the second cell
            // being handed a negative width.
            AssertPartitions(ranges, 200);
            Assert.Equal(30, ranges[0].Width);
            Assert.Equal(170, ranges[1].Width);
        }

        [Fact]
        public void ALabelPastTheBandEdge_IsClampedIntoIt()
        {
            var ranges = Partition(100, (10, 30), (400, 50));

            AssertPartitions(ranges, 100);
        }

        [Fact]
        public void SingleColumn_TakesTheWholeBand()
        {
            var ranges = Partition(480, (16, 60));

            Assert.Single(ranges);
            Assert.Equal(0, ranges[0].X);
            Assert.Equal(480, ranges[0].Width);
        }

        [Fact]
        public void TheBufferOverload_WritesTheSameSplit()
        {
            // The header rows re-split on every frame of a resize drag, so
            // that path writes into a buffer it owns rather than allocating
            // one per header per frame. It must not be a second
            // implementation.
            var extents = new[]
            {
                new HeaderCellMath.LabelExtent(50, 40),
                new HeaderCellMath.LabelExtent(500, 79)
            };
            var buffer = new HeaderCellMath.CellRange[extents.Length];

            HeaderCellMath.Partition(600, extents, buffer);
            var allocated = HeaderCellMath.Partition(600, extents);

            for (int i = 0; i < buffer.Length; i++)
            {
                Assert.Equal(allocated[i].X, buffer[i].X);
                Assert.Equal(allocated[i].Width, buffer[i].Width);
            }
        }

        // The label-gap midpoint is a fallback. A caller that knows where
        // its column actually ends says so, and the split takes it - the
        // difference is whole hundreds of pixels on a name column, which is
        // the strip of header directly above the item names.
        [Fact]
        public void AnExplicitBoundary_BeatsTheLabelMidpoint()
        {
            var labels = new[]
            {
                new HeaderCellMath.LabelExtent(50, 40, 500),
                new HeaderCellMath.LabelExtent(560, 79)
            };

            var ranges = HeaderCellMath.Partition(700, labels);

            AssertPartitions(ranges, 700);
            Assert.Equal(500, ranges[0].Width);
            Assert.Equal(500, ranges[1].X);

            // What the same two labels would have given without it.
            var derived = Partition(700, (50, 40), (560, 79));
            Assert.Equal(325, derived[0].Width);
        }

        [Fact]
        public void AnExplicitBoundary_IsStillClampedIntoTheBand()
        {
            var past = HeaderCellMath.Partition(
                200,
                new[]
                {
                    new HeaderCellMath.LabelExtent(10, 30, 900),
                    new HeaderCellMath.LabelExtent(120, 40)
                });
            AssertPartitions(past, 200);

            var before = HeaderCellMath.Partition(
                200,
                new[]
                {
                    new HeaderCellMath.LabelExtent(10, 30, -50),
                    new HeaderCellMath.LabelExtent(120, 40)
                });
            AssertPartitions(before, 200);
            Assert.Equal(0, before[0].Width);
        }

        [Fact]
        public void MixedBoundaries_TakeEachColumnsOwnRule()
        {
            // The Snapshot's band: two grid columns, each split at its own
            // Amount edge, and the last cell always closing the band.
            var labels = new[]
            {
                new HeaderCellMath.LabelExtent(40, 30, 460),
                new HeaderCellMath.LabelExtent(500, 79, 600),
                new HeaderCellMath.LabelExtent(640, 30, 1060),
                new HeaderCellMath.LabelExtent(1100, 79)
            };

            var ranges = HeaderCellMath.Partition(1200, labels);

            AssertPartitions(ranges, 1200);
            Assert.Equal(460, ranges[0].Width);
            Assert.Equal(600, ranges[2].X);
            Assert.Equal(1060, ranges[3].X);
        }

        [Fact]
        public void NoLabels_OrNoBand_AreHandled()
        {
            Assert.Empty(HeaderCellMath.Partition(500, new HeaderCellMath.LabelExtent[0]));
            Assert.Empty(HeaderCellMath.Partition(500, null));
            HeaderCellMath.Partition(500, null, new HeaderCellMath.CellRange[2]);

            var degenerate = Partition(0, (10, 20), (40, 20));
            AssertPartitions(degenerate, 0);
        }
    }
}
