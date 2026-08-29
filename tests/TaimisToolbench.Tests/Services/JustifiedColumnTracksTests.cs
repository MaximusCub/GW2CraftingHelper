using TaimisToolbench.Services;
using Xunit;

namespace TaimisToolbench.Tests.Services
{
    public class JustifiedColumnTracksTests
    {
        [Fact]
        public void AdjacentTracks_ShareAnEdgeExactly()
        {
            const int startX = 40;
            const int span = 1000;
            const int tracks = 6;

            for (int i = 0; i < tracks - 1; i++)
            {
                Assert.Equal(
                    JustifiedColumnTracks.RightEdge(startX, span, tracks, i),
                    JustifiedColumnTracks.LeftEdge(startX, span, tracks, i + 1));
            }
        }

        [Fact]
        public void TheLastTrack_EndsExactlyOnTheSpan_NotARoundingPixelShort()
        {
            // 1000 / 7 does not divide; accumulating a rounded track width
            // would strand pixels at the right edge.
            Assert.Equal(
                40 + 1000,
                JustifiedColumnTracks.RightEdge(40, 1000, 7, 6));
        }

        [Fact]
        public void TrackWidths_SumToTheWholeSpan()
        {
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                total += JustifiedColumnTracks.Width(40, 1000, 7, i);
            }

            Assert.Equal(1000, total);
        }

        [Fact]
        public void CenteredContent_SitsMidTrack()
        {
            // Track 1 of 4 over a 400px span from 0 spans 100..200.
            Assert.Equal(125, JustifiedColumnTracks.CenteredX(0, 400, 4, 1, 50));
        }

        [Fact]
        public void AHeaderAndItsCell_CentreOnTheSameAxis()
        {
            // The point of the law: a narrow header and a wide cell in the
            // same track share a centre line, so the header sits over the
            // values it names.
            int header = JustifiedColumnTracks.CenteredX(0, 400, 4, 2, 40);
            int cell = JustifiedColumnTracks.CenteredX(0, 400, 4, 2, 90);

            Assert.Equal(header + (40 / 2), cell + (90 / 2));
        }

        [Fact]
        public void ContentWiderThanItsTrack_PinsLeft_RatherThanOverhangingBothNeighbours()
        {
            Assert.Equal(
                JustifiedColumnTracks.LeftEdge(0, 400, 4, 1),
                JustifiedColumnTracks.CenteredX(0, 400, 4, 1, 500));
        }

        [Fact]
        public void ZeroTracks_DegradeToTheOrigin_RatherThanDividingByZero()
        {
            Assert.Equal(40, JustifiedColumnTracks.LeftEdge(40, 1000, 0, 0));
            Assert.Equal(40, JustifiedColumnTracks.RightEdge(40, 1000, 0, 0));
            Assert.Equal(0, JustifiedColumnTracks.Width(40, 1000, 0, 0));
            Assert.Equal(40, JustifiedColumnTracks.CenteredX(40, 1000, 0, 0, 10));
        }

        [Fact]
        public void FitsDistributed_IsFalse_WhenATrackCannotHoldItsWidestBandPlusTheGap()
        {
            Assert.True(JustifiedColumnTracks.FitsDistributed(600, 6, 80, 12));
            Assert.False(JustifiedColumnTracks.FitsDistributed(600, 6, 90, 12));
        }

        [Fact]
        public void CenteredInBand_IsTheSameLawAgainstOneColumnsOwnBand()
        {
            // The form every table's HEADER uses: the band, not an equal
            // share of the row, is what the word has to sit over.
            Assert.Equal(155, JustifiedColumnTracks.CenteredInBand(100, 150, 40));
            Assert.Equal(
                JustifiedColumnTracks.CenteredX(100, 150, 1, 0, 40),
                JustifiedColumnTracks.CenteredInBand(100, 150, 40));
        }

        [Fact]
        public void CenteredInBand_ContentAtLeastAsWideAsTheBand_PinsLeft()
        {
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 150, 150));
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 150, 400));
            Assert.Equal(100, JustifiedColumnTracks.CenteredInBand(100, 0, 40));
        }
    }
}
