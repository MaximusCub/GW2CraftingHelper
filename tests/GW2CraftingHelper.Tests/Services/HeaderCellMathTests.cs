using System.Collections.Generic;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The split behind a sortable header's whole-cell hit area: that the
    /// band is partitioned rather than padded (no dead strip, no overlap),
    /// and that degenerate widths empty a cell rather than let it swallow
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
            // near the edge. The Item column IS everything left of Amount.
            var ranges = Partition(600, (50, 40), (500, 79));

            AssertPartitions(ranges, 600);
            Assert.Equal(0, ranges[0].X);
            Assert.Equal(295, ranges[0].Width);
            Assert.Equal(295, ranges[1].X);
        }

        [Fact]
        public void AnUnsortableMiddleColumn_StillSeparatesTheTwoBesideIt()
        {
            // "Source" is inert, but ignoring it hands its pixels away.
            var ranges = Partition(900, (50, 40), (400, 60), (800, 40));

            AssertPartitions(ranges, 900);
            Assert.True(ranges[0].X + ranges[0].Width <= 400);
            Assert.True(ranges[1].X >= 90);
        }

        [Fact]
        public void EveryPixelOfTheBandBelongsToExactlyOneCell()
        {
            // The Shopping List's five columns, crowded on the right.
            var ranges = Partition(700, (50, 40), (300, 60), (450, 79), (540, 50), (640, 55));

            AssertPartitions(ranges, 700);
            Assert.Equal(5, ranges.Count);
        }

        [Fact]
        public void OverlappingLabels_PutTheBoundaryAtTheLaterLabel()
        {
            // A narrow window can run one header's text under the next
            // one's x. No midpoint to take, so the boundary is the later
            // label's own left edge and neither cell inverts.
            var ranges = Partition(200, (50, 120), (100, 60));

            AssertPartitions(ranges, 200);
            Assert.Equal(100, ranges[0].Width);
            Assert.Equal(100, ranges[1].X);
        }

        [Fact]
        public void LabelsOutOfOrder_ShrinkTheEarlierCellRatherThanInvertingIt()
        {
            // The clamp's case: a right-aligned header slid LEFT of its
            // predecessor.
            var ranges = Partition(200, (120, 40), (30, 40));

            // Still the later label's x, so the first cell shrinks rather
            // than the second being handed a negative width.
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
            // The per-frame callers write into a buffer they own; it must
            // not be a second implementation.
            var extents = new[]
            {
                new HeaderCellMath.LabelExtent(50, 40),
                new HeaderCellMath.LabelExtent(500, 79),
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

        // The label-gap midpoint is a fallback: a caller that knows its
        // real column edge says so, and on a name column the difference is
        // hundreds of pixels.
        [Fact]
        public void AnExplicitBoundary_BeatsTheLabelMidpoint()
        {
            var labels = new[]
            {
                new HeaderCellMath.LabelExtent(50, 40, 500),
                new HeaderCellMath.LabelExtent(560, 79),
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
                    new HeaderCellMath.LabelExtent(120, 40),
                });
            AssertPartitions(past, 200);

            var before = HeaderCellMath.Partition(
                200,
                new[]
                {
                    new HeaderCellMath.LabelExtent(10, 30, -50),
                    new HeaderCellMath.LabelExtent(120, 40),
                });
            AssertPartitions(before, 200);
            Assert.Equal(0, before[0].Width);
        }

        [Fact]
        public void MixedBoundaries_TakeEachColumnsOwnRule()
        {
            // Two grid columns, each split at its own Amount edge.
            var labels = new[]
            {
                new HeaderCellMath.LabelExtent(40, 30, 460),
                new HeaderCellMath.LabelExtent(500, 79, 600),
                new HeaderCellMath.LabelExtent(640, 30, 1060),
                new HeaderCellMath.LabelExtent(1100, 79),
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
