using System.Collections.Generic;
using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    /// <summary>
    /// The regression this class exists to stop: PR #232 gave every currency
    /// icon a filled frame plate, and the mostly-transparent currency art let
    /// it through as a grey BACKGROUND nobody asked for. The border must be a
    /// ring, so the assertions below are about what is NOT painted.
    /// </summary>
    public class IconFrameGeometryTests
    {
        private static HashSet<(int X, int Y)> PaintedPixels(int width, int height, int thickness)
        {
            var painted = new HashSet<(int, int)>();
            foreach (var edge in IconFrameGeometry.OutlineEdges(width, height, thickness))
            {
                for (int x = edge.X; x < edge.X + edge.Width; x++)
                {
                    for (int y = edge.Y; y < edge.Y + edge.Height; y++)
                    {
                        // A double-painted pixel is a rectangle overlap, which
                        // on a translucent frame colour would draw as a
                        // brighter corner.
                        Assert.True(painted.Add((x, y)));
                    }
                }
            }

            return painted;
        }

        [Theory]
        [InlineData(CurrencyIconTiers.WalletBarIconSize)]
        [InlineData(CurrencyIconTiers.WalletListIconSize)]
        public void ACurrencyFrame_PaintsTheRingAndNothingInsideIt(int frameSize)
        {
            const int Thickness = ItemIconTiers.FrameBorder;
            var painted = PaintedPixels(frameSize, frameSize, Thickness);

            for (int x = 0; x < frameSize; x++)
            {
                for (int y = 0; y < frameSize; y++)
                {
                    bool onRing = x < Thickness || y < Thickness
                        || x >= frameSize - Thickness || y >= frameSize - Thickness;
                    Assert.Equal(onRing, painted.Contains((x, y)));
                }
            }

            // The whole point, stated as a number: the art's own square is
            // untouched, so whatever transparency it carries shows the row
            // behind it rather than a plate.
            int art = frameSize - (2 * Thickness);
            Assert.Equal((frameSize * frameSize) - (art * art), painted.Count);
        }

        [Fact]
        public void AFrameWithNoRoomForAnInterior_IsAllBorder()
        {
            // 2px of border on a 4px box leaves no interior at all. Answered
            // with one full square rather than four strips, two of which
            // would have a negative height.
            var edges = IconFrameGeometry.OutlineEdges(4, 4, 2);
            var edge = Assert.Single(edges);
            Assert.Equal(0, edge.X);
            Assert.Equal(0, edge.Y);
            Assert.Equal(4, edge.Width);
            Assert.Equal(4, edge.Height);
        }

        [Theory]
        [InlineData(0, 16, 1)]
        [InlineData(16, 0, 1)]
        [InlineData(-8, 16, 1)]
        [InlineData(16, 16, 0)]
        [InlineData(16, 16, -1)]
        public void AFrameWithNoSizeOrNoBorder_PaintsNothing(int width, int height, int thickness)
        {
            Assert.Empty(IconFrameGeometry.OutlineEdges(width, height, thickness));
        }

        [Fact]
        public void ANonSquareFrame_RingsItsOwnEdges()
        {
            // The frames this module builds are square, but the ring is
            // painted from the control's live bounds, and a caller that
            // resizes one must not get a border floating inside the box.
            var painted = PaintedPixels(24, 16, 1);
            Assert.Contains((23, 15), painted);
            Assert.DoesNotContain((22, 14), painted);
        }
    }
}
